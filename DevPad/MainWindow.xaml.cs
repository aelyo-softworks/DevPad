﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DevPad.Ipc;
using DevPad.Model;
using DevPad.MonacoModel;
using DevPad.Utilities;
using DevPad.Utilities.Github;
using DevPad.Utilities.Grid;
using Microsoft.Win32;

namespace DevPad
{
    public partial class MainWindow : Window
    {
        public static RoutedCommand SaveAll = new RoutedCommand();
        public static MainWindow Current => (MainWindow)Application.Current?.MainWindow;

        private readonly ObservableCollection<TabGroup> _groups = new ObservableCollection<TabGroup>();
        private readonly TabGroup _defaultTabGroup = new TabGroup { Name = DevPad.Resources.Resources.DefaultGroupName, IsDefault = true };
        private WindowDataContext _dataContext;
        private MenuItem _desktopMenuItem;
        private bool _onChangedShown;
        private bool _languagesLoaded;
        private State _state = State.Opening;

        private enum State
        {
            Opening,
            Ready,
            Closing,
            Closed,
        }

        public MainWindow()
        {
            SingleInstance.Command += (s, e) => OnRemoteCommand(e);
            InitializeComponent();

            // resize back for small screens
            this.MinimizeToScreen();

            NewMenuItem.Icon = NewMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.DOCASSOC) };
            AboutMenuItem.Icon = AboutMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.HELP) };
            RecentFoldersMenuItem.Icon = RecentFoldersMenuItem.Icon ?? new Image { Source = IconUtilities.GetStockIconImageSource(StockIconId.FOLDER) };

            Loaded += OnLoaded;

            if (DevPad.Settings.Current.FirstInstanceStartScreen == FirstInstanceStartScreen.Primary)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }

        private void LoadPosition()
        {
            // only restore if caption is reachable
            var width = Math.Max(Settings.Width, (int)MinWidth);
            var height = Math.Max(Settings.Height, (int)MinHeight);
            var left = Settings.Left;
            var top = Settings.Top;
            var screen = System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(left, top, width, height));
            if (left >= screen.WorkingArea.Right)
            {
                left = screen.WorkingArea.Left;
            }

            if ((left + width) < 0)
            {
                left = screen.WorkingArea.Left;
            }

            if (top >= screen.WorkingArea.Bottom)
            {
                top = screen.WorkingArea.Top;
            }

            if ((top + height) < 0)
            {
                top = screen.WorkingArea.Top;
            }

            Left = left;
            Top = top;
            Width = width;
            Height = height;
            if (Settings.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void SavePosition()
        {
            // https://devblogs.microsoft.com/oldnewthing/20041028-00/?p=37453
            if (WindowState != WindowState.Minimized &&
                VirtualDesktop.IsValidDesktopId(DesktopId) &&
                Left > WindowsUtilities.WHERE_NOONE_CAN_SEE_ME &&
                Top > WindowsUtilities.WHERE_NOONE_CAN_SEE_ME)
            {
                Settings.Left = (int)Left;
                Settings.Top = (int)Top;
                Settings.Width = (int)Width;
                Settings.Height = (int)Height;
                Settings.IsMaximized = WindowState == WindowState.Maximized;
                Settings.SerializeToConfigurationWhenIdle();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;
            LoadPosition();
            SizeChanged += (_, __) => SavePosition();
            LocationChanged += (_, __) => SavePosition();

            _dataContext = new WindowDataContext(this);
            DataContext = _dataContext;

            GroupsTab.ItemsSource = _groups;
            _groups.Add(_defaultTabGroup);
            _groups.Add(new AddTabGroup());

            GroupsTab.SelectedItem = _defaultTabGroup;

            if (Settings.RecentGroups != null)
            {
                foreach (var group in Settings.RecentGroups)
                {
                    if (group.Name == _defaultTabGroup.Name)
                    {
                        _defaultTabGroup.ActiveTabKey = group.ActiveTabKey;
                        continue;
                    }

                    var existing = _groups.FirstOrDefault(g => g.Key == group.Key);
                    if (existing != null)
                    {
                        existing.ActiveTabKey = group.ActiveTabKey;
                        continue;
                    }

                    _ = AddGroup(group.ToTabGroup(), false);
                }
            }

            MonacoTab tab;
            if (!Program.IsNewInstance && Settings.RecentFilesPaths != null)
            {
                foreach (var file in Settings.RecentFilesPaths.Where(f => f.OpenOrder > 0).OrderBy(f => f))
                {
                    if (file.UntitledNumber > 0)
                    {
                        tab = AddTabAsync(file.GroupKey, null, false, file.UntitledNumber, file.Options);
                        _ = tab.InitializeAsync(null, file.LanguageId);
                    }
                    else
                    {
                        if (!IOUtilities.IsPathRooted(file.FilePath))
                            continue;

                        tab = AddTabAsync(file.GroupKey, file.FilePath, false, options: file.Options);
                        _ = tab.InitializeAsync(file.FilePath, file.LanguageId);
                    }
                }
            }

            RevealFile(Settings.ActiveFilePath);
            SetTitle(CurrentTab);

            foreach (var group in Groups.Where(t => !t.FileViewTabs.Any()))
            {
                tab = AddTabAsync(group.Key, null, false);
                _ = tab.InitializeAsync(null);
            }

            foreach (var group in Groups)
            {
                group.SelectTab(group.ActiveTabKey);

                // make sure +  is not selected
                if (group.SelectedTabIndex == group.Tabs.Count - 1)
                {
                    group.SelectedTabIndex = 0;
                }
            }

            var activeGroup = GetGroupOrDefault(Settings.ActiveGroupKey);
            GroupsTab.SelectedItem = activeGroup;

            ExecCommandLine(CommandLine.Current);

            _state = State.Ready;
        }

        public IntPtr Handle { get; private set; } // so we can access from any thread
        public PerDesktopSettings Settings => PerDesktopSettings.Get(DesktopId);
        public Guid DesktopId => VirtualDesktop.GetWindowDesktopId(Handle);
        public TabGroup DefaultGroup => _defaultTabGroup;
        public TabGroup CurrentGroup => GroupsTab.SelectedItem is TabGroup group && !group.IsAdd ? group : _defaultTabGroup;
        public MonacoTab CurrentTab => CurrentGroup.Tabs.ElementAtOrDefault(CurrentGroup.SelectedTabIndex);
        public IEnumerable<TabGroup> Groups => _groups.Where(g => !g.IsAdd);
        public IEnumerable<MonacoTab> AllViewTabs
        {
            get
            {
                foreach (var group in Groups)
                {
                    foreach (var tab in group.FileViewTabs)
                    {
                        yield return tab;
                    }
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (_state == State.Closed)
                return;

            if (_state != State.Closing)
            {
                _state = State.Closing;
                e.Cancel = true;

                // in autosave case, we need to get the text async and in the UI thread
                Task.Run(() => Dispatcher.Invoke(async () =>
                {
                    Settings.SerializeToConfigurationWhenIdle(0);
                    DevPad.Settings.Current.SerializeToConfigurationWhenIdle(0);
                    foreach (var tab in AllViewTabs)
                    {
                        await tab.CloseAsync(false);
                        tab.Dispose();
                    }
                    _state = State.Closed;
                    Close();
                }));
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                var groupKey = CurrentGroup?.Key;
                Task.Run(() =>
                {
                    var last = files.Last();
                    foreach (var file in files)
                    {
                        Dispatcher.Invoke(async () =>
                        {
                            await OpenFileAsync(groupKey, file, file == last);
                        });
                    }
                });
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

        public static void ShowSystemInfo(Window window = null)
        {
            var si = new SystemInformation();
            var dlg = new ObjectProperties(si, true);
            dlg.Height = 800;
            dlg.Width = 800;
            dlg.MinimizeToScreen();
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
            // e.CallingDesktopId is Guid.Empty in debug mode
            switch (e.Type)
            {
                case SingleInstanceCommandType.SendCommandLine:
                    var instanceMode = DevPad.Settings.Current.SingleInstanceMode;
#if DEBUG // see Program.cs for explanation
                    if (instanceMode == SingleInstanceMode.OneInstancePerDesktop && e.CallingDesktopId != DesktopId && VirtualDesktop.IsValidDesktopId(e.CallingDesktopId))
                        break;
#else
                    if (instanceMode == SingleInstanceMode.OneInstancePerDesktop && e.CallingDesktopId != DesktopId)
                        break;
#endif

                    e.Handled = true;
                    //Program.Trace(e.Type + " process:" + e.CallingProcessId + " user:" + e.UserDomainName + "\\" + e.UserName + " desktop:" + e.CallingDesktopId);
                    var cmdLine = CommandLine.From(e.Arguments?.Select(a => string.Format("{0}", a))?.ToArray());
                    cmdLine.CurrentDirectory = e.CurrentDirectory;
                    Dispatcher.Invoke(() =>
                    {
                        WindowsUtilities.SetForegroundWindow(Handle);
                        WindowState = WindowState.Normal;
                        Show();
                        ExecCommandLine(cmdLine);
                    });
                    break;

                case SingleInstanceCommandType.Ping:
                    e.Handled = true;
                    //Program.Trace(e.Type + " process:" + e.CallingProcessId + " user:" + e.UserDomainName + "\\" + e.UserName + " deskop:" + e.CallingDesktopId);
                    e.Output = WindowsUtilities.CurrentProcess.Id;
                    break;

                case SingleInstanceCommandType.Quit:
                    e.Handled = true;
                    //Program.Trace(e.Type + " process:" + e.CallingProcessId + " user:" + e.UserDomainName + "\\" + e.UserName + " deskop:" + e.CallingDesktopId);
                    Dispatcher.Invoke(async () =>
                    {
                        await CloseAllGroupsAsync(false, true);
                        Settings.SerializeToConfigurationWhenIdle(0); // flush if any change in queue
                        _state = State.Closing;
                        Close();
                    });
                    break;
            }
        }

        private object ExecCommandLine(CommandLine cmdLine)
        {
            if (cmdLine == null)
                return null;

            var open = cmdLine.GetNullifiedArgument(0);
            if (open != null)
            {
                if (!IOUtilities.IsPathRooted(open) && IOUtilities.IsPathRooted(cmdLine.CurrentDirectory))
                {
                    open = Path.Combine(cmdLine.CurrentDirectory, open);
                }

                var groupName = cmdLine.GetNullifiedArgument("group");
                _ = OpenFileAsync(groupName, open, true);
            }

            return null;
        }

        public void RevealFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            foreach (var group in Groups)
            {
                foreach (var tab in group.FileViewTabs)
                {
                    if (tab.FilePath.EqualsIgnoreCase(filePath))
                    {
                        GroupsTab.SelectedItem = group;
                        return;
                    }
                }
            }
        }

        private void CloseGroup(TabGroup group)
        {
            if (group == null || !group.IsClosable)
                return;

            var tabs = group.FileViewTabs.Where(t => t.HasContentChanged).ToArray();
            if (tabs.Length > 0)
            {
                string txt;
                if (tabs.Length == 1)
                {
                    txt = string.Format(DevPad.Resources.Resources.ConfirmDiscardDocument, tabs[0]);
                }
                else
                {
                    txt = string.Format(DevPad.Resources.Resources.ConfirmDiscardDocument, tabs);
                }
                if (this.ShowConfirm(txt) != MessageBoxResult.Yes)
                    return;
            }

            Settings.RemoveRecentGroup(group);
            Settings.SerializeToConfigurationWhenIdle();
            _groups.Remove(group);
        }

        private void EditGroup(TabGroup group)
        {
            if (group == null)
                return;

            var oldGroup = RecentGroup.FromTabGroup(group);
            var dlg = new EditGroup(group);
            dlg.Owner = this;
            if (dlg.ShowDialog() != true)
                return;

            Settings.RemoveRecentGroup(oldGroup);
            Settings.AddRecentGroup(group);
            Settings.SerializeToConfigurationWhenIdle();
        }

        private async Task<TabGroup> AddGroup()
        {
            var group = new TabGroup();
            var dlg = new EditGroup(group);
            dlg.Owner = this;
            if (dlg.ShowDialog() != true)
                return null;

            await AddGroup(group, true);
            Settings.AddRecentGroup(group);
            Settings.SerializeToConfigurationWhenIdle();
            return group;
        }

        private async Task AddGroup(TabGroup group, bool selectTab)
        {
            var count = _groups.Count - 1;
            _groups.Insert(count, group);
            GroupsTab.SelectedIndex = count;
            var tab = AddTabAsync(group.Key, null, selectTab);
            await tab.InitializeAsync(null);
        }

        private class WindowDataContext : DictionaryObject
        {
            private readonly MainWindow _main;

            public WindowDataContext(MainWindow main)
            {
                _main = main;
                _main.Settings.PropertyChanged += OnPerDesktopSettingsPropertyChanged;
                DevPad.Settings.Current.PropertyChanged += OnSettingsPropertyChanged;
            }

            public Dock GroupsTabPlacement { get => DictionaryObjectGetPropertyValue(Dock.Top); set => DictionaryObjectSetPropertyValue(value); }
            public Visibility GroupsTabVisibility { get => DictionaryObjectGetPropertyValue(Visibility.Visible); set => DictionaryObjectSetPropertyValue(value); }

            private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(DevPad.Settings.Current.ShowMinimap))
                {
                    OnPropertyChanged(e.PropertyName);
                    _ = _main.CurrentTab?.EnableMinimapAsync(DevPad.Settings.Current.ShowMinimap);
                    return;
                }

                if (e.PropertyName == nameof(DevPad.Settings.Current.FontSize))
                {
                    _ = _main.CurrentTab?.SetFontSizeAsync(DevPad.Settings.Current.FontSize);
                    return;
                }

                if (e.PropertyName == nameof(DevPad.Settings.Current.Theme))
                {
                    _ = _main.CurrentTab?.SetEditorThemeAsync(DevPad.Settings.Current.Theme);
                    return;
                }
            }

            private void OnPerDesktopSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
            }

            public void RaisePropertyChanged()
            {
                // from settings
                OnPropertyChanged(nameof(ShowMinimap));

                OnPropertyChanged(nameof(CursorPosition));
                OnPropertyChanged(nameof(CursorSelection));
                OnPropertyChanged(nameof(ModelLanguageName));
                OnPropertyChanged(nameof(EncodingName));
                OnPropertyChanged(nameof(EncodingToolTip));
                OnPropertyChanged(nameof(LoadingPercent));
            }

            public string CursorPosition => _main.CurrentTab?.CursorPosition;
            public string CursorSelection => _main.CurrentTab?.CursorSelection;
            public string ModelLanguageName => _main.CurrentTab?.ModelLanguageName;

            public string LoadingPercent
            {
                get
                {
                    var percent = _main.CurrentTab?.LoadingPercent;
                    if (percent == null)
                        return null;

                    return string.Format(DevPad.Resources.Resources.LoadingPercent, percent.Value);
                }
            }

            public string EncodingName
            {
                get
                {
                    var encoding = _main.CurrentTab?.Encoding;
                    if (encoding == null)
                        return string.Empty;

                    var bom = encoding.GetPreamble();
                    if (bom != null && bom.Length > 0)
                        return string.Format(DevPad.Resources.Resources.EncodingBom, encoding.WebName);

                    if (encoding == Encoding.Default)
                        return "ansi";

                    return encoding.WebName;
                }
            }

            public string EncodingToolTip
            {
                get
                {
                    var encoding = _main.CurrentTab?.Encoding;
                    if (encoding == null)
                        return string.Empty;

                    var name = encoding.BodyName;
                    if (!encoding.WebName.EqualsIgnoreCase(name))
                    {
                        name += Environment.NewLine + encoding.WebName;
                    }

                    if (!encoding.EncodingName.EqualsIgnoreCase(name) && !encoding.EncodingName.EqualsIgnoreCase(encoding.WebName))
                    {
                        name += Environment.NewLine + encoding.EncodingName;
                    }

                    var bom = encoding.GetPreamble();
                    if (bom != null && bom.Length > 0)
                        return string.Format(DevPad.Resources.Resources.EncodingBom, name);

                    return name;
                }
            }

            public bool ShowMinimap
            {
                get => DevPad.Settings.Current.ShowMinimap;
                set
                {
                    if (ShowMinimap == value)
                        return;

                    DevPad.Settings.Current.ShowMinimap = value;
                    DevPad.Settings.Current.SerializeToConfigurationWhenIdle();
                    OnPropertyChanged();

                    _ = _main.CurrentTab?.EnableMinimapAsync(value);
                }
            }
        }

        private TabGroup GetGroup(MonacoTab tab) => Groups.FirstOrDefault(g => g.Key.EqualsIgnoreCase(tab.GroupKey)) ?? DefaultGroup;
        private TabGroup GetGroupOrDefault(string key)
        {
            // try by key (name+colors) first
            var group = Groups.FirstOrDefault(g => g.Key.EqualsIgnoreCase(key));
            if (group != null)
                return group;

            group = Groups.FirstOrDefault(g => g.Name.EqualsIgnoreCase(key));
            if (group != null)
                return group;

            return _defaultTabGroup;
        }

        private async Task SetLanguageAsync(MonacoTab tab, string languageId)
        {
            if (tab == null || !tab.IsFileView)
                return;

            await tab.SetEditorLanguageAsync(languageId, PasteAction.DoNothing);
            SaveRecentFile(tab, languageId);
        }

        private async Task OpenFileAsync(string groupKey, string filePath, bool selectTab)
        {
            if (!IOUtilities.IsPathRooted(filePath) || !IOUtilities.PathIsFile(filePath))
                return;

            var tab = AllViewTabs.FirstOrDefault(t => t.FilePath.EqualsIgnoreCase(filePath));
            if (tab != null)
            {
                var tabGroup = GetGroup(tab) ?? GetGroupOrDefault(groupKey);
                GroupsTab.SelectedItem = tabGroup;
                tabGroup.SelectTab(tab);
                return;
            }

            // if only one unchanged & untitled open, remove it (so it's replacing it by this file)
            var group = GetGroupOrDefault(groupKey);
            if (group.Tabs.Count == 2 && group.Tabs[0].FilePath == null && !group.Tabs[0].HasContentChanged)
            {
                await RemoveTabAsync(group.Tabs[0], false, false, false, false);
            }

            var recent = Settings.GetRecentFile(filePath);

            tab = AddTabAsync(group.Key, filePath, selectTab);
            tab.GroupKey = group.Key;
            SaveTabToRecentFiles(tab, filePath);
            await tab.InitializeAsync(filePath, recent?.LanguageId);
            SetTitle(tab);
        }

        internal async Task RemoveTabAsync(MonacoTab tab, bool deleteAutoSave, bool checkAtLeastOneTab, bool removeFromRecent, bool removeFromOpened)
        {
            if (tab == null || tab.IsAdd)
                return;

            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.MonacoEvent -= OnTabMonacoEvent;
            tab.FileChanged -= OnTabFileChanged;

            // close must happen before we remove the control
            await tab.CloseAsync(deleteAutoSave);
            var group = GetGroup(tab);
            group.RemoveTab(tab);
            tab.Dispose();

            // always ensure we have one (untitled) tab opened
            if (checkAtLeastOneTab && !group.FileViewTabs.Any())
            {
                await AddTabAsync(group.Key, null, true).InitializeAsync(null);
            }

            if (removeFromRecent)
            {
                if (tab.FilePath != null)
                {
                    Settings.RemoveRecentFile(tab.FilePath);
                    Settings.SerializeToConfigurationWhenIdle();
                }
                else if (tab.IsUntitled)
                {
                    Settings.RemoveRecentUntitledFile(tab.UntitledNumber, group.Key);
                    Settings.SerializeToConfigurationWhenIdle();
                }
            }
            else if (removeFromOpened)
            {
                if (tab.FilePath != null)
                {
                    Settings.RemoveOpened(tab.FilePath);
                    Settings.SerializeToConfigurationWhenIdle();
                }
                else if (tab.IsUntitled)
                {
                    Settings.RemoveRecentUntitledFile(tab.UntitledNumber, group.Key);
                    Settings.SerializeToConfigurationWhenIdle();
                }
            }
        }

        private MonacoTab AddTabAsync(string groupKey, string filePath, bool select, int untitledNumber = 0, RecentFileOptions options = RecentFileOptions.None)
        {
            var group = GetGroupOrDefault(groupKey);
            //Program.Trace("path:" + filePath + " groupKey:" + groupKey?.Replace("\0", "!") + " group:" + group.Key + " select:" + select);
            var newTab = new MonacoTab();
            if (options.HasFlag(RecentFileOptions.Pinned))
            {
                newTab.IsPinned = true;
            }

            newTab.GroupKey = groupKey;
            if (filePath == null)
            {
                newTab.UntitledNumber = untitledNumber != 0 ? untitledNumber : group.FileViewTabs.Count(t => t.IsUntitled) + 1;
            }

            group.AddTab(newTab);
            if (select)
            {
                group.SelectTab(newTab);
            }

            newTab.MonacoEvent += OnTabMonacoEvent;
            newTab.FileChanged += OnTabFileChanged;
            newTab.PropertyChanged += OnTabPropertyChanged;
            return newTab;
        }

        private bool DiscardChanges(MonacoTab tab = null)
        {
            tab = tab ?? CurrentTab;
            if (tab == null || !tab.IsFileView)
                return true;

            if (!tab.HasContentChanged)
                return true;

            return this.ShowConfirm(string.Format(DevPad.Resources.Resources.ConfirmDiscardDocument, tab.Name)) == MessageBoxResult.Yes;
        }

        public async Task CloseTabAsync(MonacoTab tab, bool deleteAutoSave, bool removeFromRecent, bool removeFromOpened)
        {
            if (tab == null || !tab.IsFileView)
                return;

            if (!DiscardChanges(tab))
                return;

            await RemoveTabAsync(tab, deleteAutoSave, true, removeFromRecent, removeFromOpened);
        }

        private async Task CloseAllGroupsAsync(bool checkAtLeastOneTab, bool quitting)
        {
            foreach (var group in Groups)
            {
                foreach (var tab in group.FileViewTabs.ToArray())
                {
                    await RemoveTabAsync(tab, false, checkAtLeastOneTab, false, !quitting);
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
            if (tab == null || !tab.IsFileView)
                return;

            string ext = null;
            if (tab.FilePath != null)
            {
                ext = Path.GetExtension(tab.FilePath);
            }

            var filter = BuildFilter(ext);
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
            SaveTabToRecentFiles(tab, fd.FileName);
        }

        private void SaveTabToRecentFiles(MonacoTab tab, string filePath)
        {
            Settings.AddRecentFile(filePath, tab.GroupKey, tab.Index + 1);
            Settings.SerializeToConfigurationWhenIdle();
            WindowsUtilities.SHAddToRecentDocs(filePath);
            Program.WindowsApplication.PublishRecentList();
        }

        private async Task SaveTabAsync(MonacoTab tab)
        {
            if (tab == null || !tab.IsFileView)
                return;

            if (tab.FilePath != null)
            {
                await WrapUnauthorizedAccessAsync(async () => await tab.SaveAsync(tab.FilePath));
                SaveTabToRecentFiles(tab, tab.FilePath);
                return;
            }
            await SaveTabAsAsync(tab);
        }

        private async Task SaveAllTabsAsync()
        {
            foreach (var group in Groups)
            {
                foreach (var tab in group.FileViewTabs.ToArray())
                {
                    await SaveTabAsync(tab);
                }
            }
        }

        private async Task OpenAsync(string groupKey, string directoryPath)
        {
            var filter = BuildFilter(null);
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
                if (DevPad.Settings.Current.OpenFromCurrentTabFolder && CurrentTab.FilePath != null)
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

            var last = fd.FileName;
            foreach (var fileName in fd.FileNames)
            {
                await OpenFileAsync(groupKey, fileName, fileName == last);
            }
        }

        private (string, int) BuildFilter(string ext)
        {
            var view = DefaultGroup.FileViewTabs.FirstOrDefault()?.WebView;
            if (view == null)
                return (null, 0);

            var languages = MonacoExtensions.GetLanguages();
            var sb = new StringBuilder();
            int? index = null;
            var i = 0;
            foreach (var kv in languages.OrderBy(k => k.Value.Name))
            {
                if (kv.Value.Extensions == null || kv.Value.Extensions.Length == 0)
                    continue;

                if (sb.Length > 0)
                {
                    sb.Append('|');
                }
                sb.Append(string.Format(DevPad.Resources.Resources.OneFileFilter, kv.Value.Name, "*" + string.Join(";*", kv.Value.Extensions)));

                if (ext != null && kv.Value.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    index = i;
                }
                i++;
            }
            sb.Append(DevPad.Resources.Resources.AllFilesFilter);
            return (sb.ToString(), index ?? i);
        }

        private void SetTitle(MonacoTab tab)
        {
            if (tab == null || tab.IsAdd)
                return;

            var name = tab.FilePath ?? tab.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                Title = AssemblyUtilities.GetTitle();
            }
            else
            {
                if (tab.HasContentChanged)
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

        private void SaveRecentFile(MonacoTab tab, string languageId = null)
        {
            var options = RecentFileOptions.None;
            if (tab.IsPinned)
            {
                options |= RecentFileOptions.Pinned;
            }

            if (tab.FilePath == null)
            {
                Settings.AddRecentUntitledFile(tab.UntitledNumber, tab.Index + 1, tab.GroupKey, options, languageId);
            }
            else
            {
                Settings.AddRecentFile(tab.FilePath, tab.GroupKey, tab.Index + 1, options, languageId);
            }
            Settings.SerializeToConfigurationWhenIdle();
        }

        private void RenumberGroup(TabGroup group)
        {
            var index = 0;
            foreach (var tab in group.FileViewTabs)
            {
                tab.Index = index;
                SaveRecentFile(tab);
                index++;
            }
        }

        private void MoveTab(MonacoTab tabToMove, TabGroup group, int newIndex)
        {
            group = group ?? GetGroup(tabToMove);
            if (tabToMove.GroupKey != group.Key)
            {
                var oldGroup = GetGroup(tabToMove);
                oldGroup.Tabs.Remove(tabToMove);
                group.Tabs.Insert(newIndex, tabToMove);
                tabToMove.GroupKey = group.Key;
                SaveRecentFile(tabToMove);
                RenumberGroup(oldGroup);
                RenumberGroup(group);
                GroupsTab.SelectedItem = group;
                group.SelectTab(tabToMove);
                return;
            }

            if (newIndex != tabToMove.Index)
            {
                group.Tabs.Move(tabToMove.Index, newIndex);
                RenumberGroup(group);
            }
        }

        private void UnpinAll(TabGroup group)
        {
            if (group == null)
                return;

            var changed = false;
            foreach (var tab in group.FileViewTabs)
            {
                if (tab.IsPinned)
                {
                    tab.IsPinned = false;
                    changed = true;
                }
            }

            if (changed)
            {
                Settings.SerializeToConfigurationWhenIdle();
            }
        }

        private void PinUnpinTab(MonacoTab tab, bool pin)
        {
            if (tab == null || tab.IsPinned == pin)
                return;

            var group = GetGroup(tab);
            var lastPinned = group.FileViewTabs.LastOrDefault(t => t.IsPinned);
            var newIndex = lastPinned != null ? lastPinned.Index + (pin ? 1 : 0) : 0;
            MoveTab(tab, group, newIndex);
            tab.IsPinned = pin;
            SaveRecentFile(tab);
        }

        private async Task DiscardChangesAndReloadAsync(MonacoTab tab)
        {
            if (tab == null)
                return;

            await tab.ReloadAsync();
        }

        private async Task ShowCommandPaletteAsync(MonacoTab tab)
        {
            if (tab == null)
                return;

            await tab.ShowCommandPaletteAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            var thisVersion = Version.Parse(AssemblyUtilities.GetInformationalVersion());
            var releases = await GitHubApi.GetReleasesAsync(CancellationToken.None);
            var last = releases.LastOrDefault();
            if (last == null || thisVersion >= last.Version || last.Assets.Count == 0)
            {
                this.ShowMessage(DevPad.Resources.Resources.NoUpdate);
                return;
            }

            if (this.ShowQuestion(string.Format(DevPad.Resources.Resources.UpdateAvailable, last.Name)) != MessageBoxResult.Yes)
                return;

            var path = await GitHubApi.DownloadReleaseAsync(last.Assets[0].DownloadUrl, "DevPad.Setup.exe", CancellationToken.None);
            Process.Start(path);
            Close();
        }

        private async void OnCheckForUpdates(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync();
        private void OnShowCommandPalette(object sender, RoutedEventArgs e) => _ = ShowCommandPaletteAsync(CurrentTab);
        private void OnDiscardChanges(object sender, RoutedEventArgs e) => _ = DiscardChangesAndReloadAsync(e.GetDataContext<MonacoTab>());
        private void OnUnPinAllTabs(object sender, RoutedEventArgs e) => UnpinAll(CurrentGroup);
        private async void OnSaveTab(object sender, RoutedEventArgs e) => await SaveTabAsync(e.GetDataContext<MonacoTab>());
        private void OnPinThisTab(object sender, RoutedEventArgs e) => PinUnpinTab(GetTabFromContextMenu(e), true);
        private void OnUnPinThisTab(object sender, RoutedEventArgs e) => PinUnpinTab(GetTabFromContextMenu(e), false);
        private void OnUnpinTab(object sender, RoutedEventArgs e) => PinUnpinTab(e.GetDataContext<MonacoTab>(), false);
        private void OnPinTab(object sender, RoutedEventArgs e) => PinUnpinTab(e.GetDataContext<MonacoTab>(), true);
        private void OnCloseGroup(object sender, RoutedEventArgs e) => CloseGroup(e.GetDataContext<TabGroup>());
        private void OnEditGroup(object sender, RoutedEventArgs e) => EditGroup(e.GetDataContext<TabGroup>());
        private void OnCloseCommand(object sender, ExecutedRoutedEventArgs e) => _ = CloseTabAsync(CurrentTab, true, false, true);
        private async void OnAddGroup(object sender, RoutedEventArgs e) => await AddGroup();
        private async void OnSaveAsCommand(object sender, ExecutedRoutedEventArgs e) => await SaveTabAsAsync(CurrentTab);
        private async void OnSaveCommand(object sender, ExecutedRoutedEventArgs e) => await SaveTabAsync(CurrentTab);
        private async void OnSaveAllCommand(object sender, ExecutedRoutedEventArgs e) => await SaveAllTabsAsync();
        private async void OnNewTabCommand(object sender, ExecutedRoutedEventArgs e) => await AddTabAsync(CurrentGroup.Key, null, true).InitializeAsync(null);
        private async void OnOpenCommand(object sender, ExecutedRoutedEventArgs e) => await OpenAsync(CurrentGroup.Key, null);
        private async void OnCloseAll(object sender, RoutedEventArgs e) => await CloseAllGroupsAsync(true, false);
        private void OnCloseTab(object sender, RoutedEventArgs e) => _ = CloseTabAsync(e.GetDataContext<MonacoTab>(), true, false, true);
        private void OnExitClick(object sender, RoutedEventArgs e) => Close();
        private void OnRestartAsAdmin(object sender, RoutedEventArgs e) => RestartAsAdmin(true);
        private void OnFind(object sender, RoutedEventArgs e) => _ = CurrentTab?.ShowFindUIAsync();
        private void OnFormatDocument(object sender, RoutedEventArgs e) => _ = CurrentTab?.FormatDocumentAsync();
        private void OnOpenConfigFolder(object sender, RoutedEventArgs e) => WindowsUtilities.OpenExplorer(Path.GetDirectoryName(Settings.ConfigurationFilePath));
        private void OnOpenConfig(object sender, RoutedEventArgs e) => _ = OpenFileAsync(CurrentGroup.Key, Settings.ConfigurationFilePath, true);
        private async void OnAddTab(object sender, RoutedEventArgs e) => await AddTabAsync(CurrentGroup.Key, null, true).InitializeAsync(null);

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var dlg = new About();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_state == State.Closing)
                return;

            // never end up with + tab selected
            var ctl = (TabControl)sender;
            if (ctl.SelectedItem is MonacoAddTab && ctl.Items.Count > 1)
            {
                ctl.SelectedIndex = ctl.Items.Count - 2;
                return;
            }

            if (e.AddedItems.Count > 0)
            {
                var tab = (MonacoTab)e.AddedItems[0];
                if (!tab.IsAdd)
                {
                    _dataContext.RaisePropertyChanged();
                    SetTitle(tab);

                    var group = GetGroup(tab);
                    if (group != null)
                    {
                        if (group.ActiveTabKey != tab.Key)
                        {
                            group.ActiveTabKey = tab.Key;
                            Settings.AddRecentGroup(group);
                            Settings.SerializeToConfigurationWhenIdle();
                        }
                    }
                }
            }
        }

        private void OnTabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Program.Trace("changed:" + e.PropertyName);
            switch (e.PropertyName)
            {
                case nameof(MonacoTab.IsEditorCreated):
                    _dataContext.RaisePropertyChanged();
                    break;

                case nameof(MonacoTab.HasContentChanged):
                    SetTitle(CurrentTab);
                    break;

                default:
                    _dataContext.RaisePropertyChanged();
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

                        await CloseTabAsync(tab, true, false, true);
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
                    await tab.SetFontSizeAsync(DevPad.Settings.Current.FontSize);
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
                            if (fontSize <= 0)
                                return;

                            fontSize = Math.Round(fontSize, 1);
                            if (fontSize != DevPad.Settings.Current.FontSize)
                            {
                                DevPad.Settings.Current.RaisePropertyChanged = false;
                                DevPad.Settings.Current.FontSize = fontSize;
                                DevPad.Settings.Current.RaisePropertyChanged = true;
                                DevPad.Settings.Current.SerializeToConfigurationWhenIdle();
                            }
                            break;
                    }
                    break;

                case DevPadEventType.ContentChanged:
                    // add untitled to recent files when it changes
                    if (tab.IsUntitled)
                    {
                        Settings.AddRecentUntitledFile(tab.UntitledNumber, tab.Index + 1, tab.GroupKey);
                        Settings.SerializeToConfigurationWhenIdle();
                    }
                    break;
            }
        }

        private void OnLanguagesOpened(object sender, RoutedEventArgs e)
        {
            if (_languagesLoaded)
                return;

            var langs = MonacoExtensions.GetLanguages();
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
                            await SetLanguageAsync(CurrentTab, lang.Key);
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
                        await SetLanguageAsync(CurrentTab, subLangs[0].Key);
                    };
                }
            }
            _languagesLoaded = true;
        }

        //private void OnNewWindow(object sender, RoutedEventArgs e)
        //{
        //    var info = new ProcessStartInfo();
        //    info.FileName = Environment.GetCommandLineArgs()[0];
        //    info.Arguments = "/newinstance";
        //    info.UseShellExecute = true;
        //    Process.Start(info);
        //}

        private void OnPreferences(object sender, RoutedEventArgs e)
        {
            var clone = DevPad.Settings.Current.Clone();
            var dlg = new ObjectProperties(clone, false);
            dlg.Owner = this;
            dlg.Title = DevPad.Resources.Resources.Preferences;
            if (dlg.ShowDialog() != true)
                return;

            DevPad.Settings.Current.CopyFrom(clone);
            DevPad.Settings.Current.SerializeToConfigurationWhenIdle();
        }

        private void OnPerDesktopPreferences(object sender, RoutedEventArgs e)
        {
            var clone = Settings.Clone();
            var dlg = new ObjectProperties(clone, false);
            dlg.Owner = this;
            dlg.Title = DevPad.Resources.Resources.Preferences;
            if (dlg.ShowDialog() != true)
                return;

            Settings.CopyFrom(clone);
            Settings.SerializeToConfigurationWhenIdle();
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

            var files = Settings.RecentFilesPaths?.Where(f => f.UntitledNumber == 0).ToArray();
            RecentFilesMenuItem.IsEnabled = files != null && files.Length > 0;
            if (RecentFilesMenuItem.IsEnabled)
            {
                RecentFilesMenuItem.Items.Clear();
                foreach (var file in files)
                {
                    var item = new MenuItem { Header = file.DisplayName };
                    item.Tag = file;
                    item.Icon = new Image { Source = IconUtilities.GetItemIconAsImageSource(file.FilePath, SHIL.SHIL_SMALL) };
                    RecentFilesMenuItem.Items.Add(item);
                    item.Click += (s, e2) =>
                    {
                        _ = OpenFileAsync(CurrentGroup.Key, file.FilePath, true);
                    };
                }

                RecentFilesMenuItem.Items.Add(new Separator());
                var clear = new MenuItem { Header = DevPad.Resources.Resources.ClearRecentList };
                RecentFilesMenuItem.Items.Add(clear);
                clear.Click += (s, e2) =>
                {
                    Settings.ClearRecentFiles();
                };

                var fileItems = RecentFilesMenuItem.Items.OfType<MenuItem>().Where(m => m.Tag is RecentFile).ToList();
                if (fileItems.Count > 1)
                {
                    var last = fileItems.Last();
                    var clearLast = new MenuItem { Header = string.Format(DevPad.Resources.Resources.ClearLast, ((RecentFile)last.Tag).FilePath) };
                    clearLast.StaysOpenOnClick = true;
                    RecentFilesMenuItem.Items.Add(clearLast);
                    clearLast.Click += (s, e2) =>
                    {
                        RecentFilesMenuItem.Items.Remove(last);
                        Settings.RemoveRecentFile(((RecentFile)last.Tag).FilePath);
                        Settings.SerializeToConfigurationWhenIdle();
                        fileItems.Remove(last);
                        last = fileItems.Last();
                        if (fileItems.Count == 1)
                        {
                            clearLast.IsEnabled = false;
                        }
                    };
                }

                var clean = new MenuItem { Header = DevPad.Resources.Resources.CleanRecentList };
                RecentFilesMenuItem.Items.Add(clean);
                clean.Click += (s, e2) =>
                {
                    Settings.CleanRecentFiles();
                };
            }

            var folderPaths = Settings.RecentFolderPaths;
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
                        _ = OpenAsync(CurrentGroup.Key, folderPath);
                    };
                }
            }
        }

        private void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_state != State.Ready)
                return;

            var ctl = (TabControl)sender;
            var group = CurrentGroup;
            if (group == null)
                return;

            // never end up with + group selected
            if (ctl.SelectedItem is AddTabGroup && ctl.Items.Count > 1)
            {
                ctl.SelectedIndex = ctl.Items.Count - 2;
            }

            if (group.IsAdd)
                return;

            if (Settings.ActiveGroupKey != group.Key)
            {
                Settings.ActiveGroupKey = group.Key;
                Settings.SerializeToConfigurationWhenIdle();
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

        private void OnHelpOpened(object sender, RoutedEventArgs e)
        {
            var desktop = VirtualDesktop.GetWindowDesktopName(Current);
            if (desktop != null)
            {
                if (_desktopMenuItem == null)
                {
                    _desktopMenuItem = new MenuItem();
                    _desktopMenuItem.IsEnabled = false;
                    HelpMenu.Items.Insert(3, _desktopMenuItem);
                    HelpMenu.Items.Insert(4, new Separator());
                }
                _desktopMenuItem.Header = string.Format(DevPad.Resources.Resources.RunningOnDeskop, desktop);
            }
        }

        private void OnEncodingChange(object sender, RoutedEventArgs e)
        {
            var name = Conversions.ChangeType<EncodingName>((sender as FrameworkElement)?.Tag);
            Encoding encoding = null;
            switch (name)
            {
                case EncodingName.Ansi:
                    encoding = Encoding.Default;
                    break;

                case EncodingName.Utf16LE:
                    encoding = Encoding.Unicode;
                    break;

                case EncodingName.Utf16BE:
                    encoding = Encoding.BigEndianUnicode;
                    break;

                case EncodingName.Utf8:
                    encoding = EncodingDetector.UTF8NoBom;
                    break;

                case EncodingName.Utf8BOM:
                    encoding = Encoding.UTF8;
                    break;
            }

            if (encoding != null && CurrentTab != null)
            {
                CurrentTab.Encoding = encoding;
            }
        }

        private static MonacoTab GetTabFromContextMenu(RoutedEventArgs e) => (e.OriginalSource as DependencyObject).GetVisualSelfOrParent<ContextMenu>()?.Tag as MonacoTab;
        private void OnOpenFileLocation(object sender, RoutedEventArgs e)
        {
            var tab = GetTabFromContextMenu(e);
            if (tab == null || tab.FilePath == null || !IOUtilities.PathIsFile(tab.FilePath))
                return;

            WindowsUtilities.OpenExplorer(Path.GetDirectoryName(tab.FilePath));
        }

        private void OnCopyFilePathToClipboard(object sender, RoutedEventArgs e)
        {
            var tab = GetTabFromContextMenu(e);
            if (tab == null || tab.FilePath == null || !IOUtilities.PathIsFile(tab.FilePath))
                return;

            Clipboard.SetText(tab.FilePath);
        }

        private async void OnCloseAllTabs(object sender, RoutedEventArgs e)
        {
            foreach (var tab in CurrentGroup.Tabs.ToArray())
            {
                await CloseTabAsync(tab, true, false, true);
            }
        }

        private async void OnCloseAllTabsButThis(object sender, RoutedEventArgs e)
        {
            var thisTab = e.GetDataContext<MonacoTab>();
            foreach (var tab in CurrentGroup.Tabs.ToArray())
            {
                if (tab != thisTab)
                {
                    await CloseTabAsync(tab, true, false, true);
                }
            }
        }

        private async void OnCloseAllTabsButPinned(object sender, RoutedEventArgs e)
        {
            foreach (var tab in CurrentGroup.Tabs.ToArray().Where(t => !t.IsPinned))
            {
                await CloseTabAsync(tab, true, false, true);
            }
        }

        private void OnTabMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var tab = e.GetDataContext<MonacoTab>();
            if (tab == null || tab.IsAdd)
                return;

            if (!(sender is FrameworkElement element))
                return;

            if (element.FindName("SaveMenuItem") is MenuItem saveItem)
            {
                saveItem.Header = string.Format(DevPad.Resources.Resources.SaveTab, tab.FilePath);
            }

            if (element.FindName("DiscardChangesMenuItem") is MenuItem discardItem)
            {
                discardItem.Visibility = tab.HasContentChanged && tab.FilePath != null ? Visibility.Visible : Visibility.Collapsed;
                discardItem.Header = string.Format(DevPad.Resources.Resources.DiscardTabChanges, tab.FilePath);
            }

            if (element.FindName("UnPinAllTabsMenuItem") is MenuItem unpinAll)
            {
                unpinAll.IsEnabled = GetGroup(tab).Tabs.Any(t => t.IsPinned);
            }

            if (element.FindName("OpenWithMenuItem") is MenuItem openWith)
            {
                openWith.IsEnabled = tab.FilePath != null && IOUtilities.PathIsFile(tab.FilePath);
            }

            if (element.FindName("MoveToGroupMenuItem") is MenuItem moveItem)
            {
                moveItem.Items.Clear();
                var count = 0;
                foreach (var group in Groups)
                {
                    var groupItem = new MenuItem();
                    groupItem.Header = group.Name;
                    groupItem.IsEnabled = tab.GroupKey != group.Key;
                    groupItem.Click += (s, e2) =>
                    {
                        MoveTab(tab, group, group.FileViewTabs.Count());
                    };
                    moveItem.Items.Add(groupItem);
                    count++;
                }
                moveItem.IsEnabled = count > 1;
            }
        }

        private void OnOpenWith(object sender, RoutedEventArgs e)
        {
            var tab = e.GetDataContext<MonacoTab>();
            if (tab == null || tab.FilePath == null || !IOUtilities.PathIsFile(tab.FilePath))
                return;

            WindowsUtilities.OpenWith(new WindowInteropHelper(this).Handle, tab.FilePath);
        }
    }
}
