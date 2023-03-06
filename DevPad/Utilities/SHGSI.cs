using System;

namespace DevPad.Utilities
{
    [Flags]
    public enum SHGSI
    {
        SHGSI_ICON = 0x100,
        SHGSI_ICONLOCATION = 0,
        SHGSI_LARGEICON = 0,
        SHGSI_LINKOVERLAY = 0x8000,
        SHGSI_SELECTED = 0x10000,
        SHGSI_SHELLICONSIZE = 4,
        SHGSI_SMALLICON = 1,
        SHGSI_SYSICONINDEX = 0x4000
    }
}
