using System;
using System.Runtime.InteropServices;

namespace DevPad.MonacoModel
{
#pragma warning disable IDE1006 // Naming Styles, we want to look like traditional js code
    [ComVisible(true)]
    public class DevPadObject
    {
        public event EventHandler<DevPadLoadEventArgs> Load;
        public event EventHandler<DevPadEventArgs> Event;

        public string load()
        {
            var e = new DevPadLoadEventArgs();
            Load?.Invoke(this, e);
            return e.DocumentText;
        }

        public void onEvent(DevPadEventType type, string json = null)
        {
            var handler = Event;
            if (handler == null)
                return;

            DevPadEventArgs e;
            switch (type)
            {
                case DevPadEventType.KeyDown:
                case DevPadEventType.KeyUp:
                    e = new DevPadKeyEventArgs(type, json);
                    break;

                default:
                    e = new DevPadEventArgs(type, json);
                    break;
            }
            handler?.Invoke(this, e);
        }
    }
#pragma warning restore IDE1006 // Naming Styles
}
