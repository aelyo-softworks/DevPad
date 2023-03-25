using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace DevPad.Utilities
{
    public static class SystemUtilities
    {
        private static readonly Lazy<string> _virtualMachineType = new Lazy<string>(GetVirtualMachineType, true);
        public static string VirtualMachineType => _virtualMachineType.Value;

        private static readonly Lazy<ulong> _totalPhysicalMemory = new Lazy<ulong>(() =>
        {
            var status = new MEMORYSTATUSEX();
            status.dwLength = Marshal.SizeOf<MEMORYSTATUSEX>();
            _ = GlobalMemoryStatusEx(ref status);
            return status.ullTotalPhys;
        }, true);
        public static ulong TotalPhysicalMemory => _totalPhysicalMemory.Value;

        private static readonly Lazy<bool> _isRemoteSession = new Lazy<bool>(GetIsRemoteSession, true);
        public static bool IsRemoteSession => _isRemoteSession.Value;

        private static readonly Lazy<int?> _sessionId = new Lazy<int?>(() => ProcessIdToSessionId(WindowsUtilities.CurrentProcess.Id), true);
        public static int? SessionId => _sessionId.Value;

        private static readonly Lazy<string> _computerModel = new Lazy<string>(() => GetMsiInfo("Win32_ComputerSystem", "Model"), true);
        public static string ComputerModel => _computerModel.Value;

        private static readonly Lazy<string> _computerManufacturer = new Lazy<string>(() => GetMsiInfo("Win32_ComputerSystem", "Manufacturer"), true);
        public static string ComputerManufacturer => _computerManufacturer.Value;

        private static readonly Lazy<string> _baseBoardProduct = new Lazy<string>(() => GetMsiInfo("Win32_BaseBoard", "Product"), true);
        public static string BaseBoardProduct => _baseBoardProduct.Value;

        private static readonly Lazy<string> _baseBoardSerialNumber = new Lazy<string>(() => GetMsiInfo("Win32_BaseBoard", "SerialNumber"), true);
        public static string BaseBoardSerialNumber => _baseBoardSerialNumber.Value;

        private static readonly Lazy<string> _processor = new Lazy<string>(() => GetMsiInfo("Win32_Processor", "Name"), true);
        public static string Processor => _processor.Value;

        private static readonly Lazy<string> _screens = new Lazy<string>(GetScreens, true);
        public static string Screens => _screens.Value;

        public static string GetAvailableBrowserVersionString(string browserExecutableFolder = null)
        {
            try
            {
                return CoreWebView2Environment.GetAvailableBrowserVersionString(browserExecutableFolder);
            }
            catch
            {
                return null;
            }
        }

        public static int? ProcessIdToSessionId(int processId)
        {
            if (ProcessIdToSessionId(processId, out var id))
                return id;

            return null;
        }

        public static int GetMemoryLoadPercent()
        {
            var status = new MEMORYSTATUSEX
            {
                dwLength = Marshal.SizeOf<MEMORYSTATUSEX>()
            };
            _ = GlobalMemoryStatusEx(ref status);
            return status.dwMemoryLoad;
        }

        public static TokenElevationType GetTokenElevationType()
        {
            var type = TokenElevationType.Unknown;
            int size = IntPtr.Size;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out IntPtr handle))
                return type;

            try
            {
                GetTokenInformation(handle, TokenElevationTypeInformation, out type, size, out int returnLength);
                return type;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static string GetScreens()
        {
            var sb = new StringBuilder();
            DISPLAYCONFIG_PATH_INFO[] paths = null;
            try
            {
                paths = DisplayConfig.Query();
            }
            catch
            {
                // may fail on things like azure apps, etc.
                // continue
            }

            if (paths == null)
            {
                foreach (var screen in Screen.AllScreens)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(" | ");
                    }

                    sb.Append(screen.DeviceName.Replace(@"\\.\", string.Empty));
                    sb.Append(" (");
                    sb.Append("primary: " + screen.Primary);
                    sb.Append(", ");
                    sb.Append("bpp:" + screen.BitsPerPixel);
                    sb.Append(", ");
                    sb.Append("bounds:" + screen.Bounds);
                    sb.Append(", ");
                    sb.Append("area:" + screen.WorkingArea);
                    sb.Append(')');
                }
            }
            else
            {
                foreach (var path in paths)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(" | ");
                    }

                    try
                    {
                        var src = DisplayConfig.GetDeviceInfoSourceName(path);
                        var target = DisplayConfig.GetDeviceInfoTargetName(path);
                        var dd = DISPLAY_DEVICE.All.FirstOrDefault(d => d.DeviceName == src.viewGdiDeviceName);

                        sb.Append(src.viewGdiDeviceName.Replace(@"\\.\", string.Empty));
                        if (!string.IsNullOrWhiteSpace(dd.DeviceString))
                        {
                            sb.Append(" (");
                            sb.Append(dd.DeviceString);
                            sb.Append(", ");
                            sb.Append(dd.CurrentSettings);
                            sb.Append(", ");
                            sb.Append(dd.StateFlags.ToString().Replace("DISPLAY_DEVICE_", string.Empty));
                            sb.Append(')');
                        }

                        sb.Append(" - ");
                        sb.Append(target.outputTechnology.ToString().Replace("DISPLAYCONFIG_OUTPUT_TECHNOLOGY_", string.Empty));
                        sb.Append(" - ");
                        sb.Append(target.monitorFriendlyDeviceName);

                        var mon = dd.Monitor;
                        if (mon != null)
                        {
                            sb.Append(" (");
                            sb.Append(mon.WorkingArea);
                            var dpi = mon.Dpi;
                            sb.Append(" dpi:" + dpi[0] + "," + dpi[1]);
                            sb.Append(')');
                        }
                    }
                    catch
                    {
                        // continue
                    }
                }
            }
            return sb.ToString();
        }

        public static string GetDrives()
        {
            var sb = new StringBuilder();
            DriveInfo[] drives = null;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                // continue
            }

            if (drives != null)
            {
                foreach (var drive in drives)
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                        continue;

                    if (sb.Length > 0)
                    {
                        sb.Append(" | ");
                    }

                    sb.Append(drive.Name);
                    sb.Append(" (");
                    sb.Append(drive.TotalSize / 1024 / 1024 / 1024);
                    sb.Append("Gb free: ");
                    sb.Append(drive.TotalFreeSpace * 100 / drive.TotalSize);
                    sb.Append("%)");
                }
            }
            return sb.ToString();
        }

        private static string GetVirtualMachineType()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\mssmbios\Data", false))
                {
                    if (key?.GetValue("SMBiosData") is byte[] bytes)
                    {
                        // detect known emulators
                        if (SearchASCIICaseInsensitive(bytes, "Hyper-V"))
                            return "Hyper-V";

                        if (SearchASCIICaseInsensitive(bytes, "Microsoft") && !SearchASCIICaseInsensitive(bytes, "Surface"))
                            return "Microsoft";

                        if (SearchASCIICaseInsensitive(bytes, "VMWare"))
                            return "VMWare";

                        if (SearchASCIICaseInsensitive(bytes, "VBox"))
                            return "Virtual Box";

                        if (SearchASCIICaseInsensitive(bytes, "Bochs"))
                            return "Bochs";

                        if (SearchASCIICaseInsensitive(bytes, "QEMU"))
                            return "QEMU";

                        if (SearchASCIICaseInsensitive(bytes, "Plex86"))
                            return "Plex86";

                        if (SearchASCIICaseInsensitive(bytes, "Parallels"))
                            return "Parallels";

                        if (SearchASCIICaseInsensitive(bytes, "Xen"))
                            return "Xen";

                        if (SearchASCIICaseInsensitive(bytes, "Virtual"))
                            return "Generic Virtual Machine";
                    }
                }
            }
            catch
            {
                // do nothing
            }
            return null;
        }

        private static bool SearchASCIICaseInsensitive(byte[] bytes, string asciiString)
        {
            if (bytes == null || bytes.Length == 0)
                return false;

            var s = Encoding.ASCII.GetBytes(asciiString);
            if (s.Length > bytes.Length)
                return false;

            for (var i = 0; i < bytes.Length; i++)
            {
                var equals = true;
                for (var j = 0; j < s.Length; j++)
                {
                    var c1 = (char)bytes[i + j];
                    var c2 = (char)s[j];
                    if (char.ToLowerInvariant(c1) != char.ToLowerInvariant(c2))
                    {
                        equals = false;
                        break;
                    }
                }

                if (equals)
                    return true;
            }
            return false;
        }

        private static bool GetIsRemoteSession()
        {
            // emulate remote session
            if (CommandLine.Current.GetArgument("remoteSession", false))
                return true;

            // https://docs.microsoft.com/en-us/windows/win32/termserv/detecting-the-terminal-services-environment
            if (System.Windows.Forms.SystemInformation.TerminalServerSession) // this uses GetSystemMetrics(SM_REMOTESESSION);
                return true;

            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server", false))
            {
                if (key != null && Conversions.TryChangeType<int>(key.GetValue("GlassSessionId"), out var id) && SessionId.HasValue && id != SessionId.Value)
                    return true;
            }

            // citrix
            if (Environment.GetEnvironmentVariable("SESSIONNAME")?.StartsWith("ICA", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetMsiInfo(string key, string propertyName)
        {
            try
            {
                return GetMsiInfoPrivate(key, propertyName);
            }
            catch
            {
                // cannot load System.Management
                return null;
            }
        }

        private static string GetMsiInfoPrivate(string key, string propertyName)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            try
            {
                foreach (ManagementBaseObject mo in new ManagementObjectSearcher(new WqlObjectQuery("select * from " + key)).Get())
                {
                    foreach (PropertyData data in mo.Properties)
                    {
                        if (data?.Name == null)
                            continue;

                        if (data.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (data.Value == null)
                                return null;

                            return string.Format("{0}", data.Value);
                        }
                    }
                }
            }
            catch
            {
                // continue
            }
            return null;
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public int dwLength;
            public int dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private const int TOKEN_QUERY = 8;
        private const int TokenElevationTypeInformation = 18;

        [DllImport("kernel32")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, out TokenElevationType TokenInformation, int TokenInformationLength, out int ReturnLength);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32")]
        private static extern bool ProcessIdToSessionId(int dwProcessId, out int pSessionId);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
