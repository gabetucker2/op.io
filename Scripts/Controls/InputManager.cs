using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        // Dictionary to store control key mappings (keyboard and mouse)
        private static readonly Dictionary<string, (object key, InputType inputType)> _controlKey = new();
        private static readonly Dictionary<string, float> _cachedSpeedMultipliers = new();
        private static bool _isControlKeyLoaded = false;

        // This will be set during initialization to avoid unnecessary database calls.
        static InputManager()
        {
            LoadControlKey();
        }

        private static void LoadControlKey()
        {
            if (_isControlKeyLoaded) return;

            var controls = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlKey;");
            foreach (var control in controls)
            {
                string settingKey = control["SettingKey"].ToString();
                string inputKey = control["InputKey"].ToString();
                InputType inputType = Enum.Parse<InputType>(control["InputType"].ToString());

                if (Enum.TryParse(inputKey, out Keys key)) // Keyboard keys
                {
                    _controlKey[settingKey] = (key, inputType);
                }
                else if (inputKey == "LeftClick" || inputKey == "RightClick") // Mouse inputs
                {
                    _controlKey[settingKey] = (inputKey, inputType); // Store the string directly for mouse clicks
                }
                else
                {
                    DebugLogger.PrintError($"Failed to parse input key '{inputKey}' for '{settingKey}'.");
                }
            }

            _isControlKeyLoaded = true;
        }

        // Retrieve speed multiplier values from the cache or the database
        private static float GetCachedMultiplier(string settingKey, string databaseKey)
        {
            if (_cachedSpeedMultipliers.ContainsKey(settingKey))
            {
                return _cachedSpeedMultipliers[settingKey];
            }

            float multiplier = BaseFunctions.GetValue<float>("ControlSettings", "Value", "SettingKey", databaseKey);
            _cachedSpeedMultipliers[settingKey] = multiplier;
            return multiplier;
        }

        // Calculate the movement vector based on input keys
        public static Vector2 GetMoveVector()
        {
            KeyboardState state = Keyboard.GetState();
            Vector2 direction = Vector2.Zero;

            if (!(IsInputActive("MoveTowardsCursor") && IsInputActive("MoveAwayFromCursor")) && (IsInputActive("MoveTowardsCursor") || IsInputActive("MoveAwayFromCursor")))
            {
                float rotation = Core.Instance.Player.Rotation;
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

        // Speed multiplier considering sprint and crouch inputs
        public static float SpeedMultiplier()
        {
            float multiplier = 1f;

            if (IsInputActive("Sprint"))
            {
                multiplier *= GetCachedMultiplier("Sprint", "SprintSpeedMultiplier");
            }
            else if (IsInputActive("Crouch"))
            {
                multiplier *= GetCachedMultiplier("Crouch", "CrouchSpeedMultiplier");
            }

            return multiplier;
        }

        // Check if a specific input action is active
        public static bool IsInputActive(string settingKey)
        {
            if (_controlKey.TryGetValue(settingKey, out var control))
            {
                switch (control.inputType)
                {
                    case InputType.Hold:
                        return control.key switch
                        {
                            Keys key => InputTypeManager.IsKeyHeld(key),
                            string mouseKey => InputTypeManager.IsMouseButtonHeld(mouseKey),
                            _ => false
                        };
                    case InputType.Trigger:
                        return control.key switch
                        {
                            Keys key => InputTypeManager.IsKeyTriggered(key),
                            string mouseKey => InputTypeManager.IsMouseButtonTriggered(mouseKey),
                            _ => false
                        };
                    case InputType.Switch:
                        return control.key switch
                        {
                            Keys key => InputTypeManager.IsKeySwitch(key),
                            string mouseKey => InputTypeManager.IsMouseButtonSwitch(mouseKey),
                            _ => false
                        };
                    default:
                        return false;
                }
            }

            return false;
        }
    }
}
