using System;
using DevPad.Resources;

namespace DevPad.Utilities
{
    public class SystemInformation
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
    }
}
