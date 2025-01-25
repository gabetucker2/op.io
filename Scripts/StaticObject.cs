using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class StaticObject
    {
        public Vector2 Position { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        private Color _color;
        private Color _outlineColor;
        private int _outlineWidth;
        private Texture2D _texture;

        // New property: BoundingRadius
        public float BoundingRadius => MathF.Sqrt((Width * Width) + (Height * Height)) / 2;

        public StaticObject(Vector2 position, int width, int height, Color color, Color outlineColor, int outlineWidth)
        {
            Position = position;
            Width = width;
            Height = height;
            _color = color;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, Width, Height);
            Color[] data = new Color[Width * Height];
            for (int i = 0; i < data.Length; i++)
                data[i] = _color;
            _texture.SetData(data);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Rectangle bounds = new Rectangle((int)(Position.X - Width / 2), (int)(Position.Y - Height / 2), Width, Height);
            spriteBatch.Draw(_texture, bounds, Color.White);
        }
    }
}
