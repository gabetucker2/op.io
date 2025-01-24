using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io.Scripts
{
    public static class InputManager
    {
        public static Vector2 MoveVector()
        {
            // Init
            Vector2 direction = Vector2.Zero;

            // KEYBOARD
            var keyboard = Keyboard.GetState();
            if (keyboard.IsKeyDown(Keys.W)) direction.Y -= 1;
            if (keyboard.IsKeyDown(Keys.S)) direction.Y += 1;
            if (keyboard.IsKeyDown(Keys.A)) direction.X -= 1;
            if (keyboard.IsKeyDown(Keys.D)) direction.X += 1;

            // GAMEPAD
            var gamePad = GamePad.GetState(PlayerIndex.One);
            if (gamePad.IsConnected)
            {
                direction += gamePad.ThumbSticks.Left;
            }

            // Normalize
            if (direction.LengthSquared() > 1)
                direction.Normalize();

            // Return
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
            return (float)Math.Atan2(direction.Y, direction.X); // Return angle in radians
        }
    }
}
