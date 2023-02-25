using System;
using System.ComponentModel;

namespace Utilities
{
    public class TaskDialogEventArgs : HandledEventArgs
    {
        public TaskDialogEventArgs(IntPtr hwnd, TASKDIALOG_NOTIFICATIONS msg, IntPtr wParam, IntPtr lParam)
        {
            Hwnd = hwnd;
            Message = msg;
            WParam = wParam;
            LParam = lParam;
        }

        public IntPtr Hwnd { get; }
        public TASKDIALOG_NOTIFICATIONS Message { get; }
        public IntPtr WParam { get; }
        public IntPtr LParam { get; }
        public int HResult { get; set; }
    }
}
