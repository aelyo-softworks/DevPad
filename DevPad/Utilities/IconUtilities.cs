using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DevPad.Utilities.Grid;

namespace DevPad.Utilities
{
    public static class IconUtilities
    {
        public static ImageSource GetStockIconImageSource(StockIconId id, SHGSI flags = SHGSI.SHGSI_ICON | SHGSI.SHGSI_SMALLICON)
        {
            var hicon = GetStockIconHandle(id, flags);
            if (hicon == IntPtr.Zero)
                return null;

            var image = Imaging.CreateBitmapSourceFromHIcon(hicon, new Int32Rect(0, 0, 16, 16), BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(hicon);
            return image;
        }

        public static IntPtr GetStockIconHandle(StockIconId id, SHGSI flags = SHGSI.SHGSI_ICON | SHGSI.SHGSI_SMALLICON)
        {
            var info = new SHSTOCKICONINFO();
            info.cbSize = Marshal.SizeOf<SHSTOCKICONINFO>();
            SHGetStockIconInfo(id, flags, ref info);
            return info.hIcon;
        }

        public static ImageSource GetExtensionIconAsImageSource(string ext, SHIL shil)
        {
            if (ext == null)
                throw new ArgumentNullException(nameof(ext));

            var ctx = CreateBindCtx(ext, STGM.STGM_CREATE);
            if (ctx == null)
                return null;

            var item = GetItemFromParsingName(ext, ctx);
            if (item == null)
                return null;

            return GetItemIconAsImageSource(item, shil);
        }

        public static IntPtr GetExtensionIconHandle(string ext, SHIL shil)
        {
            if (ext == null)
                throw new ArgumentNullException(nameof(ext));

            var ctx = CreateBindCtx(ext, STGM.STGM_CREATE);
            if (ctx == null)
                return IntPtr.Zero;

            var item = GetItemFromParsingName(ext, ctx);
            if (item == null)
                return IntPtr.Zero;

            return GetItemIconHandle(item, shil);
        }

        public static ImageSource GetItemIconAsImageSource(string path, SHIL shil)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var item = GetItemFromParsingName(path);
            if (item == null)
                return null;

            return GetItemIconAsImageSource(item, shil);
        }

        public static IntPtr GetItemIconHandle(string path, SHIL shil)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var item = GetItemFromParsingName(path);
            if (item == null)
                return IntPtr.Zero;

            return GetItemIconHandle(item, shil);
        }

        private static ImageSource GetItemIconAsImageSource(IShellItem item, SHIL shil)
        {
            var hicon = GetItemIconHandle(item, shil);
            if (hicon == IntPtr.Zero)
                return null;

            int size;
            switch (shil)
            {
                case SHIL.SHIL_JUMBO:
                    size = 256;
                    break;

                case SHIL.SHIL_EXTRALARGE:
                    size = 48;
                    break;

                case SHIL.SHIL_LARGE:
                    size = 32;
                    break;

                default:
                    size = 16;
                    break;
            }

            var image = Imaging.CreateBitmapSourceFromHIcon(hicon, new Int32Rect(0, 0, size, size), BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(hicon);
            return image;
        }

        private static IntPtr GetItemIconHandle(IShellItem item, SHIL shil)
        {
            if (!(item is IParentAndItem pai))
                return IntPtr.Zero;

            if (pai.GetParentAndItem(IntPtr.Zero, out var psf, out var child) != 0)
                return IntPtr.Zero;

            var index = SHMapPIDLToSystemImageListIndex(psf, child, out _);
            Marshal.FreeCoTaskMem(child);

            var list = GetImageList(shil);
            if (list == null)
                return IntPtr.Zero;

            list.GetIcon(index, 0, out var hicon);
            return hicon;
        }

        private static IShellItem GetItemFromParsingName(string name, IBindCtx context = null)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            SHCreateItemFromParsingName(name, context, typeof(IShellItem).GUID, out var obj);
            return obj as IShellItem;
        }

        private static IImageList GetImageList(SHIL shil)
        {
            SHGetImageList(shil, typeof(IImageList).GUID, out var obj);
            return obj as IImageList;
        }

