using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace WaveLinkWin10Setup
{
    /// <summary>
    /// Read-only environment check used by the "--check" CLI mode.
    /// Writes both to the console and to env_check.txt next to the exe (a WinExe
    /// has no console, so the file is the reliable artifact).
    /// </summary>
    public static class EnvironmentCheck
    {
        public static void Run()
        {
            var lines = new System.Collections.Generic.List<string>();
            void Emit(string s) { lines.Add(s); Console.WriteLine(s); }

            Emit(Lang.T("checkHeader"));
            int build = Installer.GetOsBuild();
            Emit(string.Format(Lang.T("checkOs"), build, build >= 17763 ? Lang.T("envBuildOk") : Lang.T("envBuildBad")));
            Emit(string.Format(Lang.T("checkAdmin"), Installer.IsAdmin()));

            bool dev = false;
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                dev = k?.GetValue("AllowDevelopmentWithoutDevLicense") as int? == 1;
            Emit(string.Format(Lang.T("checkDev"), dev));

            string root = Installer.RepoRoot;
            Emit(string.Format(Lang.T("checkRoot"), root));

            var inputDir = Path.Combine(root, "input");
            var msixs = Directory.Exists(inputDir) ? Directory.GetFiles(inputDir, "*.msix") : new string[0];
            Emit(string.Format(Lang.T("checkInput"), msixs.Length > 0 ? string.Join(", ", msixs) : Lang.T("inputNone")));

            var msi = Path.Combine(root, "driver", "WaveLinkDriver_3.0.0.466_x64.msi");
            Emit(string.Format(Lang.T("checkDriver"), File.Exists(msi) ? Lang.T("driverPresent") : Lang.T("driverAbsent")));

            Emit(Lang.T("checkEnd"));

            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "env_check.txt");
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch { /* non-fatal */ }
        }
    }
}
