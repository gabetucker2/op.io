using System;

namespace op.io
{
    public static class DebugManager
    {
        public static bool IsLoggingInternally { get; set; }

        public static void InitializeConsoleIfEnabled()
        {
            // Use Print to indicate the attempt to initialize the console
            DebugLogger.PrintDebug("Initializing console...");

            if (ConsoleManager.ConsoleInitialized)
            {
                // If console already initialized, log with Print
                DebugLogger.PrintDebug("Console already initialized. Returning early.");
                return;
            }

            if (DebugModeHandler.IsDebugEnabled())
            {
                ConsoleManager.InitializeConsole();

                DebugLogger.PrintDebug("Console opened due to debug mode being enabled.");
            }
            else
            {
                // If debug mode is not enabled, do nothing
            }

        }
    }
}
