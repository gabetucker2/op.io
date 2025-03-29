using System;

namespace op.io
{
    public static class DebugModeHandler
    {
        public static bool ForceDebugMode { get; private set; } = true;
        public static int DebugMode { get; private set; } = 2; // 0: Disabled, 1: Enabled, 2: Unknown

        private static bool isDebugModeChecked = false;

        public static void ResetDebugMode()
        {
            DebugMode = 2; // Forces a fresh check
            isDebugModeChecked = false;  // Reset cache flag
        }

        public static bool IsDebugEnabled()
        {
            if (ForceDebugMode)
            {
                DebugMode = 1;
                // Use PrintMeta to log forced debug mode enabled
                DebugLogger.PrintMeta("Debug mode forced enabled.");
                return true;
            }

            if (!isDebugModeChecked)
            {
                // Use PrintDebug for logging the unknown cache state and forcing recheck
                DebugLogger.PrintDebug("Cache is unknown, forcing database recheck...");
                DebugMode = DatabaseConfig.LoadDebugSettings() == 1 ? 1 : 0;  // Set DebugMode to 1 or 0 based on database
                isDebugModeChecked = true; // Cache the result to avoid future DB hits
            }

            return DebugMode == 1;
        }

        public static void ToggleDebugMode()
        {
            if (ForceDebugMode)
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
