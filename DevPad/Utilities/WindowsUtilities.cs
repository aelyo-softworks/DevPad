using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using System.ComponentModel;
using System.Collections.Concurrent;

#if SETUP
using Resources = DevPad.Setup.Resources;
#else
using System.Windows.Interop;
#endif

namespace DevPad.Utilities
{
    public static class WindowsUtilities
    {
        public const int ApplicationIcon = 32512;

        private static readonly ConcurrentDictionary<IntPtr, Guid> _desktopIds = new ConcurrentDictionary<IntPtr, Guid>();

        private static readonly Lazy<Process> _currentProcess = new Lazy<Process>(() => Process.GetCurrentProcess(), true);
        public static Process CurrentProcess => _currentProcess.Value;

        private static readonly Lazy<Process> _parentProcess = new Lazy<Process>(() => GetParentProcess(), true);
        public static Process ParentProcess => _parentProcess.Value; // can be null

        private static readonly Lazy<IReadOnlyList<string>> _userLanguages = new Lazy<IReadOnlyList<string>>(GetUserLanguages);
        public static IReadOnlyList<string> UserLanguages => _userLanguages.Value;

        private static readonly Lazy<Version> _kernelVersion = new Lazy<Version>(() =>
        {
            var vi = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));
            return new Version(vi.FileMajorPart, vi.FileMinorPart, vi.FileBuildPart, vi.FilePrivatePart);
        }, true);
        public static Version KernelVersion => _kernelVersion.Value;

        public static Process GetParentProcess() => GetParentProcess(CurrentProcess.Handle);
        public static Process GetParentProcess(int id)
        {
            try
            {
                var process = Process.GetProcessById(id);
                return GetParentProcess((process?.Handle).GetValueOrDefault());
            }
            catch
            {
                // continue
                return null;
            }
        }

        public static Process GetParentProcess(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return null;

            var pbi = new PROCESS_BASIC_INFORMATION();
            var status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
            if (status == 0)
            {
                try
                {
                    return Process.GetProcessById((int)pbi.InheritedFromUniqueProcessId.ToInt64());
                }
                catch
                {
                    // continue
                }
            }
            return null;
        }

        private static IReadOnlyList<string> GetUserLanguages()
        {
            try
            {
                GetUserLanguages('?', out var langs);
                return Conversions.SplitToList<string>(langs, '?').AsReadOnly();
            }
            catch
            {
                const int MUI_LANGUAGE_NAME = 8;
                return GetUserPreferredUILanguages(MUI_LANGUAGE_NAME);
            }
        }

        private static IReadOnlyList<string> GetUserPreferredUILanguages(int flags)
        {
            var list = new List<string>();
            var size = 0;
            if (!GetUserPreferredUILanguages(flags, out _, IntPtr.Zero, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var ptr = Marshal.AllocHGlobal(size * 2);
            try
            {
                if (!GetUserPreferredUILanguages(flags, out var count, ptr, ref size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var current = ptr;
                for (var i = 0; i < count; i++)
                {
                    var str = Marshal.PtrToStringUni(current);
                    list.Add(str);
                    current += (str.Length + 1) * 2;
                }
                return list.AsReadOnly();
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        [DllImport("bcp47langs")]
        private static extern int GetUserLanguages(char delimiter, [MarshalAs(UnmanagedType.HString)] out string userLanguages);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserPreferredUILanguages(int dwFlags, out int pulNumLanguages, IntPtr pwszLanguagesBuffer, ref int pcchLanguagesBuffer);

        [DllImport("ntdll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

        [DllImport("shell32")]
        public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

#if !SETUP
        public static void SHAddToRecentDocs(string filePath) => SHAddToRecentDocs(SHARD.SHARD_PATHW, filePath);

        [DllImport("shell32")]
        public static extern void SHAddToRecentDocs(SHARD uFlags, [MarshalAs(UnmanagedType.LPWStr)] string AppID);
#endif

        [DllImport("user32")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32")]
        public static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("user32")]
        public static extern bool AllowSetForegroundWindow(int processId);

        [DllImport("user32")]
        public static extern IntPtr SetParent(IntPtr handle, IntPtr parentHandle);

        [DllImport("user32")]
        public static extern IntPtr SetFocus(IntPtr handle);

        [DllImport("user32")]
        public static extern IntPtr GetFocus();

        [DllImport("kernel32")]
        public static extern int GetCurrentThreadId();

        [DllImport("user32")]
        private static extern int GetGUIThreadInfo(int threadId, ref GUITHREADINFO info);

        [DllImport("user32")]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("user32", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32")]
        private static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);

        [DllImport("shell32")]
        private static extern int SHOpenWithDialog(IntPtr handle, ref OPENASINFO info);

        [DllImport("user32")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32", SetLastError = true)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, int resourceId);

        [DllImport("user32")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32")]
        private static extern bool GetMonitorInfo(IntPtr hmonitor, ref MONITORINFO info);

        [DllImport("user32")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int flags);

        [DllImport("user32")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32")]
        private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [DllImport("user32")]
        private static extern bool GetClientRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32")]
        private static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref RECT rect, int cPoints);

        [DllImport("uxtheme", CharSet = CharSet.Auto)]
        private static extern int GetCurrentThemeName(StringBuilder pszThemeFileName, int dwMaxNameChars, StringBuilder pszColorBuff, int dwMaxColorChars, StringBuilder pszSizeBuff, int cchMaxSizeChars);

        [DllImport("kernel32")]
        public static extern bool AllocConsole();

        [DllImport("dwmapi")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref bool attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OPENASINFO
        {
            public string cszFile;
            public string cszClass;
            public OPEN_AS_INFO_FLAGS oaifInFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
            public int Width => right - left;
            public int Height => bottom - top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [Flags]
        private enum OPEN_AS_INFO_FLAGS
        {
            OAIF_ALLOW_REGISTRATION = 0x00000001,
            OAIF_REGISTER_EXT = 0x00000002,
            OAIF_EXEC = 0x00000004,
            OAIF_FORCE_REGISTRATION = 0x00000008,
            OAIF_HIDE_REGISTRATION = 0x00000020,
            OAIF_URL_PROTOCOL = 0x00000040,
            OAIF_FILE_IS_URI = 0x00000080,
        }

        private const int GWL_STYLE = -16;
        private const int GW_OWNER = 4;

        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_MINIMIZE = 0x20000000;

        private const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static readonly Lazy<MethodInfo> _decodeMessage = new Lazy<MethodInfo>(() =>
        {
            var type = typeof(Message).Assembly.GetType("System.Windows.Forms.MessageDecoder", true);
            return type?.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
        }, true);

        public static string DecodeMessage(IntPtr handle, int msg, IntPtr wParam, IntPtr lParam)
        {
            var message = new Message();
            message.HWnd = handle;
            message.LParam = lParam;
            message.WParam = wParam;
            message.Msg = msg;
            return Decode(message);
        }

        public static string Decode(this Message message) => _decodeMessage.Value?.Invoke(message, null) as string;
        public static bool AttachThreadInput(int idAttach, int idAttachTo) => AttachThreadInput(idAttach, idAttachTo, true);
        public static bool DetachThreadInput(int idAttach, int idAttachTo) => AttachThreadInput(idAttach, idAttachTo, false);
        public static int GetWindowThreadId(IntPtr handle) => GetWindowThreadProcessId(handle, out _);

        public static string GetCurrentThemeFilePath()
        {
            var sb = new StringBuilder(512);
            GetCurrentThemeName(sb, sb.Capacity, null, 0, null, 0);
            return sb.ToString();
        }

        public static int GetWindowProcessId(IntPtr handle)
        {
            GetWindowThreadProcessId(handle, out int processId);
            return processId;
        }

        public static IntPtr GetThreadActiveWindow(int threadId)
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            GetGUIThreadInfo(threadId, ref info);
            return info.hwndActive;
        }

        public static IntPtr GetThreadCaptureWindow(int threadId)
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            GetGUIThreadInfo(threadId, ref info);
            return info.hwndCapture;
        }

        public static IntPtr GetThreadCaretWindow(int threadId)
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            GetGUIThreadInfo(threadId, ref info);
            return info.hwndCaret;
        }

        public static IntPtr GetThreadFocusWindow(int threadId)
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            GetGUIThreadInfo(threadId, ref info);
            return info.hwndFocus;
        }

        public static IntPtr GetThreadMenuOwnerWindow(int threadId)
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            GetGUIThreadInfo(threadId, ref info);
            return info.hwndMenuOwner;
        }

        public static IntPtr GetThreadMoveSizeWindow(int threadId)
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            GetGUIThreadInfo(threadId, ref info);
            return info.hwndMoveSize;
        }

        public static string GetWindowText(IntPtr handle)
        {
            int len = GetWindowTextLength(handle);
            var sb = new StringBuilder(len + 2);
            GetWindowText(handle, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string GetWindowClass(IntPtr handle)
        {
            var sb = new StringBuilder(260);
            GetClassName(handle, sb, sb.Capacity);
            return sb.ToString();
        }

        public static void CenterWindow(IntPtr handle) => CenterWindow(handle, IntPtr.Zero);
        public static void CenterWindow(IntPtr handle, IntPtr alternateOwner)
        {
            if (handle == IntPtr.Zero)
                throw new ArgumentException(null, nameof(handle));

            // determine owner window to center against
            int dwStyle = GetWindowLong(handle, GWL_STYLE);
            IntPtr hWndCenter = alternateOwner;
            if (alternateOwner == IntPtr.Zero)
            {
                if ((dwStyle & WS_CHILD) == WS_CHILD)
                {
                    hWndCenter = GetParent(handle);
                }
                else
                {
                    hWndCenter = GetWindow(handle, GW_OWNER);
                }
            }

            // get coordinates of the window relative to its parent
            var rcDlg = new RECT();
            GetWindowRect(handle, ref rcDlg);
            var rcArea = new RECT();
            var rcCenter = new RECT();
            if ((dwStyle & WS_CHILD) != WS_CHILD)
            {
                // don't center against invisible or minimized windows
                if (hWndCenter != IntPtr.Zero)
                {
                    int dwAlternateStyle = GetWindowLong(hWndCenter, GWL_STYLE);
                    if ((dwAlternateStyle & WS_VISIBLE) != WS_VISIBLE || (dwAlternateStyle & WS_MINIMIZE) == WS_VISIBLE)
                    {
                        hWndCenter = IntPtr.Zero;
                    }
                }

                var mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                // center within appropriate monitor coordinates
                if (hWndCenter == IntPtr.Zero)
                {
                    IntPtr hwDefault = GetActiveWindow();
                    GetMonitorInfo(MonitorFromWindow(hwDefault, MONITOR_DEFAULTTOPRIMARY), ref mi);
                    rcCenter = mi.rcWork;
                    rcArea = mi.rcWork;
                }
                else
                {
                    GetWindowRect(hWndCenter, ref rcCenter);
                    GetMonitorInfo(MonitorFromWindow(hWndCenter, MONITOR_DEFAULTTONEAREST), ref mi);
                    rcArea = mi.rcWork;
                }
            }
            else
            {
                // center within parent client coordinates
                IntPtr hWndParent = GetParent(handle);
                GetClientRect(hWndParent, ref rcArea);
                GetClientRect(hWndCenter, ref rcCenter);
                MapWindowPoints(hWndCenter, hWndParent, ref rcCenter, 2);
            }

            // find dialog's upper left based on rcCenter
            int xLeft = (rcCenter.left + rcCenter.right) / 2 - rcDlg.Width / 2;
            int yTop = (rcCenter.top + rcCenter.bottom) / 2 - rcDlg.Height / 2;

            // if the dialog is outside the screen, move it inside
            if (xLeft + rcDlg.Width > rcArea.right)
            {
                xLeft = rcArea.right - rcDlg.Width;
            }

            if (xLeft < rcArea.left)
            {
                xLeft = rcArea.left;
            }

            if (yTop + rcDlg.Height > rcArea.bottom)
            {
                yTop = rcArea.bottom - rcDlg.Height;
            }

            if (yTop < rcArea.top)
            {
                yTop = rcArea.top;
            }

            // map screen coordinates to child coordinates
            SetWindowPos(handle, IntPtr.Zero, xLeft, yTop, -1, -1, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static DialogResult RunForm(Form form, IntPtr ownerHandle)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            form.Shown += (sender, e) =>
            {
                CenterWindow(form.Handle, ownerHandle);
                // we should have been given the SetForegroundWindow right by Explorer (that should have called AllowSetForegroundWindow)
                SetForegroundWindow(form.Handle);
            };

            System.Windows.Forms.Application.Run(form);
            return form.DialogResult;
        }

        public static Task<DialogResult> ShowModelessAsync(Form form) => ShowModelessAsync(form, IntPtr.Zero);
        public static Task<DialogResult> ShowModelessAsync(Form form, IntPtr ownerHandle)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            return DoModelessAsync(() => RunForm(form, ownerHandle));
        }

        public static Task<T> DoModelessAsync<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var tcs = new TaskCompletionSource<T>();
            var thread = new Thread(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        public static Task DoModelessAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var tcs = new TaskCompletionSource<int>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(0);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        public static DialogResult ShowModal(Form form) => ShowModal(form, IntPtr.Zero);
        public static DialogResult ShowModal(Form form, IntPtr ownerHandle)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            return DoModalUI(() => RunForm(form, ownerHandle));
        }

        public static T DoModalUI<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var result = default(T);
            var thread = new Thread((state) =>
            {
                result = func();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();
            return result;
        }

        public static void DoModalUI(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var thread = new Thread((state) =>
            {
                action();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();
        }

        public static IntPtr ActivateModalWindow(IntPtr hwnd) => ActivateWindow(GetModalWindow(hwnd));
        public static IntPtr ActivateWindow(IntPtr hwnd) => ModalWindowUtil.ActivateWindow(hwnd);
        public static IntPtr GetModalWindow(IntPtr owner) => ModalWindowUtil.GetModalWindow(owner);

        private class ModalWindowUtil
        {
            private int _maxOwnershipLevel;
            private IntPtr _maxOwnershipHandle;

            private delegate bool EnumChildrenCallback(IntPtr hwnd, IntPtr lParam);

            [DllImport("user32")]
            private static extern bool EnumThreadWindows(int dwThreadId, EnumChildrenCallback lpEnumFunc, IntPtr lParam);

            private bool EnumChildren(IntPtr hwnd, IntPtr lParam)
            {
                int level = 1;
                if (IsWindowVisible(hwnd) && IsOwned(lParam, hwnd, ref level))
                {
                    if (level > _maxOwnershipLevel)
                    {
                        _maxOwnershipHandle = hwnd;
                        _maxOwnershipLevel = level;
                    }
                }
                return true;
            }

            private static bool IsOwned(IntPtr owner, IntPtr hwnd, ref int level)
            {
                var ownerHandle = GetWindow(hwnd, GW_OWNER);
                if (ownerHandle == IntPtr.Zero)
                    return false;

                if (ownerHandle == owner)
                    return true;

                level++;
                return IsOwned(owner, ownerHandle, ref level);
            }

            public static IntPtr ActivateWindow(IntPtr hwnd)
            {
                if (hwnd == IntPtr.Zero)
                    return IntPtr.Zero;

                return SetActiveWindow(hwnd);
            }

            public static IntPtr GetModalWindow(IntPtr owner)
            {
                var util = new ModalWindowUtil();
                EnumThreadWindows(GetCurrentThreadId(), util.EnumChildren, owner);
                return util._maxOwnershipHandle; // may be IntPtr.Zero
            }
        }

        public static void OpenFile(string fileName)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        public static void OpenUrl(Uri uri) => OpenUrl(uri?.ToString());
        public static void OpenUrl(string url)
        {
            if (url == null)
                return;

            Process.Start(url);
        }

        public static void OpenExplorer(string directoryPath)
        {
            if (directoryPath == null)
                return;

            if (!IOUtilities.PathIsDirectory(directoryPath))
                return;

            // see http://support.microsoft.com/kb/152457/en-us
#pragma warning disable S4036
            Process.Start("explorer.exe", "/e,/root,/select," + directoryPath);
#pragma warning restore S4036
        }

        public static int OpenWith(string filePath) => OpenWith(IntPtr.Zero, filePath);
        public static int OpenWith(IntPtr hwnd, string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (Environment.OSVersion.Version.Major > 5)
            {
                var info = new OPENASINFO();
                info.cszFile = filePath;
                info.oaifInFlags = OPEN_AS_INFO_FLAGS.OAIF_HIDE_REGISTRATION | OPEN_AS_INFO_FLAGS.OAIF_EXEC;
                return SHOpenWithDialog(hwnd, ref info);
            }

            Process.Start("rundll32", "shell32.dll,OpenAs_RunDLL " + filePath);
            return 0;
        }

        public static RegistryKey EnsureSubKey(RegistryKey root, string name)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            var key = root.OpenSubKey(name, true);
            if (key != null)
                return key;

            var parentName = Path.GetDirectoryName(name);
            if (string.IsNullOrEmpty(parentName))
                return root.CreateSubKey(name);

            using (var parentKey = EnsureSubKey(root, parentName))
            {
                return parentKey.CreateSubKey(Path.GetFileName(name));
            }
        }

        public static void SetGpuRendering(bool enable) => SetBrowserFeature("FEATURE_GPU_RENDERING", enable);

        public static void SetBrowserFeature(string name, bool value) => SetBrowserFeature(name, value ? 1 : 0);
        public static void SetBrowserFeature(string name, int value)
        {
            // https://docs.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/general-info/ee330730(v=vs.85)#browser_emulation
            using (var reg = EnsureSubKey(Registry.CurrentUser, Path.Combine(@"Software\Microsoft\Internet Explorer\Main\FeatureControl", name)))
            {
                var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
                reg.SetValue(exeName, value);
            }
        }

        public static bool IsAdministrator
        {
            get
            {
                var identity = WindowsIdentity.GetCurrent();
                return identity != null && new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool RestartAsAdmin(bool force)
        {
            if (!force && IsAdministrator)
                return false;

            var info = new ProcessStartInfo();
            info.FileName = Environment.GetCommandLineArgs()[0];
            info.UseShellExecute = true;
            info.Verb = "runas"; // Provides Run as Administrator

            return Process.Start(info) != null;
        }

        public static bool SetDarkMode(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                throw new ArgumentException(null, nameof(handle));

            var set = true;
            var size = Marshal.SizeOf<bool>();
            if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref set, size) >= 0)
                return true;

            return DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref set, size) >= 0;
        }

#if SETUP
#else
        public static string GetWindowDesktopName(Window window)
        {
            if (window == null)
                return null;

            var id = GetWindowDesktopId(new WindowInteropHelper(window).Handle);
            return GetWindowDesktopName(id);
        }
#endif

        public static string GetWindowDesktopName(Guid id) => GetWindowDesktops().FirstOrDefault(d => d.Item1 == id)?.Item2 ?? id.ToString();
        public static IReadOnlyList<Tuple<Guid, string>> GetWindowDesktops()
        {
            var list = new List<Tuple<Guid, string>>();
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
                            list.Add(new Tuple<Guid, string>(guid, name));
                            offset += guidLength;
                        }
                        while (true);
                    }
                }
            }
            return list;
        }

        public static Guid GetDesktopId()
        {
            using (var form = new NoActivateForm())
            {
                form.Show();
                return GetWindowDesktopId(form.Handle);
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

        public static Guid GetWindowDesktopId(IntPtr handle)
        {
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

    public enum SHARD
    {
        SHARD_PIDL = 1,
        SHARD_PATHA = 2,
        SHARD_PATHW = 3,
        SHARD_APPIDINFO = 4,
        SHARD_APPIDINFOIDLIST = 5,
        SHARD_LINK = 6,
        SHARD_APPIDINFOLINK = 7,
        SHARD_SHELLITEM = 8
    }
}
