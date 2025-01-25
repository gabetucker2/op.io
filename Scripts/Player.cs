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
        private Color _color;
        private Texture2D _texture;
        private float _rotation; // Rotation angle in radians
        private int _pointerLength; // Length of the rotation line

        public Player(float x, float y, int radius, float speed, Color color, int weight)
        {
            Position = new Vector2(x, y);
            Radius = radius;
            Speed = speed;
            _color = color;
            Weight = weight;
            _pointerLength = 50; // Default length of the rotation pointer
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            // Create the player's circular texture
            _texture = new Texture2D(graphicsDevice, Radius * 2, Radius * 2);
            Color[] data = new Color[Radius * Radius * 4];

            for (int y = 0; y < Radius * 2; y++)
            {
                for (int x = 0; x < Radius * 2; x++)
                {
                    int dx = x - Radius;
                    int dy = y - Radius;
                    if (dx * dx + dy * dy <= Radius * Radius)
                        data[y * Radius * 2 + x] = _color;
                    else
                        data[y * Radius * 2 + x] = Color.Transparent;
                }
            }

            _texture.SetData(data);
        }

        public void Update(float deltaTime)
        {
            // Update rotation to face the mouse
            _rotation = InputManager.GetAngleToMouse(Position);

            // Update position based on input
            Vector2 input = InputManager.MoveVector();
            Position += input * Speed * deltaTime;
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            // Draw the player texture centered at Position
            spriteBatch.Draw(
                _texture,
                Position - new Vector2(Radius), // Centered at Position
                null,
                Color.White,
                0f, // No rotation applied to the texture itself
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0f
            );

            // Draw debug visuals if enabled
            if (debugEnabled)
            {
                DebugVisualizer.DrawDebugCircle(spriteBatch, Position); // Debug circle at Position
                DrawRotationPointer(spriteBatch, Position, _rotation, _pointerLength, Color.Red); // Rotation pointer
            }
        }

        private void DrawRotationPointer(SpriteBatch spriteBatch, Vector2 center, float rotation, int length, Color color)
        {
            // Create a 1x1 texture for the line
            Texture2D lineTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            lineTexture.SetData(new[] { color });

            // Calculate the endpoint of the pointer based on the rotation
            Vector2 endpoint = center + new Vector2(
                (float)Math.Cos(rotation) * length,
                (float)Math.Sin(rotation) * length
            );

            // Draw the line from the player's center to the pointer endpoint
            float distance = Vector2.Distance(center, endpoint);
            float angle = (float)Math.Atan2(endpoint.Y - center.Y, endpoint.X - center.X);

            spriteBatch.Draw(
                lineTexture,
                center,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(distance, 1), // Scale to match distance
                SpriteEffects.None,
                0f
            );
        }
    }
}
