using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        private static KeyboardState _previousKeyboardState;
        private static MouseState _previousMouseState;

        private static readonly Dictionary<string, (Keys key, InputType inputType)> _controlSettings = new Dictionary<string, (Keys, InputType)>();

        static InputManager()
        {
            LoadControlSettings();
        }

        private static void LoadControlSettings()
        {
            var controls = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlSettings;");
            foreach (var control in controls)
            {
                if (Enum.TryParse(control["InputKey"].ToString(), out Keys key) &&
                    Enum.TryParse(control["InputType"].ToString(), out InputType inputType))
                {
                    _controlSettings[control["SettingKey"].ToString()] = (key, inputType);
                }
            }
        }

        public static Vector2 MoveVector()
        {
            KeyboardState state = Keyboard.GetState();
            Vector2 direction = Vector2.Zero;

            if (IsInputActive("MoveUp")) direction.Y -= 1;
            if (IsInputActive("MoveDown")) direction.Y += 1;
            if (IsInputActive("MoveLeft")) direction.X -= 1;
            if (IsInputActive("MoveRight")) direction.X += 1;

            if (direction.LengthSquared() > 0)
                direction.Normalize();

            return direction;
        }

        public static bool IsInputActive(string settingKey)
        {
            if (_controlSettings.TryGetValue(settingKey, out var control))
            {
                if (control.inputType == InputType.Hold)
                {
                    return Keyboard.GetState().IsKeyDown(control.key);
                }
                else if (control.inputType == InputType.Trigger)
                {
                    return IsKeyTriggered(control.key);
                }
            }
            return false;
        }

        private static bool IsKeyTriggered(Keys key)
        {
            KeyboardState currentState = Keyboard.GetState();
            bool isTriggered = currentState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
            return isTriggered;
        }

        public static Vector2 GetMousePosition()
        {
            MouseState mouseState = Mouse.GetState();

            int x = Math.Max(0, mouseState.X);
            int y = Math.Max(0, mouseState.Y);

            return new Vector2(x, y);
        }

        public static float GetAngleToMouse(Vector2 playerPosition)
        {
            if (float.IsNaN(playerPosition.X) || float.IsNaN(playerPosition.Y))
            {
                DebugLogger.PrintError($"Invalid player position: {playerPosition}");
                return 0f;
            }

            Vector2 mousePosition = GetMousePosition();
            Vector2 direction = mousePosition - playerPosition;

            if (direction == Vector2.Zero)
            {
                DebugLogger.PrintWarning("Player is already at mouse position. Defaulting angle to 0.");
                return 0f;
            }

            return (float)Math.Atan2(direction.Y, direction.X);
        }

        public static bool IsDebugTogglePressed()
        {
            return IsKeyTriggered(Keys.F1);
        }

        public static bool IsExitPressed()
        {
            return Keyboard.GetState().IsKeyDown(Keys.Escape);
        }

        public static void Update()
        {
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }
    }
}
