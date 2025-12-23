using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal static class TextSpacingHelper
    {
        public const int SpaceWidthMultiplier = UITextRenderer.WideSpaceWidthMultiplier;
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
            return UITextRenderer.ApplySpacing(text, UITextRenderer.SpacingMode.Wide);
        }

        public static Vector2 MeasureWithWideSpaces(UIStyle.UIFont font, string text)
        {
            return UITextRenderer.Measure(font, text, UITextRenderer.SpacingMode.Wide);
        }

        public static Vector2 MeasureWithWideSpaces(UIStyle.UIFont font, StringBuilder builder)
        {
            if (builder == null || builder.Length == 0)
            {
                return Vector2.Zero;
            }

            return UITextRenderer.Measure(font, builder, UITextRenderer.SpacingMode.Wide);
        }

        public static void DrawWithWideSpaces(UIStyle.UIFont font, SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            UITextRenderer.Draw(font, spriteBatch, text, position, color, UITextRenderer.SpacingMode.Wide);
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
