using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevPad.MonacoModel;
using DevPad.Utilities;
using Microsoft.Web.WebView2.Core;

namespace DevPad
{
    public partial class Main : Form
    {
        private readonly DevPadObject _dpo = new DevPadObject();
        private string _documentText;

        public Main()
        {
            InitializeComponent();
            Icon = Resources.Resources.DevPadIcon;
            _dpo.Load += DevPadOnLoad;
            _dpo.Event += DevPadEvent;
            _ = InitializeAsync();
            Task.Run(() => Settings.Current.CleanRecentFiles());
        }

        public bool IsMonacoReady { get; private set; }
        public bool HasContentChanged { get; private set; }
        public string CurrentFilePath { get; private set; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // since the webview eats keyboard, we handle some here

            if (e.KeyCode == Keys.N && e.Control)
            {
                _ = NewAsync();
                return;
            }

            if (e.KeyCode == Keys.O && e.Control)
            {
                _ = OpenAsync();
                return;
            }

            if (e.KeyCode == Keys.S && e.Control)
            {
                if (e.Shift)
                {
                    _ = SaveAsAsync();
                }
                else
                {
                    _ = SaveAsync();
                }
                return;
            }

            if (e.KeyCode == Keys.T && e.Shift && e.Control)
            {
                var lastRecent = Settings.Current.RecentFilesPaths?.FirstOrDefault();
                if (lastRecent != null)
                {
                    if (!DiscardChanges())
                        return;

                    _ = OpenFileAsync(lastRecent.FilePath);
                }
                return;
            }
        }

        private async Task InitializeAsync()
        {
            var udf = Settings.Current.UserDataFolder;
            var env = await CoreWebView2Environment.CreateAsync(null, udf);
            await WebView.EnsureCoreWebView2Async(env);

            // this is the name of the host object in js code
            // called like this: chrome.webview.hostObjects.sync.devPad.MyCustomMethod();
            WebView.CoreWebView2.AddHostObjectToScript("devPad", _dpo);
            WebView.Source = new Uri(Path.Combine(Application.StartupPath, @"Monaco\index.html"));
            IsMonacoReady = true;
        }

        private void SetFilePath(string filePath)
        {
            if (CurrentFilePath.EqualsIgnoreCase(filePath))
                return;

            CurrentFilePath = filePath;
            if (filePath == null)
            {
                Text = WinformsUtilities.ApplicationName;
            }
            else
            {
                Text = WinformsUtilities.ApplicationName + " - " + filePath;
                Settings.Current.AddRecentFile(filePath);
                Settings.Current.SerializeToConfiguration();
            }
        }

        private bool DiscardChanges()
        {
            if (!HasContentChanged)
                return true;

            return this.ShowConfirm(Resources.Resources.ConfirmDiscard) == DialogResult.Yes;
        }

        private async Task<string> GetEditorTextAsync()
        {
            var text = await WebView.ExecuteScriptAsync("editor.getValue()");
            if (text == null)
            {
                this.ShowError(Resources.Resources.ErrorGetText);
                return null;
            }

            if (text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                text = text.Substring(1, text.Length - 2);
            }
            return Regex.Unescape(text);
        }

        private Task SetEditorLanguage(string lang) => WebView.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang}');");
        private async Task SetEditorLanguageFromFilePath(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            var langs = await WebView.GetLanguagesByExtension();
            string lang;
            if (langs.TryGetValue(ext, out var langObject))
            {
                lang = langObject[0].Id;
            }
            else
            {
                lang = "plaintext";
            }
            await SetEditorLanguage(lang);
        }

        private async Task<(string, int)> BuildFilter()
        {
            var languages = await WebView.GetLanguages();
            var sb = new StringBuilder();
            var index = 0;
            foreach (var kv in languages.OrderBy(k => k.Value.Name))
            {
                if (kv.Value.Extensions == null || kv.Value.Extensions.Length == 0)
                    continue;

                if (sb.Length > 0)
                {
                    sb.Append('|');
                }
                sb.Append(string.Format(Resources.Resources.OneFileFilter, kv.Value.Name, "*" + string.Join(";*", kv.Value.Extensions)));
                index++;
            }
            sb.Append(Resources.Resources.AllFilesFilter);
            return (sb.ToString(), index);
        }

        private async Task OpenAsync()
        {
            if (!DiscardChanges())
                return;

            var filter = await BuildFilter();
            var fd = new OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = filter.Item1,
                FilterIndex = filter.Item2 + 1
            };
            if (fd.ShowDialog(this) != DialogResult.OK)
                return;

