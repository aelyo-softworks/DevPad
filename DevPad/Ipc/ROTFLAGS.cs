using System;

namespace DevPad.Ipc
{
    [Flags]
    public enum ROTFLAGS
    {
        ROTFLAGS_NONE = 0,
        ROTFLAGS_REGISTRATIONKEEPSALIVE = 1,
        ROTFLAGS_ALLOWANYCLIENT = 2,
    }
}
