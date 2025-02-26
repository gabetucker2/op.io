using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace op.io
{
    public class ShapesManager
    {
        private List<GameObject> _shapes;
        private int _viewportWidth;
        private int _viewportHeight;

        public ShapesManager(int viewportWidth, int viewportHeight)
        {
            if (viewportWidth <= 0)
                throw new ArgumentException("Viewport width must be greater than 0.", nameof(viewportWidth));
            if (viewportHeight <= 0)
                throw new ArgumentException("Viewport height must be greater than 0.", nameof(viewportHeight));

            _shapes = new List<GameObject>();
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
        }

        /// <summary>
        /// Adds a shape to the manager from a JSON configuration.
        /// </summary>
        /// <param name="shapeConfig">JSON element describing the shape properties.</param>
        public void AddShapeFromConfig(JsonElement shapeConfig)
        {
            if (shapeConfig.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Invalid shape configuration.", nameof(shapeConfig));

            string type = shapeConfig.GetProperty("Type").GetString();
            int size = shapeConfig.GetProperty("Size").GetInt32();

            // Ensure size is valid
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");

            // Randomly position shapes within the viewport
            Vector2 position = new Vector2(
                Random.Shared.Next(0, Math.Max(1, _viewportWidth - size)),
                Random.Shared.Next(0, Math.Max(1, _viewportHeight - size))
            );

            // Optional number of sides for polygons
            int sides = shapeConfig.TryGetProperty("NumberOfSides", out var sidesElement) ? sidesElement.GetInt32() : 0;

            // Extract color, outline, and physical properties
            var fillColor = BaseFunctions.GetColor(shapeConfig.GetProperty("FillColor"));
            var outlineColor = BaseFunctions.GetColor(shapeConfig.GetProperty("OutlineColor"));
            int outlineWidth = shapeConfig.GetProperty("OutlineWidth").GetInt32();
            bool isCollidable = shapeConfig.GetProperty("IsCollidable").GetBoolean();
            bool isDestructible = shapeConfig.GetProperty("IsDestructible").GetBoolean();

            // Calculate bounding radius for the shape
            float boundingRadius = type == "Circle" || type == "Polygon"
                ? size / 2f
                : MathF.Sqrt(size * size) / 2f;

            // Create the Shape instance
            var shape = new Shape(
                position,
                type,
                size,
                size,
                sides,
                fillColor,
                outlineColor,
                outlineWidth
            );

            // Create the GameObject instance and link the Shape
            var gameObject = new GameObject(
                position,
                0f,
                1f, // Default mass for shapes
                boundingRadius,
                isPlayer: false,
                isDestructible: isDestructible,
                isCollidable: isCollidable,
                shape: shape
            );

            _shapes.Add(gameObject);
        }

        /// <summary>
        /// Loads graphical content for all shapes managed by this instance.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device to use for texture generation.</param>
        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            foreach (var shape in _shapes)
            {
                shape.Shape?.LoadContent(graphicsDevice);
            }
        }

        /// <summary>
        /// Updates all shapes managed by this instance.
        /// </summary>
        /// <param name="deltaTime">The time step to use for updates.</param>
        public void Update(float deltaTime)
        {
            if (deltaTime < 0)
                throw new ArgumentException("Delta time cannot be negative.", nameof(deltaTime));

            foreach (var shape in _shapes)
            {
                shape.Update(deltaTime);
            }
        }

        /// <summary>
        /// Draws all shapes managed by this instance.
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch to use for rendering.</param>
        /// <param name="debugEnabled">Whether to enable debug rendering.</param>
        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");

            foreach (var shape in _shapes)
            {
                shape.Shape?.Draw(spriteBatch, debugEnabled);
            }
        }

        /// <summary>
        /// Gets the list of shapes managed by this instance.
        /// </summary>
        /// <returns>A list of GameObjects representing shapes.</returns>
        public List<GameObject> GetShapes()
        {
            return _shapes;
        }
    }
}
