using System;
using System.Windows.Forms;

namespace WaveLinkWin10Setup
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Headless environment check (no GUI, no elevation needed for reads).
            if (args.Length > 0 && args[0] == "--check")
            {
                EnvironmentCheck.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
