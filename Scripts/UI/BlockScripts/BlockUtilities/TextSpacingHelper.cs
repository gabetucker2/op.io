using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal static class TextSpacingHelper
    {
        public const int SpaceWidthMultiplier = 3;
        public const string WideWordSeparator = "    ";

        public static string JoinWithWideSpacing(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> filtered = new(parts.Length);
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    filtered.Add(trimmed);
                }
            }

            return filtered.Count == 0 ? string.Empty : string.Join(WideWordSeparator, filtered);
        }

        public static string ExpandSpaces(string text)
        {
            if (string.IsNullOrEmpty(text) || SpaceWidthMultiplier <= 1)
            {
                return text ?? string.Empty;
            }

            return text.Contains(' ')
                ? text.Replace(" ", new string(' ', SpaceWidthMultiplier))
                : text;
        }

        public static Vector2 MeasureWithWideSpaces(UIStyle.UIFont font, string text)
        {
            if (!font.IsAvailable || string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            return font.MeasureString(ExpandSpaces(text));
        }

        public static Vector2 MeasureWithWideSpaces(UIStyle.UIFont font, StringBuilder builder)
        {
            if (builder == null || builder.Length == 0)
            {
                return Vector2.Zero;
            }

            return MeasureWithWideSpaces(font, builder.ToString());
        }

        public static void DrawWithWideSpaces(UIStyle.UIFont font, SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            if (!font.IsAvailable || spriteBatch == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            font.DrawString(spriteBatch, ExpandSpaces(text), position, color);
        }

        public static void DrawWithWideSpaces(UIStyle.UIFont font, SpriteBatch spriteBatch, StringBuilder builder, Vector2 position, Color color)
        {
            if (builder == null || builder.Length == 0)
            {
                return;
            }

            DrawWithWideSpaces(font, spriteBatch, builder.ToString(), position, color);
        }
    }
}
