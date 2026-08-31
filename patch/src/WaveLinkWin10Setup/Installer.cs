using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// Core orchestration for the Wave Link 3.x on Windows 10 setup.
    /// GUI-agnostic: reports progress through an Action&lt;string&gt; log callback.
    /// </summary>
    public static class Installer
    {
        /// <summary>Folder the exe lives in. When published as a standalone exe it may sit in a
        /// build output folder without driver/ / scripts/ / input/ next to it, so the path
        /// resolvers below walk UP from here to the repo root to locate those assets.</summary>
        public static string RepoRoot => AppContext.BaseDirectory.TrimEnd('\\');

        /// <summary>Resolve a file relative to RepoRoot, walking UP parent dirs until found.
        /// Returns the RepoRoot-relative default if nothing is found (so callers can still report a path).</summary>
        static string ResolveExisting(string relative)
        {
            var dir = RepoRoot;
            while (true)
            {
                var candidate = Path.Combine(dir, relative);
                if (File.Exists(candidate)) return candidate;
                var parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }
            return Path.Combine(RepoRoot, relative);
        }

        /// <summary>Same as ResolveExisting but for directories.</summary>
        static string ResolveDir(string name)
        {
            var dir = RepoRoot;
            while (true)
            {
                var candidate = Path.Combine(dir, name);
                if (Directory.Exists(candidate)) return candidate;
                var parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }
            return Path.Combine(RepoRoot, name);
        }

        /// <summary>Locate the Wave Link driver MSI. Tries the exact expected path (searched upward
        /// from RepoRoot), then any WaveLinkDriver*.msi under a driver\ directory walking upward.</summary>
        static string FindDriverMsi()
        {
            var exact = ResolveExisting(Path.Combine("driver", "WaveLinkDriver_3.0.0.466_x64.msi"));
            if (File.Exists(exact)) return exact;
            var dir = RepoRoot;
            while (true)
            {
                var d = Path.Combine(dir, "driver");
                if (Directory.Exists(d))
                {
                    var hits = Directory.GetFiles(d, "WaveLinkDriver*.msi");
                    if (hits.Length > 0) return hits[0];
                }
                var parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }
            return exact;
        }

        public static bool IsAdmin()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static int GetOsBuild()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var v = key?.GetValue("CurrentBuild")?.ToString();
            return v != null && int.TryParse(v, out var b) ? b : 0;
        }

        /// <summary>
        /// Full flow. mode: "all" | "app" | "driver". skipApp/skipDriver let callers narrow scope.
        /// </summary>
        public static void Run(string mode, string msixPath, int minBuild, bool skipApp, bool skipDriver, Action<string> log, Action<int>? progress = null)
        {
            void P(int p) => progress?.Invoke(p);
            log(Lang.T("runHeader"));
            log(Lang.T("repoRoot") + RepoRoot);
            P(3);

            int build = GetOsBuild();
            log(string.Format(Lang.T("osBuild"), build));
            if (build != 0 && build < 17763)
                throw new Exception(string.Format(Lang.T("needBuild"), build));

            if (mode == "driver") skipApp = true;
            if (mode == "app") skipDriver = true;
            P(5);

            if (!skipApp)
            {
                if (string.IsNullOrWhiteSpace(msixPath))
                {
                    var inputDir = ResolveDir("input");
                    if (Directory.Exists(inputDir))
                    {
                        var c = Directory.GetFiles(inputDir, "*.msix");
                        if (c.Length > 0) msixPath = c[0];
                    }
                }
                if (string.IsNullOrWhiteSpace(msixPath) || !File.Exists(msixPath))
                    throw new Exception(Lang.T("noMsix"));
                log(Lang.T("msixPath") + msixPath);

                log(Lang.T("enableDev"));
                EnableDevMode(log);
                P(15);

                var patched = Path.Combine(RepoRoot, "input", "WaveLink_Win10_patched.msix");
                log(string.Format(Lang.T("patchRepack"), minBuild));
                PatchMsix(msixPath, patched, minBuild, log);
                if (!File.Exists(patched)) throw new Exception(Lang.T("patchNoOut") + patched);
                P(45);

                log(Lang.T("installPatched"));
                InstallAppx(patched, log);
                P(60);
            }
            else
            {
                log(Lang.T("skipApp"));
            }

            if (!skipDriver)
            {
                var msi = FindDriverMsi();
                if (!File.Exists(msi))
                {
                    log(Lang.T("driverMissingFetch"));
                    var bat = ResolveExisting(Path.Combine("scripts", "fetch_driver.bat"));
                    if (File.Exists(bat)) RunProcess("cmd.exe", "/c \"" + bat + "\"", log);
                }
                if (!File.Exists(msi)) throw new Exception(Lang.T("driverMissing") + msi);
                log(Lang.T("installDriverMsi"));
                InstallDriver(msi, log);
                P(85);
            }
            else
            {
                log(Lang.T("skipDriver"));
            }

            Verify(log, progress);
            P(95);
            log(Lang.T("done"));
            P(100);
        }

        public static void VerifyOnly(Action<string> log, Action<int>? progress = null)
        {
            Verify(log, progress);
            progress?.Invoke(100);
        }

        public static void EnvCheckGui(Action<string> log)
        {
            log(Lang.T("envHeader"));
            int build = GetOsBuild();
            log(string.Format(Lang.T("envBuildLine"), build, build >= 17763 ? Lang.T("envBuildOk") : Lang.T("envBuildBad")));
            log(Lang.T("envAdmin") + IsAdmin());

            bool dev = false;
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                dev = k?.GetValue("AllowDevelopmentWithoutDevLicense") as int? == 1;
            log(Lang.T("envDev") + dev);

            var inputDir = ResolveDir("input");
            var msixs = Directory.Exists(inputDir) ? Directory.GetFiles(inputDir, "*.msix") : new string[0];
            log(Lang.T("envInputMsix") + (msixs.Length > 0 ? string.Join(", ", msixs) : Lang.T("inputNone")));

            var msi = FindDriverMsi();
            log(Lang.T("envDriverMsi") + (File.Exists(msi) ? Lang.T("driverPresent") : Lang.T("driverAbsent")));
            log(Lang.T("envEnd"));
        }

        // ---- internals ----

        static void EnableDevMode(Action<string> log)
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            key.SetValue("AllowDevelopmentWithoutDevLicense", 1, RegistryValueKind.DWord);
            log(Lang.T("devEnabled"));
        }

        static void PatchMsix(string input, string output, int minBuild, Action<string> log)
        {
            var ps1 = ExtractEmbeddedPs1();
            var tmp = Path.Combine(Path.GetTempPath(), "wl_patch_" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(tmp, ps1, Encoding.UTF8);
            RunProcess("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{tmp}\" -InputMsix \"{input}\" -OutputMsix \"{output}\" -MinBuild {minBuild}",
                log);
        }

        static string ExtractEmbeddedPs1()
        {
            var asm = typeof(Installer).Assembly;
            using var s = asm.GetManifestResourceStream("WaveLinkWin10Setup.Resources.patch_manifest.ps1")
                ?? throw new Exception("内嵌补丁脚本缺失（patch_manifest.ps1）。");
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }

        static void InstallAppx(string patched, Action<string> log)
        {
            SignAppx(patched, log);
            var cmd = $"Add-AppxPackage -Path '{patched.Replace("'", "''")}' -ForceApplicationShutdown";
            RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"", log);

            var ps = "Get-AppxPackage -Name Elgato.WaveLink | Select-Object -ExpandProperty Version";
            var outp = RunProcessCapture("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"");
            if (string.IsNullOrWhiteSpace(outp))
                throw new Exception(Lang.T("appFail"));
            log(Lang.T("appInstalled") + outp.Trim());
        }

        static string FindSigntool()
        {
            foreach (var root in new[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\bin",
                @"C:\Program Files\Windows Kits\10\bin"
            })
            {
                if (!Directory.Exists(root)) continue;
                var hits = Directory.GetFiles(root, "signtool.exe", SearchOption.AllDirectories);
                var x64 = hits.FirstOrDefault(h => h.Replace('\\', '/').Contains("/x64/"));
                if (x64 != null) return x64;
                if (hits.Length > 0) return hits[0];
            }
            return null;
        }

        /// <summary>
        /// Create (or reuse) a self-signed cert CN=WaveLinkPatch, trust it into
        /// LocalMachine\TrustedRoot, and sign the patched MSIX with signtool so that
        /// Add-AppxPackage (which requires a trusted signature) accepts it.
        /// </summary>
        static void SignAppx(string patched, Action<string> log)
        {
            log(Lang.T("signing"));
            var st = FindSigntool();
            if (st == null) throw new Exception(Lang.T("noSigntool"));
            log(Lang.T("signTool") + st);

            var dir = Path.GetDirectoryName(patched);
            var pfx = Path.Combine(dir, "WaveLinkPatch.pfx");
            var cer = Path.Combine(dir, "WaveLinkPatch.cer");

            var script = @"$ErrorActionPreference = 'Stop'
$friendly = 'WaveLinkPatch'
$pfx = '__PFX__'
$cer = '__CER__'
$msix = '__MSIX__'
$st  = '__SIGNT__'

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.FriendlyName -eq $friendly } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=WaveLinkPatch' -KeyUsage DigitalSignature -KeyAlgorithm RSA -HashAlgorithm SHA256 -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
    Write-Host ('Created self-signed cert: ' + $cert.Thumbprint)
} else {
    Write-Host ('Reusing existing cert: ' + $cert.Thumbprint)
}
$pwd = ConvertTo-SecureString -String 'WaveLinkPatch' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pwd | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

