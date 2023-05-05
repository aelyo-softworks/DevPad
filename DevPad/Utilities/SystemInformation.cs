using System;
using System.Collections.Generic;
using System.Linq;
using DevPad.Resources;
using DevPad.Utilities.Grid;

namespace DevPad.Utilities
{
    public class SystemInformation : IPropertyGridObject
    {
        [LocalizedCategory("System")]
        public string Processor => SystemUtilities.Processor;

        [LocalizedCategory("System")]
        public int ProcessorCount => Environment.ProcessorCount;

        [LocalizedCategory("System")]
        public string Screens => SystemUtilities.Screens;

        [LocalizedCategory("System")]
        public string MachineName => Environment.MachineName;

        [LocalizedCategory("System")]
        public string BaseBoardSerialNumber => SystemUtilities.BaseBoardSerialNumber;

        [LocalizedCategory("System")]
        public string BaseBoardProduct => SystemUtilities.BaseBoardProduct;

        [LocalizedCategory("System")]
        public string ComputerManufacturer => SystemUtilities.ComputerManufacturer;

        [LocalizedCategory("System")]
        public string ComputerModel => SystemUtilities.ComputerModel;

        [LocalizedCategory("System")]
        public bool IsRemoteSession => SystemUtilities.IsRemoteSession;

        [LocalizedCategory("System")]
        public string VirtualMachineType => SystemUtilities.VirtualMachineType;

        [LocalizedCategory("System")]
        public ulong TotalPhysicalMemory => SystemUtilities.TotalPhysicalMemory;

        [LocalizedCategory("System")]
        public string Drives => SystemUtilities.GetDrives();

        [LocalizedCategory("Windows")]
        public string OSVersion => Environment.OSVersion.ToString();

        [LocalizedCategory("Windows")]
        public string KernelVersion => WindowsUtilities.KernelVersion.ToString();

        [LocalizedCategory("Windows")]
        public string ClrVersion => Environment.Version.ToString();

        [LocalizedCategory("Windows")]
        public string AvailableWebViewVersion => SystemUtilities.GetAvailableBrowserVersionString();

        [LocalizedCategory("Windows")]
        public string ThemeFilePath => WindowsUtilities.GetCurrentThemeFilePath();

        [LocalizedCategory("Windows")]
        public string CurrentDesktop => VirtualDesktop.GetWindowDesktopName(MainWindow.Current.DesktopId);

        [LocalizedCategory("Windows")]
        public string Desktops => string.Join(", ", VirtualDesktop.GetDesktops());

        [LocalizedCategory("Process")]
        public string CommandLine => Environment.CommandLine;

        [LocalizedCategory("Process")]
        public string ParentProcess => WindowsUtilities.ParentProcess?.MainModule?.FileName;

        [LocalizedCategory("Process")]
        public string CurrentProcess => WindowsUtilities.CurrentProcess?.MainModule?.FileName;

        [LocalizedCategory("Process")]
        public long WorkingSet => Environment.WorkingSet;

        [LocalizedCategory("Process")]
        public string MemoryLoad => SystemUtilities.GetMemoryLoadPercent() + " %";

        [LocalizedCategory("Security")]
        public string UserName => Environment.UserDomainName + "\\" + Environment.UserName;

        [LocalizedCategory("Security")]
        public TokenElevationType TokenElevationType => SystemUtilities.GetTokenElevationType();

        void IPropertyGridObject.EditorClosed(PropertyGridProperty property, object editor) { }
        bool IPropertyGridObject.TryShowEditor(PropertyGridProperty property, object editor, out bool? result) { result = null; return false; }
        void IPropertyGridObject.FinalizeProperties(PropertyGridDataProvider dataProvider, IList<PropertyGridProperty> properties)
        {
            if (WindowsUtilities.KernelVersion.Major < 10)
            {
                properties.Remove(properties.First(p => p.Name.Equals(nameof(Desktops))));
                properties.Remove(properties.First(p => p.Name.Equals(nameof(CurrentDesktop))));
            }
        }
    }
}
