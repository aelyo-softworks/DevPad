using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using DevPad.Model;
using DevPad.Utilities;

namespace DevPad
{
    public class PerDesktopSettings : Serializable<PerDesktopSettings>
    {
        private static readonly ConcurrentDictionary<Guid, PerDesktopSettings> _settings = new ConcurrentDictionary<Guid, PerDesktopSettings>();

        public static PerDesktopSettings Get(Guid desktopId)
        {
            if (!_settings.TryGetValue(desktopId, out var settings))
            {
                var configurationFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AssemblyUtilities.GetProduct(),
                    desktopId.ToString("N"),
                    typeof(Settings).Name + ".json");
                Backup(configurationFilePath, new TimeSpan(7, 0, 0, 0));
                settings = Deserialize(configurationFilePath);
                settings = _settings.AddOrUpdate(desktopId, settings, (k, o) => o);
                settings.ConfigurationFilePath = configurationFilePath;
            }
            return settings;
        }

        [JsonIgnore]
        [Browsable(false)]
        public string ConfigurationFilePath { get; private set; }

        private static string GetUntitledFilePath(int number, string groupKey) => Settings.GetUntitledName(number) + "\0" + groupKey;

        [DefaultValue(null)]
        [Browsable(false)]
        public virtual List<RecentFile> RecentFilesPaths { get => GetPropertyValue((List<RecentFile>)null); set { SetPropertyValue(value); } }

        [DefaultValue(null)]
        [Browsable(false)]
        public virtual List<RecentGroup> RecentGroups { get => GetPropertyValue((List<RecentGroup>)null); set { SetPropertyValue(value); } }

        [Browsable(false)]
        public string ActiveFilePath { get; set; }

        [Browsable(false)]
        public string ActiveGroupKey { get; set; }

        [JsonIgnore]
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
                        if (!IOUtilities.IsPathRooted(item.FilePath))
                            continue;

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

        private Dictionary<string, RecentFile> GetRecentFiles()
        {
            var dic = new Dictionary<string, RecentFile>(StringComparer.Ordinal);
            var recents = RecentFilesPaths;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    if (recent.UntitledNumber == 0 && (!IOUtilities.IsPathRooted(recent.FilePath) || !IOUtilities.PathIsFile(recent.FilePath)))
                        continue;

                    dic[recent.FilePath] = recent;
                }
            }
            return dic;
        }

        private void SetRecentFiles(Dictionary<string, RecentFile> dic)
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

        private Dictionary<string, RecentGroup> GetRecentGroups()
        {
            var dic = new Dictionary<string, RecentGroup>(StringComparer.Ordinal);
            var recents = RecentGroups;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    dic[recent.Key] = recent;
                }
            }
            return dic;
        }

        private void SetRecentGroups(Dictionary<string, RecentGroup> dic)
        {
            var list = dic.Select(kv => kv.Value).ToList();
            if (list.Count == 0)
            {
                RecentGroups = null;
            }
            else
            {
                RecentGroups = list;
            }
        }

        public void SerializeToConfigurationWhenIdle(int dueTime = 1000) => DevPadExtensions.DoWhenIdle(SerializeToConfiguration, dueTime);
        public void SerializeToConfiguration() => Serialize(ConfigurationFilePath);

        public void CleanRecentFiles()
        {
            SetRecentFiles(GetRecentFiles());
            SerializeToConfiguration();
        }

        public void ClearRecentFiles()
        {
            RecentFilesPaths = null;
            SerializeToConfiguration();
        }

        public void RemoveRecentUntitledFile(int untitledNumber, string groupKey) => RemoveRecentFile(GetUntitledFilePath(untitledNumber, groupKey));
        public void RemoveRecentFile(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var dic = GetRecentFiles();
            if (!dic.Remove(filePath))
                return;

            SetRecentFiles(dic);
        }

        public void RemoveOpened(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            AddRecentFile(filePath, 0, 0, null, RecentFileOptions.None);
        }

        public void AddRecentFile(string filePath, string groupKey, int openOrder, RecentFileOptions options = RecentFileOptions.None)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (groupKey == null)
                throw new ArgumentNullException(nameof(groupKey));

            if (!IOUtilities.PathIsFile(filePath))
                return;

            AddRecentFile(filePath, 0, openOrder, groupKey, options);
        }

        public void AddRecentUntitledFile(int untitledNumber, int openOrder, string groupKey, RecentFileOptions options = RecentFileOptions.None) => AddRecentFile(GetUntitledFilePath(untitledNumber, groupKey), untitledNumber, openOrder, groupKey, options);
        private void AddRecentFile(string filePath, int untitledNumber, int openOrder, string groupKey, RecentFileOptions options)
        {
            var dic = GetRecentFiles();
            dic[filePath] = new RecentFile { FilePath = filePath, OpenOrder = openOrder, UntitledNumber = untitledNumber, GroupKey = groupKey, Options = options };
            SetRecentFiles(dic);
        }

        public void ClearRecentGroups()
        {
            RecentGroups = null;
            SerializeToConfiguration();
        }

        public bool RemoveRecentGroup(IKeyable group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            var dic = GetRecentGroups();
            if (!dic.Remove(group.Key))
                return false;

            SetRecentGroups(dic);
            return true;
        }

        public void AddRecentGroup(TabGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            var dic = GetRecentGroups();
            dic[group.Key] = RecentGroup.FromTabGroup(group);
            SetRecentGroups(dic);
        }
    }
}
