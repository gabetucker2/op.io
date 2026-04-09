using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol.Features
{
    /// <summary>
    /// Centralized value display feature for block rows.
    /// - Boolean values: renders a colored dot (green=true, red=false), aligned to TextAlign.
    /// - Text values: renders text with ellipsis truncation, aligned to TextAlign.
    /// Replaces the separate bool-indicator column in Specs and Backend blocks.
    /// </summary>
    public class ValueDisplayFeature : FunctionFieldFeature
    {
        private readonly string _label;

        /// <summary>Text alignment for both dot and text display.</summary>
        public FunColTextAlign TextAlign { get; set; } = FunColTextAlign.Left;

        /// <summary>Text displayed when IsBoolean is false.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>True = display a boolean dot indicator instead of text.</summary>
        public bool IsBoolean { get; set; } = false;

        /// <summary>Boolean state when IsBoolean is true.</summary>
        public bool BoolState { get; set; } = false;

        public override string Label => _label;

        public ValueDisplayFeature(string label = "value")
        {
            _label = label ?? "value";
        }

        public override void Draw(SpriteBatch sb, Rectangle colBounds, Color baseColor,
            UIStyle.UIFont font, Texture2D pixel, bool isExpanded)
        {
            if (colBounds.Width < 4) return;

            if (IsBoolean)
                DrawBoolDot(sb, colBounds, pixel);
            else
                DrawText(sb, colBounds, font);
        }

        private void DrawBoolDot(SpriteBatch sb, Rectangle colBounds, Texture2D pixel)
        {
            if (pixel == null) return;

            const int Pad = 4;
            int dotSize = Math.Min(10, Math.Min(colBounds.Width - Pad * 2, colBounds.Height - Pad * 2));
            if (dotSize <= 0) return;

            int dx = TextAlign switch
            {
                FunColTextAlign.Left  => colBounds.X + Pad,
                FunColTextAlign.Right => colBounds.Right - dotSize - Pad,
                _                    => colBounds.X + (colBounds.Width - dotSize) / 2,
            };
            int dy = colBounds.Y + (colBounds.Height - dotSize) / 2;

            Color dotColor = BoolState ? new Color(80, 220, 80) : new Color(220, 80, 80);
            sb.Draw(pixel, new Rectangle(dx, dy, dotSize, dotSize), dotColor);
        }

        private void DrawText(SpriteBatch sb, Rectangle colBounds, UIStyle.UIFont font)
        {
            if (!font.IsAvailable) return;

            string text = Text;
            if (string.IsNullOrEmpty(text)) return;

            const int Pad = 4;
            int availW = colBounds.Width - Pad * 2;
            if (availW <= 0) return;

            string display = TruncateWithEllipsis(font, text, availW);
            if (string.IsNullOrEmpty(display)) return;

            Vector2 size = font.MeasureString(display);
            float ty = colBounds.Y + (colBounds.Height - size.Y) / 2f;
            float tx = TextAlign switch
            {
                FunColTextAlign.Right  => colBounds.Right - size.X - Pad,
                FunColTextAlign.Center => colBounds.X + (colBounds.Width - size.X) / 2f,
                _                     => colBounds.X + Pad,
            };

            font.DrawString(sb, display, new Vector2(tx, ty), Color.White);
        }

        private static string TruncateWithEllipsis(UIStyle.UIFont font, string text, int maxWidth)
        {
            Vector2 size = font.MeasureString(text);
            if (size.X <= maxWidth) return text;

            const string Ellipsis = "…";
            Vector2 ellipsisSize = font.MeasureString(Ellipsis);
            if (ellipsisSize.X >= maxWidth) return string.Empty;

            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                string candidate = text.Substring(0, mid) + Ellipsis;
                if (font.MeasureString(candidate).X <= maxWidth)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo > 0 ? text.Substring(0, lo) + Ellipsis : string.Empty;
        }
    }
}
