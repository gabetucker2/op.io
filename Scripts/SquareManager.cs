using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;

namespace op_io.Scripts
{
    public class SquareManager
    {
        private List<Rectangle> _squares;
        private Texture2D _texture;
        private Random _random;

        private int _viewportWidth;
        private int _viewportHeight;

        public SquareManager(int initialCount, int viewportWidth, int viewportHeight)
        {
            _squares = new List<Rectangle>();
            _random = new Random();
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;

            for (int i = 0; i < initialCount; i++)
            {
                GenerateRandomSquare();
            }
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, 40, 40);
            Color[] squareData = new Color[40 * 40];
            for (int i = 0; i < squareData.Length; i++)
            {
                squareData[i] = Color.Red;
            }
            _texture.SetData(squareData);
        }

        public void CheckCollisions(Circle circle)
        {
            for (int i = _squares.Count - 1; i >= 0; i--)
            {
                if (CircleIntersectsRectangle(circle.Position, circle.Radius, _squares[i]))
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

        private void GenerateRandomSquare()
        {
            int x = _random.Next(0, _viewportWidth - 40);
            int y = _random.Next(0, _viewportHeight - 40);
            _squares.Add(new Rectangle(x, y, 40, 40));
        }

        private bool CircleIntersectsRectangle(Vector2 circleCenter, float radius, Rectangle rectangle)
        {
            float nearestX = MathHelper.Clamp(circleCenter.X, rectangle.Left, rectangle.Right);
            float nearestY = MathHelper.Clamp(circleCenter.Y, rectangle.Top, rectangle.Bottom);

            float deltaX = circleCenter.X - nearestX;
            float deltaY = circleCenter.Y - nearestY;

            return (deltaX * deltaX + deltaY * deltaY) < (radius * radius);
        }
    }
}
