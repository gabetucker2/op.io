using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Text.Json;

namespace op.io
{
    public static class DebugVisualizer
    {
        private static Texture2D _debugTexture;

        public static void Initialize(GraphicsDevice graphicsDevice, JsonElement debugSettings)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            if (!debugSettings.TryGetProperty("DebugCircle", out var debugCircleSettings))
                throw new ArgumentException("DebugCircle settings are missing in the configuration.", nameof(debugSettings));

            if (!debugCircleSettings.TryGetProperty("Color", out var colorSettings))
                throw new ArgumentException("DebugCircle Color settings are missing.", nameof(debugSettings));

            if (!colorSettings.TryGetProperty("R", out var r) || !colorSettings.TryGetProperty("G", out var g) ||
                !colorSettings.TryGetProperty("B", out var b) || !colorSettings.TryGetProperty("A", out var a))
                throw new ArgumentException("DebugCircle Color must include R, G, B, and A properties.", nameof(debugSettings));

            if (!debugCircleSettings.TryGetProperty("Radius", out var radiusProperty))
                throw new ArgumentException("DebugCircle Radius setting is missing.", nameof(debugSettings));

            int debugRadius = radiusProperty.GetInt32();
            if (debugRadius <= 0)
                throw new ArgumentException("DebugCircle Radius must be greater than 0.", nameof(debugSettings));

            var debugColor = new Color(
                r.GetByte(),
                g.GetByte(),
                b.GetByte(),
                a.GetByte()
            );

            _debugTexture = CreateCircleTexture(graphicsDevice, debugColor, debugRadius);
        }

        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, Color color, int radius)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            if (radius <= 0)
                throw new ArgumentException("Radius must be greater than 0.", nameof(radius));

            int diameter = radius * 2;
            Texture2D texture = new Texture2D(graphicsDevice, diameter, diameter);
            Color[] data = new Color[diameter * diameter];

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    int dx = x - radius;
                    int dy = y - radius;
                    if (dx * dx + dy * dy <= radius * radius)
                        data[y * diameter + x] = color;
                    else
                        data[y * diameter + x] = Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
        }

        public static void DrawDebugCircle(SpriteBatch spriteBatch, GameObject gameObject)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");

            if (_debugTexture == null)
                throw new InvalidOperationException("DebugVisualizer is not initialized. Call Initialize before drawing.");

            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject), "GameObject cannot be null.");

            if (!gameObject.IsCollidable)
                return; // Skip non-collidable objects

            // Draw the debug circle for the object's bounding radius
            spriteBatch.Draw(
                _debugTexture,
                gameObject.Position - new Vector2(gameObject.BoundingRadius),
                null,
                Color.White,
                0f,
                new Vector2(_debugTexture.Width / 2f),
                gameObject.BoundingRadius / (_debugTexture.Width / 2f), // Scale texture to match the bounding radius
                SpriteEffects.None,
                0f
            );
        }
    }
}
