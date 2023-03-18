using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DevPad.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public int Length { get; set; }
        public int Flags { get; set; }
        public int ShowCmd { get; set; }
        public int MinPositionX { get; set; }
        public int MinPositionY { get; set; }
        public int MaxPositionX { get; set; }
        public int MaxPositionY { get; set; }
        public int NormalPositionLeft { get; set; }
        public int NormalPositionTop { get; set; }
        public int NormalPositionRight { get; set; }
        public int NormalPositionBottom { get; set; }

        public bool IsMinimized => ShowCmd == SW_SHOWMINIMIZED;
        public bool IsValid => Length == Marshal.SizeOf(typeof(WindowPlacement));

        public void SetPlacement(IntPtr handle) => SetWindowPlacement(handle, ref this);
        public static WindowPlacement GetPlacement(IntPtr handle, bool throwOnError = false)
        {
            var wp = new WindowPlacement();
            if (handle == IntPtr.Zero)
                return wp;

            wp.Length = Marshal.SizeOf(typeof(WindowPlacement));
            if (!GetWindowPlacement(handle, ref wp))
            {
                if (throwOnError)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                return new WindowPlacement();
            }
            return wp;
        }

        [DllImport("user32", SetLastError = true)]
        private static extern bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

        [DllImport("user32", SetLastError = true)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

        private const int SW_SHOWMINIMIZED = 2;
    }
}
