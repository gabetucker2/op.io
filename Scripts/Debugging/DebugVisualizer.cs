using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public static class DebugVisualizer
    {
        private static Texture2D _debugTexture;
        private static Texture2D _lineTexture;

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
                // Create a 1x1 white texture for lines
                _lineTexture = new Texture2D(graphicsDevice, 1, 1);
                _lineTexture.SetData(new[] { Color.White });

                // Retrieve debug settings from the database
                Color debugColor = GetDebugColor();
                int debugRadius = BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleRadius");

                if (debugRadius > 0)
                {
                    _debugTexture = CreateCircleTexture(graphicsDevice, debugColor, debugRadius);
                    DebugLogger.PrintDebug($"DebugVisualizer initialized with color: {debugColor} and radius: {debugRadius}");
                }
                else
                {
                    DebugLogger.PrintError($"Invalid debug radius: {debugRadius}. Radius must be greater than 0.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"DebugVisualizer initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the debug color from the database.
        /// </summary>
        private static Color GetDebugColor()
        {
            return new Color(
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_R"),
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_G"),
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_B"),
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_A")
            );
        }

        /// <summary>
        /// Creates a circular texture for debugging.
        /// </summary>
        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, Color color, int radius)
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

        /// <summary>
        /// Draws a debug circle around a game object.
        /// </summary>
        public static void DrawDebugCircle(SpriteBatch spriteBatch, GameObject gameObject)
        {
            if (!IsValidDraw(spriteBatch, _debugTexture, gameObject))
            {
                DebugLogger.PrintError("DrawLine failed: Invalid parameters.");
                return;
            }

            spriteBatch.Draw(
                _debugTexture,
                gameObject.Position,
                null,
                Color.White,
                0f,
                new Vector2(_debugTexture.Width / 2f, _debugTexture.Height / 2f),
                1f,
                SpriteEffects.None,
                0f
            );
        }

        /// <summary>
        /// Validates if drawing is possible with the given texture and sprite batch.
        /// </summary>
        private static bool IsValidDraw(SpriteBatch spriteBatch, Texture2D texture, GameObject gameObject)
        {
            if (spriteBatch == null)
            {
                DebugLogger.PrintError("Draw failed: SpriteBatch is null.");
                return false;
            }

            if (texture == null)
            {
                DebugLogger.PrintError("Draw failed: Texture is not initialized.");
                return false;
            }

            if (gameObject == null && texture == _debugTexture)
            {
                DebugLogger.PrintWarning("DrawDebugCircle skipped: GameObject is null.");
                return false;
            }

            return true;
        }

        public static void DrawDebugRotationPointer(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Vector2 position, float rotation, float pointerLength)
        {
            Texture2D pointerTexture = new Texture2D(graphicsDevice, 1, 1);
            pointerTexture.SetData(new[] { Color.White });

            if (spriteBatch == null)
            {
                DebugLogger.PrintError("DrawRotationPointer failed: SpriteBatch is null.");
                return;
            }

            Vector2 endpoint = position + new Vector2(
                MathF.Cos(rotation) * pointerLength,
                MathF.Sin(rotation) * pointerLength
            );

            float distance = Vector2.Distance(position, endpoint);
            float angle = MathF.Atan2(endpoint.Y - position.Y, endpoint.X - position.X);

            spriteBatch.Draw(
                pointerTexture,
                position,
                null,
                Color.Red,
                angle,
                Vector2.Zero,
                new Vector2(distance, 1),
                SpriteEffects.None,
                0f
            );
        }
    }
}
