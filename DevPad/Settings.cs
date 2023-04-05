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
        internal const uint _defaultMaxLoadBufferSize = 65536 * 16;

        public static string DefaultUserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AssemblyUtilities.GetProduct()); // will create an "EBWebView" folder in there
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

        public void SerializeToConfigurationWhenIdle(int dueTime = 1000) => DevPadExtensions.DoWhenIdle(SerializeToConfiguration, dueTime, nameof(SerializeToConfigurationWhenIdle) + "Global");
        public void SerializeToConfiguration() => Serialize(ConfigurationFilePath);

        [JsonIgnore]
        [Browsable(false)]
        public string UserDataFolder { get; set; } = DefaultUserDataFolder;

        [LocalizedCategory("Startup")]
        [DefaultValue(SingleInstanceMode.OneInstancePerDesktop)]
        public virtual SingleInstanceMode SingleInstanceMode { get => GetPropertyValue(SingleInstanceMode.OneInstancePerDesktop); set { SetPropertyValue(value); } }

        [LocalizedCategory("Startup")]
        [DefaultValue(FirstInstanceStartScreen.Current)]
        public virtual FirstInstanceStartScreen FirstInstanceStartScreen { get => GetPropertyValue(FirstInstanceStartScreen.Current); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [DefaultValue(EncodingDetectorMode.AutoDetect)]
        public virtual EncodingDetectorMode EncodingDetectionMode { get => GetPropertyValue(EncodingDetectorMode.AutoDetect); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [DefaultValue(AutoDetectLanguageMode.AutoDetect)]
        public virtual AutoDetectLanguageMode AutoDetectLanguageMode { get => GetPropertyValue(AutoDetectLanguageMode.AutoDetect); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [LocalizedDisplayName("AutoSavePeriodDisplayName")]
        public virtual int AutoSavePeriod { get => GetPropertyValue(_defaultAutoSavePeriod); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [LocalizedDisplayName("MaxLoadBufferSize")]
        public virtual uint MaxLoadBufferSize { get => GetPropertyValue(_defaultMaxLoadBufferSize); set { SetPropertyValue(value); } }

        [LocalizedCategory("Behavior")]
        [DefaultValue(true)]
        public virtual bool OpenFromCurrentTabFolder { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

        [LocalizedCategory("Appearance")]
        [DefaultValue(true)]
        public virtual bool ShowMinimap { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

        [LocalizedCategory("Appearance")]
        [PropertyGridOptions(IsEnum = true, EnumNames = new[] { "vs", "vs-dark", "hc-light", "hc-black" })]
        [DefaultValue("vs")]
        public virtual string Theme { get => GetPropertyValue("vs"); set { SetPropertyValue(value); } }

        [LocalizedCategory("Appearance")]
        [DefaultValue(13d)]
        public virtual double FontSize { get => GetPropertyValue(13d); set { SetPropertyValue(value); } }

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