$thumb = $cert.Thumbprint
# Trust the self-signed cert into the LocalMachine Trusted Root CAs store.
# NOTE: the Cert: provider store name is Root, NOT TrustedRoot (the latter is only
# the display name and does not exist as a path, causing a path-not-found error).
# certutil talks to the CryptoAPI directly, auto-creates the store, and does not
# depend on the PowerShell Cert: provider, so it is more robust here.
& certutil -addstore -ent -f Root ""$cer"" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'certutil -addstore Root failed' }
Write-Host 'Cert trusted (LocalMachine\Root)'

& $st sign /fd SHA256 /a /f $pfx /p WaveLinkPatch $msix
if ($LASTEXITCODE -ne 0) { throw 'signtool sign failed' }
Write-Host ('Signed: ' + $msix)
";
            script = script.Replace("__PFX__", pfx.Replace("'", "''"))
                          .Replace("__CER__", cer.Replace("'", "''"))
                          .Replace("__MSIX__", patched.Replace("'", "''"))
                          .Replace("__SIGNT__", st.Replace("'", "''"));

            var tmp = Path.Combine(Path.GetTempPath(), "wl_sign_" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(tmp, script, Encoding.UTF8);
            RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{tmp}\"", log);
        }

        static readonly string[] DriverServices = { "ElgatoVirtUsbAudioEmu", "ElgatoUsbAudio", "ElgatoUsbAudioks" };

        static bool AreDriverServicesRunning()
        {
            foreach (var name in DriverServices)
            {
                var svc = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == name);
                if (svc == null || svc.Status != ServiceControllerStatus.Running) return false;
            }
            return true;
        }

        static void InstallDriver(string msi, Action<string> log)
        {
            // Idempotent: if the three kernel services are already running, do nothing.
            if (AreDriverServicesRunning())
            {
                log(Lang.T("driverAlready"));
                return;
            }

            // Method 1: official MSI (Thesycon tlsetupfx installs the driver via its INFs).
            var logDir = Path.Combine(RepoRoot, "driver");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "msi_install_exe.log");
            try
            {
                var psi = new ProcessStartInfo("msiexec.exe", $"/i \"{msi}\" /qn /norestart /l*v \"{logPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi) ?? throw new Exception(Lang.T("cannotStartMsiexec"));
                p.WaitForExit();
                log(string.Format(Lang.T("driverMsiExit"), p.ExitCode));
            }
            catch (Exception ex)
            {
                log(Lang.T("driverMsiError") + ex.Message);
            }

            if (AreDriverServicesRunning())
            {
                log(Lang.T("driverOk"));
                return;
            }

            // Method 2 (fallback): install the bundled, signed driver packages via pnputil.
            log(Lang.T("driverPnpFallback"));
            var elgatoDir = Path.Combine(RepoRoot, "driver", "elgato");
            if (Directory.Exists(elgatoDir))
            {
                foreach (var inf in Directory.GetFiles(elgatoDir, "*.inf", SearchOption.AllDirectories))
                {
                    try
                    {
                        log("  pnputil " + Path.GetFileName(inf) + ": " + RunPnp($"/add-driver \"{inf}\" /install"));
                    }
                    catch (Exception ex)
                    {
                        log("  pnputil " + Path.GetFileName(inf) + " error: " + ex.Message);
                    }
                }
            }
            else
            {
                log(Lang.T("driverNoPnpDir"));
            }

            if (!AreDriverServicesRunning())
                throw new Exception(string.Format(Lang.T("driverFail"), -1, logPath));
            log(Lang.T("driverOk"));
        }

        static string RunPnp(string args)
        {
            var psi = new ProcessStartInfo("pnputil.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi) ?? throw new Exception(Lang.T("cannotStart") + "pnputil.exe");
            var o = p.StandardOutput.ReadToEnd();
            var e = p.StandardError.ReadToEnd();
            p.WaitForExit();
            var outp = (string.IsNullOrWhiteSpace(o) ? "" : o.Trim());
            if (!string.IsNullOrWhiteSpace(e)) outp += (outp.Length > 0 ? "\n" : "") + e.Trim();
            return outp;
        }

        static void Verify(Action<string> log, Action<int>? progress = null)
        {
            log(Lang.T("verifyHeader"));
            progress?.Invoke(90);
            bool ok = true;
            foreach (var name in new[] { "ElgatoVirtUsbAudioEmu", "ElgatoUsbAudio", "ElgatoUsbAudioks" })
            {
                var svc = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == name);
                var st = svc?.Status.ToString() ?? "MISSING";
                log($"  {name} : {st}");
                if (svc == null || svc.Status != ServiceControllerStatus.Running) ok = false;
            }
            var appx = RunProcessCapture("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name Elgato.WaveLink | Select-Object -ExpandProperty Version\"");
            log(Lang.T("verifyAppx") + (string.IsNullOrWhiteSpace(appx) ? "MISSING" : appx.Trim()));
            if (!ok) log(Lang.T("verifyWarn"));
        }

        static void RunProcess(string exe, string args, Action<string> log, int timeoutMs = 1200000)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi) ?? throw new Exception(Lang.T("cannotStart") + exe);
            p.OutputDataReceived += (s, e) => { if (e.Data != null) log(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) log(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                throw new Exception(string.Format(Lang.T("procTimeout"), exe, timeoutMs / 60000));
            }
            if (p.ExitCode != 0) throw new Exception(string.Format(Lang.T("exitCode"), exe, p.ExitCode));
        }

        static string RunProcessCapture(string exe, string args, int timeoutMs = 1200000)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi) ?? throw new Exception("无法启动: " + exe);
            var o = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                throw new Exception(string.Format(Lang.T("procTimeout"), exe, timeoutMs / 60000));
            }
            return o;
        }
    }
}
