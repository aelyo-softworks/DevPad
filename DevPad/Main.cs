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
        private bool _languagesLoaded;

        public Main()
        {
            InitializeComponent();
            toolStripStatusPosition.Text = string.Empty;
            toolStripStatusSelection.Text = string.Empty;
            toolStripStatusLanguage.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLanguage.Text = LanguageExtensionPoint.DefaultLanguageName;
            Task.Run(() => Settings.Current.CleanRecentFiles());
            Icon = Resources.Resources.DevPadIcon;
            Text = WinformsUtilities.ApplicationName;
            _dpo.Load += DevPadOnLoad;
            _dpo.Event += DevPadEvent;
            _ = InitializeAsync();
        }

        public bool IsMonacoReady { get; private set; }
        public bool IsEditorCreated { get; private set; }
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

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != MessageDecoder.WM_SETCURSOR && m.Msg != MessageDecoder.WM_GETICON && m.Msg != MessageDecoder.WM_NCMOUSEMOVE)
            {
                //Program.Trace(MessageDecoder.Decode(m));
            }

            if (m.Msg == MessageDecoder.WM_SYSKEYUP)
            {
                if (((Keys)(int)(long)m.WParam) == Keys.Escape)
                {
                    _ = FocusEditorAsync();
                }
            }

            if (m.Msg == MessageDecoder.WM_PARENTNOTIFY)
            {
                var lo = MessageDecoder.LOWORD(m.WParam);
                switch (lo)
                {
                    case MessageDecoder.WM_RBUTTONDOWN:
                    case MessageDecoder.WM_MBUTTONDOWN:
                    case MessageDecoder.WM_LBUTTONDOWN:
                    case MessageDecoder.WM_XBUTTONDOWN:
                        var x = MessageDecoder.LOWORD(m.LParam);
                        var y = MessageDecoder.HIWORD(m.LParam);
                        //Program.Trace("x:" + x + " y:" + y);
                        if (y < MainMenu.Height)
                        {
                            this.SafeBeginInvoke(async () =>
                            {
                                await BlurEditorAsync();
                                MainMenu.Focus();
                            });
                        }
                        else
                        {
                            _ = FocusEditorAsync();
                        }
                        break;
                }
            }
            base.WndProc(ref m);
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

        private async Task FocusEditorAsync()
        {
            MainMenu.HideDropDowns();
            await WebView.ExecuteScriptAsync("editor.focus()");
            WebView.Focus();
        }

        private async Task BlurEditorAsync()
        {
            await WebView.ExecuteScriptAsync("editor.blur()");
        }

        private async Task SetEditorThemeAsync(string theme = null)
        {
            theme = theme.Nullify() ?? "vs";
            await WebView.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')");
        }

        private async Task SetEditorPositionAsync(int lineNumber = 0, int column = 0)
        {
            //await WebView.ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
        }

        private void SetFilePath(string filePath)
        {
            if (CurrentFilePath.EqualsIgnoreCase(filePath))
                return;

            CurrentFilePath = filePath;
            if (filePath == null)
            {
                Text = WinformsUtilities.ApplicationName;
                return;
            }

            Text = WinformsUtilities.ApplicationName + " - " + filePath;
            Settings.Current.AddRecentFile(filePath);
            Settings.Current.SerializeToConfiguration();
            WindowsUtilities.SHAddToRecentDocs(filePath);
            //Program.WindowsApplication.PublishRecentList();
        }

        private bool DiscardChanges()
        {
            if (!HasContentChanged)
                return true;

            return this.ShowConfirm(Resources.Resources.ConfirmDiscard) == DialogResult.Yes;
        }

        private static string UnescapeEditorText(string text)
        {
            if (text == null)
                return null;

            if (text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                text = text.Substring(1, text.Length - 2);
            }
            return Regex.Unescape(text);
        }

        private async Task<string> GetEditorTextAsync()
        {
            var text = await WebView.ExecuteScriptAsync("editor.getValue()");
            if (text == null)
            {
                this.ShowError(Resources.Resources.ErrorGetText);
                return null;
            }
            return UnescapeEditorText(text);
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
                lang = LanguageExtensionPoint.DefaultLanguageId;
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
                lang = LanguageExtensionPoint.DefaultLanguageId;
            }

            // not this the most performant load system... we should make chunks
            _documentText = File.ReadAllText(filePath);
            await WebView.ExecuteScriptAsync($"loadFromHost('{lang}')");
            HasContentChanged = false;
            SetFilePath(filePath);
            await SetEditorPositionAsync();
        }

        private async Task NewAsync()
        {
            if (!DiscardChanges())
                return;

            _documentText = string.Empty;
            await WebView.ExecuteScriptAsync("loadFromHost('plaintext')");
            HasContentChanged = false;
            await FocusEditorAsync();
            await SetEditorPositionAsync();
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
            Program.Trace(e);
            switch (e.EventType)
            {
                case DevPadEventType.ContentChanged:
                    HasContentChanged = true;
                    break;

                case DevPadEventType.EditorLostFocus:
                    break;

                case DevPadEventType.KeyDown:
                    var ke = (DevPadKeyEventArgs)e;
                    //Trace.WriteLine("Key " + ke.Code + " " + ke.KeyCode + " Alt:" + ke.Alt + " Shift:" + ke.Shift + " Ctrl:" + ke.Ctrl + " Meta:" + ke.Meta + " AltG:" + ke.AltGraph + " Keys:" + ke.Keys);
                    OnKeyDown(new KeyEventArgs(ke.Keys));
                    break;

                case DevPadEventType.EditorCreated:
                    HasContentChanged = false;
                    IsEditorCreated = true;
                    await SetEditorThemeAsync(Settings.Current.Theme);
                    await FocusEditorAsync();
                    var open = CommandLine.GetNullifiedArgument(0);
                    if (open != null)
                    {
                        await OpenFileAsync(open);
                    }
                    break;

                case DevPadEventType.ModelLanguageChanged:
                    var langId = e.RootElement.GetNullifiedValue("newLanguage");
                    if (langId != null)
                    {
                        this.SafeBeginInvoke(async () =>
                        {
                            var text = await WebView.GetLanguageName(langId);
                            toolStripStatusLanguage.Text = text ?? langId;
                        });
                    }
                    else
                    {
                        toolStripStatusLanguage.Text = string.Empty;
                    }
                    break;

                case DevPadEventType.CursorPositionChange:
                    var line = e.RootElement.GetValue("position.lineNumber", -1);
                    var column = e.RootElement.GetValue("position.column", -1);
                    if (line >= 0 && column >= 0)
                    {
                        toolStripStatusPosition.Text = string.Format(Resources.Resources.StatusPosition, line, column);
                    }
                    else
                    {
                        toolStripStatusPosition.Text = string.Empty;
                    }
                    break;

                case DevPadEventType.CursorSelectionChanged:
                    this.SafeBeginInvoke(async () =>
                    {
                        var text = await WebView.ExecuteScriptAsync("editor.getModel().getValueInRange(editor.getSelection())");
                        text = UnescapeEditorText(text) ?? string.Empty;
                        if (text.Length == 0)
                        {
                            toolStripStatusSelection.Text = string.Empty;
                        }
                        else
                        {
                            toolStripStatusSelection.Text = string.Format(Resources.Resources.StatusSelection, text.Length);
                        }
                    });
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

        private async void ViewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (_languagesLoaded)
                return;

            var langs = await WebView.GetLanguages();
            setLanguageToolStripMenuItem.DropDownItems.Clear();
            foreach (var group in langs.OrderBy(k => k.Value.Name).GroupBy(n => n.Value.Name.Substring(0, 1), comparer: StringComparer.OrdinalIgnoreCase))
            {
                var subLangs = group.OrderBy(l => l.Value.Name).ToArray();
                if (subLangs.Length > 1)
                {
                    var item = new ToolStripMenuItem(group.Key.ToUpperInvariant());
                    setLanguageToolStripMenuItem.DropDownItems.Add(item);
                    foreach (var lang in group.OrderBy(l => l.Value.Name))
                    {
                        var subItem = item.DropDownItems.Add(lang.Value.Name);
                        subItem.Click += async (s, e2) =>
                        {
                            await SetEditorLanguage(lang.Key);
                        };
                    }
                }
                else
                {
                    var item = setLanguageToolStripMenuItem.DropDownItems.Add(subLangs[0].Value.Name);
                    item.Click += async (s, e2) =>
                    {
                        await SetEditorLanguage(subLangs[0].Key);
                    };
                }
            }
            _languagesLoaded = true;
        }

        private void PreferencesToolStripMenuItem_Click(object sender, EventArgs e2)
        {
            var dlg = new SettingsForm();
            dlg.Settings.PropertyChanged += async (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.Theme):
                        await SetEditorThemeAsync(dlg.Settings.Theme);
                        break;
                }
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Current.CopyFrom(dlg.Settings);
                Settings.Current.SerializeToConfiguration();
            }
            _ = SetEditorThemeAsync(Settings.Current.Theme);
        }

        private void GoToToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }
    }
}
