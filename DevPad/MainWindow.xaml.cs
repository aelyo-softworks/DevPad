﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevPad.Model;
using DevPad.MonacoModel;
using DevPad.Utilities;

namespace DevPad
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<MonacoTab> _tabs = new ObservableCollection<MonacoTab>();
        private bool _removing;
        private bool _languagesLoaded;
        private WindowDataContext _dataContext;
        private MonacoTab _previousTab;

        public MainWindow()
        {
            InitializeComponent();
            _dataContext = new WindowDataContext(this);
            DataContext = _dataContext;
            TabMain.ItemsSource = _tabs;

            _tabs.Add(new MonacoAddTab());
            _ = AddTab();
        }

        public MonacoTab CurrentTab
        {
            get
            {
                var tab = TabMain.SelectedItem as MonacoTab;
                return tab != null && !tab.IsAdd ? tab : null;
            }
        }

        private class WindowDataContext : INotifyPropertyChanged
        {
            private readonly MainWindow _main;

            public event PropertyChangedEventHandler PropertyChanged;

            public WindowDataContext(MainWindow main)
            {
                _main = main;
            }

            private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            public void RaisePropertyChanged(string propertyName = null)
            {
                if (propertyName != null)
                {
                    OnPropertyChanged(propertyName);
                    return;
                }

                OnPropertyChanged(nameof(CursorPosition));
                OnPropertyChanged(nameof(CursorSelection));
                OnPropertyChanged(nameof(ModelLanguageName));
            }

            public string CursorPosition => _main.CurrentTab?.CursorPosition;
            public string CursorSelection => _main.CurrentTab?.CursorSelection;
            public string ModelLanguageName => _main.CurrentTab?.ModelLanguageName;
        }

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
            if (tab == null || tab.IsAdd)
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
                    const int sysInfoId = 1;
                    td.Event += (s, e) =>
                    {
                        if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
                        {
                            WindowsUtilities.SendMessage(e.Hwnd, MessageDecoder.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                        else if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_BUTTON_CLICKED)
                        {
                            var id = (int)(long)e.WParam;
                            if (id == sysInfoId)
                            {
                                ShowSystemInfo(null);
                                e.HResult = 1; // S_FALSE => don't close
                            }
                        }
                    };

                    td.Flags |= TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT | TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS | TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
                    td.CommonButtonFlags |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
                    td.MainIcon = TaskDialog.TD_ERROR_ICON;
                    td.Title = WinformsUtilities.ApplicationTitle;
                    td.MainInstruction = DevPad.Resources.Resources.WebViewError;
                    td.CustomButtons.Add(sysInfoId, DevPad.Resources.Resources.SystemInfo);
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

        private void CloseTab(MonacoTab tab)
        {
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

        private void CloseAllTabs()
        {
            foreach (var tab in _tabs.ToArray())
            {
                RemoveTab(tab);
            }
        }

        public static void ShowSystemInfo(Window window = null)
        {
            var si = new SystemInformation();
            var dlg = new ObjectProperties(si, true);
            dlg.Height = 800;
            dlg.Width = 800;
            dlg.Title = DevPad.Resources.Resources.SystemInfo;
            dlg.Owner = window;
            if (dlg.Owner == null)
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            dlg.ShowDialog();
        }

        private void OnCloseAll(object sender, RoutedEventArgs e) => CloseAllTabs();
        private void OnClose(object sender, RoutedEventArgs e) => CloseTab(CurrentTab);
        private void OnCloseTab(object sender, RoutedEventArgs e) => CloseTab(e.GetDataContext<MonacoTab>());
        private void OnNewTab(object sender, RoutedEventArgs e) => _ = AddTab();
        private void OnExitClick(object sender, RoutedEventArgs e) => Close();
        private void OnRestartAsAdmin(object sender, RoutedEventArgs e) => RestartAsAdmin(true);
        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var dlg = new About();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_previousTab != null)
            {
                _previousTab.PropertyChanged -= TabPropertyChanged;
            }

            if (!_removing && TabMain.Items.Count > 1 && e.AddedItems.Count == 1 && e.AddedItems[0] is MonacoAddTab)
            {
                e.Handled = true;
                await AddTab();
            }

            var tab = CurrentTab;
            if (tab != null)
            {
                tab.PropertyChanged += TabPropertyChanged;
                _previousTab = tab;
                _dataContext.RaisePropertyChanged();
            }

            // never end up with + selected
            if (TabMain.SelectedItem is MonacoAddTab && TabMain.Items.Count > 1)
            {
                TabMain.SelectedIndex = TabMain.Items.Count - 2;
            }
        }

        private void TabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MonacoTab.IsEditorCreated))
            {
                _dataContext.RaisePropertyChanged();
            }
            else
            {
                _dataContext.RaisePropertyChanged(e.PropertyName);
            }
        }

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

        private bool RestartAsAdmin(bool force)
        {
            if (!force && WindowsUtilities.IsAdministrator)
                return false;

            var info = new ProcessStartInfo();
            info.FileName = Environment.GetCommandLineArgs()[0];
            info.UseShellExecute = true;
            info.Verb = "runas"; // Provides Run as Administrator
            if (Process.Start(info) != null)
            {
                Close();
                return true;
            }
            return false;
        }

        private async void OnLanguagesOpened(object sender, RoutedEventArgs e)
        {
            if (_languagesLoaded)
                return;

            var langs = await CurrentTab.WebView.GetLanguages();
            LanguagesMenuItem.Items.Clear();
            foreach (var group in langs.OrderBy(k => k.Value.Name).GroupBy(n => n.Value.Name.Substring(0, 1), comparer: StringComparer.OrdinalIgnoreCase))
            {
                var subLangs = group.OrderBy(l => l.Value.Name).ToArray();
                if (subLangs.Length > 1)
                {
                    var item = new MenuItem { Header = group.Key.ToUpperInvariant() };
                    LanguagesMenuItem.Items.Add(item);
                    foreach (var lang in group.OrderBy(l => l.Value.Name))
                    {
                        var subItem = new MenuItem { Header = lang.Value.Name };
                        item.Items.Add(subItem);
                        subItem.Click += async (s, e2) =>
                        {
                            await CurrentTab.SetEditorLanguageAsync(lang.Key);
                        };
                    }
                }
                else
                {
                    var item = new MenuItem { Header = subLangs[0].Value.Name };
                    LanguagesMenuItem.Items.Add(item);
                    item.Click += async (s, e2) =>
                    {
                        await CurrentTab.SetEditorLanguageAsync(subLangs[0].Key);
                    };
                }
            }
            _languagesLoaded = true;
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {

        }

        private void OnSave(object sender, RoutedEventArgs e)
        {

        }

        private void OnOpen(object sender, RoutedEventArgs e)
        {

        }

        private void OnNewWindow(object sender, RoutedEventArgs e)
        {
            var info = new ProcessStartInfo();
            info.FileName = Environment.GetCommandLineArgs()[0];
            info.UseShellExecute = true;
            Process.Start(info);
        }

        private void OnPreferences(object sender, RoutedEventArgs e)
        {
            var dlg = new ObjectProperties(Settings.Current, false);
            dlg.Owner = this;
            dlg.Title = DevPad.Resources.Resources.Preferences;
            dlg.ShowDialog();
        }

        private void OnFileOpened(object sender, RoutedEventArgs e)
        {
            var admin = WindowsUtilities.IsAdministrator;
            if (!admin && RestartAsAdminMenuItem.Icon == null)
            {
                var image = new Image { Source = IconUtilities.GetStockIconImageSource(Utilities.Grid.StockIconId.SHIELD) };
                RestartAsAdminMenuItem.Icon = image;
                RestartAsAdminMenuItem.Visibility = Visibility.Visible;
            }
        }
    }
}