using System;
using System.Collections.Generic;

namespace op.io
{
    public static class ControlStateManager
    {
        private static Dictionary<string, bool> _switchStates = [];
        private static Dictionary<string, bool> _switchStateBuffer = [];
        private static Dictionary<string, bool> _prevSwitchStates = [];

        public static void Tickwise_PrevSwitchTrackUpdate()
        {
            foreach (var kvp in _switchStateBuffer)
            {
                _prevSwitchStates[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in _switchStates)
            {
                _switchStateBuffer[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Retrieves the current state of a switch.
        /// </summary>
        public static bool GetSwitchState(string settingKey)
        {
            if (_switchStates.ContainsKey(settingKey))
            {
                return _switchStates[settingKey];
            }
            else
            {
                DebugLogger.PrintWarning($"Switch state for '{settingKey}' not found. Returning default: OFF.");
                return false;
            }
        }

        public static bool GetPrevTickSwitchState(string settingKey)
        {
            if (_prevSwitchStates.ContainsKey(settingKey))
            {
                return _prevSwitchStates[settingKey];
            }
            else
            {
                DebugLogger.PrintWarning($"Prev switch state for '{settingKey}' not found. Returning default: OFF.");
                return false;
            }
        }

        /// <summary>
        /// Sets the state of a switch (used by GameInitializer when loading settings).
        /// </summary>
        public static void SetSwitchState(string settingKey, bool state)
        {
            if (!ContainsSwitchState(settingKey))
            {
                int newState = DatabaseConfig.GetSetting("ControlKey", "SwitchStartState", settingKey, -1);
                DebugLogger.Print($"Initializing {settingKey} switch with default state from database: {newState}");
                _switchStates[settingKey] = TypeConversionFunctions.IntToBool(newState); // State from DB
            }
            else
            {
                if (_switchStates[settingKey] != state)
                {
                    _switchStates[settingKey] = state;
                    DebugLogger.PrintDatabase($"Updated and saved {settingKey} to {state} from {!state}");  //  NEVER CALLS
                    SaveSwitchState(settingKey, _switchStates[settingKey]);
                }
            }
            TriggerManager.PrimeTriggerIfTrue(settingKey, state);
        }

        /// <summary>
        /// Toggles the switch state and saves the new state to the database.
        /// </summary>
        public static void ToggleSwitchState(string settingKey)
        {
            SetSwitchState(settingKey, !_switchStates[settingKey]);
        }

        /// <summary>
        /// Saves the current state of a switch to the database.
        /// </summary>
        private static void SaveSwitchState(string settingKey, bool isOn)
        {
            DatabaseConfig.UpdateSetting("ControlKey", "SwitchStartState", settingKey, TypeConversionFunctions.BoolToInt(isOn));
        }

        public static bool ContainsSwitchState(string settingKey)
        {
            return _switchStates.ContainsKey(settingKey);
        }

        /// <summary>
        /// Updates the switch state based on user input or game logic.
        /// </summary>
        public static void UpdateSwitchState(string settingKey, bool newState)
        {
            if (_switchStates.ContainsKey(settingKey))
            {
                _switchStates[settingKey] = newState;
                DebugLogger.PrintDatabase($"Updated switch state: {settingKey} = {(newState ? "ON" : "OFF")}");
            }
            else
            {
                DebugLogger.PrintWarning($"Switch state for '{settingKey}' not found. Adding with state: {newState}");
                _switchStates[settingKey] = newState;
            }
        }
    }
}
