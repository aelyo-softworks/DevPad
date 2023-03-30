using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using DevPad.Ipc;
using DevPad.Resources;
using DevPad.Utilities;
using DevPad.Utilities.Grid;

namespace DevPad
{
    // settings common to all desktops
    public class Settings : Serializable<Settings>
    {
        private const int _defaultAutoSavePeriod = 2;

        public static string DefaultUserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(Settings).Namespace); // will create an "EBWebView" folder in there
        public static string GetUntitledName(int number) => string.Format(Resources.Resources.Untitled, number);

        private static readonly Lazy<Settings> _current = new Lazy<Settings>(() =>
        {
            Backup(ConfigurationFilePath, new TimeSpan(7, 0, 0, 0));
            var settings = Deserialize(ConfigurationFilePath);
            return settings;
        });
        public static Settings Current => _current.Value;

        public static string ConfigurationFilePath => _configurationFilePath.Value;
        private static readonly Lazy<string> _configurationFilePath = new Lazy<string>(() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AssemblyUtilities.GetProduct(),
            typeof(Settings).Name + ".json"), true);

        public static string AutoSavesDirectoryPath { get; } = Path.Combine(Path.GetDirectoryName(ConfigurationFilePath), "autosaves");

        internal new void OnPropertyChanged(string name) => base.OnPropertyChanged(name);

        [JsonIgnore]
        [Browsable(false)]
        public string UserDataFolder { get; set; } = DefaultUserDataFolder;

        [LocalizedCategory("Startup")]
        public virtual SingleInstanceMode SingleInstanceMode { get => GetPropertyValue(SingleInstanceMode.OneInstancePerDesktop); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        public virtual EncodingDetectorMode EncodingDetectionMode { get => GetPropertyValue(EncodingDetectorMode.AutoDetect); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [DefaultValue(true)]
        public virtual bool AutoDetectLanguageOnPaste { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [LocalizedDisplayName("AutoSavePeriodDisplayName")]
        public virtual int AutoSavePeriod { get => GetPropertyValue(_defaultAutoSavePeriod); set { SetPropertyValue(value); } }

        [DefaultValue(null)]
        [PropertyGridOptions(EditorDataTemplateResourceKey = "RegisterExtensionEditor")]
        public string RegisterExtensions
        {
            get => string.Join(",", Program.WindowsApplication.GetRegisteredFileExtensions()
            .OrderBy(e => e)
            .Where(e => !e.EqualsIgnoreCase(WindowsApp.ApplicationIconProgId)));
        }
    }
}
