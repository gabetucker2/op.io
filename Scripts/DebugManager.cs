using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace op.io
{
    public static class DebugManager
    {
        private static Dictionary<string, int> messageCount = new Dictionary<string, int>();
        private static int maxMessageRepeats = 5; // Default, overridden by database.
        private static bool _isLoggingInternally = false;

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        private static bool _consoleInitialized = false;
        private static int debugMode = 2; // 0: disabled; 1: enabled; 2: unknown, sqlite check needed

        /// <summary>
        /// Initializes the debug console if debug mode is enabled.
        /// </summary>
        public static void InitializeConsoleIfEnabled()
        {
            if (_consoleInitialized)
                return;

            if (IsDebugEnabled() && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                maxMessageRepeats = BaseFunctions.GetValue<int>(
                    "DebugSettings", "MaxRepeats", "Setting", "General", 5, suppressLog: true
                );
                AllocConsole();
                int width = 225;
                int height = 40;
                Console.SetBufferSize(width, height+800);
                Console.SetWindowSize(width, height);
                PrintMeta("Debug console initialized.");
                _consoleInitialized = true;
            }
            else
            {
                PrintError("Debug console not initialized because this device does not use Windows OS."); // Just a formality... will never be called realistically
            }
        }

        public static bool IsDebugEnabled()
        {
            switch (debugMode)
            {
                case 0:
                    return false;
                case 1:
                    return true;
                case 2: // needs to be set
                    try
                    {
                        bool debugEnabled = BaseFunctions.GetValue<bool>(
                            "DebugSettings", "Enabled", "Setting", "General", false, suppressLog: true
                        );
                        debugMode = debugEnabled ? 1 : 0;
                        return debugEnabled;
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Failed to check if debug mode is enabled: {ex.Message}");
                        return false;
                    }
                default:
                    PrintError($"Debug mode var is set to invalid integer (not 0, 1, or 2)");
                    return false;
            }
        }

        /// <summary>
        /// Toggles debug mode while ensuring proper database update.
        /// </summary>
        public static void ToggleDebugMode()
        {
            try
            {
                bool currentState = IsDebugEnabled();
                bool newState = !currentState;

                if (currentState)
                {
                    PrintMeta("Debug mode disabled.");
                }

                DatabaseQuery.ExecuteNonQuery(
                    "UPDATE DebugSettings SET Value = @newState WHERE Setting = 'Enabled';",
                    new Dictionary<string, object> { { "@newState", newState } }
                );

                PrintMeta($"Debug mode {(newState ? "enabled" : "disabled")}.");
            }
            catch (Exception ex)
            {
                PrintError($"Failed to toggle debug mode: {ex.Message}");
            }
        }

        public static void PrintMeta(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            DebugLog(message, "META", suppress: true);
            Console.ResetColor();
        }

        public static void PrintInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            DebugLog(message, "INFO", suppress: true);
            Console.ResetColor();
        }

        public static void PrintDebug(string message)
        {
            Console.ForegroundColor = ConsoleColor.White;
            DebugLog(message, "DEBUG", suppress: true);
            Console.ResetColor();
        }

        public static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            DebugLog(message, "ERROR", suppress: true);
            Console.ResetColor();
        }

        public static void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            DebugLog(message, "WARNING", suppress: true);
            Console.ResetColor();
        }

        /// <summary>
        /// Logs a message with a specified log level, capturing the calling function and script.
        /// </summary>
        private static void DebugLog(string rawMessage, string level, bool suppress)
        {
            if (_isLoggingInternally)
                return;

            _isLoggingInternally = true;
            try
            {
                if (level != "META" && !IsDebugEnabled()) return;

                StackTrace stackTrace = new StackTrace(2, true);
                StackFrame frame = stackTrace.GetFrame(0);

                string callerFile = frame?.GetFileName();
                string callerMethod = frame?.GetMethod()?.Name;
                string callerFileName = callerFile != null ? System.IO.Path.GetFileName(callerFile) : "UnknownFile";
                string callerLocation = $"{callerFileName}:{callerMethod}";
                string lineBreakSub = " - ";
                string cleanedMessage = rawMessage.Replace("\r\n", lineBreakSub).Replace("\n\r", lineBreakSub).Replace("\r", lineBreakSub).Replace("\n", lineBreakSub);
                string logMessage = $"[{level}] [{callerLocation}] {cleanedMessage}";

                if (suppress)
                {
                    if (!messageCount.ContainsKey(logMessage))
                        messageCount[logMessage] = 0;

                    if (messageCount[logMessage] < maxMessageRepeats)
                    {
                        Console.WriteLine(logMessage);
                        messageCount[logMessage]++;
                    }
                    else if (messageCount[logMessage] == maxMessageRepeats)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[SUBSEQUENT IDENTICAL MESSAGES SUPPRESSED DUE TO {maxMessageRepeats} REPEATS] {logMessage}");
                        Console.ResetColor();
                        messageCount[logMessage]++;
                    }
                }
                else
                {
                    Console.WriteLine(logMessage);
                }
            }
            finally
            {
                _isLoggingInternally = false;
            }
        }

    }
}
