using DevPad.Resources;

namespace DevPad.Ipc
{
    public enum SingleInstanceMode
    {
        [LocalizedDescription(nameof(OneInstancePerDesktop))]
        OneInstancePerDesktop,

        [LocalizedDescription(nameof(OneInstance))]
        OneInstance
    }
}
