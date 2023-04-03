using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DevPad
{
    public class RecentFile : IComparable<RecentFile>
    {
        public string FilePath { get; set; }
        public DateTime LastAccessTime { get; set; } = DateTime.Now;
        public int OpenOrder { get; set; }
        public int UntitledNumber { get; set; }
        public RecentFileOptions Options { get; set; }
        public Color Color { get; set; }
        public string GroupKey { get; set; }
        public string LanguageId { get; set; }

        [JsonIgnore]
        public string DisplayName => LastAccessTime + " " + FilePath;

        private bool IsPinned => Options.HasFlag(RecentFileOptions.Pinned);

        public int CompareTo(RecentFile other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (ReferenceEquals(this, other))
                return 0;

            if (IsPinned && !other.IsPinned)
                return -1;

            if (other.IsPinned && !IsPinned)
                return 1;

            return OpenOrder.CompareTo(other.OpenOrder);
        }

        public override string ToString() => LastAccessTime + (OpenOrder != 0 ? " {#" + OpenOrder + "}" : null) + " [" + GroupKey.Replace("\0", "!") + "]" + (FilePath != null ? FilePath : Settings.GetUntitledName(UntitledNumber));
    }
}
