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
    public partial class MonacoTab : DictionaryObject, IDisposable
    {
        private readonly DevPadObject _dpo = new DevPadObject();
        private bool _webViewUnavailable;
        private bool _disposedValue;
        private char[] _buffer;
        private int? _bufferSize;
        private long _totalRead;
        private string _pasteDetectedLangId;
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

                if (IsDarkTheme())
                {
                    WebView.DefaultBackgroundColor = System.Drawing.Color.Black;
                }

                WebView.AllowDrop = false;
                WebView.AllowExternalDrop = false;
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
        public string ItemBackColor => IsDarkTheme() ? "Black" : "Transparent";
        public string GroupKey { get => DictionaryObjectGetNullifiedPropertyValue(); set => DictionaryObjectSetPropertyValue(value); }
        public bool IsMonacoReady { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool HasContentChanged { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool IsEditorCreated { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public int? LoadingPercent { get => DictionaryObjectGetPropertyValue<int?>(); private set => DictionaryObjectSetPropertyValue(value); }
        public string ModelLanguageName { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string ModelLanguageId { get => DictionaryObjectGetNullifiedPropertyValue(); internal set => DictionaryObjectSetPropertyValue(value); }
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

        public async Task InitializeAsync(string filePath, string languageId = null)
        {
            if (filePath != null && !IOUtilities.IsPathRooted(filePath))
                throw new ArgumentException(null, nameof(filePath));

            try
            {
                FilePath = filePath;
                ModelLanguageId = languageId;
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
            catch (WebView2RuntimeNotFoundException ex)
            {
                if (!_webViewUnavailable)
                {
                    _webViewUnavailable = true;
                    // handle WebViewRuntime not properly installed
                    // point to evergreen for download
                    //Program.Trace(ex);
                    using (var td = new TaskDialog())
                    {
                        const int sysInfoId = 1;
                        td.Event += (s, e) =>
                        {
                            if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
                            {
                                WindowsUtilities.SendMessage(e.Hwnd, MessageDecoder.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                            else if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_BUTTON_CLICKED)
                            {
                                var id = (int)(long)e.WParam;
                                if (id == sysInfoId)
                                {
                                    MainWindow.ShowSystemInfo(MainWindow.Current);
                                    e.HResult = 1; // S_FALSE => don't close
                                }
                            }
                        };

                        td.Flags |= TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT | TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS | TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                        td.CommonButtonFlags |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
                        td.MainIcon = TaskDialog.TD_ERROR_ICON;
                        td.Title = WinformsUtilities.ApplicationTitle;
                        td.MainInstruction = DevPad.Resources.Resources.WebViewError;
                        td.CustomButtons.Add(sysInfoId, DevPad.Resources.Resources.SystemInfo);
                        var msg = ex.GetAllMessages();
                        msg += Environment.NewLine + Environment.NewLine;
                        msg += DevPad.Resources.Resources.WebViewDownload;
                        td.Content = msg;
                        td.Show(MainWindow.Current);
                    }
                }
                Application.Current.Shutdown();
            }
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

        public async Task<LoadingStatus> ReloadAsync()
        {
            if (FilePath == null)
                throw new InvalidOperationException();

            FilesWatcher.UnwatchFile(FilePath);
            FilesWatcher.FileChanged -= OnFileChanged;

            DeleteAutoSave();
            var status = await LoadFileIfAnyAsync();
            HasContentChanged = false;
            return status;
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

            await SetLanguageAsync();
        }

        public async Task SetEditorThemeAsync(string theme = null) { theme = theme.Nullify() ?? "vs"; await ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')"); }
        public async Task FocusEditorAsync() { await ExecuteScriptAsync("editor.focus()"); WebView.Focus(); }
        public async Task SetEditorPositionAsync(int lineNumber = 0, int column = 0) => await ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
        public async Task EditorRevealLineInCenterAsync(int lineNumber = 0) => await ExecuteScriptAsync("editor.revealLineInCenter(" + lineNumber + ")");
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
        public async Task ShowCommandPaletteAsync() => await ExecuteScriptAsync("editor.trigger('', 'editor.action.quickCommand')");
        public Task SetEditorLanguageAsync(string lang, PasteAction pasteAction)
        {
            _pasteDetectedLangId = pasteAction == PasteAction.AutoDetectLanguageAndFormat ? lang : null;
            return ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang}');");
        }

        public Task DetectLanguage(Window window, PasteAction pasteAction)
        {
            var autoDetectMode = Settings.Current.AutoDetectLanguageMode;
            if (autoDetectMode == AutoDetectLanguageMode.DontAutoDetect)
                return Task.CompletedTask;

            var dispatcher = window?.Dispatcher;
            if (dispatcher == null)
                return Task.CompletedTask;

            return Task.Run(async () =>
            {
                var text = await dispatcher.Invoke(async () => await GetEditorTextAsync(false));
                var det = new Detector();
                var lang = det.Detect(text);
                if (lang != Language.Unknown)
                {
                    var langId = ModelLanguageId;
                    var newLangId = lang.ToString().ToLowerInvariant();
                    _ = dispatcher.Invoke(async () =>
                    {
                        if (autoDetectMode == AutoDetectLanguageMode.PromptIfLanguageChanges && langId != newLangId && langId != LanguageExtensionPoint.DefaultLanguageId)
                        {
                            if (window.ShowConfirm(string.Format(Resources.Resources.LanguageChanging, MonacoExtensions.GetLanguageName(langId), MonacoExtensions.GetLanguageName(newLangId))) != MessageBoxResult.Yes)
                                return;
                        }

                        await SetEditorLanguageAsync(newLangId, pasteAction);
                    });
                }
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _buffer = null;
                    _bufferSize = null;
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

        private async Task<LoadingStatus> LoadFileIfAnyAsync()
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
                return LoadingStatus.NoFile;

            if (FilePath != null)
            {
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
                _totalRead = 0;
                var max = Settings.Current.MaxLoadBufferSize;
                if (max < 1024)
                {
                    max = Settings._defaultMaxLoadBufferSize;
                }

                _bufferSize = (int)Math.Min(_reader.BaseStream.Length, max);
                await ExecuteScriptAsync($"loadFromHost()");
            }
            catch (Exception ex)
            {
                _reader?.Dispose();
                Program.ShowError(MainWindow.Current, ex, false);
                await MainWindow.Current.CloseTabAsync(this, true, true, true);
                return LoadingStatus.Error;
            }

            await SetEditorPositionAsync();
            return fromAutoSave ? LoadingStatus.OkFromAutoSave : LoadingStatus.Ok;
        }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            if (_buffer == null && _reader != null)
            {
                _buffer = new char[_bufferSize.Value];
            }

            var read = (_reader?.ReadBlock(_buffer, 0, _buffer.Length)).GetValueOrDefault();
            if (read == 0)
            {
                LoadingPercent = null;
                e.DocumentText = null;
                _reader?.Dispose();
                _reader = null;
                _buffer = null;
                _bufferSize = null;
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

        private async Task SetLanguageAsync()
        {
            if (ModelLanguageId != null)
            {
                // lang specified at init time
                await SetEditorLanguageAsync(ModelLanguageId, PasteAction.DoNothing);
            }
            else
            {
                if (MonacoExtensions.IsUnknownLanguageExtension(Path.GetExtension(FilePath)))
                {
                    await DetectLanguage(MainWindow.Current, PasteAction.DoNothing);
                }
                else if (FilePath != null)
                {
                    await SetEditorLanguageAsync(MonacoExtensions.GetLanguageByExtension(Path.GetExtension(FilePath)), PasteAction.DoNothing);
                }
            }

            var id = await ExecuteScriptAsync("editor.getModel().getLanguageId()");
            id = UnescapeEditorText(id);
            SetLanguageId(id);
        }

        private void SetLanguageId(string id)
        {
            if (id != null)
            {
                var text = MonacoExtensions.GetLanguageName(id);
                ModelLanguageName = text ?? id;
                ModelLanguageId = id;
            }
            else
            {
                ModelLanguageName = string.Empty;
                ModelLanguageId = string.Empty;
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
            //DevPadKeyEventArgs ke;
            switch (e.EventType)
            {
                case DevPadEventType.ContentChanged:
                    if (IsEditorCreated)
                    {
                        if (LoadingPercent == null)
                        {
                            HasContentChanged = true;
                            _ = AutoSaveWhenIdleAsync(Settings.Current.AutoSavePeriod * 1000);
                        }
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
                    await SetEditorThemeAsync(Settings.Current.Theme);
                    await FocusEditorAsync();

                    var status = await LoadFileIfAnyAsync();
                    switch (status)
                    {
                        case LoadingStatus.Ok:
                            HasContentChanged = false;
                            break;

                        case LoadingStatus.OkFromAutoSave:
                            HasContentChanged = true;
                            break;

                        case LoadingStatus.Error:
                            return;
                    }

                    IsEditorCreated = true;

                    // this will force CursorPosition text to update
                    var pos = await WebView.ExecuteScriptAsync<JsonElement>("editor.getPosition()");
                    line = pos.GetValue("lineNumber", -1);
                    column = pos.GetValue("column", -1);
                    setPosition();

                    await SetLanguageAsync();
                    break;

                case DevPadEventType.ModelLanguageChanged:
                    SetLanguageId(e.RootElement.GetNullifiedValue("newLanguage"));
                    if (_pasteDetectedLangId != null)
                    {
                        _pasteDetectedLangId = null;
                        var wait = Settings.Current.FormatOnPasteWaitTime;
                        if (wait > 0)
                        {
                            await Task.Delay(wait);
                        }

                        _ = FormatDocumentAsync();
                    }
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

                case DevPadEventType.Paste:
                    var eln = e.RootElement.GetValue("range.endLineNumber", -1);
                    if (eln >= 0)
                    {
                        _ = EditorRevealLineInCenterAsync(eln);
                    }

                    var pa = Settings.Current.PasteAction;
                    if (pa == PasteAction.AutoDetectLanguage || pa == PasteAction.AutoDetectLanguageAndFormat)
                    {
                        if (e.RootElement.GetValue("range.startLineNumber", -1) == 1 && e.RootElement.GetValue("range.startColumn", -1) == 1)
                        {
                            _ = DetectLanguage(MainWindow.Current, pa);
                        }
                    }
                    break;
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

        private static bool IsDarkTheme() => Settings.Current.Theme?.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
