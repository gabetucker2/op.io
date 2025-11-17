using System;
using System.Collections.Generic;
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
        private static readonly string[] _criticalSwitchSettings = ["Crouch", "DebugMode"];
        private static bool _switchStatesInitialized;

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
                var result = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType, SwitchStartState FROM ControlKey WHERE InputType = 'Switch';");

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

                        int switchState = Convert.ToInt32(row["SwitchStartState"]);
                        bool isOn = TypeConversionFunctions.IntToBool(switchState);

                        _switchStateCache[settingKey] = isOn;
                        _settingKeyToInputKey[settingKey] = inputKey;
                        RegisterInputKeyForSetting(settingKey, inputKey);

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

            return (mouseKey == "LeftClick" && currentMouseState.LeftButton == ButtonState.Pressed) ||
                   (mouseKey == "RightClick" && currentMouseState.RightButton == ButtonState.Pressed);
        }

        public static bool IsKeyTriggered(Keys key)
        {
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

            KeyboardState currentState = Keyboard.GetState();

            if (!_triggerStates.ContainsKey(key))
                _triggerStates[key] = false;

            if (!_lastKeyTriggerTime.ContainsKey(key))
                _lastKeyTriggerTime[key] = 0;

            bool isCurrentlyPressed = currentState.IsKeyDown(key);
            bool wasPreviouslyPressed = _previousKeyboardState.IsKeyDown(key);
            bool isCooldownPassed = (Core.GAMETIME - _lastKeyTriggerTime[key]) >= _cachedTriggerCooldown.Value;

            if (isCurrentlyPressed && !wasPreviouslyPressed && isCooldownPassed)
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

            bool isTriggered = false;
            bool isCooldownPassed = (Core.GAMETIME - _lastMouseTriggerTime[mouseKey]) >= _cachedTriggerCooldown.Value;

            if ((mouseKey == "LeftClick" && currentMouseState.LeftButton == ButtonState.Pressed &&
                 _previousMouseState.LeftButton == ButtonState.Released && isCooldownPassed) ||
                (mouseKey == "RightClick" && currentMouseState.RightButton == ButtonState.Pressed &&
                 _previousMouseState.RightButton == ButtonState.Released && isCooldownPassed))
            {
                isTriggered = true;
                _lastMouseTriggerTime[mouseKey] = Core.GAMETIME; // Reset trigger cooldown to the max cooldown value
            }

            return isTriggered;
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

            if ((mouseKey == "LeftClick" && currentMouseState.LeftButton == ButtonState.Pressed &&
                 _previousMouseState.LeftButton == ButtonState.Released &&
                 (Core.GAMETIME - _lastMouseSwitchTime[mouseKey] >= _cachedSwitchCooldown.Value)) ||
                (mouseKey == "RightClick" && currentMouseState.RightButton == ButtonState.Pressed &&
                 _previousMouseState.RightButton == ButtonState.Released &&
                 (Core.GAMETIME - _lastMouseSwitchTime[mouseKey] >= _cachedSwitchCooldown.Value)))
            {
                _mouseSwitchStates[mouseKey] = !_mouseSwitchStates[mouseKey];
                _lastMouseSwitchTime[mouseKey] = Core.GAMETIME; // Reset switch cooldown to the max cooldown value
                NotifySwitchStateFromInput(mouseKey, _mouseSwitchStates[mouseKey]);
            }

            return _mouseSwitchStates[mouseKey];
        }

        public static bool IsKeySwitch(Keys key)
        {
            KeyboardState currentState = Keyboard.GetState();

            if (!_keySwitchStates.ContainsKey(key))
                _keySwitchStates[key] = false;

            if (!_lastKeySwitchTime.ContainsKey(key))
                _lastKeySwitchTime[key] = 0;

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

            if (currentState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key) &&
                (Core.GAMETIME - _lastKeySwitchTime[key] >= _cachedSwitchCooldown.Value))
            {
                _keySwitchStates[key] = !_keySwitchStates[key];
                _lastKeySwitchTime[key] = Core.GAMETIME; // Reset switch cooldown to the max cooldown value
                NotifySwitchStateFromInput(key.ToString(), _keySwitchStates[key]);
            }

            return _keySwitchStates[key];
        }

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

            var rows = DatabaseQuery.ExecuteQuery("SELECT InputKey, SwitchStartState FROM ControlKey WHERE SettingKey = @settingKey LIMIT 1;", parameters);

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

            bool state = TypeConversionFunctions.IntToBool(Convert.ToInt32(row["SwitchStartState"]));
            _switchStateCache[settingKey] = state;
            _settingKeyToInputKey[settingKey] = inputKey;
            RegisterInputKeyForSetting(settingKey, inputKey);
            TrySeedSwitchState(inputKey, state);
            return true;
        }

        private static bool TrySeedSwitchState(string inputKey, bool state)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return false;
            }

            if (IsMouseInput(inputKey))
            {
                _mouseSwitchStates[inputKey] = state;
                return true;
            }

            if (Enum.TryParse(inputKey, true, out Keys parsedKey))
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

            if (!_inputKeyToSettingKeys.TryGetValue(inputKey, out var settingList))
            {
                settingList = [];
                _inputKeyToSettingKeys[inputKey] = settingList;
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

        private static bool IsMouseInput(string inputKey)
        {
            return string.Equals(inputKey, "LeftClick", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputKey, "RightClick", StringComparison.OrdinalIgnoreCase);
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
        }
    }
}
