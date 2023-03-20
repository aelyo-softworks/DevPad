using System;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using DevPad.Model;
using DevPad.Utilities;

namespace DevPad
{
    public partial class EditGroup : Window
    {
        private TabGroup _group;

        public EditGroup(TabGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            _group = group;
            InitializeComponent();
            DataContext = group.Clone();
        }

        public TabGroup Group => _group;

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
        private void OnOKClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            _group.CopyFrom((DictionaryObject)DataContext);
            _group.Name = _group.Name.Nullify();
            Close();
        }

        private static string ChooseColor(Window window, string color)
        {
            var dlg = new ColorDialog();

            if (color != null)
            {
                dlg.Color = System.Drawing.Color.FromName(color);
                if (color.StartsWith("#") && int.TryParse(color.Substring(1), NumberStyles.HexNumber, null, out var uc))
                {
                    dlg.Color = System.Drawing.Color.FromArgb(uc);
                }
            }
            if (dlg.ShowDialog(window != null ? NativeWindow.FromHandle(new WindowInteropHelper(window).Handle) : null) != System.Windows.Forms.DialogResult.OK)
                return null;

            if (dlg.Color.Name.Length == 8 && uint.TryParse(dlg.Color.Name, NumberStyles.HexNumber, null, out var ui))
                return "#" + ui.ToString("X8");

            return dlg.Color.Name;
        }

        private void OnForeColorClick(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(this, Group.ForeColor);
            if (color != null)
            {
                Group.ForeColor = color;
            }
        }

        private void OnBackColorClick(object sender, RoutedEventArgs e)
        {
            var color = ChooseColor(this, Group.BackColor);
            if (color != null)
            {
                Group.BackColor = color;
            }
        }
    }
}
