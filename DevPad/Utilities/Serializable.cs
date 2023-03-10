using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace DevPad.Utilities
{
    public abstract class Serializable<T> : INotifyPropertyChanged where T : new()
    {
        private readonly ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;

        private Timer _timer;

        public void SerializeToConfigurationWhenIdle(int dueTime = 1000)
        {
            if (dueTime <= 0)
            {
                SerializeToConfiguration();
                return;
            }

            var timer = _timer;
            if (timer == null)
            {
                _timer = new Timer(state =>
                {
                    SerializeToConfiguration();
                }, null, dueTime, Timeout.Infinite);
            }
            else
            {
                timer.Change(dueTime, Timeout.Infinite);
            }
        }

        public void SerializeToConfiguration() => Serialize(ConfigurationFilePath);
        public void Serialize(XmlWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var serializer = new XmlSerializer(GetType());
            serializer.Serialize(writer, this);
        }

        public void Serialize(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var serializer = new XmlSerializer(GetType());
            serializer.Serialize(writer, this);
        }

        public void Serialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var serializer = new XmlSerializer(GetType());
            serializer.Serialize(stream, this);
        }

        public void Serialize(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            string dir = Path.GetDirectoryName(filePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var writer = new XmlTextWriter(filePath, Encoding.UTF8))
            {
                Serialize(writer);
            }
        }

        protected void OnPropertyChanged(string name) => OnPropertyChanged(this, new PropertyChangedEventArgs(name));
        protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);

        protected Tv GetPropertyValue<Tv>(Tv defaultValue = default, [CallerMemberName] string propertyName = null)
        {
            if (!TryGetPropertyValue(out var value, propertyName))
                return defaultValue;

            if (!Conversions.TryChangeType<Tv>(value, out var convertedValue))
                return defaultValue;

            return convertedValue;
        }

        protected virtual bool TryGetPropertyValue(out object value, [CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            return _values.TryGetValue(propertyName, out value);
        }

        protected virtual bool SetPropertyValue(object value, [CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            var changed = true;
            _values.AddOrUpdate(propertyName, value, (k, o) =>
            {
                changed = !Equals(value, o);
                return value;
            });

            if (changed)
            {
                OnPropertyChanged(propertyName);
            }
            return changed;
        }

        public virtual void CopyFrom(Serializable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            var frozen = other._values.ToArray();
            foreach (var kv in frozen)
            {
                SetPropertyValue(kv.Value, kv.Key);
            }
        }

        protected virtual T CreateNew() => new T();
        public virtual T Clone()
        {
            var clone = CreateNew();
            ((Serializable<T>)(object)clone).CopyFrom(this);
            return clone;
        }

        private static readonly Lazy<string> _configurationFilePath = new Lazy<string>(() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(T).Namespace, typeof(T).Name + ".config"), true);
        public static string ConfigurationFilePath => _configurationFilePath.Value;

        public static void BackupFromConfiguration(TimeSpan? maxDuration = null) => Backup(ConfigurationFilePath, maxDuration);
        public static void Backup(string filePath, TimeSpan? maxDuration = null)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                return;

            var bakPath = Path.Combine(Path.GetDirectoryName(filePath), "bak", string.Format("{0:yyyy}_{0:MM}_{0:dd}.{1}.xml", DateTime.Now, Environment.TickCount));
            var dir = Path.GetDirectoryName(bakPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.Copy(filePath, bakPath, true);

            if (maxDuration.HasValue)
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    if (string.Compare(file, bakPath, StringComparison.OrdinalIgnoreCase) == 0)
                        continue;

                    var name = Path.GetFileNameWithoutExtension(file);
                    var tick = name.IndexOf('.');
                    if (tick < 0)
                        continue;

                    var dates = name.Substring(0, tick).Split('.');
                    if (dates.Length < 1)
                        continue;

                    var date = dates[0].Replace("_", "/");
                    if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        continue;

                    if ((DateTime.Now - dt) > maxDuration.Value)
                    {
                        IOUtilities.FileDelete(file, false);
                    }
                }
            }
        }

        public static void RemoveAllConfiguration() => RemoveAll(ConfigurationFilePath);
        public static void RemoveAll(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir))
                return;

            Directory.Delete(dir, true);
        }

        public static T DeserializeFromConfiguration() => Deserialize(ConfigurationFilePath);
        public static T Deserialize(string filePath) => Deserialize(filePath, new T());
        public static T Deserialize(string filePath, T defaultValue)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                return defaultValue;

            try
            {
                using (var reader = new XmlTextReader(filePath))
                {
                    return Deserialize(reader, defaultValue);
                }
            }
            catch
            {
                return defaultValue;
            }
        }

        public static T Deserialize(Stream stream) => Deserialize(stream, new T());
        public static T Deserialize(Stream stream, T defaultValue)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            try
            {
                var deserializer = new XmlSerializer(typeof(T));
                return (T)deserializer.Deserialize(stream);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static T Deserialize(TextReader reader) => Deserialize(reader, new T());
        public static T Deserialize(TextReader reader, T defaultValue)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            try
            {
                var deserializer = new XmlSerializer(typeof(T));
                return (T)deserializer.Deserialize(reader);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static T Deserialize(XmlReader reader) => Deserialize(reader, new T());
        public static T Deserialize(XmlReader reader, T defaultValue)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            try
            {
                var deserializer = new XmlSerializer(typeof(T));
                return (T)deserializer.Deserialize(reader);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
