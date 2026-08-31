using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WaveLinkWin10Setup
{
    public partial class MainForm : Form
    {
        TextBox txtMsix;
        Button btnBrowse;
        CheckBox chkSkipApp;
        CheckBox chkSkipDriver;
        NumericUpDown numMinBuild;
        Label lblMsix;
        Label lblMinBuild;
        Button btnRunAll;
        Button btnInstallApp;
        Button btnInstallDriver;
        Button btnVerify;
        Button btnCheck;
        Button btnUpdate;
        ProgressBar progBar;
        Label lblStatus;
        TextBox txtLog;
        Label lblLang;
        ComboBox cboLang;
        string pendingRun;

        public MainForm(string[] args)
        {
            InitializeComponent();
            ParseArgs(args);
        }

        void ParseArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == "--run" && i + 1 < args.Length) pendingRun = args[++i];
                else if (a == "--msix" && i + 1 < args.Length) txtMsix.Text = args[++i];
                else if (a == "--minbuild" && i + 1 < args.Length)
                {
                    if (decimal.TryParse(args[++i], out var v)) numMinBuild.Value = v;
                }
                else if (a == "--skipapp") chkSkipApp.Checked = true;
                else if (a == "--skipdriver") chkSkipDriver.Checked = true;
            }

            // Auto-execute when launched elevated by RunElevated.
            if (pendingRun != null)
            {
                this.Load += (s, e) => DoRun(pendingRun);
            }
        }

        void InitializeComponent()
        {
            Text = Lang.T("title");
            ClientSize = new System.Drawing.Size(760, 580);
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            lblMsix = new Label { Left = 12, Top = 14, Width = 440, Height = 18, Text = Lang.T("lblMsix") };
            txtMsix = new TextBox { Left = 12, Top = 36, Width = 520, Height = 23 };
            btnBrowse = new Button { Left = 544, Top = 34, Width = 96, Height = 26, Text = Lang.T("btnBrowse") };
            btnBrowse.Click += (s, e) => BrowseMsix();

            lblLang = new Label { Left = 470, Top = 12, Width = 60, Height = 18, Text = Lang.T("langLabel") };
            cboLang = new ComboBox { Left = 536, Top = 10, Width = 120, Height = 23, DropDownStyle = ComboBoxStyle.DropDownList };
            cboLang.Items.Add("中文");
            cboLang.Items.Add("English");
            cboLang.SelectedIndex = Lang.Mode == "zh" ? 0 : 1;
            cboLang.SelectedIndexChanged += (s, e) =>
            {
                Lang.SetMode(cboLang.SelectedIndex == 0 ? "zh" : "en");
                ApplyLang();
            };

            chkSkipApp = new CheckBox { Left = 12, Top = 70, Width = 200, Height = 22, Text = Lang.T("chkSkipApp") };
            chkSkipDriver = new CheckBox { Left = 220, Top = 70, Width = 200, Height = 22, Text = Lang.T("chkSkipDriver") };

            lblMinBuild = new Label { Left = 470, Top = 70, Width = 120, Height = 22, Text = Lang.T("lblMinBuild") };
            numMinBuild = new NumericUpDown { Left = 596, Top = 68, Width = 120, Height = 23, Minimum = 17134, Maximum = 22000, Value = 19041, Increment = 1 };

            btnRunAll = new Button { Left = 12, Top = 104, Width = 140, Height = 30, Text = Lang.T("btnRunAll") };
            btnInstallApp = new Button { Left = 162, Top = 104, Width = 130, Height = 30, Text = Lang.T("btnInstallApp") };
            btnInstallDriver = new Button { Left = 302, Top = 104, Width = 130, Height = 30, Text = Lang.T("btnInstallDriver") };
            btnVerify = new Button { Left = 442, Top = 104, Width = 120, Height = 30, Text = Lang.T("btnVerify") };
            btnCheck = new Button { Left = 572, Top = 104, Width = 160, Height = 30, Text = Lang.T("btnCheck") };

            btnRunAll.Click += (s, e) => RunElevated("all");
            btnInstallApp.Click += (s, e) => RunElevated("app");
            btnInstallDriver.Click += (s, e) => RunElevated("driver");
            btnVerify.Click += (s, e) => DoRun("verify");
            btnCheck.Click += (s, e) => { Log(""); Installer.EnvCheckGui(Log); };

            btnUpdate = new Button { Left = 12, Top = 140, Width = 230, Height = 28, Text = Lang.T("btnUpdate") };
            btnUpdate.Click += (s, e) => StartUpdate();

            progBar = new ProgressBar { Left = 254, Top = 140, Width = 380, Height = 22, Minimum = 0, Maximum = 100, Value = 0, Style = ProgressBarStyle.Continuous };
            lblStatus = new Label { Left = 642, Top = 140, Width = 106, Height = 22, Text = "0%", TextAlign = System.Drawing.ContentAlignment.MiddleLeft };

            txtLog = new TextBox
            {
                Left = 12,
                Top = 176,
                Width = 736,
                Height = 392,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 9.5F)
            };

            Controls.AddRange(new Control[] { lblMsix, txtMsix, btnBrowse, lblLang, cboLang, chkSkipApp, chkSkipDriver,
                lblMinBuild, numMinBuild, btnRunAll, btnInstallApp, btnInstallDriver, btnVerify, btnCheck, btnUpdate, progBar, lblStatus, txtLog });

            Log(Lang.T("tipLog"));
            Log(Lang.T("stepLog"));
        }

        /// <summary>Refresh all control captions to the current language.</summary>
        void ApplyLang()
        {
            Text = Lang.T("title");
            lblMsix.Text = Lang.T("lblMsix");
            btnBrowse.Text = Lang.T("btnBrowse");
            chkSkipApp.Text = Lang.T("chkSkipApp");
            chkSkipDriver.Text = Lang.T("chkSkipDriver");
            lblMinBuild.Text = Lang.T("lblMinBuild");
            btnRunAll.Text = Lang.T("btnRunAll");
            btnInstallApp.Text = Lang.T("btnInstallApp");
            btnInstallDriver.Text = Lang.T("btnInstallDriver");
            btnVerify.Text = Lang.T("btnVerify");
            btnCheck.Text = Lang.T("btnCheck");
            btnUpdate.Text = Lang.T("btnUpdate");
            lblLang.Text = Lang.T("langLabel");
            cboLang.SelectedIndex = Lang.Mode == "zh" ? 0 : 1;
        }

        void BrowseMsix()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = Lang.T("dlgFilter"),
                Title = Lang.T("dlgTitle")
            };
            if (dlg.ShowDialog() == DialogResult.OK) txtMsix.Text = dlg.FileName;
        }

        void Log(string s)
        {
            if (txtLog.InvokeRequired) { txtLog.Invoke(new Action<string>(Log), s); return; }
            txtLog.AppendText(s + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        /// <summary>
        /// Install actions need admin. If not elevated, relaunch this exe elevated with the same
        /// options and exit the current (non-admin) instance.
        /// </summary>
        void RunElevated(string mode)
        {
            if (Installer.IsAdmin()) { DoRun(mode); return; }

            var a = "--run " + mode;
            if (!string.IsNullOrWhiteSpace(txtMsix.Text)) a += " --msix \"" + txtMsix.Text + "\"";
            a += " --minbuild " + numMinBuild.Value;
            if (chkSkipApp.Checked) a += " --skipapp";
            if (chkSkipDriver.Checked) a += " --skipdriver";

            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, a)
                {
                    Verb = "runas",
                    UseShellExecute = true
                });
                Application.Exit();
            }
            catch (Exception ex)
            {
                Log(Lang.T("elevateFail") + ex.Message);
            }
        }

        /// <summary>
        /// Runs the install/verify flow on a background thread so the UI stays responsive
        /// (the previous version called Installer.Run synchronously on the UI thread, which
        /// blocked the message loop during the long PowerShell/msiexec steps and made the
        /// window report "未响应"). Progress is reported to the progress bar via IProgress.
        /// </summary>
        void DoRun(string mode)
        {
            SetControlsEnabled(false);
            SetBusy(true);
            Action<int> progress = SetProgress;
            Task.Run(() =>
            {
                try
                {
                    Log("");
                    if (mode == "verify")
                        Installer.VerifyOnly(Log, progress);
                    else
                        Installer.Run(mode, txtMsix.Text, (int)numMinBuild.Value,
                            chkSkipApp.Checked, chkSkipDriver.Checked, Log, progress);
                    SetProgress(100);
                    SetBusy(false);
                }
                catch (Exception ex)
                {
                    Log(Lang.T("errPrefix") + ex.Message);
                    SetProgress(0);
                    SetBusy(false);
                }
                finally
                {
                    try { this.Invoke(new Action(() => SetControlsEnabled(true))); } catch { }
                }
            });
        }

        /// <summary>Enable/disable all interactive controls during a run.</summary>
        void SetControlsEnabled(bool enabled)
        {
            if (InvokeRequired) { Invoke(new Action<bool>(SetControlsEnabled), enabled); return; }
            btnRunAll.Enabled = enabled;
            btnInstallApp.Enabled = enabled;
            btnInstallDriver.Enabled = enabled;
            btnVerify.Enabled = enabled;
            btnCheck.Enabled = enabled;
            btnUpdate.Enabled = enabled;
            btnBrowse.Enabled = enabled;
            cboLang.Enabled = enabled;
            chkSkipApp.Enabled = enabled;
            chkSkipDriver.Enabled = enabled;
            numMinBuild.Enabled = enabled;
        }

        /// <summary>Marquee mode while a step of unknown duration is in flight.</summary>
        void SetBusy(bool busy)
        {
            if (progBar.InvokeRequired) { progBar.Invoke(new Action<bool>(SetBusy), busy); return; }
            progBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (busy) lblStatus.Text = Lang.T("lblProgress") + " ...";
        }

        /// <summary>Set the progress bar to a concrete percentage (0-100).</summary>
        void SetProgress(int pct)
        {
            if (progBar.InvokeRequired) { progBar.Invoke(new Action<int>(SetProgress), pct); return; }
            progBar.Style = ProgressBarStyle.Continuous;
            progBar.Value = Math.Max(0, Math.Min(100, pct));
            lblStatus.Text = Lang.T("lblProgress") + " " + pct + "%";
        }

        /// <summary>
        /// "Check &amp; Update" button: detect the latest Wave Link from Elgato, download it in the
        /// background, then launch an elevated install of the downloaded MSIX.
        /// </summary>
        void StartUpdate()
        {
            btnUpdate.Enabled = false;
            Log("");
            Task.Run(async () =>
            {
                string local = "";
                try { local = await Updater.UpdateAsync(Log); }
                catch (Exception ex) { Log(Lang.T("errPrefix") + ex.Message); }

                try
                {
                    if (!string.IsNullOrWhiteSpace(local))
                    {
                        this.Invoke(new Action(() =>
                        {
                            txtMsix.Text = local;
                            Log(Lang.T("updStart"));
                            RunElevated("all");
                        }));
                    }
                    else
                    {
                        Log(Lang.T("updNone"));
                    }
                }
                catch (Exception ex) { Log(Lang.T("errPrefix") + ex.Message); }
                finally
                {
                    try { this.Invoke(new Action(() => btnUpdate.Enabled = true)); } catch { }
                }
            });
        }
    }
}
