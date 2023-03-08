using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        public MonacoTab()
        {
            _dpo.Load += DevPadOnLoad;
            _dpo.Event += DevPadEvent;
        }

        public WebView2 WebView { get; } = new WebView2();
        public bool IsAdd => this is MonacoAddTab;
        public virtual string FontFamily => null;
        public bool IsMonacoReady { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool HasContentChanged { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public bool IsEditorCreated { get => DictionaryObjectGetPropertyValue(false); private set => DictionaryObjectSetPropertyValue(value); }
        public virtual string Name { get => DictionaryObjectGetNullifiedPropertyValue(Resources.Resources.Untitled); set => DictionaryObjectSetPropertyValue(value); }
        public string ModelLanguageName { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string CursorPosition { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }
        public string CursorSelection { get => DictionaryObjectGetNullifiedPropertyValue(); private set => DictionaryObjectSetPropertyValue(value); }

        public override string ToString() => Name;

        public async Task InitializeAsync()
        {
            var udf = Settings.Current.UserDataFolder;
            var env = await CoreWebView2Environment.CreateAsync(null, udf);
            await WebView.EnsureCoreWebView2Async(env);

            // this is the name of the host object in js code
            // called like this: chrome.webview.hostObjects.sync.devPad.MyCustomMethod();
            WebView.CoreWebView2.AddHostObjectToScript("devPad", _dpo);
            var startupPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            WebView.Source = new Uri(Path.Combine(startupPath, @"Monaco\index.html"));
            IsMonacoReady = true;
        }

        public Task SetEditorLanguageAsync(string lang) => WebView.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang}');");
        public async Task SetEditorLanguageFromFilePathAsync(string filePath)
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

        public async Task FocusEditorAsync()
        {
            //MainMenu.HideDropDowns();
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

        public async Task MoveWidgetsToStart()
        {
            await WebView.ExecuteScriptAsync("moveFindWidgetToStart()");
        }

        public async Task MoveWidgetsToEnd()
        {
            await WebView.ExecuteScriptAsync("moveFindWidgetToEnd()");
        }

        public async Task MoveTo(int? line = null, int? column = null)
        {
            await WebView.ExecuteScriptAsync($"moveEditorTo({column}, {line})");
        }

        public async Task EnableMinimap(bool enabled)
        {
            await WebView.ExecuteScriptAsync("editor.updateOptions({minimap:{enabled:" + enabled.ToString().ToLowerInvariant() + "}})");
        }

        private void DevPadOnLoad(object sender, DevPadLoadEventArgs e)
        {
            e.DocumentText = _documentText;
        }

        private async void DevPadEvent(object sender, DevPadEventArgs e)
        {
            //Program.Trace(e);
            string text;
            int line;
            int column;
            string langId;
            DevPadKeyEventArgs ke;
            switch (e.EventType)
            {
                case DevPadEventType.ContentChanged:
                    HasContentChanged = true;
                    break;

                case DevPadEventType.EditorLostFocus:
                    break;

                case DevPadEventType.KeyUp:
                    ke = (DevPadKeyEventArgs)e;
                    Program.Trace("Key " + ke.Code + " " + ke.KeyCode + " Alt:" + ke.Alt + " Shift:" + ke.Shift + " Ctrl:" + ke.Ctrl + " Meta:" + ke.Meta + " AltG:" + ke.AltGraph + " Keys:" + ke.Keys);
                    break;

                case DevPadEventType.KeyDown:
                    ke = (DevPadKeyEventArgs)e;
                    Program.Trace("Key " + ke.Code + " " + ke.KeyCode + " Alt:" + ke.Alt + " Shift:" + ke.Shift + " Ctrl:" + ke.Ctrl + " Meta:" + ke.Meta + " AltG:" + ke.AltGraph + " Keys:" + ke.Keys);
                    //OnKeyDown(new KeyEventArgs(ke.Keys));
                    break;

                case DevPadEventType.EditorCreated:
                    HasContentChanged = false;
                    IsEditorCreated = true;

                    if (!Settings.Current.ShowMinimap)
                    {
                        await EnableMinimap(false);
                    }
                    await SetEditorThemeAsync(Settings.Current.Theme);
                    await FocusEditorAsync();

                    // this will force CursorPosition text to update
                    var pos = await WebView.ExecuteScriptAsync<JsonElement>("editor.getPosition()");
                    line = pos.GetValue("lineNumber", -1);
                    column = pos.GetValue("column", -1);
                    setPosition();

                    langId = await WebView.ExecuteScriptAsync("editor.getModel().getLanguageId()");
                    langId = UnescapeEditorText(langId);
                    await setLang();
                    //var open = CommandLine.GetNullifiedArgument(0);
                    //if (open != null)
                    //{
                    //    await OpenFileAsync(open);
                    //}
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
