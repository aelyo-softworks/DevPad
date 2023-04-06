using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevPad.Setup.Utilities;
using DevPad.Utilities;
using Microsoft.Win32;

namespace DevPad.Setup
{
    public partial class Main : Form
    {
        private readonly string _tempFilePath;
        private readonly CancellationTokenSource _cancelSource = new CancellationTokenSource();
        private bool _paused;

        public Main(string tempFilePath)
        {
            _tempFilePath = tempFilePath;
            InitializeComponent();
            Icon = Resources.Resources.DevPadIcon;
#if DEBUG
            Text += " - Debug";
#endif
        }

        protected async override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Exception error = null;
            var completed = await Task.Run(() =>
            {
                try
                {
                    return Install(_cancelSource.Token);
                }
                catch (Exception ex)
                {
                    Program.Trace("Error:" + ex);
                    error = ex;
                    return false;
                }
            });

            buttonCancel.Enabled = false;

            if (error != null)
            {
                this.ShowError(string.Format(Resources.Resources.ErrorOccurred, error.GetAllMessages()));
                Close();
            }

            if (completed)
            {
                this.ShowMessage(Resources.Resources.InstallCompleted);
            }
            else
            {
                this.ShowWarning(Resources.Resources.InstallWasCancelled);
            }

            Close();
        }

        private bool WaitForPauseOrCancel(CancellationToken cancellationToken)
        {
            while (_paused)
            {
                if (cancellationToken.IsCancellationRequested)
                    return true;

                Thread.Sleep(100);
            }
            return false;
        }

        internal static string GetTargetDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AssemblyUtilities.GetProduct());
        private bool Install(CancellationToken cancellationToken)
        {
            var targetDir = GetTargetDir();
            var version = AssemblyUtilities.GetFileVersion();
            var targetApp = Path.Combine(targetDir, version);
            var size = 0L;
            using (var file = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                size += file.Length;
                if (WaitForPauseOrCancel(cancellationToken))
                    return false;

                using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    var max = archive.Entries.Count;
                    this.SafeBeginInvoke(() =>
                    {
                        progressBarMain.Maximum = max;
                    });

                    foreach (var entry in archive.Entries)
                    {
                        if (WaitForPauseOrCancel(cancellationToken))
                            return false;

                        var targetFile = Path.Combine(targetApp, entry.FullName);
                        IOUtilities.FileEnsureDirectory(targetFile);
                        entry.ExtractToFile(targetFile, true);
                        Thread.Sleep(10); // useless but otherwise, it's too fast
                        size += entry.Length;

                        this.SafeBeginInvoke(() =>
                        {
                            labelProgress.Text = string.Format(Resources.Resources.CopyingFile, entry.Name);
                            progressBarMain.Value++;
                        });
                    }
                }
            }

            // copy for uninstall
            var path = Process.GetCurrentProcess().MainModule.FileName;
            var fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            IOUtilities.FileOverwrite(path, Path.Combine(targetDir, fileName));

            // create shortcut
            AddShortcuts(targetDir);

            // register uninstall keys
            AddToUninstall(targetDir, fileName, (int)size);

            Program.WindowsApplication.Register();
            return true;
        }

        private static void AddToUninstall(string targetDir, string fileName, int size)
        {
            using (var key = WindowsUtilities.EnsureSubKey(Registry.CurrentUser, Path.Combine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Aelyo.DevPad")))
            {
                key.SetValue("DisplayName", AssemblyUtilities.GetProduct());
                key.SetValue("DisplayVersion", AssemblyUtilities.GetFileVersion());
                key.SetValue("DisplayIcon", Path.Combine(targetDir, fileName) + ",0");
                key.SetValue("Publisher", AssemblyUtilities.GetCompany());
                key.SetValue("URLInfoAbout", "https://github.com/aelyo-softworks/DevPad");
                key.SetValue("HelpLink", "https://github.com/aelyo-softworks/DevPad");
                key.SetValue("NoModify", 1);
                key.SetValue("NoRepair", 1);
                key.SetValue("EstimatedSize", size / 1000);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", "\"" + Path.Combine(targetDir, fileName) + "\" /uninstall");
            }
        }

        private static void RemoveFromUninstall()
        {
            Registry.CurrentUser.DeleteSubKeyTree(Path.Combine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Aelyo.DevPad"), false);
        }

        private void AddShortcuts(string targetDir)
        {
            var version = AssemblyUtilities.GetFileVersion();
            var product = AssemblyUtilities.GetProduct();
            var link = new Link
            {
                Path = Path.Combine(targetDir, version, "DevPad.exe")
            };
            link.Save(Path.Combine(targetDir, product + ".lnk"));

            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            link.Save(Path.Combine(programs, product + ".lnk"));
        }

        internal static void Uninstall()
        {
            var path = Process.GetCurrentProcess().MainModule.FileName;
            var targetDir = GetTargetDir();

            if (path.StartsWith(targetDir))
            {
                // copy in temp and restart
                var tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(path));
                IOUtilities.FileOverwrite(path, tempPath, true, true);

                var arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
                Process.Start(tempPath, arguments);
                return;
            }

            Program.WindowsApplication.Unregister();
            RemoveFromUninstall();
            IOUtilities.DirectoryDelete(targetDir, true, false);

            var product = AssemblyUtilities.GetProduct();
            var programsShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), product + ".lnk");
            IOUtilities.FileDelete(programsShortcut, true, false);

            WinformsUtilities.ShowMessage(null, Resources.Resources.UninstallCompleted);
        }

        protected override void OnClosed(EventArgs e)
        {
            _paused = false;
            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            _paused = true;
            if (this.ShowConfirm(Resources.Resources.CancelConfirm) == DialogResult.Yes)
            {
                _cancelSource.Cancel();
                return;
            }

            _paused = false;
        }
    }
}
