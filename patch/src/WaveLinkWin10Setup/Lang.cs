using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// Minimal two-language (zh / en) string table.
    /// Default language follows the OS UI culture; the GUI language selector can
    /// override it and the choice is persisted to %LOCALAPPDATA%\WaveLinkWin10Setup\lang.cfg
    /// so elevated child processes (RunElevated) inherit the same language.
    /// </summary>
    public static class Lang
    {
        public static string Mode { get; private set; } = "zh";

        static readonly Dictionary<string, string> Zh = new Dictionary<string, string>();
        static readonly Dictionary<string, string> En = new Dictionary<string, string>();

        static Lang()
        {
            Mode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh" : "en";
            Load();
            try
            {
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WaveLinkWin10Setup", "lang.cfg");
                if (File.Exists(p))
                {
                    var v = (File.ReadAllText(p) ?? "").Trim().ToLowerInvariant();
                    if (v == "zh" || v == "en") Mode = v;
                }
            }
            catch { /* non-fatal */ }
        }

        public static void SetMode(string mode)
        {
            if (mode != "zh" && mode != "en") return;
            Mode = mode;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WaveLinkWin10Setup");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "lang.cfg"), mode);
            }
            catch { /* non-fatal */ }
        }

        public static string T(string key)
        {
            var d = Mode == "zh" ? Zh : En;
            return d.TryGetValue(key, out var v) ? v : key;
        }

        static void Load()
        {
            // ---------------- 中文 ----------------
            Zh["title"] = "Wave Link 3.x · Windows 10 安装器";
            Zh["langLabel"] = "语言";
            Zh["lblMsix"] = "官方 Wave Link MSIX 路径（留空则自动用 input/ 下第一个）";
            Zh["btnBrowse"] = "浏览...";
            Zh["chkSkipApp"] = "跳过应用安装（已装好）";
            Zh["chkSkipDriver"] = "跳过驱动安装（已装好）";
            Zh["lblMinBuild"] = "最低 Win10 版本 (build)";
            Zh["btnRunAll"] = "一键运行全部";
            Zh["btnInstallApp"] = "仅装应用";
            Zh["btnInstallDriver"] = "仅装驱动";
            Zh["btnVerify"] = "验证";
            Zh["btnCheck"] = "环境检查 (干跑)";
            Zh["tipLog"] = "提示：安装类操作需管理员权限，点击后会自动请求提权（UAC）。";
            Zh["stepLog"] = "步骤：① 浏览/放入官方 MSIX 到 input/ → ② 点「一键运行全部」→ ③ 验证驱动已安装。";
            Zh["dlgTitle"] = "选择官方 Wave Link MSIX";
            Zh["dlgFilter"] = "MSIX 包 (*.msix)|*.msix|所有文件 (*.*)|*.*";
            Zh["elevateFail"] = "提权失败（已取消或出错）: ";
            Zh["errPrefix"] = "错误: ";

            Zh["runHeader"] = "=== Wave Link 3.x on Windows 10 - 自动安装 ===";
            Zh["repoRoot"] = "Repo root: ";
            Zh["osBuild"] = "OS build: {0}";
            Zh["envBuildLine"] = "OS build: {0} ({1})";
            Zh["needBuild"] = "需要 Windows 10 1809 (build 17763) 或以上，当前 {0}。";
            Zh["skipApp"] = "(跳过应用安装)";
            Zh["skipDriver"] = "(跳过驱动安装)";
            Zh["noMsix"] = "未在 input/ 找到 MSIX。请把官方 Wave Link MSIX 放入 input/ 目录（见 input/README.txt）。";
            Zh["msixPath"] = "MSIX: ";
            Zh["enableDev"] = "启用 Windows 开发者模式 ...";
            Zh["patchRepack"] = "补丁重打包（MinVersion -> 10.0.{0}.0） ...";
            Zh["patchNoOut"] = "补丁未产生输出: ";
            Zh["installPatched"] = "安装补丁后 MSIX（开发者模式 + 自签证书） ...";
            Zh["signing"] = "用自签证书签名补丁 MSIX（CN=WaveLinkPatch）...";
            Zh["noSigntool"] = "未找到 signtool.exe（需 Windows 10 SDK）。";
            Zh["signTool"] = "签名工具: ";
            Zh["driverMissingFetch"] = "驱动 MSI 缺失，尝试 fetch_driver.bat 下载 ...";
            Zh["driverMissing"] = "驱动 MSI 缺失: ";
            Zh["installDriverMsi"] = "安装驱动 MSI（原生宿主 /qn） ...";
            Zh["done"] = "=== 完成。Wave Link 3.x 驱动已安装在 Windows 10（服务将按需启动）。===";
            Zh["envHeader"] = "=== 环境检查 ===";
            Zh["envBuildOk"] = "满足 >= 1809";
            Zh["envBuildBad"] = "不满足，需 1809+";
            Zh["envAdmin"] = "Administrator: ";
            Zh["envDev"] = "Developer Mode: ";
            Zh["envInputMsix"] = "input/ MSIX: ";
            Zh["envDriverMsi"] = "Driver MSI: ";
            Zh["envEnd"] = "=== 结束 ===";
            Zh["devEnabled"] = "开发者模式已启用。";
            Zh["appFail"] = "应用安装失败：未找到 Elgato.WaveLink（Windows 10 1809/1909 不支持 -AllowUnsigned，需用自签证书路线，见 FAQ）。";
            Zh["appInstalled"] = "应用已安装: ";
            Zh["cannotStartMsiexec"] = "无法启动 msiexec。";
            Zh["driverFail"] = "驱动 MSI 失败，退出码 {0}。详见 {1}";
            Zh["driverFailPending"] = "驱动已安装，但服务尚未就绪（可能需重启或等待 PnP 枚举）。MSI 退出码 {0}，详见 {1}";
            Zh["driverOk"] = "驱动安装成功（将按需启动）。";
            Zh["driverAlready"] = "驱动服务已在运行，跳过安装。";
            Zh["driverMsiExit"] = "驱动 MSI 退出码: {0}";
            Zh["driverMsiError"] = "驱动 MSI 执行出错: ";
            Zh["driverPnpFallback"] = "MSI 未生成驱动服务，改用 pnputil 强制安装内置驱动包 ...";
            Zh["driverNoPnpDir"] = "未找到内置驱动目录 driver/elgato，无法回退。";
            Zh["verifyHeader"] = "验证服务 ...";
            Zh["verifyAppx"] = "  Appx Elgato.WaveLink : ";
            Zh["verifyWarn"] = "警告：部分驱动服务未运行（属正常，将按需启动），请确认服务已安装。";
            Zh["verifyDriverStore"] = "  驱动库（已暂存驱动包）: ";
            Zh["verifyYes"] = "已暂存";
            Zh["verifyNo"] = "未暂存";
            Zh["verifyOk"] = "验证通过：驱动包已暂存至驱动库，Wave Link 应用已安装。音频服务将按需启动。";
            Zh["verifyAppxFail"] = "警告：Wave Link 应用（Appx）缺失 —— 应用安装可能失败。";
            Zh["verifyDriverStoreWarn"] = "警告：驱动库未找到 Elgato 驱动包 —— 若音频不可用请重跑驱动安装。";
            Zh["cannotStart"] = "无法启动: ";
            Zh["exitCode"] = "{0} 退出码 {1}";
            Zh["lblProgress"] = "进度";
            Zh["procTimeout"] = "进程 {0} 超过 {1} 分钟未结束，已强制中止（可能底层卡死）。";

            Zh["checkHeader"] = "=== Wave Link Win10 Setup - 环境检查 (--check) ===";
            Zh["checkOs"] = "OS build            : {0} ({1})";
            Zh["checkAdmin"] = "Administrator       : {0}";
            Zh["checkDev"] = "Developer Mode       : {0}";
            Zh["checkRoot"] = "Repo root           : {0}";
            Zh["checkInput"] = "input/ MSIX         : {0}";
            Zh["checkDriver"] = "Driver MSI          : {0}";
            Zh["checkEnd"] = "=== 检查结束 ===";
            Zh["inputNone"] = "无（需放入官方 MSIX）";
            Zh["driverPresent"] = "存在";
            Zh["driverAbsent"] = "缺失（将自动从 CDN 下载）";

            // 更新按钮 / Update button
            Zh["btnUpdate"] = "检查并更新 Wave Link";
            Zh["updCheck"] = "正在查询 Elgato 官方最新 Wave Link 版本 ...";
            Zh["updLatest"] = "最新可用版本: ";
            Zh["updInstalled"] = "当前已安装版本: ";
            Zh["updSame"] = "已是最新（{0}），无需更新。";
            Zh["updDownload"] = "正在下载最新 MSIX（约 180 MB，请耐心等待）...";
            Zh["updDownloaded"] = "已下载到: ";
            Zh["updProgress"] = "下载进度: {0} / {1} MB";
            Zh["updStart"] = "即将以管理员身份运行安装（打补丁 + 驱动）...";
            Zh["updNone"] = "无需更新。";
            Zh["updNoArticle"] = "未找到 Elgato 最新版本文章。";
            Zh["updNoBody"] = "无法读取版本文章正文。";
            Zh["updNoMsix"] = "文章正文中未找到 MSIX 下载链接。";
            Zh["updTooSmall"] = "下载文件过小，可能被拦截（非真实 MSIX）";

            // ---------------- English ----------------
            En["title"] = "Wave Link 3.x · Windows 10 Installer";
            En["langLabel"] = "Language";
            En["lblMsix"] = "Official Wave Link MSIX path (leave empty to auto-use first file in input/)";
            En["btnBrowse"] = "Browse...";
            En["chkSkipApp"] = "Skip app install (already installed)";
            En["chkSkipDriver"] = "Skip driver install (already installed)";
            En["lblMinBuild"] = "Min Win10 build";
            En["btnRunAll"] = "Run All (One-Click)";
            En["btnInstallApp"] = "App Only";
            En["btnInstallDriver"] = "Driver Only";
            En["btnVerify"] = "Verify";
            En["btnCheck"] = "Env Check (dry-run)";
            En["tipLog"] = "Note: install actions need admin rights; clicking will trigger a UAC elevation prompt.";
            En["stepLog"] = "Steps: (1) Browse / put the official MSIX into input/ -> (2) Click \"Run All\" -> (3) Verify the driver is installed.";
            En["dlgTitle"] = "Select official Wave Link MSIX";
            En["dlgFilter"] = "MSIX package (*.msix)|*.msix|All files (*.*)|*.*";
            En["elevateFail"] = "Elevation failed (cancelled or error): ";
            En["errPrefix"] = "Error: ";

            En["runHeader"] = "=== Wave Link 3.x on Windows 10 - Automatic Install ===";
            En["repoRoot"] = "Repo root: ";
            En["osBuild"] = "OS build: {0}";
            En["envBuildLine"] = "OS build: {0} ({1})";
            En["needBuild"] = "Windows 10 1809 (build 17763) or newer required; current is {0}.";
            En["skipApp"] = "(skip app install)";
            En["skipDriver"] = "(skip driver install)";
            En["noMsix"] = "No MSIX found in input/. Put the official Wave Link MSIX into the input/ folder (see input/README.txt).";
            En["msixPath"] = "MSIX: ";
            En["enableDev"] = "Enabling Windows Developer Mode ...";
            En["patchRepack"] = "Patching & repacking (MinVersion -> 10.0.{0}.0) ...";
            En["patchNoOut"] = "Patch produced no output: ";
            En["installPatched"] = "Installing patched MSIX (Developer Mode + self-signed cert) ...";
            En["signing"] = "Signing patched MSIX with self-signed cert (CN=WaveLinkPatch) ...";
            En["noSigntool"] = "signtool.exe not found (Windows 10 SDK required).";
            En["signTool"] = "Sign tool: ";
            En["driverMissingFetch"] = "Driver MSI missing, trying fetch_driver.bat ...";
            En["driverMissing"] = "Driver MSI missing: ";
            En["installDriverMsi"] = "Installing driver MSI (native host /qn) ...";
            En["done"] = "=== Done. Wave Link 3.x driver is installed on Windows 10 (services start on demand). ===";
            En["envHeader"] = "=== Environment Check ===";
            En["envBuildOk"] = "OK (>= 1809)";
            En["envBuildBad"] = "not met, need 1809+";
            En["envAdmin"] = "Administrator: ";
            En["envDev"] = "Developer Mode: ";
            En["envInputMsix"] = "input/ MSIX: ";
            En["envDriverMsi"] = "Driver MSI: ";
            En["envEnd"] = "=== End ===";
            En["devEnabled"] = "Developer Mode enabled.";
            En["appFail"] = "App install failed: Elgato.WaveLink not found (Windows 10 1809/1909 does not support -AllowUnsigned; use the self-signed cert route, see FAQ).";
            En["appInstalled"] = "App installed: ";
            En["cannotStartMsiexec"] = "Cannot start msiexec.";
            En["driverFail"] = "Driver MSI failed, exit code {0}. See {1}";
            En["driverFailPending"] = "Driver installed but services not yet ready (a reboot or PnP enumeration may be pending). MSI exit code {0}. See {1}";
            En["driverOk"] = "Driver installed successfully (starts on demand).";
            En["driverAlready"] = "Driver services already running; skipping install.";
            En["driverMsiExit"] = "Driver MSI exit code: {0}";
            En["driverMsiError"] = "Driver MSI execution error: ";
            En["driverPnpFallback"] = "MSI did not create driver services; falling back to pnputil for bundled driver packages ...";
            En["driverNoPnpDir"] = "Bundled driver directory driver/elgato not found; cannot fall back.";
            En["verifyHeader"] = "Verifying services ...";
            En["verifyAppx"] = "  Appx Elgato.WaveLink : ";
            En["verifyWarn"] = "Warning: some driver services are not running (normal — they start on demand). Confirm the services are installed.";
            En["verifyDriverStore"] = "  Driver store (staged packages): ";
            En["verifyYes"] = "staged";
            En["verifyNo"] = "NOT staged";
            En["verifyOk"] = "Verification passed: driver packages are staged in the driver store and Wave Link is installed. Audio services start on demand.";
            En["verifyAppxFail"] = "Warning: Wave Link app (Appx) is MISSING — the app install may have failed.";
            En["verifyDriverStoreWarn"] = "Warning: Elgato driver packages were NOT found in the driver store — re-run the driver install if audio does not work.";
            En["cannotStart"] = "Cannot start: ";
            En["exitCode"] = "{0} exit code {1}";
            En["lblProgress"] = "Progress";
            En["procTimeout"] = "Process {0} did not finish within {1} minutes; forcibly aborted (possible underlying hang).";

            En["checkHeader"] = "=== Wave Link Win10 Setup - Environment Check (--check) ===";
            En["checkOs"] = "OS build            : {0} ({1})";
            En["checkAdmin"] = "Administrator       : {0}";
            En["checkDev"] = "Developer Mode       : {0}";
            En["checkRoot"] = "Repo root           : {0}";
            En["checkInput"] = "input/ MSIX         : {0}";
            En["checkDriver"] = "Driver MSI          : {0}";
            En["checkEnd"] = "=== Check finished ===";
            En["inputNone"] = "none (put official MSIX)";
            En["driverPresent"] = "present";
            En["driverAbsent"] = "missing (will auto-download from CDN)";

            // Update button
            En["btnUpdate"] = "Check && Update Wave Link";
            En["updCheck"] = "Querying Elgato for the latest Wave Link version ...";
            En["updLatest"] = "Latest available version: ";
            En["updInstalled"] = "Currently installed version: ";
            En["updSame"] = "Already up to date ({0}); no update needed.";
            En["updDownload"] = "Downloading the latest MSIX (~180 MB, please wait) ...";
            En["updDownloaded"] = "Downloaded to: ";
            En["updProgress"] = "Download progress: {0} / {1} MB";
            En["updStart"] = "About to install as admin (patch + driver) ...";
            En["updNone"] = "No update needed.";
            En["updNoArticle"] = "Could not find Elgato's latest-version article.";
            En["updNoBody"] = "Could not read the version article body.";
            En["updNoMsix"] = "No MSIX download link found in the article body.";
            En["updTooSmall"] = "Downloaded file too small; likely blocked (not a real MSIX)";
        }
    }
}
