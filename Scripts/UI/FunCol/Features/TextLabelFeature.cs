using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol.Features
{
    public enum FunColTextAlign { Left, Center, Right }

    /// <summary>
    /// Displays a dynamic text string inside its column, truncating with "…" if too wide.
    /// </summary>
    public class TextLabelFeature : FunctionFieldFeature
    {
        private readonly string _label;
        private readonly FunColTextAlign _align;

        public string Text { get; set; } = string.Empty;
        public override string Label => _label;

        public TextLabelFeature(string label, FunColTextAlign align = FunColTextAlign.Left)
        {
            _label = label ?? string.Empty;
            _align = align;
        }

        public override void Draw(SpriteBatch sb, Rectangle colBounds, Color baseColor,
            UIStyle.UIFont font, Texture2D pixel, bool isExpanded)
        {
            if (!font.IsAvailable || colBounds.Width < 4) return;

            string text = Text;
            if (string.IsNullOrEmpty(text)) return;

            const int Pad = 4;
            int availW = colBounds.Width - Pad * 2;
            if (availW <= 0) return;

            // Truncate with ellipsis if needed
            string display = TruncateWithEllipsis(font, text, availW);
            if (string.IsNullOrEmpty(display)) return;

            Vector2 size = font.MeasureString(display);
            float ty = colBounds.Y + (colBounds.Height - size.Y) / 2f;
            float tx;

            switch (_align)
            {
                case FunColTextAlign.Right:
                    tx = colBounds.Right - size.X - Pad;
                    break;
                case FunColTextAlign.Center:
                    tx = colBounds.X + (colBounds.Width - size.X) / 2f;
                    break;
                default: // Left
                    tx = colBounds.X + Pad;
                    break;
            }

            tx = Math.Max(colBounds.X + 2f, tx);
            font.DrawString(sb, display, new Vector2(tx, ty), Color.White);
        }

        private static string TruncateWithEllipsis(UIStyle.UIFont font, string text, int maxWidth)
        {
            Vector2 size = font.MeasureString(text);
            if (size.X <= maxWidth) return text;

            const string Ellipsis = "…";
            Vector2 ellipsisSize = font.MeasureString(Ellipsis);
            if (ellipsisSize.X >= maxWidth) return string.Empty;

            // Binary-search for longest prefix that fits
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
