using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using DevPad.Resources;
using DevPad.Utilities;
using DevPad.Utilities.Grid;

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
        });
        public static Settings Current => _current.Value;

        [DefaultValue(null)]
        [Browsable(false)]
        public virtual List<RecentFile> RecentFilesPaths { get => GetPropertyValue((List<RecentFile>)null); set { SetPropertyValue(value); } }

        [XmlIgnore]
        [Browsable(false)]
        public string UserDataFolder { get; set; } = DefaultUserDataFolder;

        [Browsable(false)]
        public string ActiveFilePath { get; set; }

        [XmlIgnore]
        [Browsable(false)]
        public IReadOnlyList<string> RecentFolderPaths
        {
            get
            {
                var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in RecentFilesPaths)
                {
                    list.Add(Path.GetDirectoryName(item.FilePath));
                }
                return list.OrderBy(s => s).ToArray();
            }
        }

        [LocalizedCategory("Appearance")]
        [PropertyGridOptions(IsEnum = true, EnumNames = new[] { "vs", "vs-dark", "hc-light", "hc-black" })]
        public virtual string Theme { get => GetPropertyValue("vs"); set { SetPropertyValue(value); } }

        [LocalizedCategory("Appearance")]
        public virtual bool ShowMinimap { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

        [LocalizedCategory("Appearance")]
        public virtual double FontSize { get => GetPropertyValue(13d); set { SetPropertyValue(value); } }

        [LocalizedCategory("Startup")]
        public virtual bool RestoreTabs { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        public virtual bool OpenFromCurrentTabFolder { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

        private Dictionary<string, RecentFile> GetRecentFiles()
        {
            var dic = new Dictionary<string, RecentFile>(StringComparer.Ordinal);
            var recents = RecentFilesPaths;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    if (recent?.FilePath == null)
                        continue;

                    if (!IOUtilities.PathIsFile(recent.FilePath))
                        continue;

                    dic[recent.FilePath] = recent;
                }
            }
            return dic;
        }

        private void SaveRecentFiles(Dictionary<string, RecentFile> dic)
        {
            var list = dic.Select(kv => kv.Value).OrderByDescending(r => r.LastAccessTime).ToList();
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

        public void AddRecentFile(string filePath, int openOrder)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!IOUtilities.PathIsFile(filePath))
                return;

            var dic = GetRecentFiles();
            dic[filePath] = new RecentFile { FilePath = filePath, OpenOrder = openOrder };
            SaveRecentFiles(dic);
        }
    }
}
