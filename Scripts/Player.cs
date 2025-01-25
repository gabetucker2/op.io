using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class Player
    {
        public Vector2 Position; // Center of the player
        public float Speed;
        public int Radius;
        public int Weight;
        private Shape _shape;
        private float _rotation; // Rotation angle in radians
        private int _pointerLength; // Length of the rotation line
        private Color _outlineColor; // Outline color for the player
        private int _outlineWidth; // Outline width for the player

        public Player(float x, float y, int radius, float speed, Color color, int weight, Color outlineColor, int outlineWidth)
        {
            if (radius <= 0)
                throw new ArgumentException("Radius must be greater than 0", nameof(radius));

            if (speed <= 0)
                throw new ArgumentException("Speed must be greater than 0", nameof(speed));

            if (weight <= 0)
                throw new ArgumentException("Weight must be greater than 0", nameof(weight));

            if (outlineWidth < 0)
                throw new ArgumentException("Outline width cannot be negative", nameof(outlineWidth));

            Position = new Vector2(x, y);
            Radius = radius;
            Speed = speed;
            Weight = weight;

            // Create a circle shape for the player
            _shape = new Shape(Position, "Circle", radius * 2, 0, color, outlineColor, outlineWidth, false, false);

            // Initialize rotation line length and outline settings
            _pointerLength = 50; // Default length of the rotation pointer
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null");

            // Delegate texture generation to the Shape class
            _shape.LoadContent(graphicsDevice);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime <= 0)
                throw new ArgumentException("DeltaTime must be greater than 0", nameof(deltaTime));

            // Update rotation to face the mouse
            _rotation = InputManager.GetAngleToMouse(Position);

            // Update player position based on input
            Vector2 input = InputManager.MoveVector();
            Position += input * Speed * deltaTime;

            // Update the shape's position to match the player's position
            _shape.Position = Position;
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null");

            // Draw the player's shape (with outline and fill handled by the Shape class)
            _shape.Draw(spriteBatch, debugEnabled);

            // Additional debug visuals
            if (debugEnabled)
            {
                DrawRotationPointer(spriteBatch);
                DebugVisualizer.DrawDebugCircle(spriteBatch, Position);
            }
        }

        private void DrawRotationPointer(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null");

            // Create a 1x1 texture for the line
            Texture2D lineTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            lineTexture.SetData(new[] { Color.Red });

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
