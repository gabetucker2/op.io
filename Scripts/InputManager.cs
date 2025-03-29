using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        private static KeyboardState _previousState;

        public static Vector2 MoveVector()
        {
            KeyboardState state = Keyboard.GetState();
            Vector2 direction = Vector2.Zero;

            // Determine movement direction
            if (state.IsKeyDown(Keys.W)) direction.Y -= 1;
            if (state.IsKeyDown(Keys.S)) direction.Y += 1;
            if (state.IsKeyDown(Keys.A)) direction.X -= 1;
            if (state.IsKeyDown(Keys.D)) direction.X += 1;

            // Normalize direction if there is input
            if (direction.LengthSquared() > 0)
                direction.Normalize();

            return direction;
        }

        public static Vector2 GetMousePosition()
        {
            MouseState mouseState = Mouse.GetState();

            // Clamp mouse position to ensure non-negative values
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
                DebugLogger.PrintWarning("[InputManager.cs:GetAngleToMouse] Player is already at mouse position. Defaulting angle to 0.");
                return 0f;
            }

            return (float)Math.Atan2(direction.Y, direction.X); // Angle in radians
        }

        public static bool IsDebugTogglePressed()
        {
            KeyboardState currentState = Keyboard.GetState();
            bool isPressed = currentState.IsKeyDown(Keys.F1) && !_previousState.IsKeyDown(Keys.F1);
            _previousState = currentState;
            return isPressed;
        }

        public static bool IsExitPressed()
        {
            KeyboardState state = Keyboard.GetState();
            return state.IsKeyDown(Keys.Escape);
        }

    }
}
