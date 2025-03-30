using System;

namespace op.io
{
    public static class DebugManager
    {
        public static bool IsLoggingInternally { get; set; }

        public static void InitializeConsoleIfEnabled()
        {
            // Use PrintDebug to indicate the attempt to initialize the console
            DebugLogger.PrintDebug("Initializing console...");

            if (ConsoleManager.ConsoleInitialized)
            {
                // If console already initialized, log with PrintDebug
                DebugLogger.PrintDebug("Console already initialized. Returning early.");
                return;
            }

            if (DebugModeHandler.IsDebugEnabled())
            {
                ConsoleManager.InitializeConsole();

                DebugLogger.PrintMeta("Console opened due to debug mode being enabled.");
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
