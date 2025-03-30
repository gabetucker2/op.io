using System;

namespace op.io
{
    public static class DebugModeHandler
    {
        public static int DebugMode { get; set; } = 2; // 0: Disabled, 1: Enabled, 2: Unknown, needs fresh check

        public static void ResetDebugMode()
        {
            DebugMode = 2; // Forces a fresh check
        }

        public static bool IsDebugEnabled()
        {
            if (DebugMode == 2)
            {
                DebugMode = 0; // set to 0 by default; subsequent lines in the block will change to 1 if necessary

                if (Core.ForceDebugMode)
                {
                    DebugMode = 1;
                    DebugLogger.PrintConsole("Debug mode force-enabled by hardcoded flag.");
                }

                DebugLogger.PrintConsole("Checking database for debug mode it should use in case the hardcoded flag is set to false...");
                int databaseDebugMode = DatabaseConfig.LoadDebugSettings() == 1 ? 1 : 0;

                switch (databaseDebugMode == 1)
                {
                    case true:
                        DebugLogger.PrintConsole("Debug mode is enabled in the database.");
                        DebugMode = 1;
                        break;
                    case false:
                        DebugLogger.PrintConsole("Debug mode is not enabled in the database.");
                        break;
                }

                DebugLogger.PrintConsole($"Final cached debug mode: {DebugMode}");

            }

            return DebugMode == 1;
        }

        public static void ToggleDebugMode()
        {
            if (Core.ForceDebugMode)
            {
                // Use PrintError for logging error when trying to toggle with forceDebugMode enabled
                DebugLogger.PrintError("Cannot toggle debug mode while forceDebugMode is enabled.");
                return;
            }

            bool currentState = IsDebugEnabled();
            bool newState = !currentState;

            // Convert bool to int (true = 1, false = 0)
            int newStateInt = newState ? 1 : 0;

            DatabaseConfig.ToggleDebugMode(newStateInt);
            ResetDebugMode();
        }
    }
}
