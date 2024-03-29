﻿using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DevPad.MonacoModel
{
#pragma warning disable IDE1006 // Naming Styles, we want to look like traditional js code
    [ComVisible(true)]
    public class DevPadObject
    {
        public event EventHandler<DevPadLoadEventArgs> Load;
        public event EventHandler<DevPadEventArgs> Event;

        public object getOptions()
        {
            return JsonSerializer.Serialize(new
            {
                automaticLayout = true,
                //language = "plaintext",
                fontSize = Settings.Current.FontSize.ToString(CultureInfo.InvariantCulture) + "px",
                dragAndDrop = false,
                mouseWheelZoom = true,
                contextmenu = false,
                theme = Settings.Current.Theme,
            });
        }

        public string load()
        {
            try
            {
                var e = new DevPadLoadEventArgs();
                Load?.Invoke(this, e);
                return e.DocumentText;
            }
            catch (Exception ex)
            {
                Program.Trace(ex);
                throw;
            }
        }

        public void onEvent(DevPadEventType type, string json = null)
        {
            var handler = Event;
            if (handler == null)
                return;

            try
            {
                DevPadEventArgs e;
                switch (type)
                {
                    case DevPadEventType.KeyDown:
                    case DevPadEventType.KeyUp:
                        e = new DevPadKeyEventArgs(type, json);
                        break;

                    case DevPadEventType.ConfigurationChanged:
                        e = new DevPadConfigurationChangedEventArgs(json);
                        break;

                    default:
                        e = new DevPadEventArgs(type, json);
                        break;
                }
                handler?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Program.Trace(ex);
                throw;
            }
        }
    }
#pragma warning restore IDE1006 // Naming Styles
}
