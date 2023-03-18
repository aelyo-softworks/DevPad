using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DevPad.Utilities;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace DevPad.Ipc
{
    public static partial class RunningObjectTable
    {
        public static int Register(CommandTarget commandTarget, ROTFLAGS flags = ROTFLAGS.ROTFLAGS_REGISTRATIONKEEPSALIVE, bool throwOnError = true)
        {
            var hr = GetRunningObjectTable(0, out var table);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return hr;
            }

            var moniker = commandTarget.Moniker + ":" + WindowsUtilities.CurrentProcess.Id;
            hr = CreateItemMoniker(CommandTarget.Delimiter, moniker, out var mk);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return hr;
            }

            hr = table.Register(flags, commandTarget, mk, out var cookie);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return hr;
            }

            return cookie;
        }

        public static int Revoke(int cookie, bool throwOnError = true)
        {
            var hr = GetRunningObjectTable(0, out var table);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return hr;
            }

            return table.Revoke(cookie);
        }

        public static object GetObject(string itemName, string delimiter = null, bool throwOnError = true)
        {
            if (itemName == null)
                throw new ArgumentNullException(nameof(itemName));

            delimiter = delimiter.Nullify() ?? "!";
            var hr = CreateItemMoniker(delimiter, itemName, out var mk);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return null;
            }
            if (mk == null)
                return null;

            hr = GetRunningObjectTable(0, out var table);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return null;
            }
            if (table == null)
                return null;

            hr = table.GetObject(mk, out var obj);
            if (throwOnError) Marshal.ThrowExceptionForHR(hr);
            return obj;
        }

        public static object GetObject(IMoniker moniker, bool throwOnError = true)
        {
            if (moniker == null)
                throw new ArgumentNullException(nameof(moniker));

            var hr = GetRunningObjectTable(0, out var table);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                return null;
            }
            if (table == null)
                return null;

            hr = table.GetObject(moniker, out var obj);
            if (throwOnError) Marshal.ThrowExceptionForHR(hr);
            return obj;
        }

        public static IEnumerable<IMoniker> Enumerate(bool throwOnError = true)
        {
            var hr = GetRunningObjectTable(0, out var table);
            if (hr < 0)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                yield break;
            }

            hr = table.EnumRunning(out var enumerator);
            if (hr < 0 || enumerator == null)
            {
                if (throwOnError) Marshal.ThrowExceptionForHR(hr);
                yield break;
            }

            var mks = new IMoniker[1];
            do
            {
                if (enumerator.Next(1, mks, IntPtr.Zero) != 0)
                    break;

                var mk = mks[0];
                if (mk != null)
                    yield return mk;
            }
            while (true);
        }

        [DllImport("ole32")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

        [DllImport("ole32", CharSet = CharSet.Auto)]
        private static extern int CreateItemMoniker(string lpszDelim, string lpszItem, out IMoniker ppmk);

        [ComImport, Guid("00000010-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IRunningObjectTable
        {
            [PreserveSig]
            int Register(ROTFLAGS grfFlags, [MarshalAs(UnmanagedType.Interface)] object punkObject, IMoniker pmkObjectName, out int pdwRegister);

            [PreserveSig]
            int Revoke(int dwRegister);

            [PreserveSig]
            int IsRunning(IMoniker pmkObjectName);

            [PreserveSig]
            int GetObject(IMoniker pmkObjectName, [MarshalAs(UnmanagedType.Interface)] out object ppunkObject);

            [PreserveSig]
            int NoteChangeTime(int dwRegister, ref FILETIME pfiletime);

            [PreserveSig]
            int GetTimeOfLastChange(IMoniker pmkObjectName, out FILETIME pfiletime);

            [PreserveSig]
            int EnumRunning(out IEnumMoniker ppenumMoniker);
        }
    }
}
