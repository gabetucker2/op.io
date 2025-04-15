using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace op.io
{
    public static class InputTypeManager
    {
        private static KeyboardState _previousKeyboardState;
        private static MouseState _previousMouseState;

        private static readonly Dictionary<Keys, bool> _triggerStates = new();
        private static readonly Dictionary<string, bool> _mouseSwitchStates = new();
        private static readonly Dictionary<Keys, bool> _keySwitchStates = new();

        private static readonly Dictionary<string, float> _lastMouseSwitchTime = new();
        private static readonly Dictionary<Keys, float> _lastKeySwitchTime = new();
        private static readonly Dictionary<Keys, double> _lastKeyTriggerTime = new();
        private static readonly Dictionary<string, double> _lastMouseTriggerTime = new();

        // Cache cooldown values to avoid redundant loading
        private static float? _cachedTriggerCooldown = null;
        private static float? _cachedSwitchCooldown = null;

        public static void InitializeControlStates()
        {
            try
            {
                // Fetch all control keys with SwitchStartState from the database
                var result = DatabaseQuery.ExecuteQuery("SELECT SettingKey, SwitchStartState, InputType FROM ControlKey WHERE InputType = 'Switch';");

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

                        // Initialize switch states only for Switch type controls
                        if (row["InputType"].ToString() == "Switch")
                        {
                            _mouseSwitchStates[settingKey] = isOn;  // Store in _mouseSwitchStates or appropriate dictionary
                            DebugLogger.PrintDatabase($"Initialized switch state for '{settingKey}' to: {isOn}");
                        }
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
            }

            return _keySwitchStates[key];
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
