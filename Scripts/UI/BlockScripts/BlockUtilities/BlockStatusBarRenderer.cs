using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal static class BlockStatusBarRenderer
    {
        public const int Height = 22;
        public const int Padding = 6;

        private static Texture2D _pixel;

        public static Rectangle CalculateBounds(Rectangle contentBounds, Rectangle anchorBounds)
        {
            int consumed = anchorBounds.Bottom - contentBounds.Y;
            int remainingHeight = Math.Max(0, contentBounds.Height - consumed);
            int statusHeight = Math.Max(0, Math.Min(Height, remainingHeight));

            return statusHeight > 0 && contentBounds.Width > 0
                ? new Rectangle(contentBounds.X, anchorBounds.Bottom, contentBounds.Width, statusHeight)
                : Rectangle.Empty;
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle bounds, UIStyle.UIFont font, string message, Color textColor)
        {
            if (spriteBatch == null || bounds == Rectangle.Empty || !font.IsAvailable || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (!EnsurePixel(spriteBatch))
            {
                return;
            }

            FillRect(spriteBatch, bounds, UIStyle.BlockBackground);
            DrawRectOutline(spriteBatch, bounds, UIStyle.BlockBorder, 1);

            Vector2 textSize = font.MeasureString(message);
            Vector2 position = new(bounds.X + Padding, bounds.Y + (bounds.Height - textSize.Y) / 2f);
            font.DrawString(spriteBatch, message, position, textColor);
        }

        private static bool EnsurePixel(SpriteBatch spriteBatch)
        {
            if (_pixel != null && !_pixel.IsDisposed)
            {
                return true;
            }

            GraphicsDevice device = spriteBatch.GraphicsDevice;
            if (device == null)
            {
                return false;
            }

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
            return true;
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixel == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixel, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixel == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            thickness = Math.Max(1, thickness);

            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(_pixel, top, color);
            spriteBatch.Draw(_pixel, bottom, color);
            spriteBatch.Draw(_pixel, left, color);
            spriteBatch.Draw(_pixel, right, color);
        }
    }
}
