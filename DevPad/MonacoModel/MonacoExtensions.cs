using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DevPad.Utilities;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace DevPad.MonacoModel
{
    public static class MonacoExtensions
    {
        public static bool LanguagesLoaded { get; private set; }

        private static ConcurrentDictionary<string, LanguageExtensionPoint> _languagesById;
        private static ConcurrentDictionary<string, IReadOnlyList<LanguageExtensionPoint>> _languagesByExtension;

        private static bool _loadingLanguages;

        internal static async Task LoadLanguages(WebView2 webView)
        {
            if (webView == null)
                throw new ArgumentNullException(nameof(webView));

            if (LanguagesLoaded)
                return;

            if (_loadingLanguages)
            {
                do
                {
                    await Task.Delay(20);
                }
                while (_loadingLanguages);
                return;
            }

            _loadingLanguages = true;

            var json = await webView.ExecuteScriptAsync("monaco.languages.getLanguages()").ConfigureAwait(false);
            var languages = JsonSerializer.Deserialize<LanguageExtensionPoint[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _languagesById = new ConcurrentDictionary<string, LanguageExtensionPoint>(StringComparer.OrdinalIgnoreCase);
            _languagesByExtension = new ConcurrentDictionary<string, IReadOnlyList<LanguageExtensionPoint>>(StringComparer.OrdinalIgnoreCase);
            if (languages != null)
            {
                foreach (var language in languages)
                {
                    _languagesById[language.Id] = language;
                    if (language.Extensions != null)
                    {
                        foreach (var ext in language.Extensions)
                        {
                            if (!_languagesByExtension.TryGetValue(ext, out var list))
                            {
                                var l = new List<LanguageExtensionPoint>();
                                list = l;
                                _languagesByExtension[ext] = list;
                            }
                            ((List<LanguageExtensionPoint>)list).Add(language);
                        }
                    }
                }
            }

            if (_languagesByExtension.Count > 0)
            {
                // TODO: add some well-known languages that are not recognized by Monaco
                addExtensionLike(".idl", ".c");

                void addExtensionLike(string ext, string likeExt)
                {
                    if (_languagesByExtension.ContainsKey(ext))
                        return;

                    if (!_languagesByExtension.TryGetValue(likeExt, out var list) || list.Count == 0)
                        return;

                    var first = list.FirstOrDefault(l => l.Extensions != null) ?? list[0];
                    if (first.Extensions == null)
                    {
                        first.Extensions = new string[] { ext };
                    }
                    else
                    {
                        var exts = new List<string>(first.Extensions);
                        exts.Add(ext);
                        first.Extensions = exts.ToArray();
                    }
                    _languagesByExtension[ext] = list;
                }
            }

            _loadingLanguages = false;
            LanguagesLoaded = true;
        }

        public static string GetLanguageName(string id)
        {
            if (!LanguagesLoaded)
                throw new InvalidOperationException();

            if (id == null)
                return null;

            var languages = GetLanguages();
            languages.TryGetValue(id, out var lang);
            if (lang != null)
                return lang.Name;

            return null;
        }

        public static IDictionary<string, IReadOnlyList<LanguageExtensionPoint>> GetLanguagesByExtension()
        {
            if (!LanguagesLoaded)
                throw new InvalidOperationException();

            return _languagesByExtension;
        }

        public static IDictionary<string, LanguageExtensionPoint> GetLanguages()
        {
            if (!LanguagesLoaded)
                throw new InvalidOperationException();

            return _languagesById;
        }

        public static string GetLanguageByExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext) || !LanguagesLoaded)
                return LanguageExtensionPoint.DefaultLanguageId;

            if (_languagesByExtension.TryGetValue(ext, out var langs) && langs.Count > 0)
                return langs[0].Id;

            using (var key = Registry.ClassesRoot.OpenSubKey(ext, false))
            {
                if (key != null)
                {
                    var ct = key.GetValue("Content Type") as string;
                    if (!string.IsNullOrWhiteSpace(ct))
                    {
                        using (var mime = Registry.ClassesRoot.OpenSubKey(Path.Combine(@"MIME\Database\Content Type", ct), false))
                        {
                            var mimeExt = (mime?.GetValue("Extension") as string)?.Nullify();
                            if (mimeExt != null && _languagesByExtension.TryGetValue(mimeExt, out langs) && langs.Count > 0)
                                return langs[0].Id;
                        }
                    }
                }
            }

            return LanguageExtensionPoint.DefaultLanguageId;
        }

        public static bool IsUnknownLanguageExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
                return true;

            if (LanguagesLoaded)
            {
                var languages = GetLanguagesByExtension();
                if (languages.TryGetValue(ext, out var list) && list.Count > 0)
                    return false;
            }

            using (var key = Registry.ClassesRoot.OpenSubKey(ext, false))
            {
                if (key == null)
                    return true;

                var ct = key.GetValue("Content Type") as string;
                if (string.IsNullOrWhiteSpace(ct))
                    return true;
            }
            return false;
        }

        public static async Task<T> ExecuteScriptAsync<T>(this WebView2 webView, string javaScript, T defaultValue = default, JsonSerializerOptions options = null)
        {
            if (javaScript == null)
                throw new ArgumentNullException(nameof(javaScript));

            if (webView == null)
                return defaultValue;

            var json = await webView.ExecuteScriptAsync(javaScript);
            if (json == null)
                return defaultValue;

            try
            {
                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception ex)
            {
                Program.Trace(ex);
                return defaultValue;
            }
        }
    }
}
