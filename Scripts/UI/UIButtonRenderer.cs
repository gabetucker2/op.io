using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Centralized button rendering so hover/variant styling stays consistent.
    /// </summary>
    internal static class UIButtonRenderer
    {
        internal enum ButtonStyle
        {
            Grey,
            Blue
        }

        private static Texture2D _pixelTexture;

        public static bool IsHovered(Rectangle bounds, Point pointer) =>
            bounds != Rectangle.Empty && bounds.Contains(pointer);

        public static void Draw(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            string label,
            ButtonStyle style,
            bool isHovered,
            bool isDisabled = false,
            Color? textColorOverride = null,
            Color? fillOverride = null,
            Color? hoverFillOverride = null,
            Color? borderOverride = null)
        {
            if (spriteBatch == null || bounds == Rectangle.Empty)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontBody;
            if (!font.IsAvailable)
            {
                return;
            }

            EnsurePixelTexture(spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice);

            Color fill = GetFillColor(style, isHovered, isDisabled, fillOverride, hoverFillOverride);
            Color border = GetBorderColor(style, isDisabled, borderOverride);
            Color textColor = textColorOverride ?? (isDisabled ? UIStyle.MutedTextColor : UIStyle.TextColor);

            FillRect(spriteBatch, bounds, fill);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            label ??= string.Empty;
            Vector2 textSize = font.MeasureString(label);
            Vector2 textPosition = new(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f);
            font.DrawString(spriteBatch, label, textPosition, textColor);
        }

        public static void DrawIcon(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            Texture2D icon,
            ButtonStyle style,
            bool isHovered,
            bool isDisabled = false,
            Color? iconColorOverride = null,
            Color? fillOverride = null,
            Color? hoverFillOverride = null,
            Color? borderOverride = null)
        {
            if (spriteBatch == null || bounds == Rectangle.Empty)
            {
                return;
            }

            EnsurePixelTexture(spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice);

            Color fill = GetFillColor(style, isHovered, isDisabled, fillOverride, hoverFillOverride);
            Color border = GetBorderColor(style, isDisabled, borderOverride);
            Color iconColor = iconColorOverride ?? Color.White;
            if (isDisabled)
            {
                iconColor *= 0.6f;
            }

            FillRect(spriteBatch, bounds, fill);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            if (icon == null || icon.IsDisposed)
            {
                return;
            }

            int padding = Math.Max(4, Math.Min(bounds.Width, bounds.Height) / 6);
            int availableWidth = Math.Max(0, bounds.Width - (padding * 2));
            int availableHeight = Math.Max(0, bounds.Height - (padding * 2));
            if (availableWidth <= 0 || availableHeight <= 0 || icon.Width <= 0 || icon.Height <= 0)
            {
                return;
            }

            float scale = Math.Min(availableWidth / (float)icon.Width, availableHeight / (float)icon.Height);
            scale = Math.Min(scale, 2f);
            if (scale <= 0f)
            {
                return;
            }

            Vector2 size = new(icon.Width * scale, icon.Height * scale);
            Vector2 position = new(bounds.X + (bounds.Width - size.X) / 2f, bounds.Y + (bounds.Height - size.Y) / 2f);

            spriteBatch.Draw(icon, position, null, iconColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static Color GetFillColor(ButtonStyle style, bool isHovered, bool isDisabled, Color? fillOverride, Color? hoverFillOverride)
        {
            Color baseFill = fillOverride ?? style switch
            {
                ButtonStyle.Blue => ColorPalette.ButtonPrimary,
                _ => ColorPalette.ButtonNeutral
            };

            Color hoverFill = hoverFillOverride ?? style switch
            {
                ButtonStyle.Blue => ColorPalette.ButtonPrimaryHover,
                _ => ColorPalette.ButtonNeutralHover
            };

            Color selected = isHovered ? hoverFill : baseFill;

            if (isDisabled)
            {
                selected *= 0.8f;
            }

            return selected;
        }

        private static Color GetBorderColor(ButtonStyle style, bool isDisabled, Color? borderOverride)
        {
            Color selected = borderOverride ?? (style switch
            {
                ButtonStyle.Blue => UIStyle.AccentColor,
                _ => UIStyle.BlockBorder
            });

            if (isDisabled)
            {
                selected = UIStyle.MutedTextColor;
            }

            return selected;
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            EnsurePixelTexture(spriteBatch?.GraphicsDevice ?? Core.Instance?.GraphicsDevice);
            if (_pixelTexture == null || spriteBatch == null)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0 || spriteBatch == null)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(_pixelTexture, top, color);
            spriteBatch.Draw(_pixelTexture, bottom, color);
            spriteBatch.Draw(_pixelTexture, left, color);
            spriteBatch.Draw(_pixelTexture, right, color);
        }

        private static void EnsurePixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture != null || graphicsDevice == null)
            {
                return;
            }

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }
    }
}
