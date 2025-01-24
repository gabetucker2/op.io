using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io.Scripts;

namespace op_io
{
    public class Player
    {
        public Vector2 Position;
        public float Speed;
        public int Radius;
        public int Weight;
        private Color _color;
        private Texture2D _texture;

        public Player(float x, float y, int radius, float speed, Color color, int weight)
        {
            Position = new Vector2(x, y);
            Radius = radius;
            Speed = speed;
            _color = color;
            Weight = weight;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
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

        public void ApplyInput(Vector2 input, float deltaTime)
        {
            Position += input * Speed * deltaTime;
            Position = new Vector2(Position.X, Position.Y);
        }

        public void Update(float deltaTime)
        {
            Vector2 input = InputManager.MoveVector();
            Position += input * Speed * deltaTime;
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            // Render the player
            spriteBatch.Draw(_texture, Position - new Vector2(Radius), Color.White);

            if (debugEnabled)
            {
                // Render a circle at the visual render center
                DebugVisualizer.DrawDebugCircle(spriteBatch, Position);
            }
        }

        public bool IsCollidingWith(FarmShape farmShape)
        {
            // Check if the player's position is inside the farm shape
            bool isColliding = farmShape.IsPointInsidePolygon(
                (int)Position.X,
                (int)Position.Y,
                farmShape.Size / 2,
                farmShape.Size / 2,
                farmShape.Sides, // Use public property
                farmShape.Size / 2,
                farmShape.Rotation // Use public property
            );
            Console.WriteLine($"Player at {Position} colliding with FarmShape: {isColliding}");
            return isColliding;
        }
    }
}
