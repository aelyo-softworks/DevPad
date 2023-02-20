using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevPad.Utilities
{
    public static class IOUtilities
    {
        public static bool DirectoryExists(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        public static void DirectoryDelete(string directoryPath, bool recursive = true)
        {
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));

            if (!DirectoryExists(directoryPath))
                return;

            try
            {
                Directory.Delete(directoryPath, recursive);
            }
            catch
            {
                // do nothing
            }
        }

        public static void DirectoryCopy(string sourcePath, string targetPath, bool recursive = true, Func<string, bool> filterFunc = null)
        {
            if (sourcePath == null)
                throw new ArgumentNullException(nameof(sourcePath));

            if (targetPath == null)
                throw new ArgumentNullException(nameof(targetPath));

            sourcePath = Path.GetFullPath(sourcePath);
            targetPath = Path.GetFullPath(targetPath);
            if (sourcePath.EqualsIgnoreCase(targetPath))
                return;

            CopyFiles(sourcePath, targetPath);

            if (recursive)
            {
                foreach (var sourceChildPath in Directory.EnumerateDirectories(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    if (filterFunc?.Invoke(sourceChildPath) == false)
                        continue;

                    var relPath = sourceChildPath.Substring(sourcePath.Length + 1);
                    var targetChildPath = Path.Combine(targetPath, relPath);
                    CopyFiles(sourceChildPath, targetChildPath);
                }
            }

            void CopyFiles(string sourceDirectoryPath, string targetDirectoryPath)
            {
                if (!Directory.Exists(targetDirectoryPath))
                {
                    Directory.CreateDirectory(targetDirectoryPath);
                }

                foreach (var file in Directory.EnumerateFiles(sourceDirectoryPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    if (filterFunc?.Invoke(file) == false)
                        continue;

                    File.Copy(file, Path.Combine(targetDirectoryPath, Path.GetFileName(file)), true);
                }
            }
        }

        public static void FileCreateDirectory(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }

            string dir = Path.GetDirectoryName(filePath);
            if (dir == null || Directory.Exists(dir))
                return;

            Directory.CreateDirectory(dir);
        }

        public static void FileMove(string source, string destination, bool unprotect = true)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            FileDelete(destination, unprotect);
            FileCreateDirectory(destination);
            File.Move(source, destination);
        }

        public static bool FileDelete(string filePath, bool unprotect = true)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (!FileExists(filePath))
                return false;

            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly && unprotect)
            {
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(filePath);
            return true;
        }

        public static void FileOverwrite(string source, string destination, bool unprotect = true)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (PathIsEqual(source, destination))
                return;

            FileDelete(destination, unprotect);
            FileCreateDirectory(destination);
            File.Copy(source, destination, true);
        }

        public static bool FileExists(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        public static bool PathIsEqual(string path1, string path2, bool normalize = true)
        {
            if (path1 == null)
                throw new ArgumentNullException(nameof(path1));

            if (path2 == null)
                throw new ArgumentNullException(nameof(path2));

            if (normalize)
            {
                path1 = Path.GetFullPath(path1);
                path2 = Path.GetFullPath(path2);
            }

            return path1.EqualsIgnoreCase(path2);
        }

        public static bool PathIsChildOrEqual(string path, string child, bool normalize = true) => PathIsChild(path, child, normalize) || PathIsEqual(path, child, normalize);
        public static bool PathIsChild(string path, string child, bool normalize = true) => PathIsChild(path, child, normalize, out _);
        public static bool PathIsChild(string path, string child, bool normalize, out string subPath)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (child == null)
                throw new ArgumentNullException(nameof(child));

            subPath = null;
            if (normalize)
            {
                path = Path.GetFullPath(path);
                child = Path.GetFullPath(child);
            }

            path = StripTerminatingPathSeparators(path);
            if (child.Length < (path.Length + 1))
                return false;

            var newChild = Path.Combine(path, child.Substring(path.Length + 1));
            var b = newChild.EqualsIgnoreCase(child);
            if (b)
            {
                subPath = child.Substring(path.Length);
                while (subPath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    subPath = subPath.Substring(1);
                }
            }
            return b;
        }

        public static string StripTerminatingPathSeparators(string path)
        {
            if (path == null)
                return null;

            while (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                path = path.Substring(0, path.Length - 1);
            }
            return path;
        }

        public static string UrlCombine(params string[] urls)
        {
            if (urls == null)
                return null;

            var sb = new StringBuilder();
            foreach (var url in urls)
            {
                if (string.IsNullOrEmpty(url))
                    continue;

                if (sb.Length > 0)
                {
                    if (sb[sb.Length - 1] != '/' && url[0] != '/')
                    {
                        sb.Append('/');
                    }
                }
                sb.Append(url);
            }
            return sb.ToString();
        }

        private static readonly string[] _reservedFileNames = new[]
        {
            "con", "prn", "aux", "nul",
            "com0", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt0", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        private static bool IsAllDots(string fileName)
        {
            foreach (char c in fileName)
            {
                if (c != '.')
                    return false;
            }
            return true;
        }

        private static int GetDriveNameEnd(string path)
        {
            var pos = path.IndexOf(':');
            if (pos < 0)
                return -1;

            var pos2 = path.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (pos2 < pos)
                return -1;

            return pos;
        }

        private static int GetServerNameEnd(string path, out bool onlyServer)
        {
            onlyServer = false;
            if (!path.StartsWith(@"\\"))
                return -1;

            var pos = path.IndexOf(Path.DirectorySeparatorChar, 3);
            if (pos < 3)
                return -1;

            var pos2 = path.IndexOf(Path.DirectorySeparatorChar, pos + 1);
            if (pos2 < pos)
            {
                onlyServer = true;
                return -1;
            }
            return pos2;
        }

        public static string PathToValidFilePath(string filePath, string reservedNameFormat = null, string reservedCharFormat = null)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var sb = new StringBuilder(filePath.Length);
            var fn = new StringBuilder();
            var serverNameEnd = GetServerNameEnd(filePath, out bool onlyServer);
            if (onlyServer)
                return filePath;

            var start = 0;
            if (serverNameEnd >= 0)
            {
                // path includes? server name? just skip it, don't validate it
                start = serverNameEnd + 1;
            }
            else
            {
                var driveNameEnd = GetDriveNameEnd(filePath);
                if (driveNameEnd >= 0)
                {
                    start = driveNameEnd + 1;
                }
            }

            for (var i = start; i < filePath.Length; i++)
            {
                var c = filePath[i];
                if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                {
                    if (fn.Length > 0)
                    {
                        sb.Append(PathToValidFileName(fn.ToString(), reservedNameFormat, reservedCharFormat));
                        fn.Length = 0;
                    }
                    sb.Append(c);
                    continue;
                }

                fn.Append(c);
            }

            if (fn.Length > 0)
            {
                sb.Append(PathToValidFileName(fn.ToString(), reservedNameFormat, reservedCharFormat));
            }

            return sb.ToString();
        }

        public static string PathToValidFileName(string fileName, string reservedNameFormat = null, string reservedCharFormat = null)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            if (string.IsNullOrWhiteSpace(reservedNameFormat))
            {
                reservedNameFormat = "_{0}_";
            }

            if (string.IsNullOrWhiteSpace(reservedCharFormat))
            {
                reservedCharFormat = "_x{0}_";
            }

            if (Array.IndexOf(_reservedFileNames, fileName.ToLowerInvariant()) >= 0 || IsAllDots(fileName))
                return string.Format(reservedNameFormat, fileName);

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(fileName.Length);
            foreach (char c in fileName)
            {
                if (Array.IndexOf(invalid, c) >= 0)
                {
                    sb.AppendFormat(reservedCharFormat, (short)c);
                }
                else
                {
                    sb.Append(c);
                }
            }

            var s = sb.ToString();
            if (s.Length >= 255) // a segment is always 255 max even with long file names
            {
                var ext = Path.GetExtension(s);
                var name = Path.GetFileNameWithoutExtension(s);
                s = name.Substring(0, 254 - ext.Length) + ext;
            }

            if (s.EqualsIgnoreCase(fileName))
                return fileName;

            return s;
        }

        public static bool PathHasInvalidChars(string path)
        {
            if (path == null)
                return true;

            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                if (c == 0x22 ||
                    c == 0x3C ||
                    c == 0x3E ||
                    c == 0x7C ||
                    c < 0x20)
                    return true;
            }
            return false;
        }

        public static bool PathIsValidFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            if (Array.IndexOf(_reservedFileNames, fileName.ToLowerInvariant()) >= 0)
                return false;

            return !IsAllDots(fileName);
        }

        public static Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        public static Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentException(null, nameof(path));

            return cancellationToken.IsCancellationRequested ? Task.FromCanceled<string>(cancellationToken) : InternalReadAllTextAsync(path, encoding, cancellationToken);
        }

        private static async Task<string> InternalReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken)
        {
            encoding = encoding ?? Encoding.UTF8;
            using (var sr = new StreamReader(path, encoding))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var buffer = new char[4096];
                var sb = new StringBuilder();
                while (true)
                {
                    int read = await sr.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (read == 0)
                        return sb.ToString();

                    sb.Append(buffer, 0, read);
                }
            }
        }

        public static Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) => WriteAllTextAsync(path, contents, Encoding.UTF8, cancellationToken);
        public static Task WriteAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentException(null, nameof(path));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            if (string.IsNullOrEmpty(contents))
            {
                new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read).Dispose();
                return Task.CompletedTask;
            }

            encoding = encoding ?? Encoding.UTF8;
            var sw = new StreamWriter(path, false, encoding);
            return InternalWriteAllTextAsync(sw, contents, cancellationToken);
        }

        private static async Task InternalWriteAllTextAsync(StreamWriter sw, string contents, CancellationToken cancellationToken)
        {
            using (sw)
            {
                var buffer = new char[0x14000];
                var count = contents.Length;
                var index = 0;
                while (index < count)
                {
                    var batchSize = Math.Min(buffer.Length, count - index);
                    contents.CopyTo(index, buffer, 0, batchSize);
                    await sw.WriteAsync(buffer, 0, batchSize).ConfigureAwait(false);
                    index += batchSize;
                }

                cancellationToken.ThrowIfCancellationRequested();
                await sw.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
