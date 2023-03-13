using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace DevPad.Utilities
{
    public static class FilesWatcher
    {
        private static readonly ConcurrentDictionary<string, Watcher> _watchers = new ConcurrentDictionary<string, Watcher>(StringComparer.OrdinalIgnoreCase);

        private sealed class Watcher : IDisposable
        {
            private readonly FileSystemWatcher _watcher;
            private readonly ConcurrentHashSet<string> _fileNames = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);

            public Watcher(string path)
            {
                _watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };

                _watcher.Created += OnChanged;
                _watcher.Changed += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnRenamed;
                _watcher.EnableRaisingEvents = true;
            }

            private void OnRenamed(object sender, RenamedEventArgs e)
            {
                if (_fileNames.Contains(e.OldName))
                {
                    FileChanged?.Invoke(sender, e);
                }
            }

            private void OnChanged(object sender, FileSystemEventArgs e)
            {
                if (_fileNames.Contains(e.Name))
                {
                    FileChanged?.Invoke(sender, e);
                }
            }

            public int Count => _fileNames.Count;
            public bool AddFile(string fileName) => _fileNames.Add(fileName);
            public bool RemoveFile(string fileName) => _fileNames.Remove(fileName);
            public void Dispose() => _watcher.Dispose();
            public override string ToString() => _watcher.Path;
        }

        public static event EventHandler<FileSystemEventArgs> FileChanged;

        public static void WatchFile(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var dir = Path.GetDirectoryName(filePath);
            if (!_watchers.TryGetValue(dir, out var watcher))
            {
                watcher = new Watcher(dir);
                watcher = _watchers.AddOrUpdate(dir, watcher, (k, o) =>
                {
                    watcher.Dispose();
                    return o;
                });
            }
            watcher.AddFile(Path.GetFileName(filePath));
        }

        public static void UnwatchFile(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var dir = Path.GetDirectoryName(filePath);
            if (!_watchers.TryGetValue(dir, out var watcher))
                return;

            watcher.RemoveFile(Path.GetFileName(filePath));
            if (watcher.Count == 0)
            {
                while (!_watchers.TryRemove(dir, out _))
                {
                    Thread.Sleep(20);
                }
                watcher.Dispose();
            }
        }
    }
}
