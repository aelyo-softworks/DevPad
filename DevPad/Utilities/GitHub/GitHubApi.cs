using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DevPad.Utilities.Github
{
    public static class GitHubApi
    {
        public const string BaseUrl = "https://api.github.com/repos/aelyo-softworks/DevPad/";
        public static HttpClient Client => _client.Value;

        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private static readonly Lazy<HttpClient> _client = new Lazy<HttpClient>(GetHttpClient, true);
        private static HttpClient GetHttpClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
            });

            var ua = $"DevPad/{AssemblyUtilities.GetInformationalVersion()} (Windows NT {Environment.OSVersion.Version})";
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);
            return client;
        }

        public static async Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(CancellationToken cancellationToken)
        {
            var url = BaseUrl + "releases";
            using (var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return Array.Empty<GitHubRelease>();

                await HandleResponseStatus(response).ConfigureAwait(false);
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var list = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, _options, cancellationToken: cancellationToken).ConfigureAwait(false);
                    list.Sort();
                    return list.AsReadOnly();
                }
            }
        }

        public static async Task<string> DownloadReleaseAsync(string url, CancellationToken cancellationToken)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            using (var response = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                await HandleResponseStatus(response).ConfigureAwait(false);
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar.Nullify() ?? response.Content.Headers.ContentDisposition?.FileName.Nullify();
                if (fileName == null)
                {
                    fileName = "DevPad.Setup.exe";
                }

                var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "." + fileName);
                IOUtilities.FileEnsureDirectory(path);
                IOUtilities.FileDelete(path, true, false);
                using (var output = File.OpenWrite(path))
                {
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var buffer = new byte[10 * 1024 * 1024];
                        do
                        {
                            var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                            if (read == 0)
                                break;

                            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            if (cancellationToken.IsCancellationRequested)
                                break;
                        }
                        while (true);
                    }
                }
                return path;
            }
        }

        private static async Task HandleResponseStatus(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            if (response.Content.Headers.ContentType?.MediaType.EqualsIgnoreCase("application/json") == true)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new Exception("GitHub server error " + (int)response.StatusCode + Environment.NewLine + json);
            }

            response.EnsureSuccessStatusCode();
        }
    }
}
