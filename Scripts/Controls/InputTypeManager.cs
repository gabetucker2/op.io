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
        private static readonly Dictionary<Keys, bool> _triggerStates = [];
        private static readonly Dictionary<string, bool> _mouseSwitchStates = [];
        private static readonly Dictionary<Keys, bool> _keySwitchStates = [];
        private static readonly Dictionary<string, float> _lastMouseSwitchTime = [];
        private static readonly Dictionary<Keys, float> _lastKeySwitchTime = [];
        private static readonly Dictionary<Keys, double> _lastKeyTriggerTime = [];
        private static readonly Dictionary<string, double> _lastMouseTriggerTime = [];

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
            KeyboardState currentState = Keyboard.GetState();
            double currentTime = Core.TimeSinceStart;

            if (!_triggerStates.ContainsKey(key))
                _triggerStates[key] = false;

            if (!_lastKeyTriggerTime.ContainsKey(key))
                _lastKeyTriggerTime[key] = 0;

            bool isCurrentlyPressed = currentState.IsKeyDown(key);
            bool wasPreviouslyPressed = _previousKeyboardState.IsKeyDown(key);
            bool isCooldownPassed = (currentTime - _lastKeyTriggerTime[key]) >= InputManager.TriggerCooldown;

            if (isCurrentlyPressed && !wasPreviouslyPressed && isCooldownPassed)
            {
                _triggerStates[key] = true;
                _lastKeyTriggerTime[key] = currentTime; // Update the last trigger time
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
            double currentTime = Core.TimeSinceStart;

            if (!_lastMouseTriggerTime.ContainsKey(mouseKey))
                _lastMouseTriggerTime[mouseKey] = 0;

            bool isTriggered = false;
            bool isCooldownPassed = (currentTime - _lastMouseTriggerTime[mouseKey]) >= InputManager.TriggerCooldown;

            if (mouseKey == "LeftClick" &&
                currentMouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released &&
                isCooldownPassed)
            {
                isTriggered = true;
                _lastMouseTriggerTime[mouseKey] = currentTime; // Update the last trigger time
            }
            else if (mouseKey == "RightClick" &&
                     currentMouseState.RightButton == ButtonState.Pressed &&
                     _previousMouseState.RightButton == ButtonState.Released &&
                     isCooldownPassed)
            {
                isTriggered = true;
                _lastMouseTriggerTime[mouseKey] = currentTime; // Update the last trigger time
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


            if (mouseKey == "LeftClick" &&
                currentMouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released &&
                (Core.TimeSinceStart - _lastMouseSwitchTime[mouseKey] >= InputManager.SwitchCooldown))
            {
                _mouseSwitchStates[mouseKey] = !_mouseSwitchStates[mouseKey];
                _lastMouseSwitchTime[mouseKey] = Core.TimeSinceStart;
            }
            else if (mouseKey == "RightClick" &&
                     currentMouseState.RightButton == ButtonState.Pressed &&
                     _previousMouseState.RightButton == ButtonState.Released &&
                     (Core.TimeSinceStart - _lastMouseSwitchTime[mouseKey] >= InputManager.SwitchCooldown))
            {
                _mouseSwitchStates[mouseKey] = !_mouseSwitchStates[mouseKey];
                _lastMouseSwitchTime[mouseKey] = Core.TimeSinceStart;
            }

            //if (toggled)
            //    DebugLogger.PrintDebug($"{mouseKey} switched to: {_mouseSwitchStates[mouseKey]}");

            return _mouseSwitchStates[mouseKey];
        }

        public static bool IsKeySwitch(Keys key)
        {
            KeyboardState currentState = Keyboard.GetState();

            if (!_keySwitchStates.ContainsKey(key))
                _keySwitchStates[key] = false;

            if (!_lastKeySwitchTime.ContainsKey(key))
                _lastKeySwitchTime[key] = 0;


            if (currentState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key) &&
                (Core.TimeSinceStart - _lastKeySwitchTime[key] >= InputManager.SwitchCooldown))
            {
                _keySwitchStates[key] = !_keySwitchStates[key];
                _lastKeySwitchTime[key] = Core.TimeSinceStart;
            }

            //if (toggled)
            //    DebugLogger.PrintDebug($"Key {key} switched to: {_keySwitchStates[key]}");

            return _keySwitchStates[key];
        }

        public static void Update()
        {
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

    }
}