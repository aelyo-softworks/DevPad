﻿using System;

namespace DevPad.Utilities
{
    [Flags]
    public enum DISPLAYCONFIG_TOPOLOGY_ID
    {
        DISPLAYCONFIG_TOPOLOGY_INTERNAL = 0x00000001,
        DISPLAYCONFIG_TOPOLOGY_CLONE = 0x00000002,
        DISPLAYCONFIG_TOPOLOGY_EXTEND = 0x00000004,
        DISPLAYCONFIG_TOPOLOGY_EXTERNAL = 0x00000008,
    }
}