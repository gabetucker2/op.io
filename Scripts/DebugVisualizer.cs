using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public static class DebugVisualizer
    {
        private static Texture2D _debugTexture;

        /// <summary>
        /// Initializes the debug visualizer by loading debug settings from the database.
        /// </summary>
        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            string tableName = "DebugSettings";
            string keyColumn = "SettingName";

            // Retrieve debug circle properties from the database
            Color debugColor = BaseFunctions.GetColor(tableName, keyColumn, "DebugCircleColor", Color.White);
            int debugRadius = BaseFunctions.GetValue<int>(tableName, "Radius", keyColumn, "DebugCircleRadius", 3);

            if (debugRadius <= 0)
                throw new ArgumentException("DebugCircle Radius must be greater than 0.");

            _debugTexture = CreateCircleTexture(graphicsDevice, debugColor, debugRadius);
        }

        /// <summary>
        /// Creates a circular texture for debugging.
        /// </summary>
        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, Color color, int radius)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            int diameter = radius * 2;
            Texture2D texture = new Texture2D(graphicsDevice, diameter, diameter);
            Color[] data = new Color[diameter * diameter];

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    int dx = x - radius;
                    int dy = y - radius;
                    data[y * diameter + x] = (dx * dx + dy * dy <= radius * radius) ? color : Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Draws a debug circle around a game object.
        /// </summary>
        public static void DrawDebugCircle(SpriteBatch spriteBatch, GameObject gameObject)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");

            if (_debugTexture == null)
                throw new InvalidOperationException("DebugVisualizer is not initialized. Call Initialize before drawing.");

            if (gameObject == null || !gameObject.IsCollidable)
                return;

            spriteBatch.Draw(
                _debugTexture,
                gameObject.Position - new Vector2(gameObject.BoundingRadius),
                null,
                Color.White,
                0f,
                new Vector2(_debugTexture.Width / 2f),
                gameObject.BoundingRadius / (_debugTexture.Width / 2f),
                SpriteEffects.None,
                0f
            );
        }
    }
}
