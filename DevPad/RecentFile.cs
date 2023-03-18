using System;
using System.Windows.Media;

namespace DevPad
{
    public class RecentFile
    {
        public string FilePath { get; set; }
        public DateTime LastAccessTime { get; set; } = DateTime.Now;
        public int OpenOrder { get; set; }
        public int UntitledNumber { get; set; }
        public RecentFileOptions Options { get; set; }
        public Color Color { get; set; }

        public string DisplayName => LastAccessTime + " " + FilePath;

        public override string ToString() => LastAccessTime + (OpenOrder != 0 ? " {#" + OpenOrder + "}" : null) + " " + (FilePath != null ? FilePath : Settings.GetUntitledName(UntitledNumber));
    }
}
