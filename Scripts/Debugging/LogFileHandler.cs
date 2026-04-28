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
        private static string _currentRedFlagsFilePath = string.Empty;
        private static readonly HashSet<string> _redFlagsSeenMessages = [];
        private static bool _initialized;
        private static int _maxLogFiles = DefaultMaxLogFiles;

        public static string LogsDirectoryPath => LogsRoot;

        public static bool InitializeSession()
        {
            try
            {
                EnsureInitialized();
                return IsSessionActive;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsSessionActive
        {
            get
            {
                lock (SyncRoot)
                {
                    return _initialized &&
                        !string.IsNullOrWhiteSpace(_currentLogFilePath) &&
                        File.Exists(_currentLogFilePath);
                }
            }
        }

        public static string CurrentLogFileName
        {
            get
            {
                lock (SyncRoot)
                {
                    return string.IsNullOrWhiteSpace(_currentLogFilePath)
                        ? "None"
                        : Path.GetFileName(_currentLogFilePath);
                }
            }
        }

        public static string CurrentRedFlagsFileName
        {
            get
            {
                lock (SyncRoot)
                {
                    return string.IsNullOrWhiteSpace(_currentRedFlagsFilePath)
                        ? "None"
                        : Path.GetFileName(_currentRedFlagsFilePath);
                }
            }
        }

        public static int MaxLogFiles
        {
            get
            {
                lock (SyncRoot)
                {
                    return _maxLogFiles;
                }
            }
        }

        public static string GetLogsDirectory()
        {
            try
            {
                Directory.CreateDirectory(LogsRoot);
            }
            catch
            {
                // If the directory cannot be created we still return the intended path.
            }

            return LogsRoot;
        }

        public static void AppendRedFlagLog(string message)
        {
            try
            {
                EnsureInitialized();

                if (string.IsNullOrWhiteSpace(_currentRedFlagsFilePath))
                    return;

                lock (SyncRoot)
                {
                    if (!_redFlagsSeenMessages.Add(message))
                        return;

                    File.AppendAllText(_currentRedFlagsFilePath, $"{message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging failures should never crash the game. Swallow exceptions silently.
            }
        }

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
                EnforceLogRetention(reserveSlotForNewLog: true);
                _currentLogFilePath = CreateLogFile();
                _currentRedFlagsFilePath = CreateRedFlagsFile(_currentLogFilePath);
                _initialized = true;
            }
        }

        public static void ConfigureMaxLogFiles(int maxLogFiles)
        {
            lock (SyncRoot)
            {
                _maxLogFiles = maxLogFiles > 0 ? maxLogFiles : DefaultMaxLogFiles;
                EnforceLogRetention(reserveSlotForNewLog: false);
            }
        }

        private static void EnforceLogRetention(bool reserveSlotForNewLog)
        {
            if (!Directory.Exists(LogsRoot))
            {
                return;
            }

            List<FileInfo> logFiles = [];
            string[] files = Directory.GetFiles(LogsRoot, "*_Log.txt", SearchOption.AllDirectories);

            foreach (string path in files)
            {
                try
                {
                    if (string.Equals(path, _currentLogFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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
            bool hasCurrentLogFile = !string.IsNullOrWhiteSpace(_currentLogFilePath) && File.Exists(_currentLogFilePath);
            int nonCurrentLogFileLimit = reserveSlotForNewLog || hasCurrentLogFile
                ? Math.Max(0, maxLogFiles - 1)
                : maxLogFiles;

            while (logFiles.Count > nonCurrentLogFileLimit)
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

                try
                {
                    string baseName = Path.GetFileNameWithoutExtension(oldest.FullName); // e.g. "2026_04_04_12_00_00_Log"
                    string redFlagsName = baseName.EndsWith("_Log")
                        ? baseName[..^4] + "_RedFlags.txt"
                        : baseName + "_RedFlags.txt";
                    string redFlagsPath = Path.Combine(oldest.DirectoryName ?? LogsRoot, redFlagsName);
                    if (File.Exists(redFlagsPath))
                    {
                        FileInfo rf = new(redFlagsPath);
                        rf.IsReadOnly = false;
                        rf.Delete();
                    }
                }
                catch
                {
                    // Swallow errors when deleting paired red flags log.
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

        private static string CreateRedFlagsFile(string logFilePath)
        {
            string dir = Path.GetDirectoryName(logFilePath) ?? LogsRoot;
            string baseName = Path.GetFileNameWithoutExtension(logFilePath); // e.g. "2026_04_04_12_00_00_Log"
            string redFlagsFileName = baseName.EndsWith("_Log")
                ? baseName[..^4] + "_RedFlags.txt"
                : baseName + "_RedFlags.txt";
            string fullPath = Path.Combine(dir, redFlagsFileName);

            using var stream = File.AppendText(fullPath);

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
