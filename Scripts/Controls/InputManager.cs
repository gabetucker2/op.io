using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        public static float TriggerCooldown = 0.5f; // Half a second cooldown
        public static float SwitchCooldown = 0.5f; // Half a second cooldown

        // Supports both keyboard keys and mouse clicks
        private static readonly Dictionary<string, (object key, InputType inputType)> _ControlKey =
            new Dictionary<string, (object, InputType)>();

        static InputManager()
        {
            LoadControlKey();
        }

        private static void LoadControlKey()
        {
            var controls = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlKey;");
            foreach (var control in controls)
            {
                string settingKey = control["SettingKey"].ToString();
                string inputKey = control["InputKey"].ToString();
                InputType inputType = Enum.Parse<InputType>(control["InputType"].ToString());

                if (Enum.TryParse(inputKey, out Keys key)) // Keyboard keys
                {
                    _ControlKey[settingKey] = (key, inputType);
                }
                else if (inputKey == "LeftClick" || inputKey == "RightClick") // Mouse inputs
                {
                    _ControlKey[settingKey] = (inputKey, inputType); // Store the string directly for mouse clicks
                }
                else
                {
                    DebugLogger.PrintError($"Failed to parse input key '{inputKey}' for '{settingKey}'.");
                }
            }
        }

        public static Vector2 GetMoveVector()
        {
            KeyboardState state = Keyboard.GetState();
            Vector2 direction = Vector2.Zero;

            // Mouse-follow dominant, can't also have WASD movement since that's too wonky
            if (!(IsInputActive("MoveTowardsCursor") && IsInputActive("MoveAwayFromCursor")) && (IsInputActive("MoveTowardsCursor") || IsInputActive("MoveAwayFromCursor")))
            {
                float rotation = Player.player.Rotation;
                if (IsInputActive("MoveTowardsCursor"))
                {
                    direction.X += MathF.Cos(rotation);
                    direction.Y += MathF.Sin(rotation);
                }
                if (IsInputActive("MoveAwayFromCursor"))
                {
                    direction.X -= MathF.Cos(rotation);
                    direction.Y -= MathF.Sin(rotation);
                }
            }
            else
            {
                if (IsInputActive("MoveUp")) direction.Y -= 1;
                if (IsInputActive("MoveDown")) direction.Y += 1;
                if (IsInputActive("MoveLeft")) direction.X -= 1;
                if (IsInputActive("MoveRight")) direction.X += 1;
            }
            if (direction.LengthSquared() > 0)
                direction.Normalize();

            return direction;
        }

        public static float SpeedMultiplier()
        {
            float multiplier = 1f;

            if (IsInputActive("Sprint"))
            {
                multiplier *= BaseFunctions.GetValue<float>("ControlSettings", "Value", "SettingKey", "SprintSpeedMultiplier");
            }
            else if (IsInputActive("Crouch"))
            {
                multiplier *= BaseFunctions.GetValue<float>("ControlSettings", "Value", "SettingKey", "CrouchSpeedMultiplier");
            }
            return multiplier;
        }

        public static bool IsInputActive(string settingKey)
        {
            if (_ControlKey.TryGetValue(settingKey, out var control))
            {
                if (control.inputType == InputType.Hold)
                {
                    if (control.key is Keys key)
                    {
                        return InputTypeManager.IsKeyHeld(key);
                    }
                    else if (control.key is string mouseKey)
                    {
                        return InputTypeManager.IsMouseButtonHeld(mouseKey);
                    }
                }
                else if (control.inputType == InputType.Trigger)
                {
                    if (control.key is Keys key)
                    {
                        return InputTypeManager.IsKeyTriggered(key);
                    }
                    else if (control.key is string mouseKey)
                    {
                        return InputTypeManager.IsMouseButtonTriggered(mouseKey);
                    }
                }
                else if (control.inputType == InputType.Switch)
                {
                    if (control.key is Keys key)
                    {
                        return InputTypeManager.IsKeySwitch(key); // Check toggle state of key
                    }
                    else if (control.key is string mouseKey)
                    {
                        return InputTypeManager.IsMouseButtonSwitch(mouseKey); // Check toggle state of mouse button
                    }
                }
            }
            return false;
        }

        public static bool IsExitPressed()
        {
            return Keyboard.GetState().IsKeyDown(Keys.Escape);
        }

        public static bool IsDebugTogglePressed()
        {
            return InputTypeManager.IsKeyTriggered(Keys.F1);
        }

        public static bool IsDockingModePressed()
        {
            return InputTypeManager.IsKeyTriggered(Keys.F1);
        }
    }
}
