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

        private static readonly Color GreyFill = new(34, 34, 34, 230);
        private static readonly Color GreyHoverFill = new(52, 52, 52, 240);
        private static readonly Color BlueFill = new(58, 78, 150, 235);
        private static readonly Color BlueHoverFill = new(86, 116, 204, 240);

        public static bool IsHovered(Rectangle bounds, Point pointer) =>
            bounds != Rectangle.Empty && bounds.Contains(pointer);

        public static void Draw(SpriteBatch spriteBatch, Rectangle bounds, string label, ButtonStyle style, bool isHovered, bool isDisabled = false)
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

            Color fill = GetFillColor(style, isHovered, isDisabled);
            Color border = GetBorderColor(style, isDisabled);
            Color textColor = isDisabled ? UIStyle.MutedTextColor : UIStyle.TextColor;

            FillRect(spriteBatch, bounds, fill);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.PanelBorderThickness);

            label ??= string.Empty;
            Vector2 textSize = font.MeasureString(label);
            Vector2 textPosition = new(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f);
            font.DrawString(spriteBatch, label, textPosition, textColor);
        }

        private static Color GetFillColor(ButtonStyle style, bool isHovered, bool isDisabled)
        {
            if (isDisabled)
            {
                return GreyFill;
            }

            return style switch
            {
                ButtonStyle.Blue => isHovered ? BlueHoverFill : BlueFill,
                _ => isHovered ? GreyHoverFill : GreyFill
            };
        }

        private static Color GetBorderColor(ButtonStyle style, bool isDisabled)
        {
            if (isDisabled)
            {
                return UIStyle.PanelBorder;
            }

            return style switch
            {
                ButtonStyle.Blue => UIStyle.AccentColor,
                _ => UIStyle.PanelBorder
            };
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
