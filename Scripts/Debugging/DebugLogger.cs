using System;
using System.Collections.Generic;

namespace op.io
{
    public static class DebugLogger
    {
        // This list holds all queued logs until the console is initialized
        private static readonly List<Tuple<string, ConsoleColor, int>> queuedLogs = new();
        private static bool ConsoleInitialized = false;

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

        public static void Log(string rawMessage, string level, ConsoleColor color)
        {
            if (DebugManager.IsLoggingInternally)
                return;

            DebugManager.IsLoggingInternally = true;

            string formattedMessage = LogFormatter.FormatLogMessage(rawMessage, level); // Combine level with message
            LogFormatter.IncrementMessageCount(formattedMessage);

            int suppressionBehavior = LogFormatter.SuppressMessageBehavior(formattedMessage);

            if (!ConsoleManager.ConsoleInitialized)
            {
                QueueLog(formattedMessage, color, suppressionBehavior); // Store suppressionBehavior in the queue
                DebugManager.IsLoggingInternally = false;
                return;
            }

            PrintToConsole(formattedMessage, color, suppressionBehavior);
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
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unknown suppression behavior int (not 0, 1, or 2).");
                    Console.ResetColor();
                    break;
            }
        }

        public static void InitializeConsole()
        {
            if (!ConsoleInitialized)
            {
                ConsoleManager.InitializeConsole();
                ConsoleInitialized = true;
                ConsoleManager.PrintQueuedMessages();
            }
        }

        // Public-facing logging methods for different log levels
        public static void Print(string message) => Log(message, "GENERAL", ConsoleColor.White);
        public static void PrintTemporary(string message) => Log(message, "TEMPORARY", ConsoleColor.Green);
        public static void PrintError(string message) => Log(message, "ERROR", ConsoleColor.Red);
        public static void PrintWarning(string message) => Log(message, "WARNING", ConsoleColor.DarkYellow);
        public static void PrintDatabase(string message) => Log(message, "DATABASE", ConsoleColor.Blue);
        public static void PrintConsole(string message) => Log(message, "CONSOLE", ConsoleColor.DarkGray);
        public static void PrintUI(string message) => Log(message, "UI", ConsoleColor.DarkGray);
        public static void PrintObject(string message) => Log(message, "OBJECT", ConsoleColor.DarkGreen);
        public static void PrintPlayer(string message) => Log(message, "PLAYER", ConsoleColor.Cyan);
    }
}
