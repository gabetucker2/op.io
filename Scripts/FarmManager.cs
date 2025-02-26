using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class FarmManager
    {
        private List<GameObject> _farmObjects; // A list to hold farm-specific GameObjects

        public FarmManager()
        {
            _farmObjects = new List<GameObject>();
        }

        public void AddFarmShape(
            Vector2 position,
            string type,
            int width,
            int height,
            int sides,
            Color fillColor,
            Color outlineColor,
            int outlineWidth,
            bool enableCollision,
            bool enablePhysics
        )
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Shape type cannot be null or whitespace.", nameof(type));

            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be greater than zero.");

            if (outlineWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(outlineWidth), "Outline width cannot be negative.");

            float boundingRadius = type == "Circle"
                ? width / 2f
                : MathF.Sqrt(width * width + height * height) / 2f;

            var shape = new Shape(
                position,
                type,
                width,
                height,
                sides,
                fillColor,
                outlineColor,
                outlineWidth
            );

            var gameObject = new GameObject(
                position,
                0f,
                1f,
                boundingRadius,
                false,
                isDestructible: enablePhysics,
                isCollidable: enableCollision,
                shape: shape
            );

            _farmObjects.Add(gameObject);
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            foreach (var farmObject in _farmObjects)
            {
                farmObject.LoadContent(graphicsDevice);
            }
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");

            foreach (var farmObject in _farmObjects)
            {
                farmObject.Update(deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");

            foreach (var farmObject in _farmObjects)
            {
                farmObject.Draw(spriteBatch, debugEnabled);
            }
        }

        public List<GameObject> GetFarmShapes()
        {
            return _farmObjects;
        }
    }
}
