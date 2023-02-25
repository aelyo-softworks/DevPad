using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace DevPad.Utilities
{
    public static class Extensions
    {
        private static readonly Lazy<IReadOnlyList<string>> _edgeBrowserPaths = new Lazy<IReadOnlyList<string>>(GetEdgeBrowserPossiblePaths);
        public static IReadOnlyList<string> EdgeBrowserPaths => _edgeBrowserPaths.Value;

        private static IReadOnlyList<string> GetEdgeBrowserPossiblePaths()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            addFromHKCR(@"MSEdgeHTM\Application", "ApplicationIcon");
            addFromHKCR(@"MSEdgeHTM\shell\open\command");
            addFromHKCR(@"MSEdgeHTM\DefaultIcon");
            addFromHKCR(@"microsoft-edge\shell\open\command");
            addFromHKCR(@"CLSID\{1FD49718-1D00-4B19-AF5F-070AF6D5D54C}\InprocServer32", null, @"..\..");
            var list = set.OrderByDescending(p => getVersion(p)).ToArray();
            return list;

            Version getVersion(string path)
            {
                try
                {
                    return Version.Parse(Path.GetFileName(path));
                }
                catch
                {
                    return new Version();
                }
            }

            void addFromHKCR(string path, string name = null, string combine = null)
            {
                using (var key = Registry.ClassesRoot.OpenSubKey(path))
                {
                    if (key == null)
                        return;

                    addPath(normPath(key.GetValue(name) as string), combine);
                }
            }

            void addPath(string path, string combine = null)
            {
                path = path.Nullify();
                if (path == null)
                    return;

                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (combine != null)
                    {
                        dir = Path.Combine(dir, combine);
                    }

                    if (!string.IsNullOrWhiteSpace(dir) && Path.IsPathRooted(dir))
                    {
                        dir = Path.GetFullPath(dir);
                        if (IOUtilities.PathIsDirectory(dir))
                        {
                            foreach (var subDir in Directory.EnumerateDirectories(dir))
                            {
                                if (IOUtilities.PathIsFile(Path.Combine(subDir, "msedgewebview2.exe")))
                                {
                                    set.Add(subDir);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // continue
                }
            }

            string normPath(string path)
            {
                path = path.Nullify();
                if (path == null)
                    return null;

                if (path.StartsWith("\""))
                {
                    var pos = path.IndexOf('"', 1);
                    if (pos > 0)
                        return path.Substring(1, pos - 1).Nullify();
                }
                return path;
            }
        }

        public static string Nullify(this string text)
        {
            if (text == null)
                return null;

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var t = text.Trim();
            return t.Length == 0 ? null : t;
        }

        public static bool EqualsIgnoreCase(this string thisString, string text) => EqualsIgnoreCase(thisString, text, false);
        public static bool EqualsIgnoreCase(this string thisString, string text, bool trim)
        {
            if (trim)
            {
                thisString = thisString.Nullify();
                text = text.Nullify();
            }

            if (thisString == null)
                return text == null;

            if (text == null)
                return false;

            if (thisString.Length != text.Length)
                return false;

            return string.Compare(thisString, text, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static string GetAllMessages(this Exception exception) => GetAllMessages(exception, Environment.NewLine);
        public static string GetAllMessages(this Exception exception, string separator)
        {
            if (exception == null)
                return null;

            var sb = new StringBuilder();
            AppendMessages(sb, exception, separator);
            return sb.ToString().Replace("..", ".");
        }

        private static void AppendMessages(StringBuilder sb, Exception e, string separator)
        {
            if (e == null)
                return;

            // this one is not interesting...
            if (!(e is TargetInvocationException))
            {
                if (sb.Length > 0)
                {
                    sb.Append(separator);
                }
                sb.Append(e.Message);
            }
            AppendMessages(sb, e.InnerException, separator);
        }

        public static Exception GetInterestingException(this Exception exception)
        {
            if (exception is TargetInvocationException tie && tie.InnerException != null)
                return GetInterestingException(tie.InnerException);

            return exception;
        }

        public static string GetInterestingExceptionMessage(this Exception exception) => GetInterestingException(exception)?.Message;
    }
}
