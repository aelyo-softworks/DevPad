using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DevPad.MonacoModel;
using DevPad.Utilities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DevPad.Model
{
    public class MonacoTab : DictionaryObject, IDisposable
    {
        private readonly DevPadObject _dpo = new DevPadObject();
        private bool _disposedValue;
        private readonly char[] _buffer = new char[65536 * 16];
        private long _totalRead;
        private StreamReader _reader;

        public event EventHandler<DevPadEventArgs> MonacoEvent;
        public event EventHandler<FileSystemEventArgs> FileChanged;

        public MonacoTab()
        {
            _dpo.Load += DevPadOnLoad;
            _dpo.Event += DevPadEvent;
            HasContentChanged = false;
            if (!IsAdd)
            {
                WebView = new WebView2();
            }
        }

        public int Index { get; set; }
        public WebView2 WebView { get; }
        public bool IsAdd => this is MonacoAddTab;
        public bool IsFileView => WebView != null;
        public bool IsUntitled => FilePath == null && !IsAdd;
        public virtual string FontFamily => "Segoe UI";
        public virtual string PinButtonTooltip => Resources.Resources.PinTabTooltip;
        public virtual string UnpinButtonTooltip => Resources.Resources.UnpinTabTooltip;
        public virtual string CloseButtonTooltip => Resources.Resources.CloseTabTooltip;
        public virtual string AddButtonTooltip => string.Empty;
        public string BackColor => "Transparent";
        public string GroupKey { get => DictionaryObjectGetNullifiedPropertyValue(); set => DictionaryObjectSetPropertyValue(value); }
        public bool IsMonacoReady { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool HasContentChanged { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool IsEditorCreated { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public int? LoadingPercent { get => DictionaryObjectGetPropertyValue<int?>(); private set => DictionaryObjectSetPropertyValue(value); }
        public string ModelLanguageName { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string ModelLanguageId { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string CursorPosition { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string CursorSelection { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public Encoding Encoding { get => DictionaryObjectGetPropertyValue<Encoding>(); set { if (DictionaryObjectSetPropertyValue(value)) HasContentChanged = true; } }
        public bool IsPinned { get => DictionaryObjectGetPropertyValue(false); set { if (DictionaryObjectSetPropertyValue(value)) OnPropertyChanged(nameof(IsUnpinned)); } }
        public bool IsUnpinned => !IsPinned && !IsAdd;
        public int UntitledNumber { get; set; }
        public string Key => FilePath != null ? FilePath : UntitledNumber.ToString();
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

        public string AutoSaveFilePath => IsFileView ? Path.Combine(Settings.AutoSavesDirectoryPath, AutoSaveId) : null;
        public string AutoSaveId
        {
            get
            {
                if (!IsFileView)
                    return null;

                var id = Conversions.ComputeGuidHash(FilePath != null ? FilePath : GroupKey + "\0" + UntitledNumber.ToString()).ToString("N");
                var name = "." + Name;
                if ((id.Length + name.Length) < 255)
                {
                    id += name;
                }
                return id;
            }
        }

        public ImageSource Image
        {
            get
            {
                if (FilePath == null)
                    return null;

                return IconUtilities.GetItemIconAsImageSource(FilePath, SHIL.SHIL_SMALL);
            }
        }

        public override string ToString() => Name;

        public async Task InitializeAsync(string filePath)
        {
            if (filePath != null && !IOUtilities.IsPathRooted(filePath))
                throw new ArgumentException(null, nameof(filePath));

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
                var text = await Application.Current.Dispatcher.SafeInvoke(async () => await GetEditorTextAsync(false));
                if (text == null)
                    return;

                var path = Path.Combine(Settings.AutoSavesDirectoryPath, id);
                IOUtilities.FileEnsureDirectory(path);
                File.WriteAllText(path, text);
            }
        }

        public Task SetEditorLanguageAsync(string lang) => ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang}');");
        private async Task SetEditorLanguageFromFilePathAsync(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            var langs = MonacoExtensions.GetLanguagesByExtension();
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

        public async Task ReloadAsync()
        {
            if (FilePath == null)
                throw new InvalidOperationException();

            if (FilePath != null)
            {
                FilesWatcher.UnwatchFile(FilePath);
                FilesWatcher.FileChanged -= OnFileChanged;
            }

            DeleteAutoSave();
            await LoadFileIfAnyAsync();
            HasContentChanged = false;
        }

        public async Task CloseAsync(bool deleteAutoSave)
        {
            if (FilePath != null)
            {
                FilesWatcher.UnwatchFile(FilePath);
                FilesWatcher.FileChanged -= OnFileChanged;
            }

            if (deleteAutoSave)
            {
                DeleteAutoSave();
            }
            else if (HasContentChanged)
            {
                await AutoSaveWhenIdleAsync(-1);
            }
        }

        public async Task SaveAsync(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!IOUtilities.IsPathRooted(filePath))
                throw new ArgumentException(null, nameof(filePath));

            var text = await GetEditorTextAsync(true);
            if (FilePath != null)
            {
                FilesWatcher.UnwatchFile(FilePath);
                FilesWatcher.FileChanged -= OnFileChanged;
            }

            var encoding = Encoding;
            if (encoding != null)
            {
                File.WriteAllText(filePath, text, encoding);
            }
            else
            {
                File.WriteAllText(filePath, text);
            }
            HasContentChanged = false;
            DeleteAutoSave();

            FilePath = filePath;
            FilesWatcher.WatchFile(FilePath);
            FilesWatcher.FileChanged += OnFileChanged;

            await SetEditorLanguageFromFilePathAsync(FilePath);
        }

        public async Task SetEditorThemeAsync(string theme = null) { theme = theme.Nullify() ?? "vs"; await ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')"); }
        public async Task FocusEditorAsync() { await ExecuteScriptAsync("editor.focus()"); WebView.Focus(); }
        public async Task SetEditorPositionAsync(int lineNumber = 0, int column = 0) => await ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
        public async Task ShowFindUIAsync() => await ExecuteScriptAsync("editor.trigger('','actions.find')");
        public async Task<bool> EditorHasFocusAsync() => await WebView.ExecuteScriptAsync<bool>("editor.hasTextFocus()");
        public async Task BlurEditorAsync() => await ExecuteScriptAsync("editor.blur()");
        public async Task MoveWidgetsToStartAsync() => await ExecuteScriptAsync("moveFindWidgetToStart()");
        public async Task MoveWidgetsToEndAsync() => await ExecuteScriptAsync("moveFindWidgetToEnd()");
        public async Task MoveEditorToAsync(int? line = null, int? column = null) => await ExecuteScriptAsync($"moveEditorTo({column}, {line})");
        public async Task<T> GetEditorOptionsAsync<T>(EditorOption option, T defaultValue = default) => await WebView.ExecuteScriptAsync($"editor.getOption({(int)option})", defaultValue);
        public async Task EnableMinimapAsync(bool enabled) => await ExecuteScriptAsync("editor.updateOptions({minimap:{enabled:" + enabled.ToString().ToLowerInvariant() + "}})");
        public async Task SetFontSizeAsync(double size) => await ExecuteScriptAsync("editor.updateOptions({fontSize:'" + size.ToString(CultureInfo.InvariantCulture) + "px'})");
        public async Task FormatDocumentAsync() => await ExecuteScriptAsync("editor.getAction('editor.action.formatDocument').run()");

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _reader?.Dispose();
                    WebView?.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                _disposedValue = true;
            }
        }

        public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }

        private async Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                //Program.Trace(script);
                return await WebView.ExecuteScriptAsync(script);
            }
            catch (ObjectDisposedException ex)
            {
                Program.Trace(ex);
                return null;
            }
        }

        public async Task<string> GetEditorTextAsync(bool throwOnNull)
        {
            var text = await ExecuteScriptAsync("editor.getValue()");
            if (text == null)
            {
                if (throwOnNull)
                    throw new InvalidOperationException();

                return null;
            }

            return UnescapeEditorText(text);
        }

        private enum LoadStatus
        {
            Ok,
            OkFromAutoSave,
            NoFile,
            Error,
        }

        private async Task<LoadStatus> LoadFileIfAnyAsync()
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
                return LoadStatus.NoFile;

            var lang = LanguageExtensionPoint.DefaultLanguageId;
            if (FilePath != null)
            {
                var ext = Path.GetExtension(FilePath);
                var langs = MonacoExtensions.GetLanguagesByExtension();
                if (langs.TryGetValue(ext, out var langObject) && langObject.Count > 0)
                {
                    lang = langObject[0].Id;
                }

                FilesWatcher.WatchFile(FilePath);
                FilesWatcher.FileChanged += OnFileChanged;
            }

            try
            {
                var encoding = EncodingDetector.DetectEncoding(filePath, Settings.Current.EncodingDetectionMode);
                // equivalent of Encoding = encoding but w/o raising property changed (this is init value)
                DictionaryObjectSetPropertyValue(encoding, DictionaryObjectPropertySetOptions.DontRaiseOnPropertyChanged, nameof(Encoding));

                _reader?.Dispose();
                _reader = new StreamReader(filePath, encoding);
                LoadingPercent = 0;
                await ExecuteScriptAsync($"loadChunksFromHost()");
            }
            catch (Exception ex)
            {
                _reader?.Dispose();
                Program.ShowError(MainWindow.Current, ex, false);
                await MainWindow.Current.CloseTabAsync(this, true, true, true);
                return LoadStatus.Error;
            }

            await SetEditorLanguageAsync(lang);

            await SetEditorPositionAsync();
            return fromAutoSave ? LoadStatus.OkFromAutoSave : LoadStatus.Ok;
        }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            var read = _reader.ReadBlock(_buffer, 0, _buffer.Length);
            if (read == 0)
            {
                LoadingPercent = null;
                e.DocumentText = null;
                _reader.Dispose();
                return;
            }

            _totalRead += read;
            e.DocumentText = new string(_buffer, 0, read);
            if (_reader.BaseStream.Length == 0)
            {
                LoadingPercent = 100;
            }
            else
            {
                LoadingPercent = (int)(_totalRead * 100 / _reader.BaseStream.Length);
            }
        }

        private void OnContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e) => e.Handled = true;
        private void OnFileChanged(object sender, FileSystemEventArgs e) => Task.Run(() => Application.Current.Dispatcher.Invoke(() => FileChanged?.Invoke(this, e)));
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
                    if (!MonacoExtensions.LanguagesLoaded)
                    {
                        await MonacoExtensions.LoadLanguages(WebView);
                    }

                    await EnableMinimapAsync(Settings.Current.ShowMinimap);
                    await SetEditorThemeAsync(MainWindow.Current.Settings.Theme);
                    await FocusEditorAsync();

                    var status = await LoadFileIfAnyAsync();
                    switch (status)
                    {
                        case LoadStatus.Ok:
                            HasContentChanged = false;
                            break;

                        case LoadStatus.OkFromAutoSave:
                            HasContentChanged = true;
                            break;

                        case LoadStatus.Error:
                            return;
                    }

                    IsEditorCreated = true;

                    // this will force CursorPosition text to update
                    var pos = await WebView.ExecuteScriptAsync<JsonElement>("editor.getPosition()");
                    line = pos.GetValue("lineNumber", -1);
                    column = pos.GetValue("column", -1);
                    setPosition();

                    langId = await ExecuteScriptAsync("editor.getModel().getLanguageId()");
                    langId = UnescapeEditorText(langId);
                    setLang();
                    break;

                case DevPadEventType.ModelLanguageChanged:
                    langId = e.RootElement.GetNullifiedValue("newLanguage");
                    setLang();
                    break;

                case DevPadEventType.CursorPositionChanged:
                    line = e.RootElement.GetValue("position.lineNumber", -1);
                    column = e.RootElement.GetValue("position.column", -1);
                    setPosition();
                    break;

                case DevPadEventType.CursorSelectionChanged:
                    text = await ExecuteScriptAsync("editor.getModel().getValueInRange(editor.getSelection())");
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

            void setLang()
            {
                if (langId != null)
                {
                    text = MonacoExtensions.GetLanguageName(langId);
                    ModelLanguageName = text ?? langId;
                    ModelLanguageId = langId;
                }
                else
                {
                    ModelLanguageName = string.Empty;
                    ModelLanguageId = string.Empty;
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
    }
}
