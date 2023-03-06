using System.Runtime.InteropServices;

namespace DevPad.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
    {
        public POINT PathSourceSize;
        public RECT DesktopImageRegion;
        public RECT DesktopImageClip;
    }
}
