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
        private int _viewportWidth;
        private int _viewportHeight;

        public Player(float x, float y, int radius, float speed, Color color, int viewportWidth, int viewportHeight, int weight)
        {
            Position = new Vector2(x, y);
            Radius = radius;
            Speed = speed;
            _color = color;
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
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

        public void Update(float deltaTime)
        {
            Vector2 input = InputManager.MoveVector();
            Position += input * Speed * deltaTime;

            // Ensure the player stays within bounds
            Position.X = MathHelper.Clamp(Position.X, Radius, _viewportWidth - Radius);
            Position.Y = MathHelper.Clamp(Position.Y, Radius, _viewportHeight - Radius);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(_texture, Position - new Vector2(Radius), Color.White);
        }
    }
}
