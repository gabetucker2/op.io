using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace op.io
{
    public static class DebugVisualizer
    {
        private static Texture2D _debugTexture;

        public static void Initialize(GraphicsDevice graphicsDevice, JsonElement debugSettings)
        {
            // Parse debug circle settings
            var debugColor = new Color(
                debugSettings.GetProperty("DebugCircle").GetProperty("Color").GetProperty("R").GetByte(),
                debugSettings.GetProperty("DebugCircle").GetProperty("Color").GetProperty("G").GetByte(),
                debugSettings.GetProperty("DebugCircle").GetProperty("Color").GetProperty("B").GetByte(),
                debugSettings.GetProperty("DebugCircle").GetProperty("Color").GetProperty("A").GetByte()
            );
            int debugRadius = debugSettings.GetProperty("DebugCircle").GetProperty("Radius").GetInt32();

            _debugTexture = CreateCircleTexture(graphicsDevice, debugColor, debugRadius);
        }

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
                    if (dx * dx + dy * dy <= radius * radius)
                        data[y * diameter + x] = color;
                    else
                        data[y * diameter + x] = Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
        }

        public static void DrawDebugCircle(SpriteBatch spriteBatch, Vector2 position)
        {
            if (_debugTexture != null)
            {
                spriteBatch.Draw(
                    _debugTexture,
                    position - new Vector2(_debugTexture.Width / 2f),
                    Color.White
                );
            }
        }
    }
}
