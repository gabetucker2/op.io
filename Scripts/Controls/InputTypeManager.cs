using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputTypeManager
    {
        private static KeyboardState _previousKeyboardState;
        private static MouseState _previousMouseState;

        private static readonly Dictionary<Keys, bool> _triggerStates = [];
        private static readonly Dictionary<string, bool> _mouseSwitchStates = [];
        private static readonly Dictionary<Keys, bool> _keySwitchStates = [];
        private static readonly Dictionary<string, bool> _switchStateCache = [];
        private static readonly Dictionary<string, string> _settingKeyToInputKey = [];
        private static readonly Dictionary<string, List<string>> _inputKeyToSettingKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Keys, List<string>> _keyToComboBindings = new();
        private static readonly string[] _criticalSwitchSettings = ["DebugMode", "AllowGameInputFreeze"];
        private static bool _switchStatesInitialized;

        private static readonly Dictionary<string, bool> _bindingSwitchStates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _bindingLastSwitchTime = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _bindingChordHeld = new(StringComparer.OrdinalIgnoreCase);

        // Minimal combo suppression state
        private static readonly HashSet<Keys> _comboActiveKeys = new();
        private static readonly HashSet<Keys> _comboBreakGuard = new();

        private static readonly Dictionary<string, Keys[]> _tokenKeyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Control"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Ctrl"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Shift"] = new[] { Keys.LeftShift, Keys.RightShift },
            ["Alt"] = new[] { Keys.LeftAlt, Keys.RightAlt }
        };

        private static readonly Dictionary<string, float> _lastMouseSwitchTime = new();
        private static readonly Dictionary<Keys, float> _lastKeySwitchTime = new();
        private static readonly Dictionary<Keys, double> _lastKeyTriggerTime = new();
        private static readonly Dictionary<string, double> _lastMouseTriggerTime = new();

        // Cache cooldown values to avoid redundant loading
        private static float? _cachedTriggerCooldown = null;
        private static float? _cachedSwitchCooldown = null;

        public static void InitializeControlStates()
        {
            if (_switchStatesInitialized)
            {
                EnsureCriticalSwitchStates();
                return;
            }

            try
            {
                const string sql = "SELECT SettingKey, InputKey, InputType, SwitchStartState FROM ControlKey WHERE InputType IN ('SaveSwitch', 'NoSaveSwitch', 'Switch');";
                var result = DatabaseQuery.ExecuteQuery(sql);

                if (result.Count == 0)
                {
                    DebugLogger.PrintWarning("No switch control states found in the database.");
                    EnsureCriticalSwitchStates();
                    return;
                }

                foreach (var row in result)
                {
                    if (row.ContainsKey("SettingKey") && row.ContainsKey("InputKey") && row.ContainsKey("SwitchStartState"))
                    {
                        string settingKey = row["SettingKey"]?.ToString();
                        string inputKey = row["InputKey"]?.ToString();

                        if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(inputKey))
                        {
                            DebugLogger.PrintWarning("Encountered a switch control with missing SettingKey or InputKey.");
                            continue;
                        }

                        string inputTypeLabel = row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() : string.Empty;
                        bool saveToBackend = !IsNoSaveSwitch(inputTypeLabel);
                        ControlStateManager.RegisterSwitchPersistence(settingKey, saveToBackend);

                        int switchState = 0;
                        if (row.TryGetValue("SwitchStartState", out object switchStateObj) && switchStateObj != null && switchStateObj != DBNull.Value)
                        {
                            switchState = Convert.ToInt32(switchStateObj);
                        }
                        bool isOn = TypeConversionFunctions.IntToBool(switchState);

                        _switchStateCache[settingKey] = isOn;
                        _settingKeyToInputKey[settingKey] = inputKey;
                        RegisterInputKeyForSetting(settingKey, inputKey);
                        SeedBindingState(settingKey, isOn);
                        RegisterComboKeyMembership(settingKey, inputKey);

                        bool mapped = TrySeedSwitchState(inputKey, isOn);

                        if (DebugModeHandler.DEBUGENABLED)
                        {
                            if (mapped)
                            {
                                DebugLogger.PrintDebug($"Initialized switch state for '{settingKey}' ({inputKey}) to {(isOn ? "ON" : "OFF")}.");
                            }
                            else
                            {
                                DebugLogger.PrintDebug($"Switch '{settingKey}' could not map input key '{inputKey}' into the runtime cache.");
                            }
                        }
                    }
                    else
                    {
                        DebugLogger.PrintWarning("Invalid row format when loading control switch states.");
                    }
                }

                _switchStatesInitialized = true;
                EnsureCriticalSwitchStates();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load control switch states: {ex.Message}");
            }
        }

        public static bool IsKeyHeld(Keys key)
        {
            return Keyboard.GetState().IsKeyDown(key);
        }

        public static bool IsMouseButtonHeld(string mouseKey)
        {
            MouseState currentMouseState = Mouse.GetState();

            if (string.IsNullOrWhiteSpace(mouseKey))
            {
                return false;
            }

            if (string.Equals(mouseKey, "LeftClick", StringComparison.OrdinalIgnoreCase))
            {
                return currentMouseState.LeftButton == ButtonState.Pressed;
            }

            if (string.Equals(mouseKey, "RightClick", StringComparison.OrdinalIgnoreCase))
            {
                return currentMouseState.RightButton == ButtonState.Pressed;
            }

            if (string.Equals(mouseKey, "MiddleClick", StringComparison.OrdinalIgnoreCase))
            {
                return currentMouseState.MiddleButton == ButtonState.Pressed;
            }

            if (string.Equals(mouseKey, "Mouse4", StringComparison.OrdinalIgnoreCase))
            {
                return currentMouseState.XButton1 == ButtonState.Pressed;
            }

            if (string.Equals(mouseKey, "Mouse5", StringComparison.OrdinalIgnoreCase))
            {
                return currentMouseState.XButton2 == ButtonState.Pressed;
            }

            return false;
        }

        public static bool IsKeyTriggered(Keys key)
        {
            if (Core.Instance.Player == null)
            {
                DebugLogger.PrintError("Player instance is null. Cannot check TriggerCooldown.");
                return false;
            }

            if (!_cachedTriggerCooldown.HasValue)
            {
                LoadCooldownValues();
            }

            KeyboardState currentState = Keyboard.GetState();

            if (!_triggerStates.ContainsKey(key))
                _triggerStates[key] = false;

            if (!_lastKeyTriggerTime.ContainsKey(key))
                _lastKeyTriggerTime[key] = 0;

            bool isCurrentlyPressed = currentState.IsKeyDown(key);
            bool wasPreviouslyPressed = _previousKeyboardState.IsKeyDown(key);
            bool isCooldownPassed = (Core.GAMETIME - _lastKeyTriggerTime[key]) >= _cachedTriggerCooldown.Value;

            // Trigger on key release (was down, now up) after cooldown
            if (!isCurrentlyPressed && wasPreviouslyPressed && isCooldownPassed)
            {
                _triggerStates[key] = true;
                _lastKeyTriggerTime[key] = Core.GAMETIME; // Reset trigger cooldown to the max cooldown value
                return true;
            }
            else
            {
                _triggerStates[key] = false;
            }

            return false;
        }

        public static bool IsMouseButtonTriggered(string mouseKey)
        {
            MouseState currentMouseState = Mouse.GetState();

            if (!_lastMouseTriggerTime.ContainsKey(mouseKey))
                _lastMouseTriggerTime[mouseKey] = 0;

            if (Core.Instance.Player == null)
            {
                DebugLogger.PrintError("Player instance is null. Cannot check TriggerCooldown.");
                return false;
            }

            // Lazy load TriggerCooldown if not cached
            if (!_cachedTriggerCooldown.HasValue)
            {
                LoadCooldownValues();
            }

            bool isCooldownPassed = (Core.GAMETIME - _lastMouseTriggerTime[mouseKey]) >= _cachedTriggerCooldown.Value;
            bool triggered = false;

            if (string.Equals(mouseKey, "ScrollUp", StringComparison.OrdinalIgnoreCase) && IsScrollUp(currentMouseState, _previousMouseState) && isCooldownPassed)
            {
                triggered = true;
            }
            else if (string.Equals(mouseKey, "ScrollDown", StringComparison.OrdinalIgnoreCase) && IsScrollDown(currentMouseState, _previousMouseState) && isCooldownPassed)
            {
                triggered = true;
            }
            else if (string.Equals(mouseKey, "LeftClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.LeftButton == ButtonState.Released &&
                     _previousMouseState.LeftButton == ButtonState.Pressed && isCooldownPassed)
            {
                triggered = true;
            }
            else if (string.Equals(mouseKey, "RightClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.RightButton == ButtonState.Released &&
                     _previousMouseState.RightButton == ButtonState.Pressed && isCooldownPassed)
            {
                triggered = true;
            }
            else if (string.Equals(mouseKey, "MiddleClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.MiddleButton == ButtonState.Released &&
                     _previousMouseState.MiddleButton == ButtonState.Pressed && isCooldownPassed)
            {
                triggered = true;
            }
            else if (string.Equals(mouseKey, "Mouse4", StringComparison.OrdinalIgnoreCase) && currentMouseState.XButton1 == ButtonState.Released &&
                     _previousMouseState.XButton1 == ButtonState.Pressed && isCooldownPassed)
            {
                triggered = true;
            }
            else if (string.Equals(mouseKey, "Mouse5", StringComparison.OrdinalIgnoreCase) && currentMouseState.XButton2 == ButtonState.Released &&
                     _previousMouseState.XButton2 == ButtonState.Pressed && isCooldownPassed)
            {
                triggered = true;
            }

            if (triggered)
            {
                _lastMouseTriggerTime[mouseKey] = Core.GAMETIME;
            }

            return triggered;
        }

        public static bool IsMouseButtonSwitch(string mouseKey)
        {
            MouseState currentMouseState = Mouse.GetState();

            if (!_mouseSwitchStates.ContainsKey(mouseKey))
                _mouseSwitchStates[mouseKey] = false;

            if (!_lastMouseSwitchTime.ContainsKey(mouseKey))
                _lastMouseSwitchTime[mouseKey] = 0;

            if (Core.Instance.Player == null)
            {
                DebugLogger.PrintError("Player instance is null. Cannot check SwitchCooldown.");
                return false;
            }

            // Lazy load SwitchCooldown if not cached
            if (!_cachedSwitchCooldown.HasValue)
            {
                LoadCooldownValues();
            }

            float elapsed = Core.GAMETIME - _lastMouseSwitchTime[mouseKey];
            bool cooldownPassed = elapsed >= _cachedSwitchCooldown.Value;

            bool shouldToggle =
                (string.Equals(mouseKey, "ScrollUp", StringComparison.OrdinalIgnoreCase) && IsScrollUp(currentMouseState, _previousMouseState) && cooldownPassed) ||
                (string.Equals(mouseKey, "ScrollDown", StringComparison.OrdinalIgnoreCase) && IsScrollDown(currentMouseState, _previousMouseState) && cooldownPassed) ||
                (string.Equals(mouseKey, "LeftClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.LeftButton == ButtonState.Released &&
                 _previousMouseState.LeftButton == ButtonState.Pressed && cooldownPassed) ||
                (string.Equals(mouseKey, "RightClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.RightButton == ButtonState.Released &&
                 _previousMouseState.RightButton == ButtonState.Pressed && cooldownPassed) ||
                (string.Equals(mouseKey, "MiddleClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.MiddleButton == ButtonState.Released &&
                 _previousMouseState.MiddleButton == ButtonState.Pressed && cooldownPassed) ||
                (string.Equals(mouseKey, "Mouse4", StringComparison.OrdinalIgnoreCase) && currentMouseState.XButton1 == ButtonState.Released &&
                 _previousMouseState.XButton1 == ButtonState.Pressed && cooldownPassed) ||
                (string.Equals(mouseKey, "Mouse5", StringComparison.OrdinalIgnoreCase) && currentMouseState.XButton2 == ButtonState.Released &&
                 _previousMouseState.XButton2 == ButtonState.Pressed && cooldownPassed);

            if (shouldToggle)
            {
                _mouseSwitchStates[mouseKey] = !_mouseSwitchStates[mouseKey];
                _lastMouseSwitchTime[mouseKey] = Core.GAMETIME; // Reset switch cooldown to the max cooldown value
                NotifySwitchStateFromInput(mouseKey, _mouseSwitchStates[mouseKey]);
            }

            return _mouseSwitchStates[mouseKey];
        }

        public static bool PeekMouseSwitchState(string mouseKey)
        {
            return _mouseSwitchStates.TryGetValue(mouseKey, out bool state) && state;
        }

        public static bool IsKeySwitch(Keys key)
        {
            KeyboardState current = Keyboard.GetState();

            if (!_keySwitchStates.ContainsKey(key))
                _keySwitchStates[key] = false;
            if (!_lastKeySwitchTime.ContainsKey(key))
                _lastKeySwitchTime[key] = 0;

            bool isReleased = !current.IsKeyDown(key) && _previousKeyboardState.IsKeyDown(key);

            // If any registered combo that uses this key is currently held, treat the key as part of the chord so its solo switch cannot toggle.
            if (!_comboActiveKeys.Contains(key) && current.IsKeyDown(key) && (IsKeyPartOfHeldCombo(key) || IsKeyComboPartnerHeld(key, current)))
            {
                _comboActiveKeys.Add(key);
            }

            // If chord was active for this key, guard until it is fully released.
            if (_comboActiveKeys.Contains(key))
            {
                if (!current.IsKeyDown(key))
                {
                    _comboActiveKeys.Remove(key);
                    _comboBreakGuard.Add(key);
                }
                return _keySwitchStates[key];
            }

            // After chord break, skip the first release edge.
            if (_comboBreakGuard.Contains(key))
            {
                return _keySwitchStates[key];
            }

            // Normal single-key switch toggle on release with cooldown.
            if (Core.Instance.Player == null)
            {
                DebugLogger.PrintError("Player instance is null. Cannot check SwitchCooldown.");
                return _keySwitchStates[key];
            }

            if (!_cachedSwitchCooldown.HasValue)
            {
                LoadCooldownValues();
            }

            if (isReleased && (Core.GAMETIME - _lastKeySwitchTime[key] >= _cachedSwitchCooldown.Value))
            {
                _keySwitchStates[key] = !_keySwitchStates[key];
                _lastKeySwitchTime[key] = Core.GAMETIME;
                NotifySwitchStateFromInput(key.ToString(), _keySwitchStates[key]);
            }

            return _keySwitchStates[key];
        }

        public static bool PeekKeySwitchState(Keys key)
        {
            return _keySwitchStates.TryGetValue(key, out bool state) && state;
        }

        public static void ConsumeKeys(IEnumerable<Keys> keys)
        {
            if (keys == null)
            {
                return;
            }

            foreach (Keys key in keys)
            {
                if (key == Keys.None)
                {
                    continue;
                }

                _comboActiveKeys.Add(key);
            }
        }

        public static void BeginFrame() { }

        private static void NotifySwitchStateFromInput(string inputKey, bool state)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return;
            }

            if (!_inputKeyToSettingKeys.TryGetValue(inputKey, out var settingKeys))
            {
                return;
            }

            foreach (string settingKey in settingKeys)
            {
                ControlStateManager.SetSwitchState(settingKey, state);
                _bindingSwitchStates[settingKey] = state;
            }
        }

        private static void EnsureCriticalSwitchStates()
        {
            foreach (string settingKey in _criticalSwitchSettings)
            {
                if (_switchStateCache.ContainsKey(settingKey))
                {
                    continue;
                }

                bool loaded = LoadSwitchStateForSetting(settingKey);

                if (!loaded && DebugModeHandler.DEBUGENABLED)
                {
                    DebugLogger.PrintDebug($"Critical switch '{settingKey}' missing from ControlKey. Defaulting to OFF.");
                }
            }

            if (!DebugModeHandler.DEBUGENABLED)
            {
                return;
            }

            foreach (string settingKey in _criticalSwitchSettings)
            {
                if (_switchStateCache.TryGetValue(settingKey, out bool cachedState))
                {
                    DebugLogger.PrintDebug($"Critical switch '{settingKey}' cached verification -> {(cachedState ? "ON" : "OFF")}.");
                }
                else
                {
                    DebugLogger.PrintDebug($"Critical switch '{settingKey}' unavailable even after fallback checks.");
                }
            }
        }

        private static bool LoadSwitchStateForSetting(string settingKey)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@settingKey", settingKey }
            };

            var rows = DatabaseQuery.ExecuteQuery("SELECT InputKey, SwitchStartState, InputType FROM ControlKey WHERE SettingKey = @settingKey LIMIT 1;", parameters);

            if (rows.Count == 0)
            {
                return false;
            }

            var row = rows[0];

            if (!row.ContainsKey("InputKey") || !row.ContainsKey("SwitchStartState"))
            {
                return false;
            }

            string inputKey = row["InputKey"]?.ToString();
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return false;
            }

            bool saveToBackend = !IsNoSaveSwitch(row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() : string.Empty);
            ControlStateManager.RegisterSwitchPersistence(settingKey, saveToBackend);

            object switchStateObj = row.TryGetValue("SwitchStartState", out object rawState) ? rawState : null;
            int switchState = switchStateObj == null || switchStateObj == DBNull.Value ? 0 : Convert.ToInt32(switchStateObj);
            bool state = TypeConversionFunctions.IntToBool(switchState);
            _switchStateCache[settingKey] = state;
            _settingKeyToInputKey[settingKey] = inputKey;
            RegisterInputKeyForSetting(settingKey, inputKey);
            SeedBindingState(settingKey, state);
            TrySeedSwitchState(inputKey, state);
            ControlStateManager.SetSwitchState(settingKey, state);
            return true;
        }

        private static bool TrySeedSwitchState(string inputKey, bool state)
        {
            string primaryToken = GetPrimaryToken(inputKey);
            if (string.IsNullOrWhiteSpace(primaryToken))
            {
                return false;
            }

            if (IsMouseInput(primaryToken))
            {
                _mouseSwitchStates[primaryToken] = state;
                return true;
            }

            if (Enum.TryParse(primaryToken, true, out Keys parsedKey))
            {
                _keySwitchStates[parsedKey] = state;
                return true;
            }

            return false;
        }

        private static void RegisterInputKeyForSetting(string settingKey, string inputKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(inputKey))
            {
                return;
            }

            AddInputKeyMapping(inputKey, settingKey);

            string primaryToken = GetPrimaryToken(inputKey);
            // Only map the primary token for single-key bindings to avoid sharing it across combos.
            bool isCombo = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries).Length > 1;
            if (!isCombo && !string.IsNullOrWhiteSpace(primaryToken))
            {
                AddInputKeyMapping(primaryToken, settingKey);
            }
        }

        public static void UpdateInputKeyMapping(string settingKey, string newInputKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(newInputKey))
            {
                return;
            }

            if (_settingKeyToInputKey.TryGetValue(settingKey, out string existing) && !string.IsNullOrWhiteSpace(existing))
            {
                RemoveInputKeyMapping(settingKey, existing);
            }

            _settingKeyToInputKey[settingKey] = newInputKey;
            RegisterInputKeyForSetting(settingKey, newInputKey);
            RegisterComboKeyMembership(settingKey, newInputKey);
        }

        public static void ClearInputKeyMapping(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            if (_settingKeyToInputKey.TryGetValue(settingKey, out string existing) && !string.IsNullOrWhiteSpace(existing))
            {
                RemoveInputKeyMapping(settingKey, existing);
            }

            _settingKeyToInputKey.Remove(settingKey);
        }

        public static IReadOnlyList<string> GetSettingKeysForInputKey(string inputKey)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return Array.Empty<string>();
            }

            if (_inputKeyToSettingKeys.TryGetValue(inputKey.Trim(), out var list) && list != null && list.Count > 0)
            {
                return list.ToList();
            }

            return Array.Empty<string>();
        }

        private static void RemoveInputKeyMapping(string settingKey, string inputKey)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return;
            }

            if (_inputKeyToSettingKeys.TryGetValue(inputKey, out var directList))
            {
                directList.Remove(settingKey);
                if (directList.Count == 0)
                {
                    _inputKeyToSettingKeys.Remove(inputKey);
                }
            }

            string primaryToken = GetPrimaryToken(inputKey);
            if (!string.IsNullOrWhiteSpace(primaryToken) && _inputKeyToSettingKeys.TryGetValue(primaryToken, out var primaryList))
            {
                primaryList.Remove(settingKey);
                if (primaryList.Count == 0)
                {
                    _inputKeyToSettingKeys.Remove(primaryToken);
                }
            }

            foreach (Keys key in GetKeysFromInputKey(inputKey))
            {
                if (_keyToComboBindings.TryGetValue(key, out var combos) && combos != null)
                {
                    combos.Remove(settingKey);
                    if (combos.Count == 0)
                    {
                        _keyToComboBindings.Remove(key);
                    }
                }
            }
        }

        private static void AddInputKeyMapping(string key, string settingKey)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!_inputKeyToSettingKeys.TryGetValue(key, out var settingList))
            {
                settingList = [];
                _inputKeyToSettingKeys[key] = settingList;
            }

            if (!settingList.Contains(settingKey))
            {
                settingList.Add(settingKey);
            }
        }

        public static IReadOnlyCollection<string> GetRegisteredSwitchKeys()
        {
            return _switchStateCache.Keys;
        }

        public static bool EnsureSwitchRegistration(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            if (_settingKeyToInputKey.ContainsKey(settingKey))
            {
                return true;
            }

            return LoadSwitchStateForSetting(settingKey);
        }

        public static bool OverrideSwitchState(string settingKey, bool state)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            if (!_switchStatesInitialized)
            {
                InitializeControlStates();
            }

            if (!_settingKeyToInputKey.TryGetValue(settingKey, out string inputKey) || string.IsNullOrWhiteSpace(inputKey))
            {
                return false;
            }

            string primaryToken = GetPrimaryToken(inputKey);
            if (string.IsNullOrWhiteSpace(primaryToken))
            {
                return false;
            }

            bool updated = false;
            if (IsMouseInput(primaryToken))
            {
                _mouseSwitchStates[primaryToken] = state;
                updated = true;
            }
            else if (Enum.TryParse(primaryToken, true, out Keys parsedKey))
            {
                _keySwitchStates[parsedKey] = state;
                if (!_lastKeySwitchTime.ContainsKey(parsedKey))
                {
                    _lastKeySwitchTime[parsedKey] = 0;
                }
                _lastKeySwitchTime[parsedKey] = Core.GAMETIME;
                updated = true;
            }

            if (!updated)
            {
                return false;
            }

            _switchStateCache[settingKey] = state;
            ControlStateManager.SetSwitchState(settingKey, state);
            SeedBindingState(settingKey, state);
            return true;
        }

        private static bool IsMouseInput(string inputKey)
        {
            return string.Equals(inputKey, "LeftClick", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "RightClick", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "MiddleClick", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "Mouse4", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "Mouse5", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "ScrollUp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "ScrollDown", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNoSaveSwitch(string inputTypeLabel)
        {
            return string.Equals(inputTypeLabel, "NoSaveSwitch", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPrimaryToken(string inputKey)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return string.Empty;
            }

            string[] tokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return inputKey.Trim();
            }

            return tokens[^1].Trim();
        }

        // Lazy load cooldown values from the Agent instance or database if not cached
        private static void LoadCooldownValues()
        {
            if (Core.Instance.Player != null)
            {
                // Load the cooldown values from the agent (or cache them)
                Core.Instance.Player.LoadTriggerCooldown();
                Core.Instance.Player.LoadSwitchCooldown();

                // Cache the values
                _cachedTriggerCooldown = Core.Instance.Player.TriggerCooldown;
                _cachedSwitchCooldown = Core.Instance.Player.SwitchCooldown;
            }
            else
            {
                DebugLogger.PrintError("Player instance is null. Cannot load cooldown values.");
            }
        }

        public static void Update()
        {
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            if (_comboBreakGuard.Count > 0)
            {
                ClearReleasedComboGuards();
            }
        }

        private static void ClearReleasedComboGuards()
        {
            List<Keys> toRemove = null;
            foreach (Keys key in _comboBreakGuard)
            {
                if (!_previousKeyboardState.IsKeyDown(key))
                {
                    toRemove ??= new List<Keys>();
                    toRemove.Add(key);
                }
            }

            if (toRemove == null)
            {
                return;
            }

            foreach (Keys key in toRemove)
            {
                _comboBreakGuard.Remove(key);
            }
        }

        public static bool EvaluateComboSwitch(string settingKey, bool allTokensHeld, IEnumerable<Keys> chordKeys)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            EnsureBindingTracking(settingKey);

            // Track whether the full chord was held in the last tick to trigger on chord break.
            bool wasFullyHeld = _bindingChordHeld.TryGetValue(settingKey, out bool held) && held;
            _bindingChordHeld[settingKey] = allTokensHeld;

            if (allTokensHeld)
            {
                if (chordKeys != null)
                {
                    foreach (Keys key in chordKeys)
                    {
                        if (key == Keys.None) continue;
                        _comboActiveKeys.Add(key);
                    }
                }

                return _bindingSwitchStates[settingKey];
            }

            if (!wasFullyHeld)
            {
                return _bindingSwitchStates[settingKey];
            }

            // Chord just broke; guard the chord keys until they are fully released.
            if (chordKeys != null)
            {
                foreach (Keys key in chordKeys)
                {
                    if (key == Keys.None) continue;
                    _comboActiveKeys.Remove(key);
                    _comboBreakGuard.Add(key);
                }
            }

            if (Core.Instance.Player == null)
            {
                DebugLogger.PrintError("Player instance is null. Cannot check SwitchCooldown.");
                return _bindingSwitchStates[settingKey];
            }

            if (!_cachedSwitchCooldown.HasValue)
            {
                LoadCooldownValues();
            }

            float lastSwitch = _bindingLastSwitchTime[settingKey];
            if ((Core.GAMETIME - lastSwitch) < _cachedSwitchCooldown.Value)
            {
                return _bindingSwitchStates[settingKey];
            }

            _bindingSwitchStates[settingKey] = !_bindingSwitchStates[settingKey];
            _bindingLastSwitchTime[settingKey] = Core.GAMETIME;

            return _bindingSwitchStates[settingKey];
        }

        private static void EnsureBindingTracking(string settingKey)
        {
            if (!_bindingSwitchStates.ContainsKey(settingKey))
            {
                if (ControlStateManager.ContainsSwitchState(settingKey))
                {
                    _bindingSwitchStates[settingKey] = ControlStateManager.GetSwitchState(settingKey);
                }
                else if (_switchStateCache.TryGetValue(settingKey, out bool cachedState))
                {
                    _bindingSwitchStates[settingKey] = cachedState;
                }
                else
                {
                    _bindingSwitchStates[settingKey] = false;
                }
            }

            if (!_bindingLastSwitchTime.ContainsKey(settingKey))
            {
                _bindingLastSwitchTime[settingKey] = 0;
            }

            if (!_bindingChordHeld.ContainsKey(settingKey))
            {
                _bindingChordHeld[settingKey] = false;
            }
        }

        private static void SeedBindingState(string settingKey, bool state)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            _bindingSwitchStates[settingKey] = state;
            if (!_bindingLastSwitchTime.ContainsKey(settingKey))
            {
                _bindingLastSwitchTime[settingKey] = 0;
            }

            _bindingChordHeld[settingKey] = false;
        }

        private static void RegisterComboKeyMembership(string settingKey, string inputKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(inputKey))
            {
                return;
            }

            bool isCombo = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries).Length > 1;
            if (!isCombo)
            {
                return;
            }

            foreach (Keys key in GetKeysFromInputKey(inputKey))
            {
                if (!_keyToComboBindings.TryGetValue(key, out var list))
                {
                    list = [];
                    _keyToComboBindings[key] = list;
                }

                if (!list.Contains(settingKey))
                {
                    list.Add(settingKey);
                }
            }
        }

        private static IEnumerable<Keys> GetKeysFromInputKey(string inputKey)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                yield break;
            }

            string[] tokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in tokens)
            {
                string token = raw.Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                foreach (Keys key in ParseTokenToKeys(token))
                {
                    yield return key;
                }
            }
        }

        private static IEnumerable<Keys> ParseTokenToKeys(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                yield break;
            }

            if (_tokenKeyAliases.TryGetValue(token, out Keys[] aliasKeys) && aliasKeys != null)
            {
                foreach (Keys aliasKey in aliasKeys)
                {
                    yield return aliasKey;
                }

                yield break;
            }

            if (Enum.TryParse(token, true, out Keys parsedKey))
            {
                yield return parsedKey;
            }
        }

        private static bool IsKeyPartOfHeldCombo(Keys key)
        {
            if (!_keyToComboBindings.TryGetValue(key, out var combos) || combos == null)
            {
                // Fallback: scan all known multi-token bindings in case this key was not registered.
                return IsKeyInAnyHeldComboByInputKeyScan(key);
            }

            foreach (string combo in combos)
            {
                if (_bindingChordHeld.TryGetValue(combo, out bool held) && held)
                {
                    return true;
                }

                if (_settingKeyToInputKey.TryGetValue(combo, out string inputKey) && AreAllTokensHeld(inputKey))
                {
                    return true;
                }
            }

            return IsKeyInAnyHeldComboByInputKeyScan(key);
        }

        // Extra defensive check: walk input key mappings to see if this key is part of any held multi-token binding.
        private static bool IsKeyInAnyHeldComboByInputKeyScan(Keys key)
        {
            foreach (var kvp in _settingKeyToInputKey)
            {
                string inputKey = kvp.Value;
                if (string.IsNullOrWhiteSpace(inputKey))
                {
                    continue;
                }

                string[] tokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length <= 1)
                {
                    continue; // Not a combo binding.
                }

                bool containsKey = false;
                foreach (string raw in tokens)
                {
                    string token = raw.Trim();
                    foreach (Keys parsed in ParseTokenToKeys(token))
                    {
                        if (parsed == key)
                        {
                            containsKey = true;
                            break;
                        }
                    }

                    if (containsKey)
                    {
                        break;
                    }
                }

                if (!containsKey)
                {
                    continue;
                }

                if (AreAllTokensHeld(inputKey))
                {
                    return true;
                }
            }

            return false;
        }

        // Checks whether any combo that includes this key has at least one of its partner tokens currently held.
        private static bool IsKeyComboPartnerHeld(Keys key, KeyboardState currentState)
        {
            List<string> candidateInputKeys = [];

            if (_keyToComboBindings.TryGetValue(key, out var combos) && combos != null)
            {
                foreach (string combo in combos)
                {
                    if (_settingKeyToInputKey.TryGetValue(combo, out string inputKey) && !string.IsNullOrWhiteSpace(inputKey))
                    {
                        candidateInputKeys.Add(inputKey);
                    }
                }
            }

            // Fallback: scan all known combo bindings for this key if the map was not populated.
            if (candidateInputKeys.Count == 0)
            {
                foreach (var kvp in _settingKeyToInputKey)
                {
                    string inputKey = kvp.Value;
                    if (string.IsNullOrWhiteSpace(inputKey))
                    {
                        continue;
                    }

                    string[] tokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length <= 1)
                    {
                        continue;
                    }

                    bool containsKey = false;
                    foreach (string raw in tokens)
                    {
                        string token = raw.Trim();
                        foreach (Keys parsed in ParseTokenToKeys(token))
                        {
                            if (parsed == key)
                            {
                                containsKey = true;
                                break;
                            }
                        }

                        if (containsKey)
                        {
                            break;
                        }
                    }

                    if (containsKey)
                    {
                        candidateInputKeys.Add(inputKey);
                    }
                }
            }

            foreach (string inputKey in candidateInputKeys)
            {
                string[] tokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length <= 1)
                {
                    continue;
                }

                bool partnerHeld = false;
                foreach (string raw in tokens)
                {
                    string token = raw.Trim();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    bool tokenIncludesKey = false;
                    foreach (Keys parsed in ParseTokenToKeys(token))
                    {
                        if (parsed == key)
                        {
                            tokenIncludesKey = true;
                            break;
                        }
                    }

                    if (tokenIncludesKey)
                    {
                        continue; // Skip the current key; we only care about partners.
                    }

                    foreach (Keys parsed in ParseTokenToKeys(token))
                    {
                        if (parsed != Keys.None && currentState.IsKeyDown(parsed))
                        {
                            partnerHeld = true;
                            break;
                        }
                    }

                    if (partnerHeld)
                    {
                        break;
                    }
                }

                if (partnerHeld)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreAllTokensHeld(string inputKey)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return false;
            }

            string[] tokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            foreach (string raw in tokens)
            {
                string token = raw.Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }

                if (IsMouseInput(token))
                {
                    if (!IsMouseTokenHeld(token))
                    {
                        return false;
                    }
                }
                else
                {
                    bool anyHeld = false;
                    foreach (Keys key in ParseTokenToKeys(token))
                    {
                        if (Keyboard.GetState().IsKeyDown(key))
                        {
                            anyHeld = true;
                            break;
                        }
                    }

                    if (!anyHeld)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsMouseTokenHeld(string token)
        {
            MouseState state = Mouse.GetState();
            return token.Equals("LeftClick", StringComparison.OrdinalIgnoreCase) ? state.LeftButton == ButtonState.Pressed
                 : token.Equals("RightClick", StringComparison.OrdinalIgnoreCase) ? state.RightButton == ButtonState.Pressed
                 : token.Equals("MiddleClick", StringComparison.OrdinalIgnoreCase) ? state.MiddleButton == ButtonState.Pressed
                 : token.Equals("Mouse4", StringComparison.OrdinalIgnoreCase) ? state.XButton1 == ButtonState.Pressed
                 : token.Equals("Mouse5", StringComparison.OrdinalIgnoreCase) ? state.XButton2 == ButtonState.Pressed
                 : false;
        }

        private static bool IsScrollUp(MouseState current, MouseState previous) => current.ScrollWheelValue > previous.ScrollWheelValue;
        private static bool IsScrollDown(MouseState current, MouseState previous) => current.ScrollWheelValue < previous.ScrollWheelValue;

    }
}
