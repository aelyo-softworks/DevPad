using System;

namespace DevPad.Utilities
{
    [Flags]
    public enum UniversalConverterOptions
    {
        None = 0x0,
        Trim = 0x1,
        Convert = 0x2,
        Nullify = 0x4,
        NullMatchesType = 0x8,
        ConvertedValueIsConverterParameter = 0x20,
    }
}
