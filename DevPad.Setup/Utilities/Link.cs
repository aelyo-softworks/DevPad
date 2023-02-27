using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DevPad.Setup.Utilities
{
    public class Link
    {
        public virtual string Description { get; set; }
        public virtual string Path { get; set; }
        public virtual string RelativePath { get; set; }
        public virtual string WorkingDirectory { get; set; }
        public virtual string Arguments { get; set; }
        public virtual string IconLocation { get; set; }
        public virtual int IconIndex { get; set; }
        public virtual short? HotKey { get; set; }
        public virtual int? ShowCmd { get; set; }
        public virtual IntPtr IDList { get; set; }

        public virtual void Save(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var link = (IShellLink)new ShellLink();
            if (Description != null)
            {
                link.SetDescription(Description);
            }

            if (Path != null)
            {
                link.SetPath(Path);
            }

            if (RelativePath != null)
            {
                link.SetRelativePath(RelativePath, 0);
            }

            if (WorkingDirectory != null)
            {
                link.SetWorkingDirectory(WorkingDirectory);
            }

            if (Arguments != null)
            {
                link.SetArguments(Arguments);
            }

            if (IconLocation != null)
            {
                link.SetIconLocation(IconLocation, IconIndex);
            }

            if (HotKey.HasValue)
            {
                link.SetHotkey(HotKey.Value);
            }

            if (ShowCmd.HasValue)
            {
                link.SetShowCmd(ShowCmd.Value);
            }

            if (IDList != IntPtr.Zero)
            {
                link.SetIDList(IDList);
            }

            var file = (IPersistFile)link;
            file.Save(filePath, false);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
