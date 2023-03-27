using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DevPad.MonacoModel;
using DevPad.Utilities;

namespace DevPad
{
    public partial class ExtensionsToRegister : Window
    {
        private readonly List<Model> _models = new List<Model>();
        public ExtensionsToRegister()
        {
            InitializeComponent();
            var langs = MonacoExtensions.GetLanguagesByExtension();
            _models = langs.OrderBy(k => k.Key).Select(kv => Model.From(kv.Key, kv.Value)).ToList();
            foreach (var ext in Program.WindowsApplication.GetRegisteredFileExtensions())
            {
                var model = _models.FirstOrDefault(m => m.Extension.EqualsIgnoreCase(ext));
                if (model != null)
                {
                    model.Register = true;
                }
            }

            DG.ItemsSource = _models;
        }

        private sealed class Model : DictionaryObject
        {
            public bool Register { get => DictionaryObjectGetPropertyValue(false); set => DictionaryObjectSetPropertyValue(value); }
            public string Extension { get; private set; }
            public string FileTypes { get; private set; }

            public override string ToString() => Extension + " => " + Register;

            public static Model From(string ext, IReadOnlyList<LanguageExtensionPoint> langs)
            {
                var model = new Model();
                model.Extension = ext;
                model.FileTypes = string.Join(", ", langs.Select(l => l.Name));
                return model;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void OnAllClick(object sender, RoutedEventArgs e)
        {
            var count = _models.Count;
            var registered = _models.Count(m => m.Register);
            var check = registered < (count / 2);
            foreach (var model in _models)
            {
                model.Register = check;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
        private void OnOKClick(object sender, RoutedEventArgs e)
        {
            Program.WindowsApplication.RegisterFileExtensions(_models.Where(m => m.Register).Select(m => m.Extension));
            Settings.Current.OnPropertyChanged(nameof(Settings.RegisterExtensions));
            DialogResult = true;
            Close();
        }
    }
}