        private static IBindCtx CreateBindCtx(string name, STGM? mode = null, long? length = null, FileAttributes? attributes = null, DateTime? lastWriteTime = null, DateTime? creationTime = null, DateTime? lastAccessTime = null)
        {
            var data = new WIN32_FIND_DATA();
            data.cFileName = Path.GetFileName(name);
            if (length.HasValue)
            {
                data.fileSizeHigh = (uint)(length.Value >> 32);
                data.fileSizeLow = (uint)(length.Value & 0xFFFFFFFF);
            }

            if (attributes.HasValue)
            {
                data.fileAttributes = attributes.Value;
            }

            if (creationTime.HasValue)
            {
                var ft = Conversions.ToPositiveFileTime(creationTime.Value);
                data.ftCreationTimeHigh = (uint)(ft >> 32);
                data.ftCreationTimeHigh = (uint)(ft & 0xFFFFFFFF);
            }

            if (lastWriteTime.HasValue)
            {
                var ft = Conversions.ToPositiveFileTime(lastWriteTime.Value);
                data.ftLastWriteTimeHigh = (uint)(ft >> 32);
                data.ftLastWriteTimeLow = (uint)(ft & 0xFFFFFFFF);
            }

            if (lastAccessTime.HasValue)
            {
                var ft = Conversions.ToPositiveFileTime(lastAccessTime.Value);
                data.ftLastAccessTimeHigh = (uint)(ft >> 32);
                data.ftLastAccessTimeLow = (uint)(ft & 0xFFFFFFFF);
            }

            return CreateBindCtx(ref data, mode);
        }

        private static IBindCtx CreateBindCtx(ref WIN32_FIND_DATA data, STGM? mode = null)
        {
            var bindData = new FileSystemBindData2();
            bindData.SetFindData(ref data);
            var ctx = CreateBindCtx(mode);
            if (ctx != null)
            {
                try
                {
                    ctx.RegisterObjectParam(STR_FILE_SYS_BIND_DATA, bindData);
                }
                catch
                {
                    //+ do nothing
                }
            }
            return ctx;
        }

        private static IBindCtx CreateBindCtx(STGM? mode = null)
        {
            var hr = CreateBindCtx(0, out var ctx);
            if (hr != 0)
                return null;

            if (mode.HasValue)
            {
                var opts = new System.Runtime.InteropServices.ComTypes.BIND_OPTS();
                opts.cbStruct = Marshal.SizeOf<System.Runtime.InteropServices.ComTypes.BIND_OPTS>();
                opts.grfMode = (int)mode.Value;
                try
                {
                    ctx.SetBindOptions(ref opts);
                }
                catch
                {
                    //+ do nothing
                }
            }
            return ctx;
        }

        private class FileSystemBindData2 : IFileSystemBindData2
        {
            // https://github.com/microsoft/Windows-classic-samples/blob/master/Samples/Win7Samples/winui/shell/appplatform/ParsingWithParameters/ParsingWithParameters.cpp
            private static readonly Guid CLSID_UnknownJunction = new Guid("fc0a77e6-9d70-4258-9783-6dab1d0fe31e");

            private WIN32_FIND_DATA _fd;
            private ulong _fileId;
            private Guid _clsidJunction;

            public int SetFindData(ref WIN32_FIND_DATA pfd)
            {
                _fd = pfd;
                return 0;
            }

            public int GetFindData(ref WIN32_FIND_DATA pfd)
            {
                pfd = _fd;
                return 0;
            }

            public int SetFileID(ulong liFileID)
            {
                _fileId = liFileID;
                return 0;
            }

            public int GetFileID(out ulong pliFileID)
            {
                pliFileID = _fileId;
                return 0;
            }

            public int SetJunctionCLSID(Guid clsid)
            {
                _clsidJunction = clsid;
                return 0;
            }

            public int GetJunctionCLSID(out Guid pclsid)
            {
                if (_clsidJunction == CLSID_UnknownJunction)
                {
                    pclsid = Guid.Empty;
                    return E_FAIL;
                }

                pclsid = _clsidJunction;
                return 0;
            }
        }

        [Flags]
        private enum STGM
        {
            STGM_DIRECT = 0x00000000,
            STGM_TRANSACTED = 0x00010000,
            STGM_SIMPLE = 0x08000000,
            STGM_READ = 0x00000000,
            STGM_WRITE = 0x00000001,
            STGM_READWRITE = 0x00000002,
            STGM_SHARE_DENY_NONE = 0x00000040,
            STGM_SHARE_DENY_READ = 0x00000030,
            STGM_SHARE_DENY_WRITE = 0x00000020,
            STGM_SHARE_EXCLUSIVE = 0x00000010,
            STGM_PRIORITY = 0x00040000,
            STGM_DELETEONRELEASE = 0x04000000,
            STGM_NOSCRATCH = 0x00100000,
            STGM_CREATE = 0x00001000,
            STGM_CONVERT = 0x00020000,
            STGM_FAILIFTHERE = 0x00000000,
            STGM_NOSNAPSHOT = 0x00200000,
            STGM_DIRECT_SWMR = 0x00400000,
        }

