using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DevPad.Utilities
{
    public class TaskDialog : IDisposable
    {
        private static readonly Pftaskdialogcallback _cb = Callback;

        public static readonly IntPtr TD_WARNING_ICON = new IntPtr(unchecked((ushort)-1));
        public static readonly IntPtr TD_ERROR_ICON = new IntPtr(unchecked((ushort)-2));
        public static readonly IntPtr TD_INFORMATION_ICON = new IntPtr(unchecked((ushort)-3));
        public static readonly IntPtr TD_SHIELD_ICON = new IntPtr(unchecked((ushort)-4));

        public event EventHandler<TaskDialogEventArgs> Event;

        private GCHandle _handle;
        private bool _disposedValue;

        public TaskDialog()
        {
            _handle = GCHandle.Alloc(this);
        }

        public virtual TASKDIALOG_FLAGS Flags { get; set; }
        public virtual TASKDIALOG_COMMON_BUTTON_FLAGS CommonButtonFlags { get; set; }
        public virtual string Title { get; set; }
        public virtual string MainInstruction { get; set; }
        public virtual string Content { get; set; }
        public virtual string VerificationText { get; set; }
        public virtual string ExpandedInformation { get; set; }
        public virtual string CollapsedControlText { get; set; }
        public virtual string Footer { get; set; }
        public virtual IntPtr MainIcon { get; set; }
        public virtual IntPtr FooterIcon { get; set; }
        public virtual int Width { get; set; }

        public int ResultButton { get; protected set; }
        public int ResultRadioButton { get; protected set; }
        public bool ResultVerificationFlagChecked { get; protected set; }

        public DialogResult Show(IWin32Window window) => Show(window?.Handle ?? IntPtr.Zero);
        public virtual DialogResult Show(IntPtr hwnd)
        {
            var config = new TASKDIALOGCONFIG();
            config.cbSize = Marshal.SizeOf(config);
            config.pfCallback = _cb;
            config.lpCallbackData = GCHandle.ToIntPtr(_handle);
            config.hwndParent = hwnd;
            config.dwFlags = Flags;
            config.dwCommonButtons = CommonButtonFlags;
            config.pszWindowTitle = Title;
            config.pszMainInstruction = MainInstruction;
            config.pszContent = Content;
            config.pszVerificationText = VerificationText;
            config.pszExpandedControlText = ExpandedInformation;
            config.pszCollapsedControlText = CollapsedControlText;
            config.pszFooter = Footer;
            if (MainIcon != IntPtr.Zero)
            {
                config.hMainIcon = MainIcon;
                //config.dwFlags |= TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN;
            }

            if (FooterIcon != IntPtr.Zero)
            {
                config.hFooterIcon = FooterIcon;
                //config.dwFlags |= TASKDIALOG_FLAGS.TDF_USE_HICON_FOOTER;
            }

            config.cxWidth = Width;
            try
            {
                var hr = TaskDialogIndirect(ref config, out var button, out var radioButton, out var verificationFlagChecked);
                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);

                ResultButton = button;
                ResultRadioButton = radioButton;
                ResultVerificationFlagChecked = verificationFlagChecked;
                return (DialogResult)button;
            }
            catch
            {
                // if you're here, make sure you've enabled Microsoft.Windows.Common-Controls in app.manifest
                return DialogResult.Abort;
            }
        }

        protected virtual void HandleEvent(TaskDialogEventArgs e)
        {
            if (e.Handled)
                return;

            if (e.Message == TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
            {
                var str = Marshal.PtrToStringUni(e.LParam).Nullify();
                if (str != null)
                {
                    WindowsUtilities.OpenUrl(str);
                }
            }
        }

        protected virtual void OnEvent(object sender, TaskDialogEventArgs e) => Event?.Invoke(sender, e);
        public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _handle.Free();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                _disposedValue = true;
            }
        }

        [DllImport("comctl32", SetLastError = true)]
        private static extern int TaskDialogIndirect(ref TASKDIALOGCONFIG pTaskConfig, out int pnButton, out int pnRadioButton, out bool pfVerificationFlagChecked);

        private delegate int Pftaskdialogcallback(IntPtr hwnd, TASKDIALOG_NOTIFICATIONS msg, IntPtr wParam, IntPtr lParam, IntPtr lpRefData);
        private static int Callback(IntPtr hwnd, TASKDIALOG_NOTIFICATIONS msg, IntPtr wParam, IntPtr lParam, IntPtr lpRefData)
        {
            try
            {
                var gch = GCHandle.FromIntPtr(lpRefData);
                var td = (TaskDialog)gch.Target;
                var e = new TaskDialogEventArgs(hwnd, msg, wParam, lParam);
                td.OnEvent(td, e);
                td.HandleEvent(e);
                return e.HResult;
            }
            catch
            {
                // continue
                return 0;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        private struct TASKDIALOGCONFIG
        {
            public int cbSize;
            public IntPtr hwndParent;
            public IntPtr hInstance;
            public TASKDIALOG_FLAGS dwFlags;
            public TASKDIALOG_COMMON_BUTTON_FLAGS dwCommonButtons;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszWindowTitle;
            public IntPtr hMainIcon;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszMainInstruction;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszContent;
            public int cButtons;
            public IntPtr pButtons;
            public int nDefaultButton;
            public int cRadioButtons;
            public IntPtr pRadioButtons;
            public int nDefaultRadioButton;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszVerificationText;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszExpandedInformation;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszExpandedControlText;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszCollapsedControlText;
            public IntPtr hFooterIcon;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszFooter;
            public Pftaskdialogcallback pfCallback;
            public IntPtr lpCallbackData;
            public int cxWidth;
        }
    }
}
