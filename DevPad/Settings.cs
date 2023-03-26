using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using DevPad.Ipc;
using DevPad.Resources;
using DevPad.Utilities;

namespace DevPad
{
    // settings common to all desktops
    public class Settings : Serializable<Settings>
    {
        public static string DefaultUserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(Settings).Namespace); // will create an "EBWebView" folder in there
        public static string GetUntitledName(int number) => string.Format(Resources.Resources.Untitled, number);

        private static readonly Lazy<Settings> _current = new Lazy<Settings>(() =>
        {
            Backup(ConfigurationFilePath, new TimeSpan(7, 0, 0, 0));
            return Deserialize(ConfigurationFilePath);
        });
        public static Settings Current => _current.Value;

        public static string ConfigurationFilePath => _configurationFilePath.Value;
        private static readonly Lazy<string> _configurationFilePath = new Lazy<string>(() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AssemblyUtilities.GetProduct(),
            typeof(Settings).Name + ".json"), true);

        [JsonIgnore]
        [Browsable(false)]
        public string UserDataFolder { get; set; } = DefaultUserDataFolder;

        [LocalizedCategory("Startup")]
        public virtual SingleInstanceMode SingleInstanceMode { get => GetPropertyValue(SingleInstanceMode.OneInstancePerDesktop); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        public virtual EncodingDetectorMode EncodingDetectionMode { get => GetPropertyValue(EncodingDetectorMode.AutoDetect); set { SetPropertyValue(value); } }
    }
}
