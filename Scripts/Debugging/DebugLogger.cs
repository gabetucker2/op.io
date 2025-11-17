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

            if (!ConsoleManager.ConsoleInitialized)
            {
                QueueLog(completeMessage, color, suppressionBehavior);
                IsLoggingInternally = false;
                return;
            }

            PrintToConsole(completeMessage, color, suppressionBehavior);
            IsLoggingInternally = false;
        }

        public static void PrintToConsole(string formattedMessage, ConsoleColor color, int suppressionBehavior)
        {
            switch (suppressionBehavior)
            {
                case 0:
                    WriteConsoleAndFile(formattedMessage, color);
                    break;

                case 1:
                    WriteConsoleAndFile($"[SUBSEQUENT MESSAGES SUPPRESSED DUE TO {DebugModeHandler.MAXMSGREPEATS} MAX REPEATS] ", ConsoleColor.Magenta, appendNewLine: false);
                    WriteConsoleAndFile(formattedMessage, color);
                    break;

                case 2:
                    // Suppressed, do nothing.
                    break;

                default:
                    PrintError("Unknown suppression behavior int (not 0, 1, or 2).");
                    break;
            }
        }

        private static void WriteConsoleAndFile(string message, ConsoleColor color, bool appendNewLine = true)
        {
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

            LogFileHandler.AppendLog(message, appendNewLine);
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
