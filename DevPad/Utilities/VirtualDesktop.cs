using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DevPad.Utilities
{
    public sealed class VirtualDesktop
    {
        private static readonly ConcurrentDictionary<IntPtr, Guid> _desktopIds = new ConcurrentDictionary<IntPtr, Guid>();

        private VirtualDesktop()
        {
        }

        public Guid Id { get; private set; }
        public string Name { get; private set; }

        public void MoveWindowTo(IntPtr handle) => MoveWindowToDesktop(handle, Id);
        public override string ToString() => Id + " '" + Name + "'";

        public static bool AreVirtualDesktopsSupported => WindowsUtilities.KernelVersion.Major >= 10;
        public static string GetWindowDesktopName(Guid id) => GetDesktops().FirstOrDefault(d => d.Id == id)?.Name;
        public static string GetWindowDesktopName(Window window)
        {
            if (!AreVirtualDesktopsSupported)
                return null;

            if (window == null)
                return null;

            var id = GetWindowDesktopId(new WindowInteropHelper(window).Handle);
            return GetWindowDesktopName(id);
        }

        public static IReadOnlyList<VirtualDesktop> GetDesktops()
        {
            var list = new List<VirtualDesktop>();
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops", false))
            {
                if (key != null)
                {
                    if (key.GetValue("VirtualDesktopIDs") is byte[] ids)
                    {
                        const int guidLength = 16;
                        var offset = 0;
                        do
                        {
                            if ((offset + guidLength) > ids.Length)
                                break;

                            var id = new byte[guidLength];
                            Array.Copy(ids, offset, id, 0, id.Length);
                            var guid = new Guid(id);
                            string name = null;

                            using (var keyName = key.OpenSubKey(Path.Combine("Desktops", guid.ToString("B")), false))
                            {
                                if (keyName != null)
                                {
                                    name = keyName.GetValue("Name") as string;
                                }
                            }

                            name = name.Nullify() ?? string.Format(Resources.Resources.DesktopIndexedName, list.Count + 1);
                            list.Add(new VirtualDesktop { Id = guid, Name = name });
                            offset += guidLength;
                        }
                        while (true);
                    }
                }
            }
            return list;
        }

        public static bool IsValidDesktopId(Guid id) => id != Guid.Empty || !AreVirtualDesktopsSupported;
        public static Guid GetDesktopId()
        {
            if (!AreVirtualDesktopsSupported)
                return Guid.Empty;

            using (var form = new NoActivateForm())
            {
                form.Show();
                return GetWindowDesktopId(form.Handle);
            }
        }

        public static Guid GetWindowDesktopId(IntPtr handle)
        {
            if (!AreVirtualDesktopsSupported)
                return Guid.Empty;

            try
            {
                var mgr = (IVirtualDesktopManager)new VirtualDesktopManager();
                var hr = mgr.GetWindowDesktopId(handle, out var guid);
                if (hr < 0)
                {
                    // for some reason, even on same (UI) thread, this call ends up with RPC_E_CANTCALLOUT_ININPUTSYNCCALL
                    // so we store the last value
                    if (_desktopIds.TryGetValue(handle, out guid))
                        return guid;

                    return Guid.Empty;
                }

                _desktopIds[handle] = guid;
                return guid;
            }
            catch
            {
                if (_desktopIds.TryGetValue(handle, out var guid))
                    return guid;

                return Guid.Empty;
            }
        }

        public static bool IsWindowOnCurrentDesktop(IntPtr handle)
        {
            if (!AreVirtualDesktopsSupported)
                return true;

            try
            {
                var mgr = (IVirtualDesktopManager)new VirtualDesktopManager();
                mgr.IsWindowOnCurrentVirtualDesktop(handle, out var ret);
                return ret;
            }
            catch
            {
                return true;
            }
        }

        public static void MoveWindowToDesktop(IntPtr handle, Guid desktopId)
        {
            if (!AreVirtualDesktopsSupported)
                return;

            try
            {
                var mgr = (IVirtualDesktopManager)new VirtualDesktopManager();
                mgr.MoveWindowToDesktop(handle, desktopId);
            }
            catch
            {
                // continue
            }
        }

        private sealed class NoActivateForm : Form
        {
            public NoActivateForm()
            {
                Location = new System.Drawing.Point(-30000, -30000);
                Width = 0;
                Height = 0;
                Text = string.Empty;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                MinimizeBox = false;
                MaximizeBox = false;
            }

            protected override bool ShowWithoutActivation => true;
        }

        [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
        private class VirtualDesktopManager { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
        private interface IVirtualDesktopManager
        {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);

            [PreserveSig]
            int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

            [PreserveSig]
            int MoveWindowToDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
        }
    }
}
