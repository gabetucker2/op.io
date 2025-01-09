using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op_io
{
    public class SquareManager
    {
        private List<Rectangle> _squares;
        private Color _color;
        private Texture2D _texture;

        public SquareManager(int count, int screenWidth, int screenHeight, Color color)
        {
            _squares = new List<Rectangle>();
            _color = color;
            for (int i = 0; i < count; i++)
            {
                int x = Random.Shared.Next(0, screenWidth - 40);
                int y = Random.Shared.Next(0, screenHeight - 40);
                _squares.Add(new Rectangle(x, y, 40, 40));
            }
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, 40, 40);
            Color[] data = new Color[40 * 40];
            for (int i = 0; i < data.Length; i++) data[i] = _color;
            _texture.SetData(data);
        }

        public void CheckCollisions(Player circle)
        {
            for (int i = _squares.Count - 1; i >= 0; i--)
            {
                Rectangle square = _squares[i];
                if (CircleIntersectsRectangle(circle.Position, circle.Radius, square))
                {
                    _squares.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            foreach (var square in _squares)
            {
                spriteBatch.Draw(_texture, square, Color.White);
            }
        }

        private bool CircleIntersectsRectangle(Vector2 circlePos, int radius, Rectangle rect)
        {
            float closestX = MathHelper.Clamp(circlePos.X, rect.Left, rect.Right);
            float closestY = MathHelper.Clamp(circlePos.Y, rect.Top, rect.Bottom);

            float distanceX = circlePos.X - closestX;
            float distanceY = circlePos.Y - closestY;

            return (distanceX * distanceX + distanceY * distanceY) < (radius * radius);
        }
    }
}
