using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace op.io
{
    public static class DebugLogger
    {
        public static bool IsLoggingInternally { get; set; }

        public readonly record struct DebugLogEntry(string Message, ConsoleColor Color);
        public readonly record struct QueuedLogEntry(string Message, ConsoleColor Color, int SuppressionBehavior, bool WasPersistedToFile);

        // This list holds all queued logs until the console is initialized
        private static readonly List<QueuedLogEntry> queuedLogs = new();
        // Holds the full session log so it can be replayed when the console is reopened
        private static readonly List<Tuple<string, ConsoleColor>> sessionLogs = new();
        // Maintains a bounded history for the in-game log viewer.
        private const int MaxUiLogEntries = 2000;
        private static readonly object UiLogSync = new();
        private static readonly List<DebugLogEntry> uiLogHistory = new();

        public static void QueueLog(string formattedMessage, ConsoleColor color, int suppressionBehavior, bool wasPersistedToFile = false)
        {
            queuedLogs.Add(new QueuedLogEntry(formattedMessage, color, suppressionBehavior, wasPersistedToFile));
        }

        public static List<QueuedLogEntry> FlushQueuedLogs() // Clears queuedLogs and returns its contents
        {
            var logsCopy = new List<QueuedLogEntry>(queuedLogs);
            queuedLogs.Clear();
            return logsCopy;
        }

        public static DebugLogEntry[] GetLogHistorySnapshot()
        {
            lock (UiLogSync)
            {
                return uiLogHistory.ToArray();
            }
        }

        public static void ReplaySessionLogToConsole()
        {
            if (!ConsoleManager.ConsoleInitialized)
                return;

            try
            {
                Console.Clear();
            }
            catch
            {
                // Ignore clear failures; we'll still try to write logs.
            }

            foreach (var log in sessionLogs)
            {
                WriteConsoleOnly(log.Item1, log.Item2);
            }
        }

        public static void Log(
            string rawMessage,
            string level,
            ConsoleColor color,
            int depth,
            int stackTraceNBack,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMethod = "",
            [CallerLineNumber] int callerLine = 0)
        {
            if (IsLoggingInternally)
                return;

            IsLoggingInternally = true;

            StackTrace stackTrace = new(stackTraceNBack, true); // +1 to skip this Log() method itself
            StackFrame frame = stackTrace.GetFrame(0);

            // Determine if the sourceTrace was provided externally
            string sourceMessage = "";
            bool sourceTraceProvidedExternally = frame == null; // If the frame is null, assume external source trace

            // Only generate a source trace if it was NOT provided externally
            if (!sourceTraceProvidedExternally && frame != null)
            {
                string fileName = frame.GetFileName();
                string methodName = frame.GetMethod()?.Name;
                int lineNumber = frame.GetFileLineNumber();

                if (fileName != null && methodName != null)
                {
                    string callerFileName = Path.GetFileName(fileName);
                    sourceMessage = $"{callerFileName}::{methodName} @ Line {lineNumber}";
                }
                else
                {
                    PrintError("Failed to retrieve source trace information.");
                    sourceMessage = "UnknownSource";
                }
            }

            // Format the log message
            string formattedMessage = LogFormatter.FormatLogMessage(rawMessage, level, !sourceTraceProvidedExternally, stackTraceNBack, depth);
            LogFormatter.IncrementMessageCount(formattedMessage);

            int suppressionBehavior = LogFormatter.SuppressMessageBehavior(formattedMessage);

            // If source trace was provided externally, include it. Otherwise, omit it.
            string completeMessage = sourceTraceProvidedExternally
                ? $"{formattedMessage} | {sourceMessage}"
                : $"{formattedMessage}";

            bool consoleInitialized = ConsoleManager.ConsoleInitialized;

            AppendUiLogEntries(completeMessage, color, suppressionBehavior, wasDeferred: !consoleInitialized);

            if (!consoleInitialized)
            {
                LogFileHandler.AppendLog(completeMessage);
                QueueLog(completeMessage, color, suppressionBehavior, wasPersistedToFile: true);
                IsLoggingInternally = false;
                return;
            }

            PrintToConsole(completeMessage, color, suppressionBehavior);
            IsLoggingInternally = false;
        }

        public static void PrintToConsole(string formattedMessage, ConsoleColor color, int suppressionBehavior, bool writeToFile = true)
        {
            switch (suppressionBehavior)
            {
                case 0:
                    WriteConsoleAndFile(formattedMessage, color, appendNewLine: true, writeToFile: writeToFile);
                    break;

                case 1:
                    WriteConsoleAndFile($"[SUBSEQUENT MESSAGES SUPPRESSED DUE TO {DebugModeHandler.MAXMSGREPEATS} MAX REPEATS] ", ConsoleColor.Magenta, appendNewLine: false, writeToFile: writeToFile);
                    WriteConsoleAndFile(formattedMessage, color, appendNewLine: true, writeToFile: writeToFile);
                    break;

                case 2:
                    // Suppressed, do nothing.
                    break;

                default:
                    PrintError("Unknown suppression behavior int (not 0, 1, or 2).");
                    break;
            }
        }

        private static void WriteConsoleAndFile(string message, ConsoleColor color, bool appendNewLine = true, bool writeToFile = true)
        {
            AppendToSessionLog(message, color);

            Console.ForegroundColor = color;
            if (appendNewLine)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }
            Console.ResetColor();

            if (writeToFile)
            {
                LogFileHandler.AppendLog(message, appendNewLine);
            }
        }

        private static void WriteConsoleOnly(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void AppendToSessionLog(string message, ConsoleColor color)
        {
            sessionLogs.Add(Tuple.Create(message, color));
        }

        private static void AppendUiLogEntries(string message, ConsoleColor color, int suppressionBehavior, bool wasDeferred)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string decorated = wasDeferred ? $"[DEFERRED OUTPUT] {message}" : message;
            List<DebugLogEntry> pending = new();

            switch (suppressionBehavior)
            {
                case 0:
                    pending.Add(new DebugLogEntry(decorated, color));
                    break;
                case 1:
                    pending.Add(new DebugLogEntry($"[SUBSEQUENT MESSAGES SUPPRESSED DUE TO {DebugModeHandler.MAXMSGREPEATS} MAX REPEATS] ", ConsoleColor.Magenta));
                    pending.Add(new DebugLogEntry(decorated, color));
                    break;
                case 2:
                    return;
                default:
                    pending.Add(new DebugLogEntry(decorated, color));
                    break;
            }

            lock (UiLogSync)
            {
                uiLogHistory.AddRange(pending);
                int excess = uiLogHistory.Count - MaxUiLogEntries;
                if (excess > 0)
                {
                    int trimCount = Math.Min(excess + 50, uiLogHistory.Count);
                    uiLogHistory.RemoveRange(0, trimCount);
                }
            }
        }

        // Public-facing logging methods for different log levels
        private const int defaultNBack = 3;
        private const int defaultDepth = 0;
        public static void Print(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "GENERAL", ConsoleColor.White, depth, stackTraceNBack);
        public static void PrintSystem(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "SYSTEM", ConsoleColor.White, depth, stackTraceNBack);
        public static void PrintTemporary(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "TEMPORARY", ConsoleColor.Green, depth, stackTraceNBack);
        public static void PrintError(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "ERROR", ConsoleColor.Red, depth, stackTraceNBack);
        public static void PrintWarning(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "WARNING", ConsoleColor.DarkYellow, depth, stackTraceNBack);
        public static void PrintDatabase(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "DATABASE", ConsoleColor.Blue, depth, stackTraceNBack);
        public static void PrintDebug(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "DEBUG", ConsoleColor.DarkGray, depth, stackTraceNBack);
        public static void PrintUI(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "UI", ConsoleColor.DarkGray, depth, stackTraceNBack);
        public static void PrintGO(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "GAMEOBJECT", ConsoleColor.DarkGreen, depth, stackTraceNBack);
        public static void PrintPlayer(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "PLAYER", ConsoleColor.Cyan, depth, stackTraceNBack);
        public static void PrintPhysics(string message, int depth = defaultDepth, int stackTraceNBack = defaultNBack) => Log(message, "PHYSICS", ConsoleColor.Blue, depth, stackTraceNBack);
    }
}
