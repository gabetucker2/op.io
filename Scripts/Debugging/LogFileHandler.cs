using System;
using System.Collections.Generic;
using System.IO;

namespace op.io
{
    public static class LogFileHandler
    {
        public const int DefaultMaxLogFiles = 5;
        private static readonly object SyncRoot = new();
        private static readonly string ProjectRoot = ResolveProjectRoot();
        private static readonly string LogsRoot = Path.Combine(ProjectRoot, "Logs");
        private static string _currentLogFilePath = string.Empty;
        private static bool _initialized;
        private static int _maxLogFiles = DefaultMaxLogFiles;

        public static void AppendLog(string message, bool appendNewLine = true)
        {
            try
            {
                EnsureInitialized();

                if (string.IsNullOrWhiteSpace(_currentLogFilePath))
                {
                    return;
                }

                string payload = appendNewLine ? $"{message}{Environment.NewLine}" : message;

                lock (SyncRoot)
                {
                    File.AppendAllText(_currentLogFilePath, payload);
                }
            }
            catch
            {
                // Logging failures should never crash the game. Swallow exceptions silently.
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                Directory.CreateDirectory(LogsRoot);
                EnforceLogRetention();
                _currentLogFilePath = CreateLogFile();
                _initialized = true;
            }
        }

        public static void ConfigureMaxLogFiles(int maxLogFiles)
        {
            lock (SyncRoot)
            {
                _maxLogFiles = maxLogFiles > 0 ? maxLogFiles : DefaultMaxLogFiles;
                EnforceLogRetention();
            }
        }

        private static void EnforceLogRetention()
        {
            if (!Directory.Exists(LogsRoot))
            {
                return;
            }

            List<FileInfo> logFiles = [];
            string[] files = Directory.GetFiles(LogsRoot, "*.txt", SearchOption.AllDirectories);

            foreach (string path in files)
            {
                try
                {
                    logFiles.Add(new FileInfo(path));
                }
                catch
                {
                    // Ignore files we cannot inspect.
                }
            }

            logFiles.Sort((a, b) =>
            {
                int creationComparison = DateTime.Compare(a.CreationTimeUtc, b.CreationTimeUtc);
                return creationComparison != 0
                    ? creationComparison
                    : DateTime.Compare(a.LastWriteTimeUtc, b.LastWriteTimeUtc);
            });

            int maxLogFiles = _maxLogFiles > 0 ? _maxLogFiles : DefaultMaxLogFiles;

            while (logFiles.Count >= maxLogFiles)
            {
                FileInfo oldest = logFiles[0];
                logFiles.RemoveAt(0);

                try
                {
                    oldest.IsReadOnly = false;
                    oldest.Delete();
                }
                catch
                {
                    // Swallow errors when deleting old logs.
                }
            }
        }

        private static string CreateLogFile()
        {
            DateTime now = DateTime.Now;

            Directory.CreateDirectory(LogsRoot);

            string baseFileName = $"{now:yyyy_MM_dd_HH_mm_ss}_Log.txt";
            string fullPath = Path.Combine(LogsRoot, baseFileName);
            int duplicateIndex = 1;

            while (File.Exists(fullPath))
            {
                string candidate = $"{now:yyyy_MM_dd_HH_mm_ss}_{duplicateIndex}_Log.txt";
                fullPath = Path.Combine(LogsRoot, candidate);
                duplicateIndex++;
            }

            using (var stream = File.AppendText(fullPath))
            {
                stream.WriteLine($"Log created at {now:O}");
            }

            return fullPath;
        }

        private static string ResolveProjectRoot()
        {
            string baseDirectory = AppContext.BaseDirectory;
            DirectoryInfo directory = new(baseDirectory);

            // Walk up to the project root (bin/Debug/net8.0-windows -> project folder)
            for (int i = 0; i < 3; i++)
            {
                if (directory.Parent == null)
                {
                    break;
                }

                directory = directory.Parent;
            }

            return directory.FullName;
        }
    }
}
