using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class FarmManager
    {
        private ShapesManager _shapesManager;

        public FarmManager()
        {
            _shapesManager = new ShapesManager();
        }

        public void AddFarmShape(Vector2 position, string type, int width, int height, int sides, Color color, Color outlineColor, int outlineWidth, bool enableCollision, bool enablePhysics)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Shape type cannot be null or whitespace.", nameof(type));

            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be greater than zero.");

            if (sides < 0)
                throw new ArgumentOutOfRangeException(nameof(sides), "Number of sides cannot be negative.");

            if (outlineWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(outlineWidth), "Outline width cannot be negative.");

            // Use the correct AddShape method with width and height explicitly
            _shapesManager.AddShape(position, type, width, height, sides, color, outlineColor, outlineWidth, enableCollision, enablePhysics);
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            _shapesManager.LoadContent(graphicsDevice);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");

            _shapesManager.Update(deltaTime);
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");

            _shapesManager.Draw(spriteBatch, debugEnabled);
        }

        public List<Shape> GetFarmShapes()
        {
            return _shapesManager.GetShapes();
        }
    }
}
