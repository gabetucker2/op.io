using System;
using System.Runtime.InteropServices;

namespace op.io
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static bool ConsoleInitialized { get; set; }

        public static void InitializeConsole()
        {
            if (ConsoleInitialized)
                return;

            ConsoleInitialized = true;

            AllocConsole();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int width = 225;
                int height = 40;
                Console.SetBufferSize(width, height + 800);
                Console.SetWindowSize(width, height);
            }

            PrintQueuedMessages();
        }

        public static void ResetConsole()
        {
            if (ConsoleInitialized)
            {
                Console.Clear();
                ConsoleInitialized = false;
            }
        }

        public static void PrintQueuedMessages()
        {
            var logs = DebugLogger.FlushQueuedLogs();

            foreach (var log in logs)
            {
                string logMessage = $"[DEFERRED OUTPUT] {log.Item1}";           // Formatted message string
                ConsoleColor color = log.Item2;                                 // Console color for the message
                int suppressionBehavior = log.Item3;                            // Suppression behavior for the message

                DebugLogger.PrintToConsole(logMessage, color, suppressionBehavior);
            }
        }

    }
}
