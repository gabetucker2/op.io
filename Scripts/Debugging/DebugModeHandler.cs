using System;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;

namespace op.io
{
    public static class DebugModeHandler
    {
        private static int _debugMode = -1; // -1 = Not initialized, True = Enabled, False = Disabled
        private static int _maxMessageRepeats = -1; // -1 = Not initialized; Anything else = Initialized

        public static bool DEBUGENABLED
        {
            get
            {
                if (Core.ForceDebugMode)
                {
                    return true;
                }

                if (_debugMode == -1) // If not initialized before getting value, default to switch value, which pulled from the DB in GameInitializer
                {
                    _debugMode = TypeConversionFunctions.BoolToInt(ControlStateManager.GetSwitchState("DebugMode"));
                    DebugLogger.PrintDatabase($"Setting starting debug mode to switch value: {_debugMode}");
                }

                return TypeConversionFunctions.IntToBool(_debugMode);
            }
            set
            {
                if (Core.ForceDebugMode)
                {
                    DebugLogger.PrintWarning("Cannot toggle debug mode while ForceDebugMode is enabled.  Ignoring DEBUGMODE set request.");
                }
                else
                {
                    DebugLogger.PrintDebug($"Setting debug mode to: {value}");
                    _debugMode = TypeConversionFunctions.BoolToInt(value);

                    // Update switch state upon set, which will then automatically encode new value into DB
                    ControlStateManager.SetSwitchState("DebugMode", value);
                }
            }
        }

        public static int MAXMSGREPEATS
        {
            get
            {
                if (_maxMessageRepeats == -1)
                {
                    _maxMessageRepeats = DatabaseConfig.GetSetting("ControlSettings", "Value", "DebugMaxRepeats", 5);
                }
                return _maxMessageRepeats;
            }
            set
            {
                if (value < 0)
                {
                    DebugLogger.PrintWarning("Attempted to set maxMessageRepeats to a negative value. Defaulting to 3.");
                    _maxMessageRepeats = 3;
                }
                else
                {
                    DebugLogger.PrintDebug($"Setting MAXMSGREPEATS to: {value}");
                    _maxMessageRepeats = value;
                }
            }
        }
    }
}
