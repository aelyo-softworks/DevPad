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
        public static string AutoSavesDirectoryPath { get; } = Path.Combine(Path.GetDirectoryName(ConfigurationFilePath), "autosaves");
        public static string DefaultUserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(Settings).Namespace); // will create an "EBWebView" folder in there

        private const int _defaultAutoSavePeriod = 2;

        private static readonly Lazy<Settings> _current = new Lazy<Settings>(() =>
        {
            BackupFromConfiguration(new TimeSpan(7, 0, 0, 0));
            return DeserializeFromConfiguration();
        });
        public static Settings Current => _current.Value;


        public static string GetUntitledName(int number) => string.Format(Resources.Resources.Untitled, number);

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
                var paths = RecentFilesPaths;
                if (paths != null)
                {
                    foreach (var item in paths.Where(i => i.UntitledNumber == 0))
                    {
                        var dir = Path.GetDirectoryName(item.FilePath);
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            list.Add(dir);
                        }
                    }
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

        [LocalizedCategory("Behavior")]
        [LocalizedDisplayName("AutoSavePeriodDisplayName")]
        public virtual int AutoSavePeriod { get => GetPropertyValue(_defaultAutoSavePeriod); set { SetPropertyValue(value); } }

        private Dictionary<string, RecentFile> GetRecentFiles()
        {
            var dic = new Dictionary<string, RecentFile>(StringComparer.Ordinal);
            var recents = RecentFilesPaths;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    if (recent.UntitledNumber == 0 && !IOUtilities.PathIsFile(recent.FilePath))
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

        public void CleanRecentFiles() => SaveRecentFiles(GetRecentFiles());
        public void ClearRecentFiles()
        {
            RecentFilesPaths = null;
            SerializeToConfiguration();
        }

        public bool RemoveRecentUntitledFile(int untitledNumber) => RemoveRecentFile(GetUntitledName(untitledNumber));
        public bool RemoveRecentFile(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            Program.Trace("file:" + filePath);
            var dic = GetRecentFiles();
            if (!dic.Remove(filePath))
                return false;

            SaveRecentFiles(dic);
            return true;
        }

        public void AddRecentFile(string filePath, int openOrder)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!IOUtilities.PathIsFile(filePath))
                return;

            AddRecentFile(filePath, openOrder, 0);
        }

        public void AddRecentUntitledFile(int openOrder, int untitledNumber) => AddRecentFile(GetUntitledName(untitledNumber), openOrder, untitledNumber);
        private void AddRecentFile(string filePath, int openOrder, int untitledNumber)
        {
            Program.Trace("file:" + filePath + " order:" + openOrder + " num:" + untitledNumber);
            var dic = GetRecentFiles();
            dic[filePath] = new RecentFile { FilePath = filePath, OpenOrder = openOrder, UntitledNumber = untitledNumber };
            SaveRecentFiles(dic);
        }
    }
}
