using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace DevPad.Utilities
{
    public partial class ObjectProperties : Window
    {
        private EventHandler _extraClick;

        public ObjectProperties(object obj, bool readOnly = false)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            DataContext = obj;
            InitializeComponent();
            PGrid.DefaultCategoryName = "General";
            Title = obj + " Properties";
            if (obj != null)
            {
                var roa = obj.GetType().GetCustomAttribute<ReadOnlyAttribute>();
                PGrid.IsReadOnly = roa?.IsReadOnly == true;
            }

            PGrid.SelectedObject = obj;
            if (readOnly)
            {
                PGrid.IsReadOnly = true;
            }

            if (PGrid.IsReadOnly)
            {
                Cancel.Content = "Close";
                OK.Visibility = Visibility.Hidden;
            }
        }

        public void EnableExtra(string text, EventHandler onClick)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (onClick == null)
                throw new ArgumentNullException(nameof(onClick));

            Extra.Content = text;
            Extra.Visibility = Visibility.Visible;
            _extraClick = onClick;

            if (OK.Visibility == Visibility.Hidden)
            {
                Extra.Margin = OK.Margin;
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

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Extra_Click(object sender, RoutedEventArgs e)
        {
            if (_extraClick != null)
            {
                _extraClick(sender, e);
            }
        }
    }
}
