using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace op.io
{
    public static class DebugLogger
    {
        // This list holds all queued logs until the console is initialized
        private static readonly List<Tuple<string, ConsoleColor, int>> queuedLogs = new();

        public static void QueueLog(string formattedMessage, ConsoleColor color, int suppressionBehavior)
        {
            queuedLogs.Add(Tuple.Create(formattedMessage, color, suppressionBehavior));
        }

        public static List<Tuple<string, ConsoleColor, int>> FlushQueuedLogs() // Clears queuedLogs and returns its contents
        {
            var logsCopy = new List<Tuple<string, ConsoleColor, int>>(queuedLogs);
            queuedLogs.Clear();
            return logsCopy;
        }

        public static void Log(
            string rawMessage,
            string level,
            ConsoleColor color,
            int stackTraceNBack,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMethod = "",
            [CallerLineNumber] int callerLine = 0)
        {
            if (DebugManager.IsLoggingInternally)
                return;

            DebugManager.IsLoggingInternally = true;

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
                    sourceMessage = "UnknownSource";
                }
            }

            // Format the log message
            string formattedMessage = LogFormatter.FormatLogMessage(rawMessage, level, !sourceTraceProvidedExternally, stackTraceNBack);
            LogFormatter.IncrementMessageCount(formattedMessage);

            int suppressionBehavior = LogFormatter.SuppressMessageBehavior(formattedMessage);

            // If source trace was provided externally, include it. Otherwise, omit it.
            string completeMessage = sourceTraceProvidedExternally
                ? $"{formattedMessage} | {sourceMessage}"
                : $"{formattedMessage}";

            if (!ConsoleManager.ConsoleInitialized)
            {
                QueueLog(completeMessage, color, suppressionBehavior);
                DebugManager.IsLoggingInternally = false;
                return;
            }

            PrintToConsole(completeMessage, color, suppressionBehavior);
            DebugManager.IsLoggingInternally = false;
        }

        public static void PrintToConsole(string formattedMessage, ConsoleColor color, int suppressionBehavior)
        {
            switch (suppressionBehavior)
            {
                case 0:
                    Console.ForegroundColor = color;
                    Console.WriteLine(formattedMessage);
                    Console.ResetColor();
                    break;

                case 1:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write($"[SUBSEQUENT MESSAGES SUPPRESSED DUE TO {LogFormatter.GetMaxMessageRepeats()} MAX REPEATS] ");
                    Console.ResetColor();

                    Console.ForegroundColor = color;
                    Console.WriteLine(formattedMessage);
                    Console.ResetColor();
                    break;

                case 2:
                    // Suppressed, do nothing.
                    break;

                default:
                    PrintError("Unknown suppression behavior int (not 0, 1, or 2).");
                    break;
            }
        }

        // Public-facing logging methods for different log levels
        private const int defaultNBack = 3;
        public static void Print(string message, int stackTraceNBack = defaultNBack) => Log(message, "GENERAL", ConsoleColor.White, stackTraceNBack);
        public static void PrintTemporary(string message, int stackTraceNBack = defaultNBack) => Log(message, "TEMPORARY", ConsoleColor.Green, stackTraceNBack);
        public static void PrintError(string message, int stackTraceNBack = defaultNBack) => Log(message, "ERROR", ConsoleColor.Red, stackTraceNBack);
        public static void PrintWarning(string message, int stackTraceNBack = defaultNBack) => Log(message, "WARNING", ConsoleColor.DarkYellow, stackTraceNBack);
        public static void PrintDatabase(string message, int stackTraceNBack = defaultNBack) => Log(message, "DATABASE", ConsoleColor.Blue, stackTraceNBack);
        public static void PrintDebug(string message, int stackTraceNBack = defaultNBack) => Log(message, "DEBUG", ConsoleColor.DarkGray, stackTraceNBack);
        public static void PrintUI(string message, int stackTraceNBack = defaultNBack) => Log(message, "UI", ConsoleColor.DarkGray, stackTraceNBack);
        public static void PrintObject(string message, int stackTraceNBack = defaultNBack) => Log(message, "OBJECT", ConsoleColor.DarkGreen, stackTraceNBack);
        public static void PrintPlayer(string message, int stackTraceNBack = defaultNBack) => Log(message, "PLAYER", ConsoleColor.Cyan, stackTraceNBack);
    }
}
