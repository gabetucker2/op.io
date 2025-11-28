using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal static class BlockIndicatorRenderer
    {
        private static Texture2D _indicatorTexture;
        private const int IndicatorDiameter = 10;
        private static readonly Color ActiveColor = new(72, 201, 115);
        private static readonly Color InactiveColor = new(192, 57, 43);

        public static bool TryDrawBooleanIndicator(SpriteBatch spriteBatch, Rectangle contentBounds, float lineHeight, float lineY, bool state)
        {
            if (spriteBatch == null)
            {
                return false;
            }

            if (!EnsureIndicatorTexture(spriteBatch))
            {
                return false;
            }

            int indicatorX = Math.Max(contentBounds.X, contentBounds.Right - IndicatorDiameter - 4);
            int indicatorY = (int)(lineY + ((lineHeight - IndicatorDiameter) / 2f));
            Rectangle indicatorBounds = new(indicatorX, indicatorY, IndicatorDiameter, IndicatorDiameter);
            Color fill = state ? ActiveColor : InactiveColor;
            spriteBatch.Draw(_indicatorTexture, indicatorBounds, fill);
            return true;
        }

        private static bool EnsureIndicatorTexture(SpriteBatch spriteBatch)
        {
            if (_indicatorTexture != null && !_indicatorTexture.IsDisposed)
            {
                return true;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return false;
            }

            int diameter = IndicatorDiameter;
            int pixels = diameter * diameter;
            Color[] data = new Color[pixels];
            float radius = (diameter - 1) / 2f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float distanceSquared = (dx * dx) + (dy * dy);
                    data[(y * diameter) + x] = distanceSquared <= radius * radius ? Color.White : Color.Transparent;
                }
            }

            _indicatorTexture = new Texture2D(graphicsDevice, diameter, diameter);
            _indicatorTexture.SetData(data);
            return true;
        }
    }
}
