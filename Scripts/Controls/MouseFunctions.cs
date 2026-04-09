using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System; // Math.Atan2

namespace op.io
{
    public static class MouseFunctions
    {
        /// <summary>
        /// Called once per frame (before rotation logic).
        /// Mouse-sensitivity dampening has been removed; this is kept as a no-op
        /// so call-sites don't need to change.
        /// </summary>
        public static void Tick() { }

        /// <summary>
        /// Returns the cursor position in game space, accounting for the current camera offset.
        /// Equivalent to <see cref="GetMousePosition"/>; retained for call-site compatibility.
        /// </summary>
        public static Vector2 GetMousePositionWithSensitivity() => GetMousePosition();

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

            Point windowPoint = new(mouseState.X, mouseState.Y);
            return BlockManager.ToGameSpace(windowPoint);
        }
    }
}
