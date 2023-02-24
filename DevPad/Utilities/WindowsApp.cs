using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace DevPad.Utilities
{
    public class WindowsApp
    {
        private readonly List<string> _fileExtensions = new List<string>();

        public WindowsApp(string appUserModelId, string progId, string friendlyName)
        {
            if (appUserModelId == null)
                throw new ArgumentNullException(nameof(appUserModelId));

            if (progId == null)
                throw new ArgumentNullException(nameof(progId));

            if (friendlyName == null)
                throw new ArgumentNullException(nameof(friendlyName));

            AppUserModelId = appUserModelId;
            ProgId = progId;
            FriendlyName = friendlyName;
        }

        public string FriendlyName { get; }
        public string PublisherName { get; set; }
        public string ProgId { get; }
        public string AppUserModelId { get; }
        public IList<string> FileExtensions => _fileExtensions;

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
            }

            using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, ProgId, "CurVer")))
            {
                key.SetValue(null, ProgId);
            }

            using (var key = WindowsUtilities.EnsureSubKey(hkcu, Path.Combine(basePath, ProgId, "Shell", "Open", "Command")))
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
        }

        public void Unregister()
        {
            var hkcu = Registry.CurrentUser;
            var basePath = @"Software\Classes";
            hkcu.DeleteSubKeyTree(Path.Combine(basePath, ProgId), false);
            foreach (var ext in FileExtensions)
            {
                using (var key = hkcu.OpenSubKey(Path.Combine(basePath, ext, "OpenWithProgids"), true))
                {
                    key.DeleteValue(ProgId, false);
                }
            }
        }
    }
}
