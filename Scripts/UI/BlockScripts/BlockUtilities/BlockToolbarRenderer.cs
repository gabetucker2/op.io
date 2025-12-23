using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    /// <summary>
    /// Standard toolbar helpers so all block toolbars share color and borders.
    /// </summary>
    internal static class BlockToolbarRenderer
    {
        public static void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle bounds)
        {
            if (spriteBatch == null || pixelTexture == null || bounds == Rectangle.Empty)
            {
                return;
            }

            spriteBatch.Draw(pixelTexture, bounds, UIStyle.DragBarBackground);

            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, UIStyle.BlockBorderThickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - UIStyle.BlockBorderThickness, bounds.Width, UIStyle.BlockBorderThickness);
            Rectangle left = new(bounds.X, bounds.Y, UIStyle.BlockBorderThickness, bounds.Height);
            Rectangle right = new(bounds.Right - UIStyle.BlockBorderThickness, bounds.Y, UIStyle.BlockBorderThickness, bounds.Height);

            spriteBatch.Draw(pixelTexture, top, UIStyle.BlockBorder);
            spriteBatch.Draw(pixelTexture, bottom, UIStyle.BlockBorder);
            spriteBatch.Draw(pixelTexture, left, UIStyle.BlockBorder);
            spriteBatch.Draw(pixelTexture, right, UIStyle.BlockBorder);
        }

        public static void DrawDropdown(
            SpriteBatch spriteBatch,
            Texture2D pixelTexture,
            UIDropdown dropdown,
            Rectangle bounds,
            UIStyle.UIFont font,
            bool isDisabled,
            string emptyPlaceholder)
        {
            if (spriteBatch == null || pixelTexture == null || bounds == Rectangle.Empty)
            {
                return;
            }

            if (dropdown != null && dropdown.HasOptions)
            {
                dropdown.Draw(spriteBatch, drawOptions: false, isDisabled: isDisabled);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, bounds, UIStyle.BlockBackground);
                DrawOutline(spriteBatch, pixelTexture, bounds, UIStyle.BlockBorder);

                if (!string.IsNullOrWhiteSpace(emptyPlaceholder) && font.IsAvailable)
                {
                    Vector2 size = font.MeasureString(emptyPlaceholder);
                    Vector2 pos = new(bounds.X + 8, bounds.Y + (bounds.Height - size.Y) / 2f);
                    font.DrawString(spriteBatch, emptyPlaceholder, pos, UIStyle.MutedTextColor);
                }
            }

            if (isDisabled)
            {
                spriteBatch.Draw(pixelTexture, bounds, UIStyle.BlockBackground * 0.45f);
            }
        }

        private static void DrawOutline(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle bounds, Color color)
        {
            if (pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0 || spriteBatch == null)
            {
                return;
            }

            int thickness = UIStyle.BlockBorderThickness;
            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(pixelTexture, top, color);
            spriteBatch.Draw(pixelTexture, bottom, color);
            spriteBatch.Draw(pixelTexture, left, color);
            spriteBatch.Draw(pixelTexture, right, color);
        }
    }
}
