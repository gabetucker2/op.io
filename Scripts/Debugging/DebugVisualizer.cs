using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

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
            {
                DebugLogger.PrintError("DebugVisualizer initialization failed: GraphicsDevice is null.");
                return;
            }

            try
            {
                // Retrieve the debug circle color from the database
                Color debugColor = new Color(
                    BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_R", 255),
                    BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_G", 0),
                    BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_B", 0),
                    BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_A", 255)
                );

                // Retrieve the debug circle radius from the database
                int debugRadius = BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleRadius", 50);

                if (debugRadius <= 0)
                {
                    DebugLogger.PrintError($"Invalid debug radius: {debugRadius}. Radius must be greater than 0.");
                    return;
                }

                _debugTexture = CreateCircleTexture(graphicsDevice, debugColor, debugRadius);
                if (_debugTexture != null)
                    DebugLogger.Print($"DebugVisualizer initialized with color: {debugColor} and radius: {debugRadius}");
                else
                    DebugLogger.PrintError("Debug texture creation failed.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"DebugVisualizer initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a circular texture for debugging.
        /// </summary>
        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, Color color, int radius)
        {
            if (graphicsDevice == null)
            {
                DebugLogger.PrintError("CreateCircleTexture failed: GraphicsDevice is null.");
                return null;
            }

            try
            {
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
            catch (Exception ex)
            {
                DebugLogger.PrintError($"CreateCircleTexture failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Draws a debug circle around a game object.
        /// </summary>
        public static void DrawDebugCircle(SpriteBatch spriteBatch, GameObject gameObject)
        {
            if (spriteBatch == null)
            {
                DebugLogger.PrintError("DrawDebugCircle failed: SpriteBatch is null.");
                return;
            }

            if (_debugTexture == null)
            {
                DebugLogger.PrintError("DrawDebugCircle failed: DebugVisualizer is not initialized. Call Initialize before drawing.");
                return;
            }

            if (gameObject == null)
            {
                DebugLogger.PrintWarning("DrawDebugCircle skipped: GameObject is null.");
                return;
            }

            // Instead of using gameObject.BoundingRadius, use the actual texture size
            float scale = 1f; // Render the texture at its natural size

            spriteBatch.Draw(
                _debugTexture,
                gameObject.Position,
                null,
                Color.White,
                0f,
                new Vector2(_debugTexture.Width / 2f, _debugTexture.Height / 2f),
                scale,
                SpriteEffects.None,
                0f
            );

            //DebugLogger.Print($"DrawDebugCircle: Drawing circle at {gameObject.Position} with texture size {_debugTexture.Width}x{_debugTexture.Height}");
        }

    }
}
