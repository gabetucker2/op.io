using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        public static readonly Dictionary<string, (string inputKey, InputType inputType)> _ControlKey = [];

        static InputManager()
        {
            LoadControlKey();
        }

        public static bool IsInputActive(string settingKey)
        {
            return InputTypeManager.IsInputActive(settingKey);
        }

        private static void LoadControlKey()
        {
            var controls = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlKey;");

            foreach (var control in controls)
            {
                string settingKey = control["SettingKey"].ToString();
                string inputKey = control["InputKey"].ToString();
                InputType inputType = Enum.Parse<InputType>(control["InputType"].ToString());

                _ControlKey[settingKey] = (inputKey, inputType);
            }
        }

        public static Vector2 GetMoveVector()
        {
            Vector2 direction = Vector2.Zero;

            if (!(IsInputActive("MoveTowardsCursor") && IsInputActive("MoveAwayFromCursor")) &&
                (IsInputActive("MoveTowardsCursor") || IsInputActive("MoveAwayFromCursor")))
            {
                float rotation = Player.InstancePlayer.Rotation;

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
    }
}
