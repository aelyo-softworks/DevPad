using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevPad.Ipc;
using DevPad.Model;
using DevPad.MonacoModel;
using DevPad.Utilities;
using DevPad.Utilities.Grid;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace DevPad
{
    public partial class MainWindow : Window
    {
        public static RoutedCommand SaveAll = new RoutedCommand();
        public static MainWindow Current => (MainWindow)Application.Current.MainWindow;

        private readonly ObservableCollection<TabGroup> _groups = new ObservableCollection<TabGroup>();
        private readonly WindowDataContext _dataContext;
        private readonly TabGroup _defaultTabGroup = new TabGroup { Name = DevPad.Resources.Resources.DefaultGroupName, IsDefault = true };
        private readonly bool _loading;
        private bool _onChangedShown;
        private bool _webViewUnavailable;
        private bool _closing;
        private bool _languagesLoaded;

        public MainWindow()
        {
            SingleInstance.Command += (s, e) => OnRemoteCommand(e);
            InitializeComponent();
            NewMenuItem.Icon = NewMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.DOCASSOC) };
            AboutMenuItem.Icon = AboutMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.HELP) };
            FindMenuItem.Icon = FindMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.FIND) };

            _dataContext = new WindowDataContext(this);
            DataContext = _dataContext;

            GroupsTab.ItemsSource = _groups;
            _groups.Add(_defaultTabGroup);
            _groups.Add(new AddTabGroup());

            GroupsTab.SelectedItem = _defaultTabGroup;

            var open = CommandLine.Current.GetNullifiedArgument(0);
            if (open != null)
            {
                _ = AddTabAsync(open);
            }
            else
            {
                _loading = true;
                var any = false;
                if (!Program.IsNewInstance && Settings.Current.RecentFilesPaths != null)
                {
                    foreach (var file in Settings.Current.RecentFilesPaths.Where(f => f.OpenOrder > 0).OrderBy(f => f.OpenOrder))
                    {
                        if (file.UntitledNumber > 0)
                        {
                            _ = AddTabAsync(null, file.UntitledNumber);
                        }
                        else
                        {
                            _ = AddTabAsync(file.FilePath);
                        }
                        any = true;
                    }
                }

                if (!any)
                {
                    _ = AddTabAsync(null);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(Settings.Current.ActiveFilePath))
                    {
                        //TabMain.SelectedItem = _tabs.FirstOrDefault(t => t.FilePath.EqualsIgnoreCase(Settings.Current.ActiveFilePath));
                        //if (TabMain.SelectedItem == null)
                        //{
                        //    TabMain.SelectedItem = _tabs.FirstOrDefault(t => t.Name.EqualsIgnoreCase(Settings.Current.ActiveFilePath));
                        //}
                    }
                }
                SetTitle();
                _loading = false;
            }
        }

        public TabGroup DefaultGroup => _groups.First(g => g.IsDefault);
        public TabGroup CurrentGroup => GroupsTab.SelectedItem is TabGroup group && !group.IsAdd ? group : null;
        public MonacoTab CurrentTab => CurrentGroup.CurrentTab;
        public IEnumerable<TabGroup> Groups => _groups.Where(g => !g.IsAdd);
        public IEnumerable<MonacoTab> AllTabs
        {
            get
            {
                foreach (var group in Groups)
                {
                    foreach (var tab in group.Tabs)
                    {
                        yield return tab;
                    }
                }
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

        private void OnRemoteCommand(SingleInstanceCommandEventArgs e)
        {
            switch (e.Type)
            {
                case SingleInstanceCommandType.SendCommandLine:
                    e.Handled = true;
                    Program.Trace(e.Type + " process:" + e.CallingProcessId + " user:" + e.UserDomainName + "\\" + e.UserName);
                    Dispatcher.Invoke(() => ExecCommandFromCommandLine(e.Arguments?.Select(a => string.Format("{0}", a))));
                    break;

                case SingleInstanceCommandType.Ping:
                    e.Handled = true;
                    Program.Trace(e.Type + " process:" + e.CallingProcessId + " user:" + e.UserDomainName + "\\" + e.UserName);
                    e.Output = WindowsUtilities.CurrentProcess.Id;
                    break;

                case SingleInstanceCommandType.Quit:
                    e.Handled = true;
                    Program.Trace(e.Type + " process:" + e.CallingProcessId + " user:" + e.UserDomainName + "\\" + e.UserName);
                    Dispatcher.Invoke(async () =>
                    {
                        await CloseAllGroups(false, false, true);
                        Settings.Current.SerializeToConfigurationWhenIdle(0); // flush if any change in queue
                        _closing = true;
                        Close();
                    });
                    break;
            }
        }

        private object ExecCommandFromCommandLine(IEnumerable<string> arguments)
        {
            var cmdLine = CommandLine.From(arguments?.ToArray());
            var open = cmdLine.GetNullifiedArgument(0);
            if (open != null)
            {
                _ = AddTabAsync(open);
            }

            return null;
        }

        private void CloseGroup(TabGroup group)
        {
            if (group == null)
                return;
        }

        private void EditGroup(TabGroup group)
        {
            if (group == null)
                return;

            var dlg = new EditGroup(group);
            dlg.Owner = this;
            if (dlg.ShowDialog() != true)
                return;
        }

        private async Task<TabGroup> AddGroup()
        {
            var group = new TabGroup();
            var dlg = new EditGroup(group);
            dlg.Owner = this;
            if (dlg.ShowDialog() != true)
                return null;

            await AddGroup(group);
            return group;
        }

        private Task AddGroup(TabGroup group)
        {
            var count = _groups.Count - 1;
            _groups.Insert(count, group);
            GroupsTab.SelectedIndex = count;
            return Task.CompletedTask;
        }

        private class WindowDataContext : DictionaryObject
        {
            private readonly MainWindow _main;

            public WindowDataContext(MainWindow main)
            {
                _main = main;
                Settings.Current.PropertyChanged += OnSettingsPropertyChanged;
            }

            public Dock GroupsTabPlacement { get => DictionaryObjectGetPropertyValue(Dock.Top); set => DictionaryObjectSetPropertyValue(value); }
            public Visibility GroupsTabVisibility { get => DictionaryObjectGetPropertyValue(Visibility.Visible); set => DictionaryObjectSetPropertyValue(value); }

            private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Settings.ShowMinimap))
                {
                    OnPropertyChanged(e.PropertyName);
                }
            }

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
                    Settings.Current.SerializeToConfigurationWhenIdle();
                    OnPropertyChanged();

                    _ = _main.CurrentTab?.EnableMinimapAsync(value);
                }
            }
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_closing)
            {
                _closing = true;
                e.Cancel = true;
                await CloseAllGroups(false, false, true);
                Settings.Current.SerializeToConfigurationWhenIdle(0); // flush if any change in queue
                try
                {
                    Close();
                }
                catch (Exception ex)
                {
                    Program.ShowError(null, ex);
                    Application.Current.Shutdown();
                }
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
                    await CurrentTab?.MoveWidgetsToEndAsync();
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private TabGroup GetGroup(MonacoTab tab) => Groups.FirstOrDefault(g => g.Key.EqualsIgnoreCase(tab.GroupKey)) ?? DefaultGroup;

        private async Task OpenFileAsync(string filePath)
        {
            var tab = AllTabs.FirstOrDefault(t => t.FilePath.EqualsIgnoreCase(filePath));
            if (tab != null)
            {
                var group = GetGroup(tab);
                GroupsTab.SelectedItem = group;
                group.SelectTab(tab);
                return;
            }

            //if (_tabs.Count == 2 && _tabs[0].FilePath == null && !_tabs[0].HasContentChanged)
            //{
            //    await RemoveTabAsync(_tabs[0], false, false, false);
            //}

            tab = await AddTabAsync(filePath);
            Settings.Current.AddRecentFile(filePath, tab.Index + 1);
            Settings.Current.SerializeToConfigurationWhenIdle();
            WindowsUtilities.SHAddToRecentDocs(filePath);
            Program.WindowsApplication.PublishRecentList();
            SetTitle();
        }

        private async Task RemoveTabAsync(MonacoTab tab, bool deleteAutoSave, bool checkAtLeastOneTab, bool removeFromRecent)
        {
            if (tab == null || tab.IsAdd)
                return;

            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.MonacoEvent -= OnTabMonacoEvent;
            tab.FileChanged -= OnTabFileChanged;

            // close must happen before we remove the control
            await tab.CloseAsync(deleteAutoSave);
            //_tabs.Remove(tab);
            var group = GetGroup(tab);
            group.RemoveTab(tab);
            tab.Dispose();

            Program.Trace("tab:" + tab);
            // always ensure we have one (untitled) tab opened
            //if ((checkAtLeastOneTab || _closing) && _tabs.Count == 1)
            //{
            //    await AddTabAsync(null);
            //}

            if (removeFromRecent)
            {
                if (tab.FilePath != null)
                {
                    Settings.Current.AddRecentFile(tab.FilePath, 0);
                    Settings.Current.SerializeToConfigurationWhenIdle();
                }
                else if (tab.IsUntitled)
                {
                    Settings.Current.RemoveRecentUntitledFile(tab.UntitledNumber);
                    Settings.Current.SerializeToConfigurationWhenIdle();
                }
            }
        }

        private async Task<MonacoTab> AddTabAsync(string filePath, int untitledNumber = 0)
        {
            try
            {
                Program.Trace("path:" + filePath);
                var newTab = new MonacoTab();
                if (filePath == null)
                {
                    //newTab.UntitledNumber = untitledNumber != 0 ? untitledNumber : _tabs.Count(t => t.IsUntitled) + 1;
                }

                //var c = _tabs.Count - 1;
                //_tabs.Insert(c, newTab);
                //TabMain.SelectedIndex = c;
                //newTab.Index = TabMain.SelectedIndex;

                var c = CurrentGroup.Tabs.Count();

                await newTab.InitializeAsync(filePath);
                newTab.MonacoEvent += OnTabMonacoEvent;
                newTab.FileChanged += OnTabFileChanged;
                newTab.PropertyChanged += OnTabPropertyChanged;
                return newTab;
            }
            catch (WebView2RuntimeNotFoundException ex)
            {
                if (!_webViewUnavailable)
                {
                    _webViewUnavailable = true;
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
                }
                Application.Current.Shutdown();
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

        public async Task CloseTabAsync(MonacoTab tab, bool deleteAutoSave, bool removeFromRecent)
        {
            if (tab == null || tab.IsAdd)
                return;

            if (!DiscardChanges(tab))
                return;

            await RemoveTabAsync(tab, deleteAutoSave, true, removeFromRecent);
        }

        private async Task CloseAllGroups(bool checkAtLeastOneTab, bool removeFromRecent, bool quitting)
        {
            foreach (var group in Groups)
            {
                foreach (var tab in group.Tabs.ToArray())
                {
                    await RemoveTabAsync(tab, false, checkAtLeastOneTab, removeFromRecent);
                }
            }

            if (quitting)
            {
                foreach (var group in Groups)
                {
                    group.ClearTabs();
                }
                _groups.Clear();
            }
        }

        private async Task WrapUnauthorizedAccessAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (UnauthorizedAccessException)
            {
                if (WindowsUtilities.IsAdministrator)
                    throw;

                if (this.ShowConfirm(DevPad.Resources.Resources.ConfirmRestartAsAdmin) != MessageBoxResult.Yes)
                    return;

                RestartAsAdmin(false);
            }
        }

        private async Task SaveTabAsAsync(MonacoTab tab)
        {
            if (tab == null || tab.IsAdd)
                return;

            var filter = await BuildFilterAsync();
            if (filter.Item1 == null)
                return;

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

            await WrapUnauthorizedAccessAsync(async () => await tab.SaveAsync(fd.FileName));
        }

        private async Task SaveTabAsync(MonacoTab tab)
        {
            if (tab == null || tab.IsAdd)
                return;

            if (tab.FilePath != null)
            {
                await WrapUnauthorizedAccessAsync(async () => await tab.SaveAsync(tab.FilePath));
                return;
            }
            await SaveTabAsAsync(tab);
        }

        private async Task SaveAllTabsAsync()
        {
            foreach (var group in Groups)
            {
                foreach (var tab in group.Tabs.ToArray())
                {
                    await SaveTabAsync(tab);
                }
            }
        }

        private async Task OpenAsync(string directoryPath)
        {
            var filter = await BuildFilterAsync();
            if (filter.Item1 == null)
                return;

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

        private async Task<(string, int)> BuildFilterAsync()
        {
            var view = DefaultGroup.Tabs.FirstOrDefault()?.WebView;
            if (view == null)
                return (null, 0);

            var languages = await view.GetLanguages();
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
                if (CurrentTab.HasContentChanged)
                {
                    name += " *";
                }
                Title = name + " - " + AssemblyUtilities.GetTitle();
            }

            if (WindowsUtilities.IsAdministrator)
            {
                Title += " - " + DevPad.Resources.Resources.Administrator;
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

        private void OnEditCurrentGroup(object sender, RoutedEventArgs e) => EditGroup(CurrentGroup);
        private void OnCloseGroup(object sender, RoutedEventArgs e) => CloseGroup(e.GetDataContext<TabGroup>());
        private void OnEditGroup(object sender, RoutedEventArgs e) => EditGroup(e.GetDataContext<TabGroup>());
        private void OnCloseCommand(object sender, ExecutedRoutedEventArgs e) => _ = CloseTabAsync(CurrentTab, true, true);
        private async void OnAddGroup(object sender, RoutedEventArgs e) => await AddGroup();
        private async void OnSaveAsCommand(object sender, ExecutedRoutedEventArgs e) => await SaveTabAsAsync(CurrentTab);
        private async void OnSaveCommand(object sender, ExecutedRoutedEventArgs e) => await SaveTabAsync(CurrentTab);
        private async void OnSaveAllCommand(object sender, ExecutedRoutedEventArgs e) => await SaveAllTabsAsync();
        private async void OnNewCommand(object sender, ExecutedRoutedEventArgs e) => await AddTabAsync(null);
        private async void OnOpenCommand(object sender, ExecutedRoutedEventArgs e) => await OpenAsync(null);
        private async void OnCloseAll(object sender, RoutedEventArgs e) => await CloseAllGroups(true, true, false);
        private void OnCloseTab(object sender, RoutedEventArgs e) => _ = CloseTabAsync(e.GetDataContext<MonacoTab>(), true, true);
        private void OnExitClick(object sender, RoutedEventArgs e) => Close();
        private void OnRestartAsAdmin(object sender, RoutedEventArgs e) => RestartAsAdmin(true);
        private void OnClearRecentList(object sender, RoutedEventArgs e) => Settings.Current.ClearRecentFiles();
        private void OnFind(object sender, RoutedEventArgs e) => _ = CurrentTab?.ShowFindUIAsync();
        private void OnFormat(object sender, RoutedEventArgs e) => _ = CurrentTab?.FormatDocumentAsync();
        private void OnOpenConfig(object sender, RoutedEventArgs e) => _ = OpenFileAsync(Settings.ConfigurationFilePath);
        private async void OnAddTab(object sender, RoutedEventArgs e) => await AddTabAsync(null);

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var dlg = new About();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ctl = (TabControl)sender;
            var tab = CurrentTab;
            if (tab == null)
                return;

            _dataContext.RaisePropertyChanged(null);

            // never end up with + tab selected
            if (ctl.SelectedItem is MonacoAddTab && ctl.Items.Count > 1)
            {
                ctl.SelectedIndex = ctl.Items.Count - 2;
            }

            SetTitle();

            if (!_loading && !_closing && !tab.IsAdd)
            {
                var active = Settings.Current.ActiveFilePath;
                var name = tab.IsUntitled ? tab.Name : tab.FilePath;
                if (active != name)
                {
                    Settings.Current.ActiveFilePath = name;
                    Settings.Current.SerializeToConfigurationWhenIdle();
                }
            }
        }

        private void OnTabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Program.Trace("changed:" + e.PropertyName);
            switch (e.PropertyName)
            {
                case nameof(MonacoTab.IsEditorCreated):
                    _dataContext.RaisePropertyChanged(null);
                    break;

                case nameof(MonacoTab.HasContentChanged):
                    SetTitle();
                    break;

                default:
                    _dataContext.RaisePropertyChanged(e.PropertyName);
                    break;
            }
        }

        private async void OnTabFileChanged(object sender, FileSystemEventArgs e)
        {
            var tab = (MonacoTab)sender;
            string txt;
            MessageBoxResult res;
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    if (!_onChangedShown)
                    {
                        txt = string.Format(tab.HasContentChanged ? DevPad.Resources.Resources.ConfirmModifiedDeleted : DevPad.Resources.Resources.ConfirmDeleted, tab.FilePath);
                        _onChangedShown = true;
                        res = this.ShowConfirm(txt);
                        _onChangedShown = false;
                        if (res != MessageBoxResult.Yes)
                            return;

                        await CloseTabAsync(tab, true, false);
                    }
                    break;

                case WatcherChangeTypes.Changed:
                    if (!_onChangedShown)
                    {
                        txt = string.Format(tab.HasContentChanged ? DevPad.Resources.Resources.ConfirmModifiedChanged : DevPad.Resources.Resources.ConfirmChanged, tab.FilePath);
                        _onChangedShown = true;
                        res = this.ShowConfirm(txt);
                        _onChangedShown = false;
                        if (res != MessageBoxResult.Yes)
                            return;

                        await tab.ReloadAsync();
                    }
                    break;
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
                    var cfe = (DevPadConfigurationChangedEventArgs)e;
                    switch (cfe.Option)
                    {
                        case EditorOption.fontInfo:
                            var option = await tab.GetEditorOptionsAsync<JsonElement>(cfe.Option);
                            var fontSize = option.GetValue("fontSize", 0d);
                            if (fontSize > 0 && fontSize != Settings.Current.FontSize)
                            {
                                Settings.Current.SerializeToConfigurationWhenIdle();
                            }
                            break;
                    }
                    break;

                case DevPadEventType.ContentChanged:
                    // add untitled to recent files when it changes
                    if (tab.IsUntitled)
                    {
                        Settings.Current.AddRecentUntitledFile(tab.Index + 1, tab.UntitledNumber);
                        Settings.Current.SerializeToConfigurationWhenIdle();
                    }
                    break;
            }
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
                        lang.Value.SetImage(subItem);

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
                    subLangs[0].Value.SetImage(item);
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
            info.Arguments = "/newinstance";
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

            var files = Settings.Current.RecentFilesPaths?.Where(f => f.UntitledNumber == 0).ToArray();
            RecentFilesMenuItem.IsEnabled = files != null && files.Length > 0;
            if (RecentFilesMenuItem.IsEnabled)
            {
                RecentFilesMenuItem.Items.Clear();
                foreach (var file in files)
                {
                    var item = new MenuItem { Header = file.DisplayName };
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

                var clean = new MenuItem { Header = DevPad.Resources.Resources.CleanRecentList };
                RecentFilesMenuItem.Items.Add(clean);
                clean.Click += (s, e2) =>
                {
                    Settings.Current.CleanRecentFiles();
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

        private void OnPinTab(object sender, RoutedEventArgs e)
        {

        }

        private void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ctl = (TabControl)sender;
            var group = CurrentGroup;
            if (group == null)
                return;

            // never end up with + group selected
            if (ctl.SelectedItem is AddTabGroup && ctl.Items.Count > 1)
            {
                ctl.SelectedIndex = ctl.Items.Count - 2;
            }
        }

        private void OnTabsGroupPlacementChange(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            foreach (var child in ((MenuItem)item.Parent).Items.OfType<MenuItem>())
            {
                child.IsChecked = child == item;
            }

            if (item == HiddenTabPos)
            {
                var s = new Style();
                s.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
                GroupsTab.ItemContainerStyle = s;
            }
            else
            {
                GroupsTab.ItemContainerStyle = null;
                GroupsTab.TabStripPlacement = (Dock)item.Tag;
            }
        }

        private void OnGroupMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var group = e.GetDataContext<TabGroup>();
            GroupsTab.ContextMenu.Visibility = group != null && !group.IsAdd ? Visibility.Visible : Visibility.Collapsed;
            GroupsTab.ContextMenu.DataContext = group;
        }
    }
}
