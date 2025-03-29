using System;

namespace op.io
{
    public static class DebugManager
    {
        public static bool IsLoggingInternally { get; set; }

        public static void Reset()
        {
            // Reset all relevant counters and states
            LogFormatter.ResetMessageCount();
            DebugModeHandler.ResetDebugMode();
            ConsoleManager.ResetConsole();

            // Use DebugLogger's PrintMeta to log reset information
            DebugLogger.PrintMeta("DebugManager Reset - All variables reinitialized to default state.");
        }

        public static void InitializeConsoleIfEnabled()
        {
            Reset();

            // Use PrintDebug to indicate the attempt to initialize the console
            DebugLogger.PrintDebug("Initializing console...");

            if (ConsoleManager.ConsoleInitialized)
            {
                // If console already initialized, log with PrintDebug
                DebugLogger.PrintDebug("Console already initialized. Returning early.");
                return;
            }

            // Load MaxMessageRepeats from the database
            DatabaseConfig.LoadDebugSettings();

            if (DebugModeHandler.IsDebugEnabled())
            {
                ConsoleManager.InitializeConsole();
                // PrintMeta is used here to indicate the successful initialization of the console
                DebugLogger.PrintMeta("Console opened due to DebugSettings Enabled being set to true.");
            }
            else
            {
                // If debug mode is not enabled, do nothing
            }
        }

        public static void ToggleDebugMode()
        {
            // Using DebugLogger to handle debug mode toggle operations
            DebugModeHandler.ToggleDebugMode();
        }
    }
}
