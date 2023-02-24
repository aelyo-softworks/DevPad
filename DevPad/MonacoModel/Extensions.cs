using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;

namespace DevPad.MonacoModel
{
    public static class Extensions
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
    }
}
