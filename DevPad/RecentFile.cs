using System;

namespace DevPad
{
    public class RecentFile
    {
        public string FilePath { get; set; }
        public DateTime LastAccessTime { get; set; } = DateTime.Now;
        public int OpenOrder { get; set; }
        public int UntitledNumber { get; set; }

        public override string ToString()
        {
            if (FilePath != null)
                return LastAccessTime + " " + FilePath;

            return LastAccessTime + " " + Settings.GetUntitledName(UntitledNumber);
        }
    }
}
