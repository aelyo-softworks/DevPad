using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using DevPad.MonacoModel;
using DevPad.Utilities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DevPad.Model
{
    public class MonacoTab : DictionaryObject, IDisposable
    {
        private readonly DevPadObject _dpo = new DevPadObject();
        private string _documentText;
        private bool _disposedValue;

        public event EventHandler<DevPadEventArgs> MonacoEvent;

        public MonacoTab()
        {
            _dpo.Load += DevPadOnLoad;
            _dpo.Event += DevPadEvent;
            HasContentChanged = false;
        }

        public int Index { get; set; }
        public WebView2 WebView { get; } = new WebView2();
        public bool IsAdd => this is MonacoAddTab;
        public bool IsUntitled => FilePath == null && !IsAdd;
        public virtual string FontFamily => string.Empty;
        public bool IsMonacoReady { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool HasContentChanged { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool IsEditorCreated { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public string ModelLanguageName { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string CursorPosition { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string CursorSelection { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public int UntitledNumber { get; set; }
        public virtual string Name => IsUntitled ? Settings.GetUntitledName(UntitledNumber) : Path.GetFileName(FilePath);
        public string FilePath
        {
            get => DictionaryObjectGetNullifiedPropertyValue();
            private set
            {
                if (DictionaryObjectSetPropertyValue(value))
                {
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(IsUntitled));
                }
            }
        }

        public string AutoSaveFilePath => Path.Combine(Settings.AutoSavesDirectoryPath, AutoSaveId);
        public string AutoSaveId
        {
            get
            {
                var id = Conversions.ComputeGuidHash(FilePath != null ? FilePath : UntitledNumber.ToString()).ToString("N");
                var name = "." + Name;
                if ((id.Length + name.Length) < 255)
                {
                    id += name;
                }
                return id;
            }
        }

        public override string ToString() => Name;

        public async Task InitializeAsync(string filePath)
        {
            FilePath = filePath;
            var udf = Settings.Current.UserDataFolder;
            var env = await CoreWebView2Environment.CreateAsync(null, udf);
            await WebView.EnsureCoreWebView2Async(env);
            WebView.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;

            // this is the name of the host object in js code
            // called like this: chrome.webview.hostObjects.sync.devPad.MyCustomMethod();
            WebView.CoreWebView2.AddHostObjectToScript("devPad", _dpo);
            var startupPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            WebView.Source = new Uri(Path.Combine(startupPath, @"Monaco\index.html"));
            IsMonacoReady = true;
        }

        private bool DeleteAutoSave() => IOUtilities.FileDelete(AutoSaveFilePath, true, false);
        public async Task AutoSaveWhenIdleAsync(int timer)
        {
            var id = AutoSaveId;
            await DevPadExtensions.DoWhenIdle(autoSaveAsync, timer, id);

            async Task autoSaveAsync()
            {
                var text = await Application.Current.Dispatcher.SafeInvoke(async () => await GetEditorTextAsync());
                if (text == null)
                    return;

                var path = Path.Combine(Settings.AutoSavesDirectoryPath, id);
                IOUtilities.FileEnsureDirectory(path);
                File.WriteAllText(path, text);
            }
        }

        private void OnContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            e.Handled = true;
        }

        public Task SetEditorLanguageAsync(string lang) => WebView.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang}');");
        private async Task SetEditorLanguageFromFilePathAsync(string filePath)
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
            await SetEditorLanguageAsync(lang);
        }

        public async Task SetEditorThemeAsync(string theme = null)
        {
            theme = theme.Nullify() ?? "vs";
            await WebView.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')");
        }

        public async Task SetEditorPositionAsync(int lineNumber = 0, int column = 0)
        {
            await WebView.ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
        }

        public async Task ShowFindUIAsync()
        {
            await WebView.ExecuteScriptAsync("editor.trigger('','actions.find')");
        }

        public async Task SaveAsync(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var text = await GetEditorTextAsync();
            if (text == null)
                return;

            File.WriteAllText(filePath, text);
            FilePath = filePath;
            HasContentChanged = false;
            await SetEditorLanguageFromFilePathAsync(FilePath);
        }

        private async Task<string> GetEditorTextAsync()
        {
            var text = await WebView.ExecuteScriptAsync("editor.getValue()");
            if (text == null)
                throw new InvalidOperationException();

            return UnescapeEditorText(text);
        }

        private async Task<bool?> LoadFileIfAnyAsync()
        {
            var fromAutoSave = false;
            var filePath = FilePath;
            var autoSavePath = AutoSaveFilePath;
            if (autoSavePath != null && IOUtilities.PathIsFile(autoSavePath))
            {
                filePath = autoSavePath;
                fromAutoSave = true;
            }
            if (filePath == null)
                return null;

            var lang = LanguageExtensionPoint.DefaultLanguageId;
            if (FilePath != null)
            {
                var ext = Path.GetExtension(FilePath);
                var langs = await WebView.GetLanguagesByExtension();
                if (langs.TryGetValue(ext, out var langObject) && langObject.Count > 0)
                {
                    lang = langObject[0].Id;
                }
            }

            // not this the most performant load system... we should make chunks
            _documentText = File.ReadAllText(filePath);

            await WebView.ExecuteScriptAsync($"loadFromHost('{lang}')");
            await SetEditorPositionAsync();
            return fromAutoSave;
        }

        public async Task FocusEditorAsync()
        {
            await WebView.ExecuteScriptAsync("editor.focus()");
            WebView.Focus();
        }

        public async Task<bool> EditorHasFocusAsync()
        {
            return await WebView.ExecuteScriptAsync<bool>("editor.hasTextFocus()");
        }

        public async Task BlurEditorAsync()
        {
            await WebView.ExecuteScriptAsync("editor.blur()");
        }

        public async Task MoveWidgetsToStartAsync()
        {
            await WebView.ExecuteScriptAsync("moveFindWidgetToStart()");
        }

        public async Task MoveWidgetsToEndAsync()
        {
            await WebView.ExecuteScriptAsync("moveFindWidgetToEnd()");
        }

        public async Task MoveEditorToAsync(int? line = null, int? column = null)
        {
            await WebView.ExecuteScriptAsync($"moveEditorTo({column}, {line})");
        }

        public async Task<T> GetEditorOptionsAsync<T>(EditorOption option, T defaultValue = default)
        {
            return await WebView.ExecuteScriptAsync($"editor.getOption({(int)option})", defaultValue);
        }

        public async Task EnableMinimapAsync(bool enabled)
        {
            await WebView.ExecuteScriptAsync("editor.updateOptions({minimap:{enabled:" + enabled.ToString().ToLowerInvariant() + "}})");
        }

        public async Task SetFontSizeAsync(double size)
        {
            await WebView.ExecuteScriptAsync("editor.updateOptions({fontSize:'" + size.ToString(CultureInfo.InvariantCulture) + "px'})");
        }

        public async Task FormatDocumentAsync()
        {
            await WebView.ExecuteScriptAsync("editor.getAction('editor.action.formatDocument').run()");
        }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            e.DocumentText = _documentText;
        }

        private async void DevPadEvent(object sender, DevPadEventArgs e)
        {
            MonacoEvent?.Invoke(this, e);
            if (e.Handled)
                return;

            string text;
            int line;
            int column;
            string langId;
            //DevPadKeyEventArgs ke;
            switch (e.EventType)
            {
                case DevPadEventType.ContentChanged:
                    if (IsEditorCreated)
                    {
                        HasContentChanged = true;
                        _ = AutoSaveWhenIdleAsync(Settings.Current.AutoSavePeriod * 1000);
                    }
                    break;

                case DevPadEventType.EditorLostFocus:
                    break;

                case DevPadEventType.KeyUp:
                    //ke = (DevPadKeyEventArgs)e;
                    //Program.Trace("Key " + ke.Code + " " + ke.KeyCode + " Alt:" + ke.Alt + " Shift:" + ke.Shift + " Ctrl:" + ke.Ctrl + " Meta:" + ke.Meta + " AltG:" + ke.AltGraph + " Keys:" + ke.Keys);
                    break;

                case DevPadEventType.KeyDown:
                    //ke = (DevPadKeyEventArgs)e;
                    //Program.Trace("Key " + ke.Code + " " + ke.KeyCode + " Alt:" + ke.Alt + " Shift:" + ke.Shift + " Ctrl:" + ke.Ctrl + " Meta:" + ke.Meta + " AltG:" + ke.AltGraph + " Keys:" + ke.Keys);
                    break;

                case DevPadEventType.EditorCreated:
                    if (!Settings.Current.ShowMinimap)
                    {
                        await EnableMinimapAsync(false);
                    }
                    await SetEditorThemeAsync(Settings.Current.Theme);
                    await FocusEditorAsync();

                    if (await LoadFileIfAnyAsync() == true)
                    {
                        HasContentChanged = true;
                    }
                    else
                    {
                        HasContentChanged = false;
                    }

                    IsEditorCreated = true;

                    // this will force CursorPosition text to update
                    var pos = await WebView.ExecuteScriptAsync<JsonElement>("editor.getPosition()");
                    line = pos.GetValue("lineNumber", -1);
                    column = pos.GetValue("column", -1);
                    setPosition();

                    langId = await WebView.ExecuteScriptAsync("editor.getModel().getLanguageId()");
                    langId = UnescapeEditorText(langId);
                    await setLang();
                    break;

                case DevPadEventType.ModelLanguageChanged:
                    langId = e.RootElement.GetNullifiedValue("newLanguage");
                    await setLang();
                    break;

                case DevPadEventType.CursorPositionChanged:
                    line = e.RootElement.GetValue("position.lineNumber", -1);
                    column = e.RootElement.GetValue("position.column", -1);
                    setPosition();
                    break;

                case DevPadEventType.CursorSelectionChanged:
                    text = await WebView.ExecuteScriptAsync("editor.getModel().getValueInRange(editor.getSelection())");
                    text = UnescapeEditorText(text) ?? string.Empty;
                    if (text.Length == 0)
                    {
                        CursorSelection = string.Empty;
                    }
                    else
                    {
                        CursorSelection = string.Format(Resources.Resources.StatusSelection, text.Length);
                    }
                    break;
            }

            async Task setLang()
            {
                if (langId != null)
                {
                    text = await WebView.GetLanguageName(langId);
                    ModelLanguageName = text ?? langId;
                }
                else
                {
                    ModelLanguageName = string.Empty;
                }
            }

            void setPosition()
            {
                if (line >= 0 && column >= 0)
                {
                    CursorPosition = string.Format(Resources.Resources.StatusPosition, line, column);
                }
                else
                {
                    CursorPosition = string.Empty;
                }
            }
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

        public async Task CloseAsync(bool deleteAutoSave)
        {
            if (deleteAutoSave)
            {
                DeleteAutoSave();
            }
            else if (HasContentChanged)
            {
                await AutoSaveWhenIdleAsync(-1);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    WebView?.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                _disposedValue = true;
            }
        }

        public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
    }
}
