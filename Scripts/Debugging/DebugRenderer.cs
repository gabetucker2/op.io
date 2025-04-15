using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public static class DebugRenderer
    {
        private static Texture2D _circleTexture;
        private static Texture2D _lineTexture;
        private static Texture2D _pointerTexture;
        private static Color _debugColor;
        private static int _debugRadius;

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                DebugLogger.PrintError("DebugRenderer initialization failed: GraphicsDevice is null.");
                return;
            }

            // Basic 1x1 white texture
            _lineTexture = new Texture2D(graphicsDevice, 1, 1);
            _lineTexture.SetData([Color.White]);

            _pointerTexture = new Texture2D(graphicsDevice, 1, 1);
            _pointerTexture.SetData([Color.Red]);

            _debugColor = new Color(
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_R"),
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_G"),
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_B"),
                BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleColor_A")
            );

            _debugRadius = BaseFunctions.GetValue<int>("DebugVisuals", "Value", "SettingKey", "DebugCircleRadius");

            if (_debugRadius > 0)
            {
                _circleTexture = CreateCircleTexture(graphicsDevice, _debugColor, _debugRadius);
                DebugLogger.PrintDebug($"DebugRenderer initialized with color {_debugColor} and radius {_debugRadius}");
            }
            else
            {
                DebugLogger.PrintError($"Invalid debug radius: {_debugRadius}");
            }
        }

        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, Color color, int radius)
        {
            int diameter = radius * 2;
            Texture2D texture = new(graphicsDevice, diameter, diameter);
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

        public static void DrawDebugCircle(SpriteBatch spriteBatch, GameObject gameObject)
        {
            if (spriteBatch == null || _circleTexture == null || gameObject == null)
                return;

            spriteBatch.Draw(
                _circleTexture,
                gameObject.Position,
                null,
                Color.White,
                0f,
                new Vector2(_circleTexture.Width / 2f, _circleTexture.Height / 2f),
                1f,
                SpriteEffects.None,
                0f
            );
        }

        public static void DrawRotationPointer(SpriteBatch spriteBatch, Agent agent)
        {
            if (spriteBatch == null || agent == null)
            {
                DebugLogger.PrintError("DrawRotationPointer failed: missing parameters.");
                return;
            }

            const int pointerLength = 50;
            Vector2 position = agent.Position;

            // Use agent's rotation instead of mouse-based calculation
            float rotation = agent.Rotation;

            // Calculate the endpoint of the pointer using the agent's rotation
            Vector2 endpoint = position + new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * pointerLength;

            // Calculate the distance and angle for the pointer's direction
            float distance = Vector2.Distance(position, endpoint);
            float angle = MathF.Atan2(endpoint.Y - position.Y, endpoint.X - position.X);

            // Draw the pointer at the agent's position
            spriteBatch.Draw(
                _pointerTexture,
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
