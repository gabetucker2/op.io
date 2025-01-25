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

            if (state.IsKeyDown(Keys.W)) direction.Y -= 1;
            if (state.IsKeyDown(Keys.S)) direction.Y += 1;
            if (state.IsKeyDown(Keys.A)) direction.X -= 1;
            if (state.IsKeyDown(Keys.D)) direction.X += 1;

            if (direction.LengthSquared() > 0)
                direction.Normalize();

            return direction;
        }

        public static Vector2 GetMousePosition()
        {
            MouseState mouseState = Mouse.GetState();
            return new Vector2(mouseState.X, mouseState.Y);
        }

        public static float GetAngleToMouse(Vector2 playerPosition)
        {
            Vector2 mousePosition = GetMousePosition();
            Vector2 direction = mousePosition - playerPosition;
            return (float)Math.Atan2(direction.Y, direction.X); // Angle in radians
        }
    }
}
