using System;

namespace op.io
{
    public static class DebugModeHandler
    {
        private static bool _debugMode = false; // True = Enabled, False = Disabled
        private static bool _needsFreshCheck = true; // True when a database check is needed, False otherwise

        public static bool DEBUGMODE
        {
            get => _debugMode;
            set
            {
                _debugMode = value;
                _needsFreshCheck = false; // Reset fresh check flag once the mode is manually set
            }
        }

        public static bool NeedsFreshCheck
        {
            get => _needsFreshCheck;
            set => _needsFreshCheck = value;
        }

        public static bool IsDebugEnabled()
        {
            if (_needsFreshCheck)
            {
                _needsFreshCheck = false; // Reset flag after performing a check

                // Default state: Disabled
                _debugMode = false;

                if (Core.ForceDebugMode)
                {
                    _debugMode = true;
                    DebugLogger.PrintDebug("Debug mode force-enabled by hardcoded flag.");
                }
                else
                {
                    DebugLogger.PrintDebug("Checking database for debug mode in case the hardcoded flag is not set...");
                    int databaseDebugMode = DatabaseConfig.LoadDebugSettings();

                    _debugMode = databaseDebugMode == 1;

                    if (_debugMode)
                        DebugLogger.PrintDebug("Debug mode is enabled in the database.");
                    else
                        DebugLogger.PrintDebug("Debug mode is not enabled in the database.");
                }

                DebugLogger.PrintDebug($"Final cached debug mode: {_debugMode}");
            }

            return _debugMode;
        }

        public static void SetDebugMode(bool newState)
        {
            if (Core.ForceDebugMode)
            {
                DebugLogger.PrintWarning("Cannot toggle debug mode while ForceDebugMode is enabled.");
                return;
            }

            DebugLogger.PrintDebug($"Setting debug mode to: {newState}");
            _debugMode = newState;
            _needsFreshCheck = false;

            int newStateInt = newState ? 1 : 0;
            DatabaseConfig.ToggleDebugMode(newStateInt); // Persist change to the database
        }
    }
}
