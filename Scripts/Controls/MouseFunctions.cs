using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace op.io
{
    public static class MouseFunctions
    {
        public static float GetAngleToMouse(Vector2 playerPosition)
        {
            if (float.IsNaN(playerPosition.X) || float.IsNaN(playerPosition.Y))
            {
                DebugLogger.PrintError($"Invalid player position: {playerPosition}");
                return 0f;
            }

            Vector2 mousePosition = GetMousePosition();
            Vector2 direction = mousePosition - playerPosition;

            // Automatically return 0 if player is within 1 unit of the mouse position
            if (direction == Vector2.Zero)
            {
                //DebugLogger.PrintWarning("Player is already at mouse position. Defaulting angle to 0.");
                return 0f;
            }

            return (float)Math.Atan2(direction.Y, direction.X);
        }

        public static Vector2 GetMousePosition()
        {
            MouseState mouseState = Mouse.GetState();

            int x = Math.Max(0, mouseState.X);
            int y = Math.Max(0, mouseState.Y);

            return new Vector2(x, y);
        }
    }
}