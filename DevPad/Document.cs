using System;
using DevPad.Utilities;

namespace DevPad
{
    public class Document : IComparable, IComparable<Document>, IEquatable<Document>
    {
        public bool IsNew { get; set; }
        public string FilePath { get; set; }
        public DateTime LastOpenTime { get; set; }
        public int TabOrder { get; set; }
        public bool IsRecent { get; set; }
        public bool IsOpen { get; set; }
        public bool IsSelected { get; set; }

        int IComparable.CompareTo(object obj) => CompareTo(obj as Document);
        public int CompareTo(Document other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            return LastOpenTime.CompareTo(other.LastOpenTime);
        }

        public override string ToString() => FilePath;
        public override int GetHashCode() => (FilePath?.GetHashCode()).GetValueOrDefault();
        public override bool Equals(object obj) => Equals(obj as Document);
        public bool Equals(Document other)
        {
            if (other == null)
                return false;

            return FilePath.EqualsIgnoreCase(other.FilePath);
        }
    }
}
