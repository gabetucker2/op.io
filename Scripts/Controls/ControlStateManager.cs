using System;
using System.Collections.Generic;

namespace op.io
{
    public static class ControlStateManager
    {
        private static Dictionary<string, bool> _switchStates = new Dictionary<string, bool>();

        /// <summary>
        /// Loads switch states from the database.
        /// </summary>
        public static void LoadSwitchStates()
        {
            DebugLogger.PrintDatabase("Loading control switch states...");

            try
            {
                var result = DatabaseQuery.ExecuteQuery("SELECT SettingKey, SwitchStartState FROM ControlKey WHERE InputType = 'Switch';");

                if (result.Count == 0)
                {
                    DebugLogger.PrintWarning("No switch control states found in the database.");
                    return;
                }

                foreach (var row in result)
                {
                    if (row.ContainsKey("SettingKey") && row.ContainsKey("SwitchStartState"))
                    {
                        string settingKey = row["SettingKey"].ToString();
                        int switchState = Convert.ToInt32(row["SwitchStartState"]);
                        bool isOn = switchState == 1;

                        _switchStates[settingKey] = isOn; // Store state in memory
                        DebugLogger.PrintDatabase($"Loaded switch state: {settingKey} = {(isOn ? "ON" : "OFF")}");
                    }
                    else
                    {
                        DebugLogger.PrintWarning("Invalid row format when loading control switch states.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load control switch states: {ex.Message}");
            }
        }

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
            _switchStates[settingKey] = state;
            DebugLogger.PrintDatabase($"Set switch state: {settingKey} = {(state ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Toggles the switch state and saves the new state to the database.
        /// </summary>
        public static void ToggleSwitchState(string settingKey)
        {
            if (!_switchStates.ContainsKey(settingKey))
            {
                DebugLogger.PrintWarning($"Switch state for '{settingKey}' not found. Adding with default state: OFF.");
                _switchStates[settingKey] = false;
            }

            _switchStates[settingKey] = !_switchStates[settingKey]; // Toggle state
            SaveSwitchState(settingKey, _switchStates[settingKey]);
        }

        /// <summary>
        /// Saves the current state of a switch to the database.
        /// </summary>
        private static void SaveSwitchState(string settingKey, bool isOn)
        {
            try
            {
                int switchState = isOn ? 1 : 0;
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

    }
}
