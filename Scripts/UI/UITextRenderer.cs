using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Centralized text rendering to keep spacing consistent across UI elements.
    /// </summary>
    public static class UITextRenderer
    {
        public enum SpacingMode
        {
            Normal,
            Wide
        }

        public const int DefaultSpaceWidthMultiplier = 2;
        public const int WideSpaceWidthMultiplier = 3;

        public static Vector2 Measure(UIStyle.UIFont font, string text, SpacingMode spacingMode = SpacingMode.Normal)
        {
            if (!font.IsAvailable || string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            string content = ApplySpacing(text, spacingMode);
            return font.MeasureRawString(content);
        }

        public static Vector2 Measure(UIStyle.UIFont font, StringBuilder builder, SpacingMode spacingMode = SpacingMode.Normal)
        {
            if (!font.IsAvailable || builder == null || builder.Length == 0)
            {
                return Vector2.Zero;
            }

            return Measure(font, builder.ToString(), spacingMode);
        }

        public static void Draw(UIStyle.UIFont font, SpriteBatch spriteBatch, string text, Vector2 position, Color color, SpacingMode spacingMode = SpacingMode.Normal)
        {
            if (!font.IsAvailable || spriteBatch == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            string content = ApplySpacing(text, spacingMode);
            font.DrawRawString(spriteBatch, content, position, color);
        }

        public static void Draw(UIStyle.UIFont font, SpriteBatch spriteBatch, StringBuilder builder, Vector2 position, Color color, SpacingMode spacingMode = SpacingMode.Normal)
        {
            if (!font.IsAvailable || spriteBatch == null || builder == null || builder.Length == 0)
            {
                return;
            }

            Draw(font, spriteBatch, builder.ToString(), position, color, spacingMode);
        }

        public static string ApplySpacing(string text, SpacingMode spacingMode = SpacingMode.Normal)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int multiplier = spacingMode == SpacingMode.Wide ? WideSpaceWidthMultiplier : DefaultSpaceWidthMultiplier;
            if (multiplier <= 1 || !text.Contains(' '))
            {
                return text;
            }

            return text.Replace(" ", new string(' ', multiplier));
        }
    }
}
