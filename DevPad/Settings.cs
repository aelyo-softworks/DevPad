using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using DevPad.Utilities;

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

        private List<Document> _documents = new List<Document>();
        private List<WindowSetting> _windowSettings = new List<WindowSetting>();


        [XmlIgnore]
        public IEnumerable<Document> RecentDocuments => _documents.Where(d => d.IsRecent).OrderByDescending(d => d.LastOpenTime);

        [XmlIgnore]
        public IEnumerable<Document> OpenDocuments => _documents.Where(d => d.IsOpen);

        public string UserDataFolder { get; set; } = DefaultUserDataFolder;

        public Document[] Documents
        {
            get => _documents.ToArray();
            set
            {
                _documents = new List<Document>();
                if (value != null)
                {
                    foreach (var doc in value)
                    {
                        if (IOUtilities.FileExists(doc.FilePath))
                        {
                            AddDocument(doc);
                        }
                    }
                }
                _documents.Sort();
            }
        }

        public WindowSetting[] WindowSettings
        {
            get => _windowSettings.ToArray();
            set
            {
                _windowSettings = new List<WindowSetting>();
                if (value != null)
                {
                    _windowSettings.AddRange(value);
                }
            }
        }

        public WindowSetting SaveWindow(string name, Form window)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (window == null)
                throw new ArgumentNullException(nameof(window));

            var existing = _windowSettings.FirstOrDefault(w => w.Name.EqualsIgnoreCase(name));
            if (existing == null)
            {
                existing = new WindowSetting();
                existing.HasChanged = true;
                existing.Name = name;
                _windowSettings.Add(existing);
            }

            if (window.WindowState == FormWindowState.Maximized)
            {
                existing.Left = window.RestoreBounds.Left;
                existing.Top = window.RestoreBounds.Top;
                existing.Width = window.RestoreBounds.Width;
                existing.Height = window.RestoreBounds.Height;
                existing.IsMaximized = true;
            }
            else
            {
                existing.Left = window.Left;
                existing.Top = window.Top;
                existing.Width = window.Width;
                existing.Height = window.Height;
                existing.IsMaximized = false;
            }

            if (existing.HasChanged)
            {
                SerializeToConfiguration();
                existing.HasChanged = false;
            }
            return existing;
        }

        public WindowSetting RestoreWindow(string name, Form window)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (window == null)
                throw new ArgumentNullException(nameof(window));

            var existing = _windowSettings.FirstOrDefault(w => w.Name.EqualsIgnoreCase(name));
            if (existing != null)
            {
                window.Left = existing.Left;
                window.Top = existing.Top;
                window.Width = existing.Width;
                window.Height = existing.Height;
                if (existing.IsMaximized)
                {
                    window.WindowState = FormWindowState.Maximized;
                }
            }
            return existing;
        }

        private void AddDocument(Document doc)
        {
            _documents.Add(doc);
        }

        public void SelectDocument(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            foreach (var doc in _documents)
            {
                doc.IsSelected = doc.FilePath.EqualsIgnoreCase(filePath);
            }
        }

        public Document GetDocument(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            return _documents.FirstOrDefault(d => d.FilePath.EqualsIgnoreCase(filePath));
        }

        public Document AddDocument(string filePath, int tabOrder)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var doc = _documents.FirstOrDefault(d => d.FilePath.EqualsIgnoreCase(filePath));
            if (doc == null)
            {
                doc = new Document();
                doc.FilePath = filePath;
                AddDocument(doc);
                _documents.Sort();
            }

            doc.IsRecent = true;
            doc.LastOpenTime = DateTime.Now;
            doc.TabOrder = tabOrder;
            SerializeToConfiguration();
            return doc;
        }

        public Document AddNewDocument(int tabOrder)
        {
            IOUtilities.FileCreateDirectory(Path.Combine(TempFileDirectory, "dummy"));
            int i = 1;
            string filePath;
            do
            {
                filePath = Path.Combine(TempFileDirectory, "(unnamed " + i + ")");
                if (!File.Exists(filePath))
                    break;

                i++;
            }
            while (true);

            var doc = new Document();
            doc.IsNew = true;
            doc.FilePath = filePath;
            doc.TabOrder = tabOrder;
            File.WriteAllText(doc.FilePath, string.Empty, Encoding.UTF8);
            AddDocument(doc);
            _documents.Sort();
            SerializeToConfiguration();
            return doc;
        }
    }
}
