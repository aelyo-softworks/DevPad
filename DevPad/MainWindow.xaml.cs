using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevPad.Model;
using DevPad.MonacoModel;
using DevPad.Utilities;
using DevPad.Utilities.Grid;
using Microsoft.Win32;

namespace DevPad
{
    public partial class MainWindow : Window
    {
        public static RoutedCommand SaveAll = new RoutedCommand();

        private readonly ObservableCollection<MonacoTab> _tabs = new ObservableCollection<MonacoTab>();
        private readonly WindowDataContext _dataContext;
        private readonly bool _loading;
        private bool _languagesLoaded;

        public MainWindow()
        {
            InitializeComponent();
            NewMenuItem.Icon = NewMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.DOCASSOC) };
            AboutMenuItem.Icon = AboutMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.HELP) };

            _dataContext = new WindowDataContext(this);
            DataContext = _dataContext;
            TabMain.ItemsSource = _tabs;
            Settings.Current.CleanRecentFiles();

            _tabs.Add(new MonacoAddTab());

            var open = CommandLine.GetNullifiedArgument(0);
            if (open != null)
            {
                _ = AddTabAsync(open);
            }
            else
            {
                _loading = true;
                var any = false;
                foreach (var file in Settings.Current.RecentFilesPaths.Where(f => f.OpenOrder > 0).OrderBy(f => f.OpenOrder))
                {
                    _ = AddTabAsync(file.FilePath);
                    any = true;
                }

                if (!any)
                {
                    _ = AddTabAsync(null);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(Settings.Current.ActiveFilePath))
                    {
                        TabMain.SelectedItem = _tabs.FirstOrDefault(t => t.FilePath.EqualsIgnoreCase(Settings.Current.ActiveFilePath));
                    }
                }
                SetTitle();
                _loading = false;
            }
        }

        public MonacoTab CurrentTab => TabMain.SelectedItem is MonacoTab tab && !tab.IsAdd ? tab : null;
        public IEnumerable<MonacoTab> Tabs => _tabs.Where(t => !t.IsAdd);

        private class WindowDataContext : INotifyPropertyChanged
        {
            private readonly MainWindow _main;

            public event PropertyChangedEventHandler PropertyChanged;

            public WindowDataContext(MainWindow main)
            {
                _main = main;
                Settings.Current.PropertyChanged += OnSettingsPropertyChanged;
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

                    _ = _main.CurrentTab?.EnableMinimapAsync(value);
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
                        _ = CurrentTab?.MoveEditorToAsync(1, 1);
                    }
                    else
                    {
                        _ = CurrentTab?.MoveEditorToAsync(column: 1);
                    }
                }
                else
                {
                    await CurrentTab?.MoveWidgetsToStartAsync();
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
                        _ = CurrentTab?.MoveEditorToAsync(int.MaxValue, int.MaxValue);
                    }
                    else
                    {
                        _ = CurrentTab?.MoveEditorToAsync(column: int.MaxValue);
                    }
                }
                else
                {
                    await CurrentTab?.MoveWidgetsToEndAsync(); ;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private async Task OpenFileAsync(string filePath)
        {
            var tab = Tabs.FirstOrDefault(t => t.FilePath.EqualsIgnoreCase(filePath));
            if (tab != null)
            {
                TabMain.SelectedItem = tab;
                return;
            }

            if (_tabs.Count == 2 && _tabs[0].FilePath == null && !_tabs[0].HasContentChanged)
            {
                RemoveTab(_tabs[0], false);
            }

            tab = await AddTabAsync(filePath);
            Settings.Current.AddRecentFile(filePath, tab.Index + 1);
            Settings.Current.SerializeToConfiguration();
            WindowsUtilities.SHAddToRecentDocs(filePath);
            Program.WindowsApplication.PublishRecentList();
            SetTitle();
        }

        private void RemoveTab(MonacoTab tab = null, bool checkAtLeastOneTab = true, bool serializeSettings = true)
        {
            tab = tab ?? CurrentTab;
            if (tab == null || tab.IsAdd)
                return;

            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.MonacoEvent -= OnTabMonacoEvent;

            _tabs.Remove(tab);
            tab.Dispose();

            // always ensure we have one (untitled) tab opened
            if (checkAtLeastOneTab && _tabs.Count == 1)
            {
                _ = AddTabAsync(null);
            }

            if (tab.FilePath != null)
            {
                Settings.Current.AddRecentFile(tab.FilePath, 0);
                if (serializeSettings)
                {
                    Settings.Current.SerializeToConfiguration();
                }
            }
        }

        private async Task<MonacoTab> AddTabAsync(string filePath)
        {
            try
            {
                var newTab = new MonacoTab();
                var c = _tabs.Count - 1;
                _tabs.Insert(c, newTab);
                TabMain.SelectedIndex = c;
                newTab.Index = TabMain.SelectedIndex;
                await newTab.InitializeAsync(filePath);
                newTab.MonacoEvent += OnTabMonacoEvent;
                newTab.PropertyChanged += OnTabPropertyChanged;
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
            if (tab == null || tab.IsAdd)
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
            foreach (var tab in Tabs.ToArray())
            {
                RemoveTab(tab, serializeSettings: false);
            }
            Settings.Current.SerializeToConfiguration();
        }

        private async Task SaveTabAsAsync(MonacoTab tab)
        {
            if (tab == null || tab.IsAdd)
                return;

            var filter = await BuildFilterAsync();
            var fd = new SaveFileDialog
            {
                RestoreDirectory = true,
                OverwritePrompt = true,
                CheckPathExists = true,
                Filter = filter.Item1,
                FilterIndex = filter.Item2 + 1
            };
            if (fd.ShowDialog(this) != true)
                return;

            await tab.SaveAsync(fd.FileName);
        }

        private async Task SaveTabAsync(MonacoTab tab)
        {
            if (tab == null || tab.IsAdd)
                return;

            if (tab.FilePath != null)
            {
                await tab.SaveAsync(tab.FilePath);
                return;
            }
            await SaveTabAsAsync(tab);
        }

        private async Task SaveAllTabsAsync()
        {
            foreach (var tab in Tabs.ToArray())
            {
                await SaveTabAsync(tab);
            }
        }

        private async Task OpenAsync(string directoryPath)
        {
            var filter = await BuildFilterAsync();
            var fd = new OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = filter.Item1,
                FilterIndex = filter.Item2 + 1
            };

            if (directoryPath != null)
            {
                fd.InitialDirectory = directoryPath;
                fd.RestoreDirectory = false;
            }
            else
            {
                if (Settings.Current.OpenFromCurrentTabFolder && CurrentTab.FilePath != null)
                {
                    fd.InitialDirectory = Path.GetDirectoryName(CurrentTab.FilePath);
                    fd.RestoreDirectory = false;
                }
                else
                {
                    fd.RestoreDirectory = true;
                }
            }

            if (fd.ShowDialog(this) != true)
                return;

            foreach (var fileName in fd.FileNames)
            {
                await OpenFileAsync(fileName);
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

        private void OnCloseCommand(object sender, ExecutedRoutedEventArgs e) => CloseTab(CurrentTab);
        private async void OnSaveAsCommand(object sender, ExecutedRoutedEventArgs e) => await SaveTabAsAsync(CurrentTab);
        private async void OnSaveCommand(object sender, ExecutedRoutedEventArgs e) => await SaveTabAsync(CurrentTab);
        private async void OnSaveAllCommand(object sender, ExecutedRoutedEventArgs e) => await SaveAllTabsAsync();
        private async void OnNewCommand(object sender, ExecutedRoutedEventArgs e) => await AddTabAsync(null);
        private async void OnOpenCommand(object sender, ExecutedRoutedEventArgs e) => await OpenAsync(null);
        private void OnCloseAll(object sender, RoutedEventArgs e) => CloseAllTabs();
        private void OnCloseTab(object sender, RoutedEventArgs e) => CloseTab(e.GetDataContext<MonacoTab>());
        private void OnExitClick(object sender, RoutedEventArgs e) => Close();
        private void OnRestartAsAdmin(object sender, RoutedEventArgs e) => RestartAsAdmin(true);
        private void OnClearRecentList(object sender, RoutedEventArgs e) => Settings.Current.ClearRecentFiles();
        private void OnFind(object sender, RoutedEventArgs e) => _ = CurrentTab?.ShowFindUI();
        private async void OnAddTab(object sender, RoutedEventArgs e) => await AddTabAsync(null);

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var dlg = new About();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void SetTitle()
        {
            if (CurrentTab == null)
                return;

            var name = CurrentTab.FilePath ?? CurrentTab.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                Title = AssemblyUtilities.GetTitle();
            }
            else
            {
                Title = name + " - " + AssemblyUtilities.GetTitle();
            }
        }

        private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tab = CurrentTab;
            if (tab != null)
            {
                _dataContext.RaisePropertyChanged(null);
            }

            // never end up with + tab selected
            if (TabMain.SelectedItem is MonacoAddTab && TabMain.Items.Count > 1)
            {
                TabMain.SelectedIndex = TabMain.Items.Count - 2;
            }

            SetTitle();

            if (!_loading)
            {
                var active = Settings.Current.ActiveFilePath;
                if (active != CurrentTab?.FilePath)
                {
                    Settings.Current.ActiveFilePath = CurrentTab?.FilePath;
                    Settings.Current.SerializeToConfiguration();
                }
            }
        }

        private void OnTabPropertyChanged(object sender, PropertyChangedEventArgs e)
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

        private async void OnTabMonacoEvent(object sender, DevPadEventArgs e)
        {
            //Program.Trace("e:" + e);
            var tab = (MonacoTab)sender;
            switch (e.EventType)
            {
                case DevPadEventType.EditorCreated:
                    await tab.SetFontSizeAsync(Settings.Current.FontSize);
                    break;

                case DevPadEventType.OpenResource:
                    var uri = e.RootElement.GetValue<Uri>("resource");
                    if (uri != null)
                    {
                        WindowsUtilities.OpenUrl(uri);
                    }
                    e.Handled = true;
                    break;

                case DevPadEventType.ConfigurationChanged:
                    Program.Trace(e);
                    var cfe = (DevPadConfigurationChangedEventArgs)e;
                    switch (cfe.Option)
                    {
                        case EditorOption.fontInfo:
                            var option = await tab.GetEditorOptionsAsync<JsonElement>(cfe.Option);
                            var fontSize = option.GetValue("fontSize", 0d);
                            if (fontSize > 0)
                            {
                                Settings.Current.FontSize = fontSize;
                                Settings.Current.SerializeToConfigurationWhenIdle();
                            }
                            break;
                    }
                    break;
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
                var image = new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.SHIELD) };
                RestartAsAdminMenuItem.Icon = image;
                RestartAsAdminMenuItem.Visibility = Visibility.Visible;
            }

            var files = Settings.Current.RecentFilesPaths;
            RecentFilesMenuItem.IsEnabled = files.Count > 0;
            if (RecentFilesMenuItem.IsEnabled)
            {
                RecentFilesMenuItem.Items.Clear();
                foreach (var file in files)
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

            var folderPaths = Settings.Current.RecentFolderPaths;
            RecentFoldersMenuItem.IsEnabled = folderPaths.Count > 0;
            if (RecentFoldersMenuItem.IsEnabled)
            {
                RecentFoldersMenuItem.Items.Clear();
                foreach (var folderPath in folderPaths)
                {
                    var item = new MenuItem { Header = folderPath };
                    RecentFoldersMenuItem.Items.Add(item);
                    item.Click += (s, e2) =>
                    {
                        _ = OpenAsync(folderPath);
                    };
                }
            }
        }

        private static string GetExtFilter(string ext, string name = null)
        {
            name = name ?? ext;
            return string.Format(DevPad.Resources.Resources.OneFileFilter, name, "*" + ext);
        }

        private async Task<(string, int)> BuildFilterAsync()
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
