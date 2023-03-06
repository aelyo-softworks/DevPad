using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevPad.Model;
using DevPad.Utilities;

namespace DevPad
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<MonacoTab> _tabs = new ObservableCollection<MonacoTab>();

        public MainWindow()
        {
            InitializeComponent();
            TabMain.ItemsSource = _tabs;

            _tabs.Add(new MonacoAddTab());
            _ = AddTab();
        }

        public MonacoTab CurrentTab => TabMain.SelectedItem as MonacoTab;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!DiscardAllChanges())
            {
                e.Cancel = true;
                return;
            }
        }

        private void RemoveTab(MonacoTab tab = null)
        {
            tab = tab ?? CurrentTab;
            if (tab == null)
                return;

            _tabs.Remove(tab);
            tab.Dispose();

            // always ensure we have one (untitled) tab opened
            if (_tabs.Count == 1)
            {
                _ = AddTab();
            }
        }

        private async Task AddTab()
        {
            try
            {
                var newTab = new MonacoTab { Name = DevPad.Resources.Resources.Untitled };
                var c = _tabs.Count - 1;
                _tabs.Insert(c, newTab);
                TabMain.SelectedIndex = c;
                await newTab.InitializeAsync();
            }
            catch (Exception ex)
            {
                // handle WebViewRuntime not properly installed
                // point to evergreen for download
                Program.Trace(ex);
                using (var td = new TaskDialog())
                {
                    td.Event += (s, e) =>
                    {
                        if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
                        {
                            WindowsUtilities.SendMessage(e.Hwnd, MessageDecoder.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                    };
                    td.Flags |= TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT | TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS | TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                    td.MainIcon = TaskDialog.TD_ERROR_ICON;
                    td.Title = WinformsUtilities.ApplicationTitle;
                    td.MainInstruction = DevPad.Resources.Resources.WebViewError;
                    var msg = ex.GetAllMessages();
                    msg += Environment.NewLine + Environment.NewLine;
                    msg += DevPad.Resources.Resources.WebViewDownload;
                    td.Content = msg;
                    td.Show(this);
                }
                Close();
            }
        }

        private bool DiscardChanges(MonacoTab tab = null)
        {
            tab = tab ?? CurrentTab;
            if (tab == null)
                return true;

            if (!tab.HasContentChanged)
                return true;

            return this.ShowConfirm(string.Format(DevPad.Resources.Resources.ConfirmDiscardDocument, tab.Name)) == MessageBoxResult.Yes;
        }

        private bool DiscardAllChanges()
        {
            var changes = _tabs.Where(t => t.HasContentChanged).ToArray();
            if (changes.Length == 0)
                return true;

            var format = changes.Length == 1 ? DevPad.Resources.Resources.ConfirmDiscardDocument : DevPad.Resources.Resources.ConfirmDiscardDocuments;
            return this.ShowConfirm(string.Format(format, string.Join(", ", changes.Select(c => "'" + c + "'")))) == MessageBoxResult.Yes;
        }

        private void OnExitClick(object sender, RoutedEventArgs e) => Close();

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
        }

        private async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_removing && TabMain.Items.Count > 1 && e.AddedItems.Count == 1 && e.AddedItems[0] is MonacoAddTab)
            {
                e.Handled = true;
                Program.Trace("add tab");
                await AddTab();
            }
        }

        private bool _removing;

        private void OnTabMouseUp(object sender, MouseButtonEventArgs e)
        {

            // this studid code is to circumvent a bug that happens when you click the + the first time
            // if you remove this code, the + doesn't work anymore (just once) unless you click somewhere else...
            if (TabMain.Items.Count == 3)
            {
                var index = TabMain.SelectedIndex;
                TabMain.SelectedIndex = index - 1;
                TabMain.SelectedIndex = index;
            }
        }

        private void OnCloseTab(object sender, RoutedEventArgs e)
        {
            var tab = e.GetDataContext<MonacoTab>();
            if (tab == null || tab.IsAdd)
                return;

            if (!DiscardChanges(tab))
                return;

            _removing = true;
            try
            {
                RemoveTab(tab);
            }
            finally
            {
                _removing = false;
            }
        }
    }
}
