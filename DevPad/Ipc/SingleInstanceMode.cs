using System.ComponentModel;

namespace DevPad.Ipc
{
    public enum SingleInstanceMode
    {
        [Description("One Instance Per Virtual Desktop")]
        OneInstancePerDesktop,

        [Description("One Instance")]
        OneInstance
    }
}
