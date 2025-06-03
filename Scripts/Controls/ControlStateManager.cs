using System;
using System.Collections.Generic;

namespace op.io
{
    public static class ControlStateManager
    {
        private static Dictionary<string, bool> _switchStates = [];

        /// <summary>
        /// Retrieves the current state of a switch.
        /// </summary>
        public static bool GetSwitchState(string settingKey)
        {
            if (_switchStates.ContainsKey(settingKey))
                return _switchStates[settingKey];

            DebugLogger.PrintWarning($"Switch state for '{settingKey}' not found. Returning default: OFF.");
            return false; // Default state if not found
        }

        /// <summary>
        /// Sets the state of a switch (used by GameInitializer when loading settings).
        /// </summary>
        public static void SetSwitchState(string settingKey, bool state)
        {
            if (!_switchStates.ContainsKey(settingKey))
            {
                DebugLogger.PrintWarning($"Switch state for '{settingKey}' not found. Adding with default state: OFF.");
                _switchStates[settingKey] = false; // Default state
            }
            else
            {
                _switchStates[settingKey] = state;
            }
            SaveSwitchState(settingKey, _switchStates[settingKey]);
            DebugLogger.PrintDatabase($"Set switch state: {settingKey} = {(state ? "ON" : "OFF")}");
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
            try
            {
                DebugLogger.Print("SAVING SWITCH STATE");
                int switchState = isOn ? 1 : 0; // Convert boolean to integer for DB
                string query = @"
                    UPDATE ControlKey 
                    SET SwitchStartState = @switchState 
                    WHERE SettingKey = @settingKey AND InputType = 'Switch';
                ";

                var parameters = new Dictionary<string, object>
                {
                    { "@switchState", switchState },
                    { "@settingKey", settingKey }
                };

                DatabaseQuery.ExecuteNonQuery(query, parameters);
                DebugLogger.PrintDatabase($"Saved switch state: {settingKey} = {(isOn ? "ON" : "OFF")}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to save switch state for '{settingKey}': {ex.Message}");
            }
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
