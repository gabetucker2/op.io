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

        private static readonly Dictionary<string, bool> _switchStates = new();
        private static readonly Dictionary<string, double> _lastTriggerTime = new();
        private static readonly Dictionary<string, double> _lastSwitchTime = new();

        public static void Update()
        {
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public static bool IsInputActive(string settingKey)
        {
            if (InputManager._ControlKey.TryGetValue(settingKey, out var control))
            {
                string inputKey = control.inputKey;
                InputType inputType = control.inputType;

                return inputType switch
                {
                    InputType.Hold => IsHeld(inputKey),
                    InputType.Trigger => IsTriggered(inputKey),
                    InputType.Switch => IsSwitch(inputKey),
                    _ => false
                };
            }

            return false;
        }

        public static bool IsHeld(string inputKey)
        {
            var (_, parsedInput) = ParseInputKey(inputKey);
            return IsPressed(parsedInput);
        }

        public static bool IsTriggered(string inputKey)
        {
            var (keyType, parsedInput) = ParseInputKey(inputKey);

            if (!_lastTriggerTime.ContainsKey(inputKey))
                _lastTriggerTime[inputKey] = 0;

            bool isCooldownPassed = (Core.gameTime - _lastTriggerTime[inputKey]) >= Player.TriggerCooldown;

            bool isPressedNow = IsPressed(parsedInput);
            bool wasPressedBefore = WasPressed(parsedInput);

            if (isPressedNow && !wasPressedBefore && isCooldownPassed)
            {
                _lastTriggerTime[inputKey] = Core.gameTime;
                DebugLogger.PrintPlayer($"[TRIGGER] {inputKey} triggered.");
                return true;
            }

            return false;
        }

        public static bool IsSwitch(string inputKey)
        {
            var (keyType, parsedInput) = ParseInputKey(inputKey);

            if (!_switchStates.ContainsKey(inputKey))
                _switchStates[inputKey] = false;

            if (!_lastSwitchTime.ContainsKey(inputKey))
                _lastSwitchTime[inputKey] = 0;

            bool isCooldownPassed = (Core.gameTime - _lastSwitchTime[inputKey]) >= Player.SwitchCooldown;

            bool isPressedNow = IsPressed(parsedInput);
            bool wasPressedBefore = WasPressed(parsedInput);

            if (isPressedNow && !wasPressedBefore && isCooldownPassed)
            {
                _switchStates[inputKey] = !_switchStates[inputKey];
                _lastSwitchTime[inputKey] = Core.gameTime;
                DebugLogger.PrintPlayer($"[SWITCH] {inputKey} switched to: {_switchStates[inputKey]}");
            }

            return _switchStates[inputKey];
        }

        // Unified press detection function (reused)
        private static bool IsPressed(object parsedInput) => parsedInput switch
        {
            Keys key => Keyboard.GetState().IsKeyDown(key),
            MouseButton button => IsMouseButtonDown(button),
            _ => false
        };

        // Unified previous press detection function (reused)
        private static bool WasPressed(object parsedInput) => parsedInput switch
        {
            Keys key => _previousKeyboardState.IsKeyDown(key),
            MouseButton button => IsMouseButtonPreviouslyDown(button),
            _ => false
        };


        private static (InputKeyType, object) ParseInputKey(string inputKey)
        {
            if (Enum.TryParse(inputKey, true, out Keys parsedKey))
                return (InputKeyType.Keyboard, parsedKey);

            return inputKey switch
            {
                "LeftClick" => (InputKeyType.Mouse, MouseButton.LeftClick),
                "RightClick" => (InputKeyType.Mouse, MouseButton.RightClick),
                _ => (InputKeyType.None, null)
            };
        }

        private static bool IsMouseButtonDown(MouseButton button)
        {
            var currentState = Mouse.GetState();

            return button switch
            {
                MouseButton.LeftClick => currentState.LeftButton == ButtonState.Pressed,
                MouseButton.RightClick => currentState.RightButton == ButtonState.Pressed,
                _ => false
            };
        }

        private static bool IsMouseButtonPreviouslyDown(MouseButton button)
        {
            var previousState = _previousMouseState;

            return button switch
            {
                MouseButton.LeftClick => previousState.LeftButton == ButtonState.Pressed,
                MouseButton.RightClick => previousState.RightButton == ButtonState.Pressed,
                _ => false
            };
        }
    }
}
