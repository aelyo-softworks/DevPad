using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DevPad.Utilities;
using Microsoft.Win32.SafeHandles;

namespace DevPad.Setup
{
    internal static class Program
    {
#if DEBUG
        private static readonly EventProvider _provider = new EventProvider(new Guid("964d4572-adb9-4f3a-8170-fcbecec27466"));
#endif
        [Conditional("DEBUG")]
        public static void Trace(object value = null, [CallerMemberName] string methodName = null)
        {
#if DEBUG
            _provider.WriteMessageEvent("#PADS(" + Thread.CurrentThread.ManagedThreadId + ")::" + methodName + " " + string.Format("{0}", value), 0, 0);
#endif
        }

        public static WindowsApp WindowsApplication { get; } = new WindowsApp("Aelyo.DevPad", AssemblyUtilities.GetTitle());

        public static string ApplicationName => AssemblyUtilities.GetTitle();
        public static string ApplicationVersion => AssemblyUtilities.GetFileVersion();
        public static string ApplicationTitle => ApplicationName + " V" + ApplicationVersion;

        [STAThread]
        static void Main()
        {
            if (IntPtr.Size == 4)
            {
                MessageBox.Show(Resources.Resources.Only64BitWindows, ApplicationTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            WindowsApplication.PublisherName = AssemblyUtilities.GetCompany();

            // building mode
            var buildPath = CommandLine.GetNullifiedArgument("buildpath");
            var outPath = CommandLine.GetNullifiedArgument("outpath");
            if (buildPath != null && outPath != null)
            {
                try
                {
                    Build(Path.GetFullPath(buildPath), Path.GetFullPath(outPath));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.GetAllMessages(), ApplicationTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            var uninstall = CommandLine.GetArgument<bool>("uninstall");
            if (uninstall)
            {
                Setup.Main.Uninstall();
                return;
            }

            string tempFilePath = null;
            var path = Process.GetCurrentProcess().MainModule.FileName;
            var fileSize = new FileInfo(path).Length;
            var exeSize = 0L;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var view = MemoryMappedFile.CreateFromFile(file, null, fileSize, access: MemoryMappedFileAccess.Read, HandleInheritability.None, false))
            {
                using (var accessor = view.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read))
                {
                    var ptr = ImageNtHeader(accessor.SafeMemoryMappedViewHandle);
                    var nth = Marshal.PtrToStructure<IMAGE_NT_HEADERS>(ptr);
                    uint maxSize = 0;
                    for (var i = 0; i < nth.FileHeader.NumberOfSections; i++)
                    {
                        var secPtr = ptr + Marshal.SizeOf<IMAGE_NT_HEADERS>() + Marshal.SizeOf<IMAGE_SECTION_HEADER>() * i;
                        var sec = Marshal.PtrToStructure<IMAGE_SECTION_HEADER>(secPtr);
                        if (sec.PointerToRawData > maxSize)
                        {
                            maxSize = sec.PointerToRawData;
                            exeSize = sec.PointerToRawData + sec.SizeOfRawData;
                        }
                    }
                }

                if (exeSize >= fileSize)
                {
                    MessageBox.Show(Resources.Resources.InvalidFile, ApplicationTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var overlaySize = fileSize - exeSize;
                file.Seek(exeSize, SeekOrigin.Begin);
                tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    using (var tempFile = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        file.CopyTo(tempFile);
                    }
                }
                catch
                {
                    IOUtilities.FileDelete(tempFilePath, true, false);
                    throw;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main(tempFilePath));
        }

        private static void Build(string inPath, string outFilePath)
        {
            Trace("inpath:" + inPath + " outpath:" + outFilePath);
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {

                using (var zfile = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite))
                {
                    using (var archive = new ZipArchive(zfile, ZipArchiveMode.Create))
                    {
                        addDirectory(inPath, string.Empty);
                        void addDirectory(string dir, string relativePath)
                        {
                            var dirName = Path.GetFileName(dir);
                            if (dirName.EqualsIgnoreCase("win-x86")) // we'll never support x86
                                return;

                            foreach (var file in Directory.EnumerateFiles(dir))
                            {
                                var ext = Path.GetExtension(file);
#if !DEBUG
                            if (ext.EqualsIgnoreCase(".pdb"))
                                continue;
#endif
                                var fileName = Path.GetFileName(file);
                                if (fileName.EqualsIgnoreCase("Microsoft.Web.WebView2.WinForms.dll"))
                                    continue;

                                archive.CreateEntryFromFile(file, Path.Combine(relativePath, fileName), CompressionLevel.Optimal);
                            }

                            foreach (var subDir in Directory.EnumerateDirectories(dir))
                            {
                                addDirectory(subDir, Path.Combine(relativePath, Path.GetFileName(subDir)));
                            }
                        }
                    }
                }

                var path = Process.GetCurrentProcess().MainModule.FileName;
                IOUtilities.FileOverwrite(path, outFilePath);
                using (var exeFile = new FileStream(outFilePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    exeFile.Seek(0, SeekOrigin.End);
                    using (var tempFile = new FileStream(temp, FileMode.Open, FileAccess.Read))
                    {
                        tempFile.CopyTo(exeFile);
                    }
                }
            }
            finally
            {
                IOUtilities.FileDelete(temp, true, false);
            }
        }

        [DllImport("dbghelp")]
        private static extern IntPtr ImageNtHeader(SafeMemoryMappedViewHandle handle);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct IMAGE_NT_HEADERS
        {
            public uint Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct IMAGE_FILE_HEADER
        {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct IMAGE_OPTIONAL_HEADER
        {
            public ushort Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public uint SizeOfCode;
            public uint SizeOfInitializedData;
            public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint;
            public uint BaseOfCode;
            public uint BaseOfData;
            public uint ImageBase;
            public uint SectionAlignment;
            public uint FileAlignment;
            public ushort MajorOperatingSystemVersion;
            public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion;
            public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion;
            public ushort MinorSubsystemVersion;
            public uint Win32VersionValue;
            public uint SizeOfImage;
            public uint SizeOfHeaders;
            public uint CheckSum;
            public ushort Subsystem;
            public ushort DllCharacteristics;
            public uint SizeOfStackReserve;
            public uint SizeOfStackCommit;
            public uint SizeOfHeapReserve;
            public uint SizeOfHeapCommit;
            public uint LoaderFlags;
            public uint NumberOfRvaAndSizes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public IMAGE_DATA_DIRECTORY[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct IMAGE_DATA_DIRECTORY
        {
            public uint VirtualAddress;
            public uint Size;

            public override string ToString() => VirtualAddress + ":" + Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] name;
            public uint PhysicalAddress;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
            public uint PointerToRelocations;
            public uint PointerToLinenumbers;
            public ushort NumberOfRelocations;
            public ushort NumberOfLinenumbers;
            public uint Characteristics;

            public string Name => Encoding.ASCII.GetString(name).Replace("\0", string.Empty);
            public override string ToString() => Name;
        }
    }
}
