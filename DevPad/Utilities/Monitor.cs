using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace DevPad.Utilities
{
    public class Monitor
    {
        private const int MONITORINFOF_PRIMARY = 0x00000001;

        private Monitor(IntPtr handle)
        {
            Handle = handle;
            var mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(mi);
            if (!GetMonitorInfo(handle, ref mi))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            DeviceName = mi.szDevice.ToString();
            Bounds = mi.rcMonitor;
            WorkingArea = mi.rcWork;
            IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) == MONITORINFOF_PRIMARY;
        }

        public IntPtr Handle { get; }
        public bool IsPrimary { get; }
        public RECT WorkingArea { get; }
        public RECT Bounds { get; }
        public string DeviceName { get; }
        public int[] Dpi => GetDpi();
        public DISPLAY_DEVICE? DISPLAY_DEVICE
        {
            get
            {
                foreach (var dd in Utilities.DISPLAY_DEVICE.All)
                {
                    if (dd.DeviceName.EqualsIgnoreCase(DeviceName))
                        return dd;
                }
                return null;
            }
        }

        public int[] GetDpi(MONITOR_DPI_TYPE type = MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, bool perMonitorAware = true)
        {
            if (perMonitorAware)
            {
                var dpi = new[] { 96, 96 };
                var thread = new Thread(state =>
                {
                    try
                    {
                        var DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = new IntPtr(-3);
                        SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
                        dpi = GetDpi(type, false);
                    }
                    catch
                    {
                        // continue
                    }
                })
                {
                    IsBackground = true
                };

                thread.Start();
                thread.Join(1000);
                return dpi;
            }
            GetDpiForMonitor(Handle, type, out var x, out var y);
            return new[] { x, y };
        }

        public override string ToString() => DeviceName;

        public static Monitor FromWindow(IntPtr hwnd, MFW flags = MFW.MONITOR_DEFAULTTONULL)
        {
            if (hwnd == IntPtr.Zero)
                return null;

            var h = MonitorFromWindow(hwnd, flags);
            if (h == IntPtr.Zero)
                return null;

            return new Monitor(h);
        }

        public static Monitor FromPoint(int x, int y, MFW flags = MFW.MONITOR_DEFAULTTONULL)
        {
            var h = MonitorFromPoint(new POINT { x = x, y = y }, flags);
            if (h == IntPtr.Zero)
                return null;

            return new Monitor(h);
        }

        public static Monitor FromRect(RECT rc, MFW flags = MFW.MONITOR_DEFAULTTONULL)
        {
            var h = MonitorFromRect(ref rc, flags);
            if (h == IntPtr.Zero)
                return null;

            return new Monitor(h);
        }

        public static Monitor Primary => All.FirstOrDefault(m => m.IsPrimary);
        public static IEnumerable<Monitor> All
        {
            get
            {
                var all = new List<Monitor>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, h, rc, p) =>
                {
                    all.Add(new Monitor(m));
                    return true;
                }, IntPtr.Zero);
                return all;
            }
        }

        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam);

        [DllImport("user32")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("shcore")]
        private static extern bool GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, out int dpix, out int dpiy);

        [DllImport("user32", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hmonitor, ref MONITORINFOEX info);

        [DllImport("user32")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, MFW flags);

        [DllImport("user32")]
        private static extern IntPtr MonitorFromPoint(POINT pt, MFW flags);

        [DllImport("user32")]
        private static extern IntPtr MonitorFromRect(ref RECT rect, MFW flags);
    }
}
