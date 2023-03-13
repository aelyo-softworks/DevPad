using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

namespace DevPad.Utilities
{
    public class WindowsApp
    {
        private readonly List<string> _fileExtensions = new List<string>();

        public WindowsApp(string appUserModelId, string friendlyName, string progId = null)
        {
            if (appUserModelId == null)
                throw new ArgumentNullException(nameof(appUserModelId));

            if (friendlyName == null)
                throw new ArgumentNullException(nameof(friendlyName));

            AppUserModelId = appUserModelId;
            ProgId = progId ?? appUserModelId;
            FriendlyName = friendlyName;
            FileExtensions.Add(ApplicationIconProgId);
        }

        public string FriendlyName { get; }
        public string PublisherName { get; set; }
        public string ProgId { get; }
        public string AppUserModelId { get; }
        public IList<string> FileExtensions => _fileExtensions;
        internal string ApplicationIconProgId => ".iconAelyo"; // this is only used to be able to identify the app's icon in the imagelist...

        public void RegisterProcess()
        {
            WindowsUtilities.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }

        public void Register()
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/gg281362.aspx
            var hkcu = Registry.CurrentUser;
            var basePath = @"Software\Classes";

            using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, ProgId)))
            {
                key.SetValue("FriendlyTypeName", FriendlyName);
                key.SetValue("AppUserModelID", AppUserModelId);
                key.SetValue("FileExtensions", string.Join(",", FileExtensions));
            }

            using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, ProgId, "CurVer")))
            {
                key.SetValue(null, ProgId);
            }

            using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, ProgId, "shell", "open", "command")))
            {
                var path = Assembly.GetEntryAssembly().Location;
                key.SetValue(null, "\"" + path + "\" \"%1\"");
            }

            foreach (var ext in FileExtensions)
            {
                using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, ext, "OpenWithProgids")))
                {
                    key.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                }
            }

            var appName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, "Applications", appName, "shell", "open", "command")))
            {
                var path = Assembly.GetEntryAssembly().Location;
                key.SetValue(null, "\"" + path + "\" \"%1\"");
            }
        }

        public void Unregister()
        {
            var appName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            var hkcu = Registry.CurrentUser;
            var basePath = @"Software\Classes";

            // get saved file extensions
            using (var key = hkcu.OpenSubKey(Path.Combine(basePath, ProgId), false))
            {
                if (key != null)
                {
                    foreach (var ext in Conversions.SplitToList<string>(key.GetValue("FileExtensions") as string, ','))
                    {
                        FileExtensions.Add(ext);
                    }
                }
            }

            hkcu.DeleteSubKeyTree(Path.Combine(basePath, ProgId), false);
            hkcu.DeleteSubKeyTree(Path.Combine(basePath, "Applications", appName), false);

            foreach (var ext in FileExtensions)
            {
                using (var key = hkcu.OpenSubKey(Path.Combine(basePath, ext, "OpenWithProgids"), true))
                {
                    key.DeleteValue(ProgId, false);
                }
            }
            hkcu.DeleteSubKeyTree(Path.Combine(basePath, ApplicationIconProgId), false);
        }

        public void PublishRecentList()
        {
            var list = (ICustomDestinationList)new DestinationList();
            list.SetAppID(AppUserModelId);

            list.BeginList(out _, typeof(IObjectArray).GUID, out var removed);
            list.AppendKnownCategory(KNOWNDESTCATEGORY.KDC_RECENT);

            removed.GetCount(out var removedCount);

            var oa = (IObjectCollection)new EnumerableObjectCollection();
            oa.AddObject(CreateItemFromParsingName(@"d:\temp\test2.xml"));
            var x = list.AppendCategory("test", oa);

            list.CommitList();
        }

        private static bool Contains(IObjectArray array, IShellItem item)
        {
            array.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                array.GetAt(i, typeof(IShellItem).GUID, out var obj);
                if (obj is IShellItem compare)
                {
                    const int SICHINT_CANONICAL = 0x10000000;
                    if (compare.Compare(item, SICHINT_CANONICAL, out _) == 0)
                        return true;
                }
            }
            return false;
        }

        private static IShellItem CreateItemFromParsingName(string path)
        {
            SHCreateItemFromParsingName(path, null, typeof(IShellItem).GUID, out var obj);
            return obj as IShellItem;
        }

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
