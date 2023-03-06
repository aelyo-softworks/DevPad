using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevPad.MonacoModel;
using DevPad.Utilities;
using Microsoft.Web.WebView2.WinForms;

namespace DevPad
{
    public partial class Main : Form
    {
        private bool _languagesLoaded;
        private Font _symbolsFont;
        private Font _symbolsFontBigger;
        private const int _basePadding = 6;
        private readonly Button _addButton = new Button();

        public Main()
        {
            InitializeComponent();
            SetupFonts();

            _addButton.Font = _symbolsFontBigger;
            _addButton.Text = "🞣";
            _addButton.FlatStyle = FlatStyle.Flat;
            _addButton.FlatAppearance.BorderSize = 0;
            _addButton.Click += OnAddClick;
            Controls.Add(_addButton);
            _addButton.BringToFront();

            toolStripStatusPosition.Text = string.Empty;
            toolStripStatusSelection.Text = string.Empty;
            toolStripStatusLanguage.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLanguage.Text = LanguageExtensionPoint.DefaultLanguageName;
            Task.Run(() => Settings.Current.CleanRecentFiles());
            Icon = Resources.Resources.DevPadIcon;
            Text = WinformsUtilities.ApplicationName;

            tabControlMain.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControlMain.DrawItem += OnTabControlDrawItem;
            tabControlMain.Padding = new Point(_basePadding * 3, _basePadding);
            tabControlMain.MouseMove += OnTabControlMouseMove;
            tabControlMain.SelectedIndexChanged += OnTabSelectedIndexChanged;

            AddWebPage();

            //_ = CurrentPage.InitializeAsync();
        }

        public WebPage CurrentPage => (WebPage)tabControlMain.SelectedTab;
        public WebView2 CurrentWebView => CurrentPage.WebView;

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            SetupFonts();
        }

        private void SetupFonts()
        {
            _symbolsFont?.Dispose();
            _symbolsFont = new Font("Segoe UI Symbol", Font.Size);
            _symbolsFontBigger?.Dispose();
            _symbolsFontBigger = new Font("Segoe UI Symbol", Font.Size, FontStyle.Bold);
        }

        private void OnTabSelectedIndexChanged(object sender, EventArgs e)
        {
            toolStripStatusSelection.Text = CurrentPage.CursorSelection;
            toolStripStatusPosition.Text = CurrentPage.CursorPosition;
            toolStripStatusLanguage.Text = CurrentPage.ModelLanguage;
        }

        private void OnAddClick(object sender, EventArgs e)
        {
            AddWebPage();
        }

        private void AddWebPage()
        {
            var wp = new WebPage();
            wp.FilePathChanged += OnFilePathChanged;
            wp.ModelLanguageChanged += OnModelLanguageChanged;
            wp.CursorPositionChanged += OnCursorPositionChanged;
            wp.CursorSelectionChanged += OnCursorSelectionChanged;
            tabControlMain.TabPages.Add(wp);
        }

        private void OnCursorSelectionChanged(object sender, EventArgs e)
        {
            toolStripStatusSelection.Text = CurrentPage.CursorSelection;
        }

        private void OnCursorPositionChanged(object sender, EventArgs e)
        {
            toolStripStatusPosition.Text = CurrentPage.CursorPosition;
        }

        private void OnModelLanguageChanged(object sender, EventArgs e)
        {
            toolStripStatusLanguage.Text = CurrentPage.ModelLanguage;
        }

        private void OnFilePathChanged(object sender, EventArgs e)
        {
            Text = WinformsUtilities.ApplicationName + " - " + CurrentPage.FilePath;
        }

        private void OnTabControlMouseMove(object sender, MouseEventArgs e)
        {
            var mouseOverTab = GetTabWithMouseOverCloseButton();
            var changed = false;
            for (var i = 0; i < tabControlMain.TabPages.Count; i++)
            {
                var tab = (WebPage)tabControlMain.TabPages[i];
                var state = tab.CloseButtonState;
                if (tab == mouseOverTab)
                {
                    tab.CloseButtonState = WebPageCloseButtonState.Hover;
                }
                else
                {
                    tab.CloseButtonState = WebPageCloseButtonState.Normal;
                }

                if (state != tab.CloseButtonState)
                {
                    changed = true;
                }
            }

            if (changed)
            {
                tabControlMain.Refresh();
            }
        }

