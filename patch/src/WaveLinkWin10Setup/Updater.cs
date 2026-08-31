using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// Checks Elgato for the latest Wave Link Windows MSIX and downloads it, so the
    /// installer can keep Wave Link itself up to date. Detection uses the same Zendesk
    /// REST API the CI uses (no Akamai/Cloudflare challenge), parsing the article body
    /// for the edge.elgato.com MSIX direct link.
    /// </summary>
    public static class Updater
    {
        const string ZendeskBase = "https://elgato.zendesk.com";
        const string SectionId = "4913442828941";

        /// <summary>
        /// Detect the latest MSIX, compare with the installed appx version, and download
        /// it into input/. Returns the local path, or "" when already up to date.
        /// Throws on detection/download failure.
        /// </summary>
        public static async Task<string> UpdateAsync(Action<string> log)
        {
            log(Lang.T("updCheck"));
            var (url, ver) = await FetchLatestAsync(log);
            log(Lang.T("updLatest") + ver);

            var installed = GetInstalledVersion();
            log(Lang.T("updInstalled") + (string.IsNullOrWhiteSpace(installed) ? Lang.T("inputNone") : installed));

            if (!string.IsNullOrWhiteSpace(installed) && CompareVer(installed, ver) >= 0)
            {
                log(string.Format(Lang.T("updSame"), ver));
                return ""; // already up to date
            }

            log(Lang.T("updDownload"));
            var path = await DownloadAsync(url, ver, log);
            log(Lang.T("updDownloaded") + path);
            return path;
        }

        public static async Task<(string url, string version)> FetchLatestAsync(Action<string> log)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.Add("Accept", "application/json");

            var listUrl = $"{ZendeskBase}/api/v2/help_center/en-us/sections/{SectionId}/articles.json";
            string listJson = await http.GetStringAsync(listUrl);

            string articleId = null;
            using (var doc = JsonDocument.Parse(listJson))
            {
                // Pick the Windows release-notes article with the highest version number.
                // (Titles look like "Elgato Wave Link 3.2.10 (Windows) Release Notes".)
                if (doc.RootElement.TryGetProperty("articles", out var arts))
                {
                    var verRe = new Regex(@"Elgato Wave Link (\d+\.\d+\.\d+) \(Windows\) Release Notes");
                    int[] best = { -1, -1, -1 };
                    foreach (var a in arts.EnumerateArray())
                    {
                        if (!a.TryGetProperty("title", out var t)) continue;
                        var m = verRe.Match(t.GetString() ?? "");
                        if (!m.Success) continue;
                        var v = ParseVer(m.Groups[1].Value);
                        if (v[0] > best[0] || (v[0] == best[0] && v[1] > best[1]) ||
                            (v[0] == best[0] && v[1] == best[1] && v[2] > best[2]))
                        {
                            best = v;
                            if (a.TryGetProperty("id", out var id)) articleId = id.GetRawText();
                        }
                    }
                }
            }
            if (articleId == null) throw new Exception(Lang.T("updNoArticle"));

            var artUrl = $"{ZendeskBase}/api/v2/help_center/en-us/articles/{articleId}.json";
            string artJson = await http.GetStringAsync(artUrl);
            string body = null;
            using (var doc = JsonDocument.Parse(artJson))
            {
                if (doc.RootElement.TryGetProperty("article", out var art) &&
                    art.TryGetProperty("body", out var b)) body = b.GetString();
            }
            if (string.IsNullOrEmpty(body)) throw new Exception(Lang.T("updNoBody"));

            var msixMatch = new Regex(
                "https://edge\\.elgato\\.com/egc/windows/ewlw/[\\d.]+/Stable/Elgato\\.WaveLink_[\\d.]+_x64\\.msix");
            var mm = msixMatch.Match(body);
            if (!mm.Success) throw new Exception(Lang.T("updNoMsix"));
            string url = mm.Value;

            var verMatch = new Regex("Elgato\\.WaveLink_([\\d.]+)_\\d+_x64\\.msix");
            var vm = verMatch.Match(url);
            string ver = vm.Success ? vm.Groups[1].Value : "";
            return (url, ver);
        }

        /// <summary>Reads the installed Wave Link version from the appx package, or "" if not installed.</summary>
        public static string GetInstalledVersion()
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-AppxPackage -Name Elgato.WaveLink).Version\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };
                using var p = Process.Start(psi);
                var o = p?.StandardOutput.ReadToEnd()?.Trim();
                p?.WaitForExit();
                return string.IsNullOrWhiteSpace(o) ? "" : o;
            }
            catch { return ""; }
        }

        static int CompareVer(string a, string b)
        {
            var va = ParseVer(a);
            var vb = ParseVer(b);
            for (int i = 0; i < 4; i++)
                if (va[i] != vb[i]) return va[i].CompareTo(vb[i]);
            return 0;
        }

        static int[] ParseVer(string s)
        {
            var parts = (s ?? "").Split('.');
            var r = new int[4];
            for (int i = 0; i < 4; i++) int.TryParse(i < parts.Length ? parts[i] : "0", out r[i]);
            return r;
        }

        public static async Task<string> DownloadAsync(string url, string ver, Action<string> log)
        {
            var inputDir = Path.Combine(Installer.RepoRoot, "input");
            Directory.CreateDirectory(inputDir);
            var fname = string.IsNullOrEmpty(ver)
                ? "Elgato.WaveLink_latest_x64.msix"
                : $"Elgato.WaveLink_{ver}_x64.msix";
            var dest = Path.Combine(inputDir, fname);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? 0;

            using var inStream = await resp.Content.ReadAsStreamAsync();
            using var outStream = File.Create(dest);
            var buf = new byte[8192];
            long read = 0, lastLog = 0;
            int n;
            while ((n = await inStream.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                await outStream.WriteAsync(buf, 0, n);
                read += n;
                if (total > 0 && read - lastLog > 5 * 1024 * 1024)
                {
                    lastLog = read;
                    log(string.Format(Lang.T("updProgress"), read / 1048576, total / 1048576));
                }
            }

            if (read < 100 * 1024 * 1024)
                throw new Exception(Lang.T("updTooSmall") + " (" + (read / 1048576) + " MB)");
            return dest;
        }
    }
}
