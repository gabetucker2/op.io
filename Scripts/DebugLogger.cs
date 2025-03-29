using System;

namespace op.io
{
    public static class DebugLogger
    {
        private static void Log(string rawMessage, string level, ConsoleColor color)
        {
            if (DebugManager.IsLoggingInternally)
                return;

            DebugManager.IsLoggingInternally = true;

            try
            {
                if (ConsoleManager.ConsoleInitialized)
                {
                    string logMessage = LogFormatter.FormatLogMessage(rawMessage, level);

                    // Increment the message count for this specific log message
                    LogFormatter.IncrementMessageCount(logMessage);

                    // If the message count exceeds the max allowed repeats, suppress the message
                    switch (LogFormatter.SuppressMessageBehavior(logMessage))
                    {
                        case 0:

                            Console.ForegroundColor = color;
                            Console.WriteLine(logMessage);
                            Console.ResetColor();
                            return;

                        case 1:

                            // Split the suppression message and print the [SUBSEQUENT...] part in magenta
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.Write($"[SUBSEQUENT MESSAGES SUPPRESSED DUE TO {LogFormatter.GetMaxMessageRepeats()} MAX REPEATS] ");
                            Console.ResetColor();

                            // Print the rest of the message in the original color
                            Console.ForegroundColor = color;
                            Console.WriteLine(logMessage);
                            Console.ResetColor();

                            return;

                        case 2:
                            // Ignore printing suppression
                            return;
                        default:
                            PrintError("Unknown suppression behavior int (not 0, 1, or 2).");
                            return;
                    }
                }
            }
            finally
            {
                DebugManager.IsLoggingInternally = false;
            }
        }

        // Print functions for logging
        public static void PrintMeta(string message) => Log(message, "META", ConsoleColor.DarkGray);
        public static void PrintInfo(string message) => Log(message, "INFO", ConsoleColor.Blue);
        public static void PrintDebug(string message) => Log(message, "DEBUG", ConsoleColor.White);
        public static void PrintError(string message) => Log(message, "ERROR", ConsoleColor.Red);
        public static void PrintWarning(string message) => Log(message, "WARNING", ConsoleColor.DarkYellow);
    
    }
}
