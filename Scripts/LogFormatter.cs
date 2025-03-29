using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace op.io
{
    public static class LogFormatter
    {
        private static Dictionary<string, int> messageCount = new Dictionary<string, int>();
        private static int maxMessageRepeats;

        static LogFormatter()
        {
            maxMessageRepeats = DatabaseConfig.GetSetting<int>("DebugSettings", "MaxRepeats", "General", 5);
        }

        public static string FormatLogMessage(string rawMessage, string level)
        {
            // Use StackTrace(3, true) to go back to the original caller of the logging function
            StackTrace stackTrace = new StackTrace(3, true);
            StackFrame frame = stackTrace.GetFrame(0); // Get the relevant frame (caller)
            string callerFile = frame?.GetFileName();
            string callerMethod = frame?.GetMethod()?.Name;
            int callerLine = frame?.GetFileLineNumber() ?? -1;  // Get the line number of the caller

            string callerFileName = callerFile != null ? System.IO.Path.GetFileName(callerFile) : "UnknownFile";

            // Format the caller location in a cleaner way
            string callerLocation = $"{callerFileName}::{callerMethod} @ Line {callerLine}";

            // Return the formatted log message
            return $"[{level}] {rawMessage} | {callerLocation}";
        }

        // Increment the message count for a specific log message
        public static void IncrementMessageCount(string logMessage)
        {
            if (!messageCount.ContainsKey(logMessage))
            {
                messageCount[logMessage] = 0;
            }

            messageCount[logMessage]++;
        }

        // Check if a message should be suppressed based on the repeat count
        public static int SuppressMessageBehavior(string logMessage) // 0 = print normally; 1 = print suppresssion message; 2 = suppress and don't print suppression message
        {
            if (messageCount.ContainsKey(logMessage))
            {
                if (messageCount[logMessage] == maxMessageRepeats)
                {
                    return 1;
                }
                else if (messageCount[logMessage] > maxMessageRepeats)
                {
                    return 2;
                }
                else
                {
                    return 0;
                }
            }
            
            return 2;
        }

        // Get the maximum message repeats (from Database or Default)
        public static int GetMaxMessageRepeats()
        {
            return maxMessageRepeats;
        }

        // Reset all message counts (only call this when you want to reset the counters)
        public static void ResetMessageCount()
        {
            messageCount.Clear();
        }
    }
}
