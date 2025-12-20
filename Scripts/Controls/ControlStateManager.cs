using System;
using System.Collections.Generic;

namespace op.io
{
    public static class ControlStateManager
    {
        private static Dictionary<string, bool> _switchStates = [];
        private static Dictionary<string, bool> _switchStateBuffer = [];
        private static Dictionary<string, bool> _prevSwitchStates = [];
        private static readonly Dictionary<string, bool> _switchPersistence = new(StringComparer.OrdinalIgnoreCase);

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
        public static void SetSwitchState(string settingKey, bool state, string source = null)
        {
            bool isDockingSwitch = string.Equals(settingKey, "DockingMode", StringComparison.OrdinalIgnoreCase);
            bool hasPersistenceFlag = _switchPersistence.ContainsKey(settingKey);

            if (!ContainsSwitchState(settingKey))
            {
                int newState = DatabaseConfig.GetSetting("ControlKey", "SwitchStartState", settingKey, -1);
                if (newState < 0)
                {
                    newState = TypeConversionFunctions.BoolToInt(state);
                }

                if (isDockingSwitch)
                {
                    DockingDiagnostics.RecordSwitchInitialization(
                        "ControlStateManager.SetSwitchState",
                        TypeConversionFunctions.IntToBool(newState),
                        newState,
                        state,
                        hasPersistenceFlag,
                        source);
                }

                DebugLogger.Print($"Initializing {settingKey} switch with default state from database: {newState}");
                _switchStates[settingKey] = TypeConversionFunctions.IntToBool(newState); // State from DB
            }
            else
            {
                if (_switchStates[settingKey] != state)
                {
                    bool previousState = _switchStates[settingKey];
                    _switchStates[settingKey] = state;
                    bool shouldPersist = ShouldPersist(settingKey);

                    if (isDockingSwitch)
                    {
                        DockingDiagnostics.RecordSwitchChange(
                            "ControlStateManager.SetSwitchState",
                            previousState,
                            state,
                            shouldPersist,
                            source,
                            reason: "state-change");
                    }

                    if (shouldPersist)
                    {
                        DebugLogger.PrintDatabase($"Updated and saved {settingKey} to {state} from {!state}");  //  NEVER CALLS
                        SaveSwitchState(settingKey, _switchStates[settingKey]);
                    }
                    DispatchSwitchChange(settingKey, state);
                }
                else if (isDockingSwitch && !string.Equals(source, "SwitchStateScanner.Tick", StringComparison.OrdinalIgnoreCase))
                {
                    DockingDiagnostics.RecordNoOpSet(
                        "ControlStateManager.SetSwitchState",
                        state,
                        source,
                        reason: "state-unchanged");
                }
            }
            TriggerManager.PrimeTriggerIfTrue(settingKey, state);
        }

        /// <summary>
        /// Toggles the switch state and saves the new state to the database.
        /// </summary>
        public static void ToggleSwitchState(string settingKey)
        {
            SetSwitchState(settingKey, !_switchStates[settingKey], "ControlStateManager.ToggleSwitchState");
        }

        /// <summary>
        /// Saves the current state of a switch to the database.
        /// </summary>
        private static void SaveSwitchState(string settingKey, bool isOn)
        {
            DatabaseConfig.UpdateSetting("ControlKey", "SwitchStartState", settingKey, TypeConversionFunctions.BoolToInt(isOn));
        }

        private static bool ShouldPersist(string settingKey)
        {
            return _switchPersistence.TryGetValue(settingKey, out bool persist) && persist;
        }

        public static bool ContainsSwitchState(string settingKey)
        {
            return _switchStates.ContainsKey(settingKey);
        }

        public static IReadOnlyDictionary<string, bool> GetCachedSwitchStatesSnapshot()
        {
            return new Dictionary<string, bool>(_switchStates);
        }

        public static void RegisterSwitchPersistence(string settingKey, bool saveToBackend)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            _switchPersistence[settingKey] = saveToBackend;
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
                DispatchSwitchChange(settingKey, newState);
            }
            else
            {
                DebugLogger.PrintWarning($"Switch state for '{settingKey}' not found. Adding with state: {newState}");
                _switchStates[settingKey] = newState;
            }
        }

        private static void DispatchSwitchChange(string settingKey, bool state)
        {
            SwitchRegistry.NotifyConsumers(settingKey, state);
        }

        public static void LoadControlSwitchStates()
        {
            DebugLogger.PrintDatabase("Loading control switch states...");

            try
            {
                // Fetch all control keys with SwitchStartState from the database
                const string sql = "SELECT SettingKey, SwitchStartState, InputType, InputKey FROM ControlKey WHERE InputType IN ('SaveSwitch', 'NoSaveSwitch', 'Switch');";
                var result = DatabaseQuery.ExecuteQuery(sql);

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
                        object switchStateObj = row.TryGetValue("SwitchStartState", out object rawState) ? rawState : null;
                        int switchState = switchStateObj == null || switchStateObj == DBNull.Value ? 0 : Convert.ToInt32(switchStateObj);
                        bool switchStateBool = TypeConversionFunctions.IntToBool(switchState);
                        bool saveToBackend = !string.Equals(row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() : string.Empty, "NoSaveSwitch", StringComparison.OrdinalIgnoreCase);
                        string inputKey = row.TryGetValue("InputKey", out object inputObj) ? inputObj?.ToString() : string.Empty;

                        DockingDiagnostics.RecordRawControlState(
                            "ControlStateManager.LoadControlSwitchStates",
                            settingKey,
                            inputKey,
                            row.TryGetValue("InputType", out object typeLabelObj) ? typeLabelObj?.ToString() : string.Empty,
                            switchState,
                            switchStateBool,
                            saveToBackend,
                            note: "Bulk load");

                        RegisterSwitchPersistence(settingKey, saveToBackend);

                        // Store this information in ControlStateManager
                        SetSwitchState(settingKey, switchStateBool, "ControlStateManager.LoadControlSwitchStates");
                        DebugLogger.PrintDatabase($"Loaded switch state: {settingKey} = {(switchStateBool ? "ON" : "OFF")}");
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
    }
}
