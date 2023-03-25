using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevPad.Utilities
{
    public abstract class Serializable<T> : INotifyPropertyChanged where T : new()
    {
        private readonly ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;

        public void SerializeToConfigurationWhenIdle(string filePath, int dueTime = 1000) => DevPadExtensions.DoWhenIdle(() => Serialize(filePath), dueTime);
        public void Serialize(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            Program.Trace("path:" + filePath);
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            };
            var json = JsonSerializer.Serialize((T)(object)this, options);
            File.WriteAllText(filePath, json);
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

        public static void RemoveAll(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir))
                return;

            Directory.Delete(dir, true);
        }

        public static T Deserialize(string filePath) => Deserialize(filePath, new T());
        public static T Deserialize(string filePath, T defaultValue)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                return defaultValue;

            try
            {
                var bytes = File.ReadAllBytes(filePath);
                return JsonSerializer.Deserialize<T>(bytes);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
