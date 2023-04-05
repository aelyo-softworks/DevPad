using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DevPad.Utilities.Github
{
    public class GitHubRelease : IComparable<GitHubRelease>, IComparable
    {
        private static readonly Regex _version = new Regex(@"\d+(\.\d+)+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public long Id { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        public bool? Draft { get; set; }
        public bool? PreRelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        public IReadOnlyList<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>().AsReadOnly();

        [JsonIgnore]
        public Version Version
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name))
                {
                    try
                    {
                        var m = _version.Match(Name);
                        if (m.Success)
                            return Version.Parse(m.Value);
                    }
                    catch
                    {
                        // continue
                    }
                }
                return new Version(1, 0);
            }
        }

        int IComparable.CompareTo(object obj) => CompareTo(obj as GitHubRelease);
        public int CompareTo(GitHubRelease other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            return Version.CompareTo(other.Version);
        }

        public override string ToString() => Id + " " + Version + " " + Name + " " + Body;
    }
}
