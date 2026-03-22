using System;
using System.IO;
using ClaudeCodeGameStudios.Data;
using UnityEditor;

namespace ClaudeCodeGameStudios.Utilities
{
    /// <summary>
    /// Watches .claude/, production/, and design/ directories for changes.
    /// Debounces and marshals callbacks to the main thread.
    /// </summary>
    [InitializeOnLoad]
    public static class FileWatcher
    {
        private static FileSystemWatcher[] _watchers;
        private static double _lastChangeTime;
        private static bool _pendingRefresh;
        private const double DebounceSeconds = 0.5;

        public static event Action OnProjectDataChanged;

        static FileWatcher()
        {
            StartWatching();
            EditorApplication.update += Update;
        }

        private static void StartWatching()
        {
            StopWatching();

            var root = PathResolver.ProjectRoot;
            var watchPaths = new[]
            {
                PathResolver.ClaudeDir,
                PathResolver.ProductionDir,
                PathResolver.DesignDir
            };

            _watchers = new FileSystemWatcher[watchPaths.Length];

            for (int i = 0; i < watchPaths.Length; i++)
            {
                var path = watchPaths[i];
                if (!Directory.Exists(path)) continue;

                try
                {
                    var watcher = new FileSystemWatcher(path)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName |
                                       NotifyFilters.DirectoryName,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += OnFileChanged;
                    watcher.Created += OnFileChanged;
                    watcher.Deleted += OnFileChanged;
                    watcher.Renamed += OnFileRenamed;

                    _watchers[i] = watcher;
                }
                catch (Exception)
                {
                    // FileSystemWatcher can fail on some platforms — fail silently
                }
            }
        }

        private static void StopWatching()
        {
            if (_watchers == null) return;
            foreach (var w in _watchers)
            {
                if (w != null)
                {
                    w.EnableRaisingEvents = false;
                    w.Dispose();
                }
            }
            _watchers = null;
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Only care about .md, .json files
            if (!e.FullPath.EndsWith(".md") && !e.FullPath.EndsWith(".json"))
                return;

            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingRefresh = true;
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingRefresh = true;
        }

        /// <summary>
        /// Called every editor frame. Checks if a debounced refresh is due.
        /// </summary>
        private static void Update()
        {
            if (!_pendingRefresh) return;
            if (EditorApplication.timeSinceStartup - _lastChangeTime < DebounceSeconds) return;

            _pendingRefresh = false;

            // Refresh the project context on the main thread
            if (ClaudeProjectContext.Instance != null)
            {
                ClaudeProjectContext.Instance.Refresh();
            }

            OnProjectDataChanged?.Invoke();
        }
    }
}