        private WebPage GetTabWithMouseOverCloseButton()
        {
            var pos = tabControlMain.PointToClient(Cursor.Position);
            for (var i = 0; i < tabControlMain.TabPages.Count; i++)
            {
                var tab = (WebPage)tabControlMain.TabPages[i];
                var rc = tab.CloseButtonRect;
                rc.Inflate(_basePadding, _basePadding);
                if (rc.Contains(pos))
                    return tab;
            }
            return null;
        }

        private void OnTabControlDrawItem(object sender, DrawItemEventArgs e)
        {
            Program.Trace("i:" + e.Index + " state:" + e.State + " b:" + e.Bounds + " clip:" + e.Graphics.VisibleClipBounds);
            //e.Graphics.FillRectangle(Brushes.White, e.Bounds);

            var tab = (WebPage)tabControlMain.TabPages[e.Index];
            using (var foreBrush = new SolidBrush(ForeColor))
            {
                var size = e.Graphics.MeasureString(tab.Text, e.Font);
                e.Graphics.DrawString(tab.Text, e.Font, foreBrush, e.Bounds.X + _basePadding, (e.Bounds.Height - size.Height) / 2);

                const string closeIcon = "✕";
                size = e.Graphics.MeasureString(closeIcon, _symbolsFont);
                var pt = new PointF(e.Bounds.X + e.Bounds.Width - size.Width - _basePadding, (e.Bounds.Height - size.Height) / 2);

                if (tab.CloseButtonState == WebPageCloseButtonState.Hover)
                {
                    e.Graphics.DrawString(closeIcon, _symbolsFontBigger, foreBrush, pt);
                }
                else
                {
                    e.Graphics.DrawString(closeIcon, _symbolsFont, foreBrush, pt);
                }

                tab.CloseButtonRect = new RectangleF(pt.X, pt.Y, size.Width, size.Height);
                if (e.State == DrawItemState.Focus)
                {
                    //e.DrawFocusRectangle();
                }
            }

            if (e.Index == tabControlMain.TabPages.Count - 1)
            {
                var rc = tab.CloseButtonRect;
                _addButton.Size = new Size(e.Bounds.Size.Height, e.Bounds.Size.Height); // square
                _addButton.Location = new Point((int)rc.Right + _basePadding, tabControlMain.Top);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // since the webview eats keyboard, we handle some here

            if (e.KeyCode == Keys.N && e.Control)
            {
                _ = CurrentPage.NewAsync();
                return;
            }

            if (e.KeyCode == Keys.O && e.Control)
            {
                _ = CurrentPage.OpenAsync();
                return;
            }

            if (e.KeyCode == Keys.S && e.Control)
            {
                if (e.Shift)
                {
                    _ = CurrentPage.SaveAsAsync();
                }
                else
                {
                    _ = CurrentPage.SaveAsync();
                }
                return;
            }

            if (e.KeyCode == Keys.T && e.Shift && e.Control)
            {
                var lastRecent = Settings.Current.RecentFilesPaths?.FirstOrDefault();
                if (lastRecent != null)
                {
                    if (!CurrentPage.DiscardChanges())
                        return;

                    _ = CurrentPage.OpenFileAsync(lastRecent.FilePath);
                }
                return;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != MessageDecoder.WM_SETCURSOR && m.Msg != MessageDecoder.WM_GETICON && m.Msg != MessageDecoder.WM_NCMOUSEMOVE)
            {
                //Program.Trace(MessageDecoder.Decode(m));
            }

            if (m.Msg == MessageDecoder.WM_SYSKEYUP)
            {
                if (((Keys)(int)(long)m.WParam) == Keys.Escape)
                {
                    _ = CurrentPage.FocusEditorAsync();
                }
            }

            if (m.Msg == MessageDecoder.WM_PARENTNOTIFY)
            {
                var lo = MessageDecoder.LOWORD(m.WParam);
                switch (lo)
                {
                    case MessageDecoder.WM_RBUTTONDOWN:
                    case MessageDecoder.WM_MBUTTONDOWN:
                    case MessageDecoder.WM_LBUTTONDOWN:
                    case MessageDecoder.WM_XBUTTONDOWN:
                        var x = MessageDecoder.LOWORD(m.LParam);
                        var y = MessageDecoder.HIWORD(m.LParam);
                        //Program.Trace("x:" + x + " y:" + y);
                        if (y < MainMenu.Height)
                        {
                            this.SafeBeginInvoke(async () =>
                            {
                                await CurrentPage.BlurEditorAsync();
                                MainMenu.Focus();
                            });
                        }
                        else
                        {
                            _ = CurrentPage.FocusEditorAsync();
                        }
                        break;
                }
            }
            base.WndProc(ref m);
        }

        private void AboutDevPadToolStripMenuItem_Click(object sender, EventArgs e) { var dlg = new AboutForm(); dlg.ShowDialog(this); }
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e) => _ = CurrentPage.OpenAsync();
        private void ClearRecentListToolStripMenuItem_Click(object sender, EventArgs e) => Settings.Current.ClearRecentFiles();
        private void NewToolStripMenuItem_Click(object sender, EventArgs e) => _ = CurrentPage.NewAsync();
        private void NewWindowToolStripMenuItem_Click(object sender, EventArgs e) => Process.Start(Application.ExecutablePath);
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e) => Close();
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e) => _ = CurrentPage.SaveAsync();
        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e) => _ = CurrentPage.SaveAsAsync();
        private void FileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            const int fixedRecentItemsCount = 2;
            while (openRecentToolStripMenuItem.DropDownItems.Count > fixedRecentItemsCount)
            {
                openRecentToolStripMenuItem.DropDownItems.RemoveAt(0);
            }

            var recents = Settings.Current.RecentFilesPaths;
            if (recents != null)
            {
                foreach (var recent in recents)
                {
                    var item = new ToolStripMenuItem(recent.FilePath);
                    openRecentToolStripMenuItem.DropDownItems.Insert(openRecentToolStripMenuItem.DropDownItems.Count - fixedRecentItemsCount, item);
                    item.Click += async (s, ex) =>
                    {
                        if (!CurrentPage.DiscardChanges())
                            return;

                        await CurrentPage.OpenFileAsync(recent.FilePath);
                    };
                }
            }
            openRecentToolStripMenuItem.Enabled = openRecentToolStripMenuItem.DropDownItems.Count > fixedRecentItemsCount;
        }

        private async void ViewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (_languagesLoaded)
                return;

            var langs = await CurrentWebView.GetLanguages();
            setLanguageToolStripMenuItem.DropDownItems.Clear();
            foreach (var group in langs.OrderBy(k => k.Value.Name).GroupBy(n => n.Value.Name.Substring(0, 1), comparer: StringComparer.OrdinalIgnoreCase))
            {
                var subLangs = group.OrderBy(l => l.Value.Name).ToArray();
                if (subLangs.Length > 1)
                {
                    var item = new ToolStripMenuItem(group.Key.ToUpperInvariant());
                    setLanguageToolStripMenuItem.DropDownItems.Add(item);
                    foreach (var lang in group.OrderBy(l => l.Value.Name))
                    {
                        var subItem = item.DropDownItems.Add(lang.Value.Name);
                        subItem.Click += async (s, e2) =>
                        {
                            await CurrentPage.SetEditorLanguage(lang.Key);
                        };
                    }
                }
                else
                {
                    var item = setLanguageToolStripMenuItem.DropDownItems.Add(subLangs[0].Value.Name);
                    item.Click += async (s, e2) =>
                    {
                        await CurrentPage.SetEditorLanguage(subLangs[0].Key);
                    };
                }
            }
            _languagesLoaded = true;
        }

        private void PreferencesToolStripMenuItem_Click(object sender, EventArgs e2)
        {
            var dlg = new SettingsForm();
            dlg.Settings.PropertyChanged += async (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.Theme):
                        await CurrentPage.SetEditorThemeAsync(dlg.Settings.Theme);
                        break;
                }
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Current.CopyFrom(dlg.Settings);
                Settings.Current.SerializeToConfiguration();
            }
            _ = CurrentPage.SetEditorThemeAsync(Settings.Current.Theme);
        }

        private void GoToToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
