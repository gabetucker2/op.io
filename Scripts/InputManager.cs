using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
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
                throw new ArgumentException("Player position must contain valid numeric values.", nameof(playerPosition));

            Vector2 mousePosition = GetMousePosition();
            Vector2 direction = mousePosition - playerPosition;

            if (direction == Vector2.Zero)
                throw new ArgumentException("Direction vector between player and mouse cannot be zero.", nameof(playerPosition));

            return (float)Math.Atan2(direction.Y, direction.X); // Angle in radians
        }
    }
}
