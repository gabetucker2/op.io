using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class ActionHandler
    {
        public static void Move(GameObject gameObject, Vector2 direction, float speed, float deltaTime)
        {
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject), "GameObject cannot be null.");

            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
                throw new ArgumentException("Direction vector must contain valid numeric values.", nameof(direction));

            if (direction == Vector2.Zero)
                throw new ArgumentException("Direction vector cannot be zero.", nameof(direction));

            if (speed <= 0)
                throw new ArgumentException("Speed must be greater than 0.", nameof(speed));

            if (deltaTime <= 0)
                throw new ArgumentException("DeltaTime must be greater than 0.", nameof(deltaTime));

            gameObject.Position += direction * speed * deltaTime;
        }
    }
}
