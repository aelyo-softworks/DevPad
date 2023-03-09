using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevPad.Model;
using DevPad.MonacoModel;
using DevPad.Utilities;
using Microsoft.Win32;

namespace DevPad
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<MonacoTab> _tabs = new ObservableCollection<MonacoTab>();
        private bool _languagesLoaded;
        private readonly WindowDataContext _dataContext;
        private MonacoTab _previousTab;

        public MainWindow()
        {
            InitializeComponent();
            _dataContext = new WindowDataContext(this);
            DataContext = _dataContext;
            TabMain.ItemsSource = _tabs;
            Task.Run(() => Settings.Current.CleanRecentFiles());

            _tabs.Add(new MonacoAddTab());

            var open = CommandLine.GetNullifiedArgument(0);
            _ = AddTab(open);
        }

        public MonacoTab CurrentTab => TabMain.SelectedItem is MonacoTab tab && !tab.IsAdd ? tab : null;

        private class WindowDataContext : INotifyPropertyChanged
        {
            private readonly MainWindow _main;

            public event PropertyChangedEventHandler PropertyChanged;

            public WindowDataContext(MainWindow main)
            {
                _main = main;
                Settings.Current.PropertyChanged += OnSettingsPropertyChanged; ;
            }

            private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Settings.ShowMinimap))
                {
                    OnPropertyChanged(e.PropertyName);
                }
            }

            private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            public void RaisePropertyChanged(string propertyName)
            {
                if (propertyName != null)
                {
                    OnPropertyChanged(propertyName);
                    return;
                }

                OnPropertyChanged(nameof(CursorPosition));
                OnPropertyChanged(nameof(CursorSelection));
                OnPropertyChanged(nameof(ModelLanguageName));
                OnPropertyChanged(nameof(ShowMinimap));
            }

            public string CursorPosition => _main.CurrentTab?.CursorPosition;
            public string CursorSelection => _main.CurrentTab?.CursorSelection;
            public string ModelLanguageName => _main.CurrentTab?.ModelLanguageName;

            public bool ShowMinimap
            {
                get => Settings.Current.ShowMinimap;
                set
                {
                    if (ShowMinimap == value)
                        return;

                    Settings.Current.ShowMinimap = value;
                    Settings.Current.SerializeToConfiguration();
                    OnPropertyChanged();

                    _ = _main.CurrentTab?.EnableMinimap(value);
                }
            }
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

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Program.Trace("activated");
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            Program.Trace("focused");
        }

        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F12 && !Program.InDebugMode)
            {
                e.Handled = true;
            }

            // for some reason, Home and End are completely eaten by WebView/Monaco?
            // so we capture them and do it "manually"
            if (e.Key == Key.Home)
            {
                e.Handled = true;
                var hasFocus = await CurrentTab?.EditorHasFocusAsync();
                if (hasFocus) // because widgets (find, etc.) can get focus too
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        _ = CurrentTab?.MoveTo(1, 1);
                    }
                    else
                    {
                        _ = CurrentTab?.MoveTo(column: 1);
                    }
                }
                else
                {
                    await CurrentTab?.MoveWidgetsToStart();
                }
            }

            if (e.Key == Key.End)
            {
                e.Handled = true;
                var hasFocus = await CurrentTab?.EditorHasFocusAsync();
                if (hasFocus)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        _ = CurrentTab?.MoveTo(int.MaxValue, int.MaxValue);
                    }
                    else
                    {
                        _ = CurrentTab?.MoveTo(column: int.MaxValue);
                    }
                }
                else
                {
                    await CurrentTab?.MoveWidgetsToEnd(); ;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private async Task OpenFileAsync(string filePath)
        {
            var tab = _tabs.FirstOrDefault(t => t.FilePath.EqualsIgnoreCase(filePath));
            if (tab != null)
            {
                TabMain.SelectedItem = tab;
                return;
            }

            if (_tabs.Count == 2 && _tabs[0].FilePath == null && !_tabs[0].HasContentChanged)
            {
                RemoveTab(_tabs[0], false);
            }

            tab = await AddTab(filePath);
            Settings.Current.AddRecentFile(filePath);
            Settings.Current.SerializeToConfiguration();
            WindowsUtilities.SHAddToRecentDocs(filePath);
            Program.WindowsApplication.PublishRecentList();
        }

        private void RemoveTab(MonacoTab tab = null, bool checkAtLeastOneTab = true)
        {
            tab = tab ?? CurrentTab;
            if (tab == null || tab.IsAdd)
                return;

            _tabs.Remove(tab);
            tab.Dispose();

            // always ensure we have one (untitled) tab opened
            if (checkAtLeastOneTab && _tabs.Count == 1)
            {
                _ = AddTab(null);
            }
        }

        private async Task<MonacoTab> AddTab(string filePath)
        {
            try
            {
                var newTab = new MonacoTab();
                var c = _tabs.Count - 1;
                _tabs.Insert(c, newTab);
                TabMain.SelectedIndex = c;
                await newTab.InitializeAsync(filePath);
                return newTab;
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
                return null;
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

            RemoveTab(tab);
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
        private void OnNewTab(object sender, RoutedEventArgs e) => _ = AddTab(null);
        private void OnExitClick(object sender, RoutedEventArgs e) => Close();
        private void OnRestartAsAdmin(object sender, RoutedEventArgs e) => RestartAsAdmin(true);
        private async void OnAddTab(object sender, RoutedEventArgs e) => await AddTab(null);
        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var dlg = new About();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_previousTab != null)
            {
                _previousTab.PropertyChanged -= TabPropertyChanged;
            }

            var tab = CurrentTab;
            if (tab != null)
            {
                tab.PropertyChanged += TabPropertyChanged;
                _previousTab = tab;
                _dataContext.RaisePropertyChanged(null);
            }

            // never end up with + tab selected
            if (TabMain.SelectedItem is MonacoAddTab && TabMain.Items.Count > 1)
            {
                TabMain.SelectedIndex = TabMain.Items.Count - 2;
            }
        }

        private void TabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MonacoTab.IsEditorCreated))
            {
                _dataContext.RaisePropertyChanged(null);
            }
            else
            {
                _dataContext.RaisePropertyChanged(e.PropertyName);
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

        private void OnRecentOpened(object sender, RoutedEventArgs e)
        {
        }

        private void OnClearRecentList(object sender, RoutedEventArgs e)
        {
            Settings.Current.ClearRecentFiles();
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {

        }

        private void OnSave(object sender, RoutedEventArgs e)
        {

        }

        private async void OnOpen(object sender, RoutedEventArgs e)
        {
            var filter = await BuildFilter();
            var fd = new OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = filter.Item1,
                FilterIndex = filter.Item2 + 1
            };
            if (fd.ShowDialog(this) != true)
                return;

            await OpenFileAsync(fd.FileName);
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

            var list = Settings.Current.RecentFilesPaths;
            RecentFilesMenuItem.IsEnabled = list.Count > 0;
            if (RecentFilesMenuItem.IsEnabled)
            {
                RecentFilesMenuItem.Items.Clear();
                foreach (var file in list)
                {
                    var item = new MenuItem { Header = file };
                    RecentFilesMenuItem.Items.Add(item);
                    item.Click += (s, e2) =>
                    {
                        _ = OpenFileAsync(file.FilePath);
                    };
                }

                RecentFilesMenuItem.Items.Add(new Separator());
                var clear = new MenuItem { Header = DevPad.Resources.Resources.ClearRecentList };
                RecentFilesMenuItem.Items.Add(clear);
                clear.Click += (s, e2) =>
                {
                    Settings.Current.ClearRecentFiles();
                };
            }
        }

        private async Task<(string, int)> BuildFilter()
        {
            var languages = await CurrentTab.WebView.GetLanguages();
            var sb = new StringBuilder();
            var index = 0;
            foreach (var kv in languages.OrderBy(k => k.Value.Name))
            {
                if (kv.Value.Extensions == null || kv.Value.Extensions.Length == 0)
                    continue;

                if (sb.Length > 0)
                {
                    sb.Append('|');
                }
                sb.Append(string.Format(DevPad.Resources.Resources.OneFileFilter, kv.Value.Name, "*" + string.Join(";*", kv.Value.Extensions)));
                index++;
            }
            sb.Append(DevPad.Resources.Resources.AllFilesFilter);
            return (sb.ToString(), index);
        }
    }
}
