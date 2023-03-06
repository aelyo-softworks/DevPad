using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevPad.MonacoModel;
using DevPad.Utilities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DevPad
{
    public class WebPage : TabPage
    {
        private readonly DevPadObject _dpo = new DevPadObject();
        private string _documentText;

        public event EventHandler ModelLanguageChanged;
        public event EventHandler CursorPositionChanged;
        public event EventHandler CursorSelectionChanged;
        public event EventHandler FilePathChanged;

        public WebPage()
        {
            Padding = new Padding(3);
            UseVisualStyleBackColor = true;
            Text = Resources.Resources.Untitled;

            WebView = new WebView2();
            WebView.Dock = DockStyle.Fill;
            Controls.Add(WebView);

            _dpo.Load += DevPadOnLoad;
            _dpo.Event += DevPadEvent;
        }

        public WebView2 WebView { get; }
        public bool IsMonacoReady { get; private set; }
        public bool IsEditorCreated { get; private set; }
        public bool HasContentChanged { get; private set; }
        public string FilePath { get; private set; }
        public WebPageCloseButtonState CloseButtonState { get; set; }
        public RectangleF CloseButtonRect { get; set; }
        public string ModelLanguage { get; private set; }
        public string CursorPosition { get; private set; }
        public string CursorSelection { get; private set; }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            e.DocumentText = _documentText;
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

                            ModelLanguage = text ?? langId;
                            ModelLanguageChanged?.Invoke(this, EventArgs.Empty);
                        });
                    }
                    else
                    {
                        ModelLanguage = string.Empty;
                        ModelLanguageChanged?.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case DevPadEventType.CursorPositionChanged:
                    var line = e.RootElement.GetValue("position.lineNumber", -1);
                    var column = e.RootElement.GetValue("position.column", -1);
                    if (line >= 0 && column >= 0)
                    {
                        CursorPosition = string.Format(Resources.Resources.StatusPosition, line, column);
                    }
                    else
                    {
                        CursorPosition = string.Empty;
                    }

                    CursorPositionChanged?.Invoke(this, EventArgs.Empty);
                    break;

                case DevPadEventType.CursorSelectionChanged:
                    this.SafeBeginInvoke(async () =>
                    {
                        var text = await WebView.ExecuteScriptAsync("editor.getModel().getValueInRange(editor.getSelection())");
                        text = UnescapeEditorText(text) ?? string.Empty;
                        if (text.Length == 0)
                        {
                            CursorSelection = string.Empty;
                        }
                        else
                        {
                            CursorSelection = string.Format(Resources.Resources.StatusSelection, text.Length);
                        }
                        CursorSelectionChanged?.Invoke(this, EventArgs.Empty);
                    });
                    break;
            }
        }

        public async Task InitializeAsync()
        {
            try
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
            catch (Exception ex)
            {
                // handle WebViewRuntime not properly installed
                // point to evergreen for download
                Program.Trace(ex);
                using (var td = new TaskDialog())
                {
                    td.Event += (s, e) =>
                    {
                        if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
                        {
                            WindowsUtilities.SendMessage(e.Hwnd, MessageDecoder.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                    };
                    td.Flags |= TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT | TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS | TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                    td.MainIcon = TaskDialog.TD_ERROR_ICON;
                    td.Title = WinformsUtilities.ApplicationTitle;
                    td.MainInstruction = Resources.Resources.WebViewError;
                    var msg = ex.GetAllMessages();
                    msg += Environment.NewLine + Environment.NewLine;
                    msg += Resources.Resources.WebViewDownload;
                    td.Content = msg;
                    td.Show(this);
                }

                Application.Exit();
            }
        }

        public bool DiscardChanges()
        {
            if (!HasContentChanged)
                return true;

            return this.ShowConfirm(Resources.Resources.ConfirmDiscard) == DialogResult.Yes;
        }

        private void SetFilePath(string filePath)
        {
            if (FilePath.EqualsIgnoreCase(filePath))
                return;

            FilePath = filePath;
            if (filePath == null)
            {
                Text = WinformsUtilities.ApplicationName;
                return;
            }

            Settings.Current.AddRecentFile(filePath);
            Settings.Current.SerializeToConfiguration();
            WindowsUtilities.SHAddToRecentDocs(filePath);
            Program.WindowsApplication.PublishRecentList();
            Text = Path.GetFileName(filePath);
        }

        public async Task FocusEditorAsync()
        {
            //MainMenu.HideDropDowns();
            await WebView.ExecuteScriptAsync("editor.focus()");
            WebView.Focus();
        }

        public async Task BlurEditorAsync()
        {
            await WebView.ExecuteScriptAsync("editor.blur()");
        }

        public async Task SetEditorThemeAsync(string theme = null)
        {
            theme = theme.Nullify() ?? "vs";
            await WebView.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')");
        }

        private async Task SetEditorPositionAsync(int lineNumber = 0, int column = 0)
        {
            await WebView.ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
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

        public Task SetEditorLanguage(string lang) => WebView.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang}');");
        public async Task SetEditorLanguageFromFilePath(string filePath)
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

        public async Task OpenAsync()
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

        public async Task OpenFileAsync(string filePath)
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
            FilePathChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task NewAsync()
        {
            if (!DiscardChanges())
                return;

            _documentText = string.Empty;
            await WebView.ExecuteScriptAsync("loadFromHost('plaintext')");
            HasContentChanged = false;
            await FocusEditorAsync();
            await SetEditorPositionAsync();
        }

        public async Task SaveAsAsync()
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

        public async Task SaveAsync()
        {
            if (FilePath == null)
            {
                await SaveAsAsync();
                return;
            }
            await SaveAsync(FilePath);
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
    }
}