            await OpenFileAsync(fd.FileName);
        }

        private async Task OpenFileAsync(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            var langs = await WebView.GetLanguagesByExtension();
            string lang;
            if (langs.TryGetValue(ext, out var langObject))
            {
                lang = langObject[0].Id;
            }
            else
            {
                lang = "plaintext";
            }

            // not this the most performant load system... we should make chunks
            _documentText = File.ReadAllText(filePath);
            await WebView.ExecuteScriptAsync($"loadFromHost('{lang}')");
            HasContentChanged = false;
            SetFilePath(filePath);
        }

        private async Task NewAsync()
        {
            if (!DiscardChanges())
                return;

            _documentText = string.Empty;
            await WebView.ExecuteScriptAsync("loadFromHost('plaintext')");
            HasContentChanged = false;
        }

        private async Task SaveAsAsync()
        {
            var filter = await BuildFilter();
            var fd = new SaveFileDialog
            {
                RestoreDirectory = true,
                CheckPathExists = true,
                Filter = filter.Item1,
                FilterIndex = filter.Item2 + 1
            };
            if (fd.ShowDialog(this) != DialogResult.OK)
                return;

            await SaveAsync(fd.FileName);
            await SetEditorLanguageFromFilePath(fd.FileName);
        }

        private async Task SaveAsync()
        {
            if (CurrentFilePath == null)
            {
                await SaveAsAsync();
                return;
            }
            await SaveAsync(CurrentFilePath);
        }

        private async Task SaveAsync(string filePath)
        {
            var text = await GetEditorTextAsync();
            if (text == null)
                return;

            File.WriteAllText(filePath, text);
            SetFilePath(filePath);
            HasContentChanged = false;
        }

        private async void DevPadEvent(object sender, DevPadEventArgs e)
        {
            switch (e.EventType)
            {
                case DevPadEventType.ContentChanged:
                    HasContentChanged = true;
                    break;

                case DevPadEventType.KeyDown:
                    var ke = (DevPadKeyEventArgs)e;
                    //Trace.WriteLine("Key " + ke.Code + " " + ke.KeyCode + " Alt:" + ke.Alt + " Shift:" + ke.Shift + " Ctrl:" + ke.Ctrl + " Meta:" + ke.Meta + " AltG:" + ke.AltGraph + " Keys:" + ke.Keys);
                    OnKeyDown(new KeyEventArgs(ke.Keys));
                    break;

                case DevPadEventType.EditorCreated:
                    HasContentChanged = false;
                    await WebView.ExecuteScriptAsync($"editor.focus()");
                    break;
            }
        }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            e.DocumentText = _documentText;
        }

        private void AboutDevPadToolStripMenuItem_Click(object sender, EventArgs e) { var dlg = new AboutForm(); dlg.ShowDialog(this); }
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e) => _ = OpenAsync();
        private void ClearRecentListToolStripMenuItem_Click(object sender, EventArgs e) => Settings.Current.ClearRecentFiles();
        private void NewToolStripMenuItem_Click(object sender, EventArgs e) => _ = NewAsync();
        private void NewWindowToolStripMenuItem_Click(object sender, EventArgs e) => Process.Start(Application.ExecutablePath);
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e) => Close();
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e) => _ = SaveAsync();
        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e) => _ = SaveAsAsync();
        private void FileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            const int fixedRecentItemsCount = 2;
            while (openRecentToolStripMenuItem.DropDownItems.Count > fixedRecentItemsCount)
            {
                openRecentToolStripMenuItem.DropDownItems.RemoveAt(0);
            }

            var recents = Settings.Current.RecentFilesPaths;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    var item = new ToolStripMenuItem(recent.FilePath);
                    openRecentToolStripMenuItem.DropDownItems.Insert(openRecentToolStripMenuItem.DropDownItems.Count - fixedRecentItemsCount, item);
                    item.Click += async (s, ex) =>
                    {
                        if (!DiscardChanges())
                            return;

                        await OpenFileAsync(recent.FilePath);
                    };
                }
            }
            openRecentToolStripMenuItem.Enabled = openRecentToolStripMenuItem.DropDownItems.Count > fixedRecentItemsCount;
        }

        private bool _loaded;
        private async void viewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (_loaded)
                return;

            _loaded = true;
            var langs = await WebView.GetLanguages();
            setLanguageToolStripMenuItem.DropDownItems.Clear();
            foreach (var kv in langs.OrderBy(k => k.Value.Name))
            {
                var item = setLanguageToolStripMenuItem.DropDownItems.Add(kv.Value.Name);
                item.Click += async (s, e2) =>
                {
                    await SetEditorLanguage(kv.Key);
                };
            }
        }
    }
}
