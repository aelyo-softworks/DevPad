using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using DevPad.Utilities;
using MonacoModel;
using Resources;

namespace DevPad
{
    public class Settings : Serializable<Settings>
    {
        public static string TempFileDirectory { get; } = Path.Combine(Path.GetDirectoryName(ConfigurationFilePath), "files");
        public static string DefaultUserDataFolder { get; } = Path.GetDirectoryName(ConfigurationFilePath); // will create an "EBWebView" folder in there

        private static readonly Lazy<Settings> _current = new Lazy<Settings>(() =>
        {
            BackupFromConfiguration(new TimeSpan(7, 0, 0, 0));
            return DeserializeFromConfiguration();
        }, true);
        public static Settings Current => _current.Value;

        [DefaultValue(null)]
        [Browsable(false)]
        public virtual List<RecentFile> RecentFilesPaths { get => GetPropertyValue((List<RecentFile>)null); set { SetPropertyValue(value); } }

        [XmlIgnore]
        [Browsable(false)]
        public string UserDataFolder { get; set; } = DefaultUserDataFolder;

        [LocalizedCategory("Appearance")]
        [TypeConverter(typeof(ThemeConverter))]
        public virtual string Theme { get => GetPropertyValue("vs"); set { SetPropertyValue(value); } }

        private Dictionary<string, DateTime> GetRecentFiles()
        {
            var dic = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            var recents = RecentFilesPaths;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    if (recent?.FilePath == null)
                        continue;

                    if (!IOUtilities.PathIsFile(recent.FilePath))
                        continue;

                    dic[recent.FilePath] = recent.LastAccessTime;
                }
            }
            return dic;
        }

        private void SaveRecentFiles(Dictionary<string, DateTime> dic)
        {
            var list = dic.Select(kv => new RecentFile { FilePath = kv.Key, LastAccessTime = kv.Value }).OrderByDescending(r => r.LastAccessTime).ToList();
            if (list.Count == 0)
            {
                RecentFilesPaths = null;
            }
            else
            {
                RecentFilesPaths = list;
            }
        }

        public void CopyFrom(Settings settings)
        {
            if (settings == null || settings == this)
                return;

            Theme = settings.Theme;
        }

        public void CleanRecentFiles() => SaveRecentFiles(GetRecentFiles());
        public void ClearRecentFiles()
        {
            RecentFilesPaths = null;
            SerializeToConfiguration();
        }

        public void AddRecentFile(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!IOUtilities.PathIsFile(filePath))
                return;

            var dic = GetRecentFiles();
            dic[filePath] = DateTime.UtcNow;
            SaveRecentFiles(dic);
        }
    }
}
