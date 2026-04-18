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

        // Float type state
        private static readonly Dictionary<string, float> _floatStates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _floatPersistence = new(StringComparer.OrdinalIgnoreCase);

        // Enum type state
        private static readonly HashSet<string> _enumTypes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> _enumIndices = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string[]> _enumOptions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _enumPersistence = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool[]> _enumDisabledOptions = new(StringComparer.OrdinalIgnoreCase);
        private const string EnumDisabledOptionsColumn = "EnumDisabledOptions";

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

        // ── Enum state API ────────────────────────────────────────────────────

        public static void RegisterEnumOptions(string settingKey, string[] options, int defaultIndex, bool persist)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || options == null || options.Length == 0)
            {
                return;
            }

            _enumTypes.Add(settingKey);
            _enumOptions[settingKey] = options;
            _enumPersistence[settingKey] = persist;

            int clampedDefault = Math.Clamp(defaultIndex, 0, options.Length - 1);
            bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, clampedDefault);
            int existingIndex = _enumIndices.TryGetValue(settingKey, out int cachedIndex) ? cachedIndex : clampedDefault;
            _enumIndices[settingKey] = ResolveEnumIndexForSelection(options.Length, existingIndex, clampedDefault, disabled);
        }

        public static void LoadEnumState(string settingKey, int index, bool persist, string encodedDisabledOptions = null)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            _enumTypes.Add(settingKey);
            _enumPersistence[settingKey] = persist;
            if (_enumOptions.TryGetValue(settingKey, out string[] options) && options.Length > 0)
            {
                bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, index, encodedDisabledOptions);
                index = ResolveEnumIndexForSelection(options.Length, index, 0, disabled);

                if (persist && !string.IsNullOrWhiteSpace(encodedDisabledOptions))
                {
                    string normalizedEncoding = EncodeEnumDisabledOptions(disabled);
                    if (!string.Equals(normalizedEncoding, encodedDisabledOptions, StringComparison.Ordinal))
                    {
                        SaveEnumDisabledOptions(settingKey, normalizedEncoding);
                    }
                }
            }

            _enumIndices[settingKey] = index;
        }

        public static bool ContainsEnumState(string settingKey) =>
            _enumTypes.Contains(settingKey);

        public static int GetEnumIndex(string settingKey)
        {
            if (!_enumIndices.TryGetValue(settingKey, out int index))
            {
                return 0;
            }

            if (_enumOptions.TryGetValue(settingKey, out string[] options) && options.Length > 0)
            {
                bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, index);
                int resolved = ResolveEnumIndexForSelection(options.Length, index, 0, disabled);
                if (resolved != index)
                {
                    _enumIndices[settingKey] = resolved;
                }
                return resolved;
            }

            return index;
        }

        public static string GetEnumValue(string settingKey)
        {
            if (!_enumOptions.TryGetValue(settingKey, out string[] options) || options.Length == 0)
            {
                return string.Empty;
            }

            int index = GetEnumIndex(settingKey);
            return options[Math.Clamp(index, 0, options.Length - 1)];
        }

        public static string[] GetEnumOptions(string settingKey)
        {
            return _enumOptions.TryGetValue(settingKey, out string[] options) ? options : System.Array.Empty<string>();
        }

        public static bool IsEnumOptionDisabled(string settingKey, string optionValue)
        {
            int index = FindEnumOptionIndex(settingKey, optionValue);
            return index >= 0 && IsEnumOptionDisabled(settingKey, index);
        }

        public static bool IsEnumOptionDisabled(string settingKey, int optionIndex)
        {
            if (!_enumOptions.TryGetValue(settingKey, out string[] options) || options.Length == 0)
            {
                return false;
            }

            int index = Math.Clamp(optionIndex, 0, options.Length - 1);
            bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, index);
            return disabled[index];
        }

        public static bool TrySetEnumValue(string settingKey, string optionValue, string source = null)
        {
            if (!_enumTypes.Contains(settingKey))
            {
                return false;
            }

            int index = FindEnumOptionIndex(settingKey, optionValue);
            if (index < 0 || IsEnumOptionDisabled(settingKey, index))
            {
                return false;
            }

            SetEnumIndex(settingKey, index, source ?? "ControlStateManager.TrySetEnumValue");
            return true;
        }

        public static bool TrySetEnumOptionDisabled(string settingKey, string optionValue, bool disabled, string source = null)
        {
            if (!_enumTypes.Contains(settingKey))
            {
                return false;
            }

            if (!_enumOptions.TryGetValue(settingKey, out string[] options) || options.Length == 0)
            {
                return false;
            }

            int optionIndex = FindEnumOptionIndex(settingKey, optionValue);
            if (optionIndex < 0)
            {
                return false;
            }

            bool[] disabledOptions = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, optionIndex);
            if (disabledOptions[optionIndex] == disabled)
            {
                return false;
            }

            int enabledCount = CountEnabledOptions(disabledOptions);
            if (disabled && enabledCount <= 1)
            {
                DebugLogger.PrintWarning($"Rejected enum disable for '{settingKey}:{optionValue}' because at least one option must remain enabled.");
                return false;
            }

            disabledOptions[optionIndex] = disabled;
            _enumDisabledOptions[settingKey] = disabledOptions;

            int currentIndex = GetEnumIndex(settingKey);
            if (disabled && currentIndex == optionIndex)
            {
                int fallbackIndex = GetNextEnabledEnumIndex(currentIndex, disabledOptions);
                _enumIndices[settingKey] = fallbackIndex;
                if (_enumPersistence.TryGetValue(settingKey, out bool persistCurrent) && persistCurrent)
                {
                    SaveEnumState(settingKey, fallbackIndex);
                }
            }

            if (_enumPersistence.TryGetValue(settingKey, out bool persist) && persist)
            {
                SaveEnumDisabledOptions(settingKey, EncodeEnumDisabledOptions(disabledOptions));
            }

            return true;
        }

        public static string GetEnumDisabledOptionsEncoded(string settingKey)
        {
            if (!_enumOptions.TryGetValue(settingKey, out string[] options) || options.Length == 0)
            {
                return string.Empty;
            }

            bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, GetEnumIndex(settingKey));
            return EncodeEnumDisabledOptions(disabled);
        }

        public static string GetAllEnumDisabledOptionsSummary()
        {
            if (_enumOptions.Count == 0)
            {
                return string.Empty;
            }

            List<string> segments = new();
            foreach (KeyValuePair<string, string[]> entry in _enumOptions)
            {
                if (entry.Value == null || entry.Value.Length == 0)
                {
                    continue;
                }

                bool[] disabled = GetOrInitializeEnumDisabledOptions(entry.Key, entry.Value.Length, GetEnumIndex(entry.Key));
                List<string> disabledNames = new();
                for (int i = 0; i < entry.Value.Length; i++)
                {
                    if (disabled[i])
                    {
                        disabledNames.Add(entry.Value[i]);
                    }
                }

                string summary = disabledNames.Count == 0 ? "none" : string.Join(",", disabledNames);
                segments.Add($"{entry.Key}[{summary}]");
            }

            return string.Join(" | ", segments);
        }

        public static int GetEnabledEnumOptionCount(string settingKey)
        {
            if (!_enumOptions.TryGetValue(settingKey, out string[] options) || options.Length == 0)
            {
                return 0;
            }

            bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, GetEnumIndex(settingKey));
            return CountEnabledOptions(disabled);
        }

        public static bool HasMultipleEnabledEnumOptions(string settingKey) =>
            GetEnabledEnumOptionCount(settingKey) > 1;

        public static void SetEnumIndex(string settingKey, int index, string source = null)
        {
            if (!_enumTypes.Contains(settingKey))
            {
                return;
            }

            if (_enumOptions.TryGetValue(settingKey, out string[] options) && options.Length > 0)
            {
                bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, options.Length, index);
                int fallback = _enumIndices.TryGetValue(settingKey, out int current) ? current : 0;
                index = ResolveEnumIndexForSelection(options.Length, index, fallback, disabled);
            }

            _enumIndices[settingKey] = index;
            if (_enumPersistence.TryGetValue(settingKey, out bool persist) && persist)
            {
                SaveEnumState(settingKey, index);
            }
        }

        public static void CycleEnum(string settingKey)
        {
            if (!_enumTypes.Contains(settingKey))
            {
                return;
            }

            int current = GetEnumIndex(settingKey);
            int optionCount = _enumOptions.TryGetValue(settingKey, out string[] options) ? options.Length : 0;
            if (optionCount <= 1)
            {
                return;
            }

            bool[] disabled = GetOrInitializeEnumDisabledOptions(settingKey, optionCount, current);
            int next = GetNextEnabledEnumIndex(current, disabled);
            if (next == current)
            {
                return;
            }

            SetEnumIndex(settingKey, next, "ControlStateManager.CycleEnum");
        }

        private static int FindEnumOptionIndex(string settingKey, string optionValue)
        {
            if (string.IsNullOrWhiteSpace(optionValue) ||
                !_enumOptions.TryGetValue(settingKey, out string[] options) ||
                options == null)
            {
                return -1;
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], optionValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool[] GetOrInitializeEnumDisabledOptions(string settingKey, int optionCount, int preferredEnabledIndex, string encoded = null)
        {
            if (optionCount <= 0)
            {
                return System.Array.Empty<bool>();
            }

            bool[] disabled;
            if (!string.IsNullOrWhiteSpace(encoded))
            {
                disabled = DecodeEnumDisabledOptions(encoded, optionCount);
            }
            else if (_enumDisabledOptions.TryGetValue(settingKey, out bool[] existing))
            {
                disabled = new bool[optionCount];
                int copyCount = Math.Min(optionCount, existing.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    disabled[i] = existing[i];
                }
            }
            else
            {
                disabled = new bool[optionCount];
            }

            NormalizeEnumDisabledOptions(disabled, preferredEnabledIndex);
            _enumDisabledOptions[settingKey] = disabled;
            return disabled;
        }

        private static void NormalizeEnumDisabledOptions(bool[] disabled, int preferredEnabledIndex)
        {
            if (disabled == null || disabled.Length == 0)
            {
                return;
            }

            if (CountEnabledOptions(disabled) > 0)
            {
                return;
            }

            int fallbackIndex = Math.Clamp(preferredEnabledIndex, 0, disabled.Length - 1);
            disabled[fallbackIndex] = false;
        }

        private static int CountEnabledOptions(bool[] disabled)
        {
            if (disabled == null || disabled.Length == 0)
            {
                return 0;
            }

            int enabled = 0;
            for (int i = 0; i < disabled.Length; i++)
            {
                if (!disabled[i])
                {
                    enabled++;
                }
            }

            return enabled;
        }

        private static int ResolveEnumIndexForSelection(int optionCount, int requestedIndex, int fallbackIndex, bool[] disabledOptions)
        {
            if (optionCount <= 0)
            {
                return 0;
            }

            int clampedRequested = Math.Clamp(requestedIndex, 0, optionCount - 1);
            int clampedFallback = Math.Clamp(fallbackIndex, 0, optionCount - 1);
            if (disabledOptions == null || disabledOptions.Length != optionCount)
            {
                return clampedRequested;
            }

            if (!disabledOptions[clampedRequested])
            {
                return clampedRequested;
            }

            if (!disabledOptions[clampedFallback])
            {
                return clampedFallback;
            }

            for (int i = 0; i < optionCount; i++)
            {
                if (!disabledOptions[i])
                {
                    return i;
                }
            }

            disabledOptions[clampedRequested] = false;
            return clampedRequested;
        }

        private static int GetNextEnabledEnumIndex(int currentIndex, bool[] disabledOptions)
        {
            if (disabledOptions == null || disabledOptions.Length == 0)
            {
                return 0;
            }

            int count = disabledOptions.Length;
            int current = Math.Clamp(currentIndex, 0, count - 1);

            for (int offset = 1; offset <= count; offset++)
            {
                int candidate = (current + offset) % count;
                if (!disabledOptions[candidate])
                {
                    return candidate;
                }
            }

            return current;
        }

        private static bool[] DecodeEnumDisabledOptions(string encoded, int optionCount)
        {
            bool[] disabled = new bool[optionCount];
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return disabled;
            }

            int len = Math.Min(optionCount, encoded.Length);
            for (int i = 0; i < len; i++)
            {
                disabled[i] = encoded[i] == '1';
            }

            return disabled;
        }

        private static string EncodeEnumDisabledOptions(bool[] disabled)
        {
            if (disabled == null || disabled.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = new char[disabled.Length];
            for (int i = 0; i < disabled.Length; i++)
            {
                chars[i] = disabled[i] ? '1' : '0';
            }

            return new string(chars);
        }

        private static void SaveEnumState(string settingKey, int index)
        {
            DatabaseConfig.UpdateSetting("ControlKey", "SwitchStartState", settingKey, index);
        }

        private static void SaveEnumDisabledOptions(string settingKey, string encoded)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            try
            {
                if (!ControlKeyData.ColumnExists(EnumDisabledOptionsColumn))
                {
                    return;
                }

                const string sql = "UPDATE ControlKey SET EnumDisabledOptions = @encoded WHERE SettingKey = @key;";
                var parameters = new Dictionary<string, object>
                {
                    ["@encoded"] = encoded ?? string.Empty,
                    ["@key"] = settingKey
                };
                DatabaseQuery.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to save enum disabled options for '{settingKey}': {ex.Message}");
            }
        }

        // ── Switch state API ──────────────────────────────────────────────────

        /// <summary>
        /// Sets the state of a switch (used by GameInitializer when loading settings).
        /// </summary>
        public static void SetSwitchState(string settingKey, bool state, string source = null)
        {
            // Enum types: true = cycle to next option, false = no-op
            if (_enumTypes.Contains(settingKey))
            {
                if (state) CycleEnum(settingKey);
                return;
            }
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

        // ── Float state API ───────────────────────────────────────────────────

        public static float GetFloat(string settingKey, float fallback = 1.0f)
        {
            return _floatStates.TryGetValue(settingKey, out float val) ? val : fallback;
        }

        public static bool ContainsFloatState(string settingKey) =>
            _floatStates.ContainsKey(settingKey);

        public static void SetFloat(string settingKey, float value)
        {
            if (string.IsNullOrWhiteSpace(settingKey)) return;
            _floatStates[settingKey] = value;
            if (_floatPersistence.TryGetValue(settingKey, out bool persist) && persist)
                SaveFloatState(settingKey, value);
        }

        private static void SaveFloatState(string settingKey, float value)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    ["@value"] = value,
                    ["@key"] = settingKey
                };
                const string sql = "UPDATE ControlKey SET FloatStartState = @value WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to save float state for '{settingKey}': {ex.Message}");
            }
        }

        public static void LoadFloatStates()
        {
            DebugLogger.PrintDatabase("Loading float control states...");
            try
            {
                const string sql = "SELECT SettingKey, FloatStartState FROM ControlKey WHERE InputType = 'Float';";
                var result = DatabaseQuery.ExecuteQuery(sql);
                foreach (var row in result)
                {
                    if (!row.TryGetValue("SettingKey", out object keyObj) || keyObj == null) continue;
                    string settingKey = keyObj.ToString();
                    float val = 1.0f;
                    if (row.TryGetValue("FloatStartState", out object floatObj) && floatObj != null && floatObj != DBNull.Value)
                    {
                        try { val = Convert.ToSingle(floatObj); } catch { }
                    }
                    _floatStates[settingKey] = val;
                    _floatPersistence[settingKey] = true;
                    DebugLogger.PrintDatabase($"Loaded float state: {settingKey} = {val}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load float states: {ex.Message}");
            }
        }

        public static void LoadControlSwitchStates()
        {
            DebugLogger.PrintDatabase("Loading control switch states...");

            try
            {
                // Fetch all control keys with SwitchStartState from the database
                bool hasEnumDisabledColumn = ControlKeyData.ColumnExists(EnumDisabledOptionsColumn);
                string sql = hasEnumDisabledColumn
                    ? "SELECT SettingKey, SwitchStartState, InputType, InputKey, COALESCE(EnumDisabledOptions, '') AS EnumDisabledOptions FROM ControlKey WHERE InputType IN ('SaveSwitch', 'NoSaveSwitch', 'Switch', 'SaveEnum', 'NoSaveEnum');"
                    : "SELECT SettingKey, SwitchStartState, InputType, InputKey, '' AS EnumDisabledOptions FROM ControlKey WHERE InputType IN ('SaveSwitch', 'NoSaveSwitch', 'Switch', 'SaveEnum', 'NoSaveEnum');";
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
                        string inputTypeLabel = row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() : string.Empty;
                        object switchStateObj = row.TryGetValue("SwitchStartState", out object rawState) ? rawState : null;
                        int switchState = switchStateObj == null || switchStateObj == DBNull.Value ? 0 : Convert.ToInt32(switchStateObj);
                        string inputKey = row.TryGetValue("InputKey", out object inputObj) ? inputObj?.ToString() : string.Empty;

                        bool isEnum = string.Equals(inputTypeLabel, "SaveEnum", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(inputTypeLabel, "NoSaveEnum", StringComparison.OrdinalIgnoreCase);

                        if (isEnum)
                        {
                            bool persist = !string.Equals(inputTypeLabel, "NoSaveEnum", StringComparison.OrdinalIgnoreCase);
                            string encodedDisabled = row.TryGetValue(EnumDisabledOptionsColumn, out object disabledObj)
                                ? disabledObj?.ToString() ?? string.Empty
                                : string.Empty;
                            LoadEnumState(settingKey, switchState, persist, encodedDisabled);
                            DebugLogger.PrintDatabase($"Loaded enum state: {settingKey} = index {switchState} ({GetEnumValue(settingKey)})");
                            continue;
                        }

                        bool switchStateBool = TypeConversionFunctions.IntToBool(switchState);
                        bool saveToBackend = !string.Equals(inputTypeLabel, "NoSaveSwitch", StringComparison.OrdinalIgnoreCase);

                        DockingDiagnostics.RecordRawControlState(
                            "ControlStateManager.LoadControlSwitchStates",
                            settingKey,
                            inputKey,
                            inputTypeLabel,
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