        [Flags]
        private enum ILD
        {
            ILD_NORMAL = 0x00000000,
            ILD_TRANSPARENT = 0x00000001,
            ILD_MASK = 0x00000010,
            ILD_IMAGE = 0x00000020,
            ILD_ROP = 0x00000040,
            ILD_BLEND25 = 0x00000002,
            ILD_BLEND50 = 0x00000004,
            ILD_OVERLAYMASK = 0x00000F00,
            ILD_PRESERVEALPHA = 0x00001000,
            ILD_SCALE = 0x00002000,
            ILD_DPISCALE = 0x00004000,
            ILD_ASYNC = 0x00008000,
            ILD_SELECTED = ILD_BLEND50,
            ILD_FOCUS = ILD_BLEND25,
            ILD_BLEND = ILD_BLEND50,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public int cbSize;
            public IntPtr hIcon;
            public int iSysIconIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATA
        {
            public FileAttributes fileAttributes;
            public uint ftCreationTimeLow;
            public uint ftCreationTimeHigh;
            public uint ftLastAccessTimeLow;
            public uint ftLastAccessTimeHigh;
            public uint ftLastWriteTimeLow;
            public uint ftLastWriteTimeHigh;
            public uint fileSizeHigh;
            public uint fileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        private const string STR_FILE_SYS_BIND_DATA = "File System Bind Data";
        private const int E_FAIL = unchecked((int)0x80004005);

        [DllImport("user32")]
        public static extern bool DestroyIcon(IntPtr handle);

        [DllImport("shell32")]
        private static extern int SHGetImageList(SHIL iImageList, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [DllImport("shell32")]
        private static extern int SHMapPIDLToSystemImageListIndex(IntPtr pshf, IntPtr pidl, out int piIndexSel);

        [DllImport("shell32")]
        private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [DllImport("ole32")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("Shell32")]
        private static extern int SHGetStockIconInfo(StockIconId siid, SHGSI uFlags, ref SHSTOCKICONINFO psii);

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private partial interface IShellItem
        {
        }

        [ComImport, Guid("b3a4b685-b685-4805-99d9-5dead2873236"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private partial interface IParentAndItem
        {
            [PreserveSig]
            int SetParentAndItem(IntPtr pidlParent, IntPtr psf, IntPtr pidlChild);

            [PreserveSig]
            int GetParentAndItem(IntPtr ppidlParent, out IntPtr ppsf, out IntPtr ppidlChild);
        }

        [ComImport, Guid("46eb5926-582e-4017-9fdf-e8998daa0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private partial interface IImageList
        {
#pragma warning disable IDE1006 // Naming Styles
            void _VtblGap0_7(); // this name is special (hardcoded)
#pragma warning restore IDE1006 // Naming Styles

            [PreserveSig]
            int GetIcon(int i, ILD flags, out IntPtr picon);
        }

        [ComImport, Guid("01E18D10-4D8B-11d2-855D-006008059367"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileSystemBindData
        {
            [PreserveSig]
            int SetFindData(ref WIN32_FIND_DATA pfd);

            [PreserveSig]
            int GetFindData(ref WIN32_FIND_DATA pfd);
        }

        [ComImport, Guid("3acf075f-71db-4afa-81f0-3fc4fdf2a5b8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileSystemBindData2 : IFileSystemBindData
        {
            // IFileSystemBindData
            [PreserveSig]
            new int SetFindData(ref WIN32_FIND_DATA pfd);

            [PreserveSig]
            new int GetFindData(ref WIN32_FIND_DATA pfd);

            // IFileSystemBindData2
            [PreserveSig]
            int SetFileID(ulong liFileID);

            [PreserveSig]
            int GetFileID(out ulong pliFileID);

            [PreserveSig]
            int SetJunctionCLSID([MarshalAs(UnmanagedType.LPStruct)] Guid clsid);

            [PreserveSig]
            int GetJunctionCLSID(out Guid pclsid);
        }
    }

    [Flags]
    public enum SHGSI
    {
        SHGSI_ICON = 0x100,
        SHGSI_ICONLOCATION = 0,
        SHGSI_LARGEICON = 0,
        SHGSI_LINKOVERLAY = 0x8000,
        SHGSI_SELECTED = 0x10000,
        SHGSI_SHELLICONSIZE = 4,
        SHGSI_SMALLICON = 1,
        SHGSI_SYSICONINDEX = 0x4000
    }

    public enum SHIL
    {
        SHIL_LARGE = 0,
        SHIL_SMALL = 1,
        SHIL_EXTRALARGE = 2,
        SHIL_SYSSMALL = 3,
        SHIL_JUMBO = 4,
    }
}
