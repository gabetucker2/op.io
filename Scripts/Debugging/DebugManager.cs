using System;

namespace op.io
{
    public static class DebugManager
    {
        public static bool IsLoggingInternally { get; set; }

        public static void InitializeConsoleIfEnabled()
        {
            // Use Print to indicate the attempt to initialize the console
            DebugLogger.PrintConsole("Initializing console...");

            if (ConsoleManager.ConsoleInitialized)
            {
                // If console already initialized, log with Print
                DebugLogger.PrintConsole("Console already initialized. Returning early.");
                return;
            }

            if (DebugModeHandler.IsDebugEnabled())
            {
                ConsoleManager.InitializeConsole();

                DebugLogger.PrintConsole("Console opened due to debug mode being enabled.");
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
