using System.Text.Json.Serialization;

namespace DevPad.Utilities.Github
{
    public class GitHubAsset
    {
        public long Id { get; set; }
        public string Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; }

        public override string ToString() => Id + " " + Name + " " + DownloadUrl;
    }
}
