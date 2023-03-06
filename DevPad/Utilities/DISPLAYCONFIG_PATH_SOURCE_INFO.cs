using System.Runtime.InteropServices;

namespace DevPad.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public DISPLAYCONFIG_SOURCE_FLAGS statusFlags;
    }
}
