using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace DevPad.MonacoModel
{
    public static class MonacoExtensions
    {
        private static ConcurrentDictionary<string, LanguageExtensionPoint> _languagesById;
        private static ConcurrentDictionary<string, IReadOnlyList<LanguageExtensionPoint>> _languagesByExtension;

        private static async Task FillLanguages(WebView2 webView)
        {
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

                foreach (var kv in _languagesByExtension)
                {
                    Program.WindowsApplication.FileExtensions.Add(kv.Key);
                }

                Program.WindowsApplication.Register();

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
        }

        public static async ValueTask<string> GetLanguageName(this WebView2 webView, string id)
        {
            if (id == null)
                return null;

            var languages = await GetLanguages(webView);
            languages.TryGetValue(id, out var lang);
            if (lang != null)
                return lang.Name;

            return null;
        }

        public static async ValueTask<IDictionary<string, IReadOnlyList<LanguageExtensionPoint>>> GetLanguagesByExtension(this WebView2 webView)
        {
            if (_languagesByExtension == null)
            {
                await FillLanguages(webView);
            }
            return _languagesByExtension;
        }

        public static async ValueTask<IDictionary<string, LanguageExtensionPoint>> GetLanguages(this WebView2 webView)
        {
            if (_languagesById == null)
            {
                await FillLanguages(webView);
            }
            return _languagesById;
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
