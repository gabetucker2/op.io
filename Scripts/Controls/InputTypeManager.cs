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

        private static bool _startupSnapshotCaptured;
        private static readonly HashSet<Keys> _startupHeldKeys = new();
        private const float StartupIgnoreSeconds = 1.0f;
        private const int StartupIgnoreFrames = 5;
        private static int _frameCounter;

        private static readonly Dictionary<Keys, bool> _triggerStates = [];
        private static readonly Dictionary<string, bool> _mouseSwitchStates = [];
        private static readonly Dictionary<Keys, bool> _keySwitchStates = [];
        private static readonly Dictionary<string, bool> _switchStateCache = [];
        private static readonly Dictionary<string, string> _settingKeyToInputKey = [];
        private static readonly Dictionary<string, List<string>> _inputKeyToSettingKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Keys, List<string>> _keyToComboBindings = new();
        private static readonly string[] _criticalSwitchSettings = ["DebugMode"];
        private static bool _switchStatesInitialized;

        private static readonly Dictionary<string, bool> _bindingSwitchStates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _bindingLastSwitchTime = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _bindingChordHeld = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _doubleTapAwaitingSecondTap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _doubleTapFirstTapTime = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _doubleTapChordHeld = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _singleToggleSuppressionUntil = new(StringComparer.OrdinalIgnoreCase);

        // Minimal combo suppression state
        private static readonly HashSet<Keys> _comboActiveKeys = new();
        private static readonly HashSet<Keys> _comboBreakGuard = new();
        internal static bool HasStableSnapshot => _frameCounter > 0;

        private static readonly Dictionary<string, Keys[]> _tokenKeyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Control"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Ctrl"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Shift"] = new[] { Keys.LeftShift, Keys.RightShift },
            ["Alt"] = new[] { Keys.LeftAlt, Keys.RightAlt },
            ["["] = new[] { Keys.OemOpenBrackets },
            ["]"] = new[] { Keys.OemCloseBrackets }
        };

        private static readonly Dictionary<string, float> _lastMouseSwitchTime = new();
        private static readonly Dictionary<Keys, float> _lastKeySwitchTime = new();
        private static readonly Dictionary<Keys, double> _lastKeyTriggerTime = new();
        private static readonly Dictionary<string, double> _lastMouseTriggerTime = new();

        // Ctrl-key release time for the post-release combo buffer
        private static float _ctrlKeyReleaseTime = -1f;

        // Scroll wheel accumulators — carry fractional notch deltas across frames
        // so each ScrollIncrement fires exactly one trigger.
        private static float _scrollUpAccumulator;
        private static float _scrollDownAccumulator;

        // Cache cooldown values to avoid redundant loading
        private static float? _cachedTriggerCooldown = null;
        private static float? _cachedSwitchCooldown = null;
        private static float? _cachedDoubleTapSuppressionSeconds = null;
        private const float DefaultDoubleTapSuppressionSeconds = 0.25f;
        private const string DoubleTapSuppressionSettingKey = "DoubleTapSuppressionSeconds";
        private static bool _hasPreviousState;
        private static bool IsFocusBlocked() => FocusModeManager.IsFocusModeActive && InputManager.IsFocusModeBlocking();

        public static void InitializeControlStates()
        {
            if (_switchStatesInitialized)
            {
                EnsureCriticalSwitchStates();
                return;
            }

            try
            {
                const string sql = "SELECT SettingKey, InputKey, InputType, SwitchStartState FROM ControlKey WHERE InputType IN ('SaveSwitch', 'NoSaveSwitch', 'DoubleTapToggle', 'Switch', 'SaveEnum', 'NoSaveEnum');";
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
                            continue;
                        }

                        string inputTypeLabel = row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() : string.Empty;

                        // Enum types use trigger semantics: register for scanning but skip switch state seeding.
                        if (string.Equals(inputTypeLabel, "SaveEnum", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(inputTypeLabel, "NoSaveEnum", StringComparison.OrdinalIgnoreCase))
                        {
                            _switchStateCache[settingKey] = false;
                            _settingKeyToInputKey[settingKey] = inputKey;
                            RegisterInputKeyForSetting(settingKey, inputKey);
                            RegisterComboKeyMembership(settingKey, inputKey);
                            DebugLogger.PrintDebug($"Registered enum control '{settingKey}' ({inputKey}) for scanning.");
                            continue;
                        }

                        bool saveToBackend = !IsNoSaveSwitch(inputTypeLabel);
                        ControlStateManager.RegisterSwitchPersistence(settingKey, saveToBackend);

                        int switchState = 0;
                        if (row.TryGetValue("SwitchStartState", out object switchStateObj) && switchStateObj != null && switchStateObj != DBNull.Value)
                        {
                            switchState = Convert.ToInt32(switchStateObj);
                        }
                        bool isOn = TypeConversionFunctions.IntToBool(switchState);

                        DockingDiagnostics.RecordRawControlState(
                            "InputTypeManager.InitializeControlStates",
                            settingKey,
                            inputKey,
                            inputTypeLabel,
                            switchState,
                            isOn,
                            saveToBackend,
                            note: "Initial switch cache hydrate");
                        _switchStateCache[settingKey] = isOn;
                        _settingKeyToInputKey[settingKey] = inputKey;
                        RegisterInputKeyForSetting(settingKey, inputKey);
                        SeedBindingState(settingKey, isOn);
                        RegisterComboKeyMembership(settingKey, inputKey);

                        bool isComboInput = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries).Length > 1;
                        bool mapped = TrySeedSwitchState(inputKey, isOn);

                        if (DebugModeHandler.DEBUGENABLED)
                        {
                            if (mapped)
                            {
                                DebugLogger.PrintDebug($"Initialized switch state for '{settingKey}' ({inputKey}) to {(isOn ? "ON" : "OFF")}.");
                            }
                            else if (isComboInput)
                            {
                                DebugLogger.PrintDebug($"Initialized combo switch '{settingKey}' ({inputKey}) — state tracked via chord, not key cache.");
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
            if (IsFocusBlocked())
            {
                return false;
            }

            return Keyboard.GetState().IsKeyDown(key);
        }

        public static bool WasKeyHeld(Keys key)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

            return _previousKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Returns true if the Ctrl key was released within the configured CtrlBuffer window
        /// and is not currently held. Used to allow Ctrl+key combos to register slightly after Ctrl is lifted.
        /// </summary>
        public static bool IsCtrlWithinBuffer()
        {
            if (_ctrlKeyReleaseTime < 0f) return false;
            // Don't double-count: if Ctrl is held right now, IsHeld() already covers it.
            bool ctrlCurrentlyHeld = Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl);
            if (ctrlCurrentlyHeld) return false;
            float buffer = ControlStateManager.GetFloat(ControlKeyMigrations.CtrlBufferKey, 0.2f);
            return (Core.GAMETIME - _ctrlKeyReleaseTime) <= buffer;
        }

        public static bool IsMouseButtonHeld(string mouseKey)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

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
            if (IsFocusBlocked())
            {
                return false;
            }

            EnsurePreviousState();

            if (!_cachedTriggerCooldown.HasValue)
                LoadCooldownValues();

            KeyboardState currentState = Keyboard.GetState();

            if (!_triggerStates.ContainsKey(key))
                _triggerStates[key] = false;

            if (!_lastKeyTriggerTime.ContainsKey(key))
                _lastKeyTriggerTime[key] = 0;

            bool isCurrentlyPressed = currentState.IsKeyDown(key);
            bool wasPreviouslyPressed = _previousKeyboardState.IsKeyDown(key);
            bool isCooldownPassed = (Core.GAMETIME - _lastKeyTriggerTime[key]) >= _cachedTriggerCooldown.Value;

            bool withinStartup = _frameCounter < StartupIgnoreFrames || Core.GAMETIME < StartupIgnoreSeconds;

            if ((withinStartup && isCurrentlyPressed) && !_startupHeldKeys.Contains(key))
            {
                _startupHeldKeys.Add(key);
            }

            if (_startupHeldKeys.Contains(key))
            {
                if (!isCurrentlyPressed)
                {
                    _startupHeldKeys.Remove(key);
                    if (withinStartup)
                    {
                        return false;
                    }
                }

                if (isCurrentlyPressed)
                {
                    return false;
                }
            }

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
            if (IsFocusBlocked())
            {
                return false;
            }

            EnsurePreviousState();

            // Scroll events use the per-increment accumulator and bypass TriggerCooldown.
            if (string.Equals(mouseKey, "ScrollUp", StringComparison.OrdinalIgnoreCase))
            {
                float increment = ControlStateManager.GetFloat(ControlKeyMigrations.ScrollIncrementKey, 120f);
                if (increment <= 0f) increment = 120f;
                if (_scrollUpAccumulator >= increment)
                {
                    _scrollUpAccumulator -= increment;
                    return true;
                }
                return false;
            }

            if (string.Equals(mouseKey, "ScrollDown", StringComparison.OrdinalIgnoreCase))
            {
                float increment = ControlStateManager.GetFloat(ControlKeyMigrations.ScrollIncrementKey, 120f);
                if (increment <= 0f) increment = 120f;
                if (_scrollDownAccumulator >= increment)
                {
                    _scrollDownAccumulator -= increment;
                    return true;
                }
                return false;
            }

            MouseState currentMouseState = Mouse.GetState();

            if (!_lastMouseTriggerTime.ContainsKey(mouseKey))
                _lastMouseTriggerTime[mouseKey] = 0;

            if (!_cachedTriggerCooldown.HasValue)
                LoadCooldownValues();

            bool isCooldownPassed = (Core.GAMETIME - _lastMouseTriggerTime[mouseKey]) >= _cachedTriggerCooldown.Value;
            bool triggered = false;

            if (string.Equals(mouseKey, "LeftClick", StringComparison.OrdinalIgnoreCase) && currentMouseState.LeftButton == ButtonState.Released &&
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

        public static bool IsMouseButtonTapReleased(string mouseKey)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

            EnsurePreviousState();

            if (string.IsNullOrWhiteSpace(mouseKey))
            {
                return false;
            }

            // Scroll taps are treated as per-increment release events so they can participate
            // in double-tap detection consistently with trigger/switch behavior.
            if (string.Equals(mouseKey, "ScrollUp", StringComparison.OrdinalIgnoreCase))
            {
                float increment = ControlStateManager.GetFloat(ControlKeyMigrations.ScrollIncrementKey, 120f);
                if (increment <= 0f) increment = 120f;
                if (_scrollUpAccumulator >= increment)
                {
                    _scrollUpAccumulator -= increment;
                    return true;
                }

                return false;
            }

            if (string.Equals(mouseKey, "ScrollDown", StringComparison.OrdinalIgnoreCase))
            {
                float increment = ControlStateManager.GetFloat(ControlKeyMigrations.ScrollIncrementKey, 120f);
                if (increment <= 0f) increment = 120f;
                if (_scrollDownAccumulator >= increment)
                {
                    _scrollDownAccumulator -= increment;
                    return true;
                }

                return false;
            }

            MouseState currentMouseState = Mouse.GetState();

            return (string.Equals(mouseKey, "LeftClick", StringComparison.OrdinalIgnoreCase) &&
                    currentMouseState.LeftButton == ButtonState.Released &&
                    _previousMouseState.LeftButton == ButtonState.Pressed) ||
                   (string.Equals(mouseKey, "RightClick", StringComparison.OrdinalIgnoreCase) &&
                    currentMouseState.RightButton == ButtonState.Released &&
                    _previousMouseState.RightButton == ButtonState.Pressed) ||
                   (string.Equals(mouseKey, "MiddleClick", StringComparison.OrdinalIgnoreCase) &&
                    currentMouseState.MiddleButton == ButtonState.Released &&
                    _previousMouseState.MiddleButton == ButtonState.Pressed) ||
                   (string.Equals(mouseKey, "Mouse4", StringComparison.OrdinalIgnoreCase) &&
                    currentMouseState.XButton1 == ButtonState.Released &&
                    _previousMouseState.XButton1 == ButtonState.Pressed) ||
                   (string.Equals(mouseKey, "Mouse5", StringComparison.OrdinalIgnoreCase) &&
                    currentMouseState.XButton2 == ButtonState.Released &&
                    _previousMouseState.XButton2 == ButtonState.Pressed);
        }

        public static bool IsKeyTapReleased(Keys key)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

            EnsurePreviousState();

            KeyboardState current = Keyboard.GetState();
            bool isReleased = !current.IsKeyDown(key) && _previousKeyboardState.IsKeyDown(key);
            bool withinStartup = Core.GAMETIME < StartupIgnoreSeconds;

            if ((withinStartup && current.IsKeyDown(key)) && !_startupHeldKeys.Contains(key))
            {
                _startupHeldKeys.Add(key);
            }

            if (_startupHeldKeys.Contains(key))
            {
                if (!current.IsKeyDown(key))
                {
                    _startupHeldKeys.Remove(key);
                    if (withinStartup)
                    {
                        return false;
                    }
                }

                return false;
            }

            if (!_comboActiveKeys.Contains(key) && current.IsKeyDown(key) && (IsKeyPartOfHeldCombo(key) || IsKeyComboPartnerHeld(key, current)))
            {
                _comboActiveKeys.Add(key);
            }

            if (_comboActiveKeys.Contains(key))
            {
                if (!current.IsKeyDown(key))
                {
                    _comboActiveKeys.Remove(key);
                    _comboBreakGuard.Add(key);
                }

                return false;
            }

            if (_comboBreakGuard.Contains(key))
            {
                return false;
            }

            return isReleased;
        }

        public static bool IsMouseButtonSwitch(string mouseKey)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

            EnsurePreviousState();

            MouseState currentMouseState = Mouse.GetState();

            if (!_mouseSwitchStates.ContainsKey(mouseKey))
                _mouseSwitchStates[mouseKey] = false;

            if (!_lastMouseSwitchTime.ContainsKey(mouseKey))
                _lastMouseSwitchTime[mouseKey] = 0;

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

            if (shouldToggle && IsSingleToggleSuppressed(BuildMouseSuppressionToken(mouseKey)))
            {
                return _mouseSwitchStates[mouseKey];
            }

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
            if (IsFocusBlocked())
            {
                return false;
            }

            return _mouseSwitchStates.TryGetValue(mouseKey, out bool state) && state;
        }

        public static bool IsKeySwitch(Keys key)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

            EnsurePreviousState();

            KeyboardState current = Keyboard.GetState();

            if (!_keySwitchStates.ContainsKey(key))
                _keySwitchStates[key] = false;
            if (!_lastKeySwitchTime.ContainsKey(key))
                _lastKeySwitchTime[key] = 0;

            bool isReleased = !current.IsKeyDown(key) && _previousKeyboardState.IsKeyDown(key);
            bool withinStartup = Core.GAMETIME < StartupIgnoreSeconds;

            if ((withinStartup && current.IsKeyDown(key)) && !_startupHeldKeys.Contains(key))
            {
                _startupHeldKeys.Add(key);
            }

            if (_startupHeldKeys.Contains(key))
            {
                if (!current.IsKeyDown(key))
                {
                    _startupHeldKeys.Remove(key);
                    if (withinStartup)
                    {
                        return _keySwitchStates[key];
                    }
                }
                return _keySwitchStates[key];
            }

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
            if (!_cachedSwitchCooldown.HasValue)
                LoadCooldownValues();

            if (isReleased && (Core.GAMETIME - _lastKeySwitchTime[key] >= _cachedSwitchCooldown.Value))
            {
                if (IsSingleToggleSuppressed(BuildKeySuppressionToken(key)))
                {
                    return _keySwitchStates[key];
                }

                _keySwitchStates[key] = !_keySwitchStates[key];
                _lastKeySwitchTime[key] = Core.GAMETIME;
                if (_inputKeyToSettingKeys.TryGetValue(key.ToString(), out var linkedSettings) &&
                    linkedSettings.Any(sk => string.Equals(sk, "DockingMode", StringComparison.OrdinalIgnoreCase)))
                {
                    DockingDiagnostics.RecordInputEdge(
                        "InputTypeManager.IsKeySwitch",
                        key.ToString(),
                        _keySwitchStates[key],
                        context: $"withinStartup={withinStartup};comboGuard={_comboActiveKeys.Contains(key)};breakGuard={_comboBreakGuard.Contains(key)}",
                        note: "Primary token toggle (release edge)");
                }
                NotifySwitchStateFromInput(key.ToString(), _keySwitchStates[key]);
            }

            return _keySwitchStates[key];
        }

        public static bool PeekKeySwitchState(Keys key)
        {
            if (IsFocusBlocked())
            {
                return false;
            }

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

        public static void BeginFrame()
        {
            EnsurePreviousState();
            AccumulateScroll();
        }

        private static void AccumulateScroll()
        {
            if (!_hasPreviousState) return;
            MouseState current = Mouse.GetState();
            int delta = current.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (delta > 0) _scrollUpAccumulator += delta;
            else if (delta < 0) _scrollDownAccumulator += (-delta);

            // Drain accumulators when the cursor is over an unlocked block or docking mode
            // is active so scroll doesn't trigger game actions (camera zoom, etc.).
            if (BlockManager.ShouldSuppressScrollWheel())
            {
                _scrollUpAccumulator = 0;
                _scrollDownAccumulator = 0;
            }
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
                ControlStateManager.SetSwitchState(settingKey, state, $"InputTypeManager.NotifySwitchStateFromInput:{inputKey}");
                if (string.Equals(settingKey, "DockingMode", StringComparison.OrdinalIgnoreCase))
                {
                    DockingDiagnostics.RecordInputEdge(
                        "InputTypeManager.NotifySwitchStateFromInput",
                        inputKey,
                        state,
                        context: $"HasStableSnapshot={HasStableSnapshot}",
                        note: "Switch state updated from live input edge");
                }
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

            string inputTypeLabel = row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() : string.Empty;
            bool saveToBackend = !IsNoSaveSwitch(inputTypeLabel);
            ControlStateManager.RegisterSwitchPersistence(settingKey, saveToBackend);

            object switchStateObj = row.TryGetValue("SwitchStartState", out object rawState) ? rawState : null;
            int switchState = switchStateObj == null || switchStateObj == DBNull.Value ? 0 : Convert.ToInt32(switchStateObj);
            bool state = TypeConversionFunctions.IntToBool(switchState);

            DockingDiagnostics.RecordRawControlState(
                "InputTypeManager.LoadSwitchStateForSetting",
                settingKey,
                inputKey,
                inputTypeLabel,
                switchState,
                state,
                saveToBackend,
                note: "Fallback loader");
            _switchStateCache[settingKey] = state;
            _settingKeyToInputKey[settingKey] = inputKey;
            RegisterInputKeyForSetting(settingKey, inputKey);
            SeedBindingState(settingKey, state);
            TrySeedSwitchState(inputKey, state);
            ControlStateManager.SetSwitchState(settingKey, state, "InputTypeManager.LoadSwitchStateForSetting");
            return true;
        }

        private static bool TrySeedSwitchState(string inputKey, bool state)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return false;
            }

            bool isCombo = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries).Length > 1;
            if (isCombo)
            {
                // Combo bindings track their own switch state; avoid seeding shared key caches that could bleed across settings.
                return false;
            }

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

            if (TryParseTokenToKey(primaryToken, out Keys parsedKey))
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

            bool isCombo = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries).Length > 1;

            string primaryToken = GetPrimaryToken(inputKey);
            if (string.IsNullOrWhiteSpace(primaryToken))
            {
                return false;
            }

            bool updated = false;
            if (!isCombo && IsMouseInput(primaryToken))
            {
                _mouseSwitchStates[primaryToken] = state;
                updated = true;
            }
            else if (!isCombo && TryParseTokenToKey(primaryToken, out Keys parsedKey))
            {
                _keySwitchStates[parsedKey] = state;
                if (!_lastKeySwitchTime.ContainsKey(parsedKey))
                {
                    _lastKeySwitchTime[parsedKey] = 0;
                }
                _lastKeySwitchTime[parsedKey] = Core.GAMETIME;
                updated = true;
            }
            else if (isCombo)
            {
                // Combo bindings track their own state via _bindingSwitchStates; avoid sharing key-level switch flags.
                updated = true;
            }

            if (!updated)
            {
                return false;
            }

            _switchStateCache[settingKey] = state;
            if (string.Equals(settingKey, "DockingMode", StringComparison.OrdinalIgnoreCase))
            {
                DockingDiagnostics.RecordInputEdge(
                    "InputTypeManager.OverrideSwitchState",
                    inputKey,
                    state,
                    note: "Override invoked");
            }
            ControlStateManager.SetSwitchState(settingKey, state, "InputTypeManager.OverrideSwitchState");
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
            return string.Equals(inputTypeLabel, "NoSaveSwitch", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputTypeLabel, "DoubleTapToggle", StringComparison.OrdinalIgnoreCase);
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
            _cachedTriggerCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "TriggerCooldown");
            _cachedSwitchCooldown  = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "SwitchCooldown");
            _cachedDoubleTapSuppressionSeconds = DatabaseFetch.GetSetting(
                "ControlSettings",
                "Value",
                "SettingKey",
                DoubleTapSuppressionSettingKey,
                DefaultDoubleTapSuppressionSeconds);
            if (_cachedDoubleTapSuppressionSeconds <= 0f)
            {
                _cachedDoubleTapSuppressionSeconds = DefaultDoubleTapSuppressionSeconds;
            }
        }

        public static void Update()
        {
            KeyboardState currentKeyboardState = Keyboard.GetState();

            // Track when Ctrl is released so IsCtrlWithinBuffer() can detect the post-release window.
            bool ctrlWasHeld = _previousKeyboardState.IsKeyDown(Keys.LeftControl) || _previousKeyboardState.IsKeyDown(Keys.RightControl);
            bool ctrlIsHeld  = currentKeyboardState.IsKeyDown(Keys.LeftControl)   || currentKeyboardState.IsKeyDown(Keys.RightControl);
            if (ctrlWasHeld && !ctrlIsHeld)
                _ctrlKeyReleaseTime = Core.GAMETIME;

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = Mouse.GetState();
            _hasPreviousState = true;

            _frameCounter++;

            if (_comboBreakGuard.Count > 0)
            {
                ClearReleasedComboGuards();
            }
        }

        private static void EnsurePreviousState()
        {
            if (_hasPreviousState)
            {
                return;
            }

            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();

            if (!_startupSnapshotCaptured)
            {
                Keys[] held = _previousKeyboardState.GetPressedKeys();
                if (held != null)
                {
                    foreach (Keys key in held)
                    {
                        _startupHeldKeys.Add(key);
                    }
                }
                _startupSnapshotCaptured = true;
            }

            _hasPreviousState = true;
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

        /// <summary>
        /// Programmatically overrides the internal binding toggle state for a switch so the
        /// SwitchStateScanner reflects the forced state on the next tick instead of re-applying
        /// the old toggle value.
        /// </summary>
        public static void ForceSwitchBindingState(string settingKey, bool state)
        {
            if (string.IsNullOrWhiteSpace(settingKey)) return;
            EnsureBindingTracking(settingKey);
            _bindingSwitchStates[settingKey] = state;
        }

        public static bool EvaluateDoubleTapSwitchTap(
            string settingKey,
            bool tapDetected,
            string primaryMouseToken,
            IEnumerable<Keys> primaryKeyTokens)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            EnsureBindingTracking(settingKey);
            if (IsFocusBlocked())
            {
                return _bindingSwitchStates[settingKey];
            }

            return EvaluateDoubleTapTapInternal(settingKey, tapDetected, primaryMouseToken, primaryKeyTokens);
        }

        public static bool EvaluateComboDoubleTapSwitch(
            string settingKey,
            bool allTokensHeld,
            IEnumerable<Keys> chordKeys,
            string primaryMouseToken,
            IEnumerable<Keys> primaryKeyTokens)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            EnsureBindingTracking(settingKey);
            if (IsFocusBlocked())
            {
                return _bindingSwitchStates[settingKey];
            }

            bool wasFullyHeld = _doubleTapChordHeld.TryGetValue(settingKey, out bool held) && held;
            _doubleTapChordHeld[settingKey] = allTokensHeld;

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

                return EvaluateDoubleTapTapInternal(settingKey, tapDetected: false, primaryMouseToken, primaryKeyTokens);
            }

            if (!wasFullyHeld)
            {
                return EvaluateDoubleTapTapInternal(settingKey, tapDetected: false, primaryMouseToken, primaryKeyTokens);
            }

            if (chordKeys != null)
            {
                foreach (Keys key in chordKeys)
                {
                    if (key == Keys.None) continue;
                    _comboActiveKeys.Remove(key);
                    _comboBreakGuard.Add(key);
                }
            }

            return EvaluateDoubleTapTapInternal(settingKey, tapDetected: true, primaryMouseToken, primaryKeyTokens);
        }

        public static bool EvaluateComboSwitch(string settingKey, bool allTokensHeld, IEnumerable<Keys> chordKeys)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            EnsureBindingTracking(settingKey);
            if (IsFocusBlocked())
            {
                return _bindingSwitchStates[settingKey];
            }

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

        public static float DoubleTapSuppressionSeconds => GetDoubleTapSuppressionSeconds();

        private static bool EvaluateDoubleTapTapInternal(
            string settingKey,
            bool tapDetected,
            string primaryMouseToken,
            IEnumerable<Keys> primaryKeyTokens)
        {
            EnsureBindingTracking(settingKey);

            if (!_doubleTapAwaitingSecondTap.ContainsKey(settingKey))
            {
                _doubleTapAwaitingSecondTap[settingKey] = false;
            }

            if (!_doubleTapFirstTapTime.ContainsKey(settingKey))
            {
                _doubleTapFirstTapTime[settingKey] = -1f;
            }

            float now = Core.GAMETIME;
            float window = GetDoubleTapSuppressionSeconds();

            if (_doubleTapAwaitingSecondTap[settingKey])
            {
                float firstTapTime = _doubleTapFirstTapTime[settingKey];
                if (firstTapTime < 0f || (now - firstTapTime) > window)
                {
                    _doubleTapAwaitingSecondTap[settingKey] = false;
                    _doubleTapFirstTapTime[settingKey] = -1f;
                }
            }

            if (!tapDetected)
            {
                return _bindingSwitchStates[settingKey];
            }

            if (!_doubleTapAwaitingSecondTap[settingKey])
            {
                _doubleTapAwaitingSecondTap[settingKey] = true;
                _doubleTapFirstTapTime[settingKey] = now;
                ArmSingleToggleSuppression(primaryMouseToken, primaryKeyTokens, now + window);
                return _bindingSwitchStates[settingKey];
            }

            float elapsed = now - _doubleTapFirstTapTime[settingKey];
            if (elapsed <= window)
            {
                _doubleTapAwaitingSecondTap[settingKey] = false;
                _doubleTapFirstTapTime[settingKey] = -1f;
                _bindingSwitchStates[settingKey] = !_bindingSwitchStates[settingKey];
                _bindingLastSwitchTime[settingKey] = now;
                return _bindingSwitchStates[settingKey];
            }

            _doubleTapFirstTapTime[settingKey] = now;
            _doubleTapAwaitingSecondTap[settingKey] = true;
            ArmSingleToggleSuppression(primaryMouseToken, primaryKeyTokens, now + window);
            return _bindingSwitchStates[settingKey];
        }

        private static void ArmSingleToggleSuppression(string primaryMouseToken, IEnumerable<Keys> primaryKeyTokens, float suppressUntil)
        {
            if (!string.IsNullOrWhiteSpace(primaryMouseToken))
            {
                _singleToggleSuppressionUntil[BuildMouseSuppressionToken(primaryMouseToken)] = suppressUntil;
            }

            if (primaryKeyTokens == null)
            {
                return;
            }

            foreach (Keys key in primaryKeyTokens)
            {
                if (key == Keys.None)
                {
                    continue;
                }

                _singleToggleSuppressionUntil[BuildKeySuppressionToken(key)] = suppressUntil;
            }
        }

        private static bool IsSingleToggleSuppressed(string suppressionKey)
        {
            if (string.IsNullOrWhiteSpace(suppressionKey))
            {
                return false;
            }

            if (!_singleToggleSuppressionUntil.TryGetValue(suppressionKey, out float suppressUntil))
            {
                return false;
            }

            if (Core.GAMETIME <= suppressUntil)
            {
                return true;
            }

            _singleToggleSuppressionUntil.Remove(suppressionKey);
            return false;
        }

        private static string BuildKeySuppressionToken(Keys key)
        {
            return $"Key::{key}";
        }

        private static string BuildMouseSuppressionToken(string mouseToken)
        {
            return $"Mouse::{mouseToken?.Trim()}";
        }

        private static float GetDoubleTapSuppressionSeconds()
        {
            if (!_cachedDoubleTapSuppressionSeconds.HasValue)
            {
                LoadCooldownValues();
            }

            float configured = _cachedDoubleTapSuppressionSeconds ?? DefaultDoubleTapSuppressionSeconds;
            if (configured <= 0f)
            {
                return DefaultDoubleTapSuppressionSeconds;
            }

            return configured;
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

            if (!_doubleTapAwaitingSecondTap.ContainsKey(settingKey))
            {
                _doubleTapAwaitingSecondTap[settingKey] = false;
            }

            if (!_doubleTapFirstTapTime.ContainsKey(settingKey))
            {
                _doubleTapFirstTapTime[settingKey] = -1f;
            }

            if (!_doubleTapChordHeld.ContainsKey(settingKey))
            {
                _doubleTapChordHeld[settingKey] = false;
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
            _doubleTapAwaitingSecondTap[settingKey] = false;
            _doubleTapFirstTapTime[settingKey] = -1f;
            _doubleTapChordHeld[settingKey] = false;
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

        private static bool TryParseTokenToKey(string token, out Keys parsedKey)
        {
            parsedKey = Keys.None;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (_tokenKeyAliases.TryGetValue(token, out Keys[] aliasKeys) && aliasKeys is { Length: > 0 })
            {
                parsedKey = aliasKeys[0];
                return true;
            }

            return Enum.TryParse(token, true, out parsedKey);
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
