using System;
using System.Runtime.InteropServices;

namespace DevPad.MonacoModel
{
#pragma warning disable IDE1006 // Naming Styles
    [ComVisible(true)]
    public class DevPadObject
    {
        public event EventHandler<DevPadLoadEventArgs> Load;

        public string load()
        {
            var e = new DevPadLoadEventArgs();
            Load?.Invoke(this, e);
            return e.Load;
        }
    }
#pragma warning restore IDE1006 // Naming Styles
}
