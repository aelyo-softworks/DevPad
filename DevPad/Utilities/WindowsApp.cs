using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

namespace DevPad.Utilities
{
    public class WindowsApp
    {
        private static readonly RegistryKey _hkcu = Registry.CurrentUser;
        private const string _basePath = @"Software\Classes";

        public WindowsApp(string appUserModelId, string friendlyName, string progId = null)
        {
            if (appUserModelId == null)
                throw new ArgumentNullException(nameof(appUserModelId));

            if (friendlyName == null)
                throw new ArgumentNullException(nameof(friendlyName));

            AppUserModelId = appUserModelId;
            ProgId = progId ?? appUserModelId;
            FriendlyName = friendlyName;
        }

        public string FriendlyName { get; }
        public string PublisherName { get; set; }
        public string ProgId { get; }
        public string AppUserModelId { get; }
        internal const string ApplicationIconProgId = ".iconAelyo"; // this is only used to be able to identify the app's icon in the imagelist...

        public void RegisterProcess()
        {
            WindowsUtilities.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }

        public void Register()
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/gg281362.aspx
            using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, ProgId)))
            {
                key.SetValue("FriendlyTypeName", FriendlyName);
                key.SetValue("AppUserModelID", AppUserModelId);
            }

            using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, ProgId, "CurVer")))
            {
                key.SetValue(null, ProgId);
            }

            using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, ProgId, "shell", "open", "command")))
            {
                var path = Assembly.GetEntryAssembly().Location;
                key.SetValue(null, "\"" + path + "\" \"%1\"");
            }

            var appName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, "Applications", appName, "shell", "open", "command")))
            {
                var path = Assembly.GetEntryAssembly().Location;
                key.SetValue(null, "\"" + path + "\" \"%1\"");
            }
        }

        public IReadOnlyList<string> GetRegisteredFileExtensions() => GetRegisteredFileExtensionsPrivate().ToList().AsReadOnly();
        private HashSet<string> GetRegisteredFileExtensionsPrivate()
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var key = _hkcu.OpenSubKey(Path.Combine(_basePath, ProgId), false))
            {
                if (key != null)
                {
                    foreach (var ext in Conversions.SplitToList<string>(key.GetValue("FileExtensions") as string, ','))
                    {
                        existing.Add(ext);
                    }
                }
            }
            return existing;
        }

        private void UnregisterFileExtensions()
        {
            var existing = GetRegisteredFileExtensionsPrivate();
            foreach (var ext in existing)
            {
                using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, ext, "OpenWithProgids")))
                {
                    key.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                }
            }
        }

        public void RegisterFileExtensions(IEnumerable<string> extensions)
        {
            if (extensions == null)
                throw new ArgumentNullException(nameof(extensions));

            var existing = GetRegisteredFileExtensionsPrivate();
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (extensions != null)
            {
                all.Add(ApplicationIconProgId);
            }

            var toAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in extensions)
            {
                all.Add(ext);
                if (!existing.Remove(ext))
                {
                    toAdd.Add(ext);
                }
            }

            // remove what's left in existing and not added
            foreach (var ext in existing.Where(e => !e.EqualsIgnoreCase(ApplicationIconProgId)))
            {
                using (var key = _hkcu.OpenSubKey(Path.Combine(_basePath, ext, "OpenWithProgids"), true))
                {
                    key?.DeleteValue(ProgId, false);
                }
            }

            // write all extensions we support
            using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, ProgId)))
            {
                if (all.Count > 0)
                {
                    key.SetValue("FileExtensions", string.Join(",", all));
                }
                else
                {
                    key.DeleteValue("FileExtensions", false);
                }
            }

            foreach (var ext in toAdd)
            {
                using (var key = WindowsUtilities.EnsureSubKey(_hkcu, Path.Combine(_basePath, ext, "OpenWithProgids")))
                {
                    key.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                }
            }
        }

        public void Unregister()
        {
            UnregisterFileExtensions();

            _hkcu.DeleteSubKeyTree(Path.Combine(_basePath, ProgId), false);
            _hkcu.DeleteSubKeyTree(Path.Combine(_basePath, ApplicationIconProgId), false);

            var appName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            _hkcu.DeleteSubKeyTree(Path.Combine(_basePath, "Applications", appName), false);
        }

        public void PublishRecentList()
        {
            var list = (ICustomDestinationList)new DestinationList();
            list.SetAppID(AppUserModelId);
            list.BeginList(out _, typeof(IObjectArray).GUID, out _);
            list.AppendKnownCategory(KNOWNDESTCATEGORY.KDC_RECENT);
            list.CommitList();
        }

        //private static bool Contains(IObjectArray array, IShellItem item)
        //{
        //    array.GetCount(out var count);
        //    for (var i = 0; i < count; i++)
        //    {
        //        array.GetAt(i, typeof(IShellItem).GUID, out var obj);
        //        if (obj is IShellItem compare)
        //        {
        //            const int SICHINT_CANONICAL = 0x10000000;
        //            if (compare.Compare(item, SICHINT_CANONICAL, out _) == 0)
        //                return true;
        //        }
        //    }
        //    return false;
        //}

        //private static IShellItem CreateItemFromParsingName(string path)
        //{
        //    SHCreateItemFromParsingName(path, null, typeof(IShellItem).GUID, out var obj);
        //    return obj as IShellItem;
        //}

        [DllImport("shell32")]
        private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [ComImport, Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")]
        private class DestinationList { }

        [ComImport, Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a")]
        private class EnumerableObjectCollection { }

        private enum KNOWNDESTCATEGORY
        {
            KDC_FREQUENT = 1,
            KDC_RECENT = KDC_FREQUENT + 1
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6332debf-87b5-4670-90c0-5e57b408a49e"), ComImport]
        private interface ICustomDestinationList
        {
            [PreserveSig]
            int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);

            [PreserveSig]
            int BeginList(out int pcMaxSlots, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IObjectArray ppv);

            [PreserveSig]
            int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, IObjectArray poa);

            [PreserveSig]
            int AppendKnownCategory(KNOWNDESTCATEGORY category);

            [PreserveSig]
            int AddUserTasks(IObjectArray poa);

            [PreserveSig]
            int CommitList();

            [PreserveSig]
            int GetRemovedDestinations([MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IObjectArray ppv);

            [PreserveSig]
            int DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);

            [PreserveSig]
            int AbortList();
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9"), ComImport]
        private interface IObjectArray
        {
            [PreserveSig]
            int GetCount(out int count);

            [PreserveSig]
            int GetAt(int uiIndex, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9"), ComImport]
        private interface IObjectCollection : IObjectArray
        {
            [PreserveSig]
            new int GetCount(out int count);

            [PreserveSig]
            new int GetAt(int uiIndex, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

            [PreserveSig]
            int AddObject([MarshalAs(UnmanagedType.IUnknown)] object punk);

            [PreserveSig]
            int AddFromArray(IObjectArray poaSource);

            [PreserveSig]
            int RemoveObjectAt(int uiIndex);

            [PreserveSig]
            int Clear();
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), ComImport]
        private partial interface IShellItem
        {
            [PreserveSig]
            int BindToHandler(IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

            [PreserveSig]
            int GetParent(out IShellItem ppsi);

            [PreserveSig]
            int GetDisplayName(int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

            [PreserveSig]
            int GetAttributes(int sfgaoMask, out int psfgaoAttribs);

            [PreserveSig]
            int Compare(IShellItem psi, int hint, out int piOrder);
        }
    }
}
