using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class Player : GameObject
    {
        public float Speed { get; private set; } // Movement speed multiplier
        private float _rotation; // Rotation angle in radians
        private const int _pointerLength = 50; // Length of the rotation pointer

        // Constructor
        public Player(Vector2 position, int radius, float speed, Color fillColor, Color outlineColor, int outlineWidth)
            : base(
                position,
                0f,
                1f,
                isPlayer: true,
                isDestructible: false,
                isCollidable: true,
                shape: new Shape(position, "Circle", radius * 2, radius * 2, 0, fillColor, outlineColor, outlineWidth))
        {
            if (radius <= 0)
                throw new ArgumentException("Radius must be greater than 0", nameof(radius));
            if (speed <= 0)
                throw new ArgumentException("Speed must be greater than 0", nameof(speed));
            if (outlineWidth < 0)
                throw new ArgumentException("Outline width cannot be negative", nameof(outlineWidth));

            Speed = speed;
        }

        public override void LoadContent(GraphicsDevice graphicsDevice)
        {
            base.LoadContent(graphicsDevice);
        }

        public override void Update(float deltaTime)
        {
            if (deltaTime <= 0)
                throw new ArgumentException("DeltaTime must be greater than 0", nameof(deltaTime));

            base.Update(deltaTime);

            // Update rotation to face the mouse
            _rotation = InputManager.GetAngleToMouse(Position);

            // Handle input-based movement
            Vector2 input = InputManager.MoveVector();
            ApplyForce(input * Speed);

            // Update the shape's position to match the player's position
            Shape.Position = Position;
        }

        public override void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            base.Draw(spriteBatch, debugEnabled);

            // Additional debug visuals
            if (debugEnabled)
            {
                DrawRotationPointer(spriteBatch);
            }
        }

        private void DrawRotationPointer(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null");

            // Create a 1x1 texture for the line
            Texture2D lineTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            lineTexture.SetData([Color.Red]);

            // Calculate the endpoint of the pointer based on the rotation
            Vector2 endpoint = Position + new Vector2(
                (float)Math.Cos(_rotation) * _pointerLength,
                (float)Math.Sin(_rotation) * _pointerLength
            );

            // Draw the line from the player's center to the pointer endpoint
            float distance = Vector2.Distance(Position, endpoint);
            float angle = (float)Math.Atan2(endpoint.Y - Position.Y, endpoint.X - Position.X);

            spriteBatch.Draw(
                lineTexture,
                Position,
                null,
                Color.Red,
                angle,
                Vector2.Zero,
                new Vector2(distance, 1), // Scale to match distance
                SpriteEffects.None,
                0f
            );
        }
    }
}
