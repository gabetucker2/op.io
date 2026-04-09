using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol.Features
{
    /// <summary>
    /// Displays a text string inside its column with word-wrapping.
    /// Unlike TextLabelFeature, long text wraps to additional lines instead of truncating.
    /// The column height must be sized externally (e.g. by the row owning this feature)
    /// to accommodate the number of wrapped lines; call CalculateLineCount to determine it.
    /// </summary>
    public class WrappingTextFeature : FunctionFieldFeature
    {
        private readonly string _label;
        private readonly FunColTextAlign _align;
        private const int Pad = 4;

        public string Text { get; set; } = string.Empty;
        public override string Label => _label;

        public WrappingTextFeature(string label, FunColTextAlign align = FunColTextAlign.Left)
        {
            _label = label ?? string.Empty;
            _align = align;
        }

        /// <summary>
        /// Returns how many wrapped lines the current Text would need when drawn
        /// inside a column of the given pixel width with the given font.
        /// Returns 1 if text is empty or font unavailable (minimum one line height is always reserved).
        /// </summary>
        public int CalculateLineCount(UIStyle.UIFont font, int columnWidth)
        {
            int availW = columnWidth - Pad * 2;
            if (availW <= 0 || !font.IsAvailable || string.IsNullOrEmpty(Text))
                return 1;

            return WrapText(font, Text, availW).Count;
        }

        public override void Draw(SpriteBatch sb, Rectangle colBounds, Color baseColor,
            UIStyle.UIFont font, Texture2D pixel, bool isExpanded)
        {
            if (!font.IsAvailable || colBounds.Width < Pad * 2 + 1) return;

            string text = Text;
            if (string.IsNullOrEmpty(text)) return;

            int availW = colBounds.Width - Pad * 2;
            if (availW <= 0) return;

            List<string> lines = WrapText(font, text, availW);
            if (lines.Count == 0) return;

            float lineH = font.LineHeight;
            float totalTextH = lines.Count * lineH;
            float startY = colBounds.Y + (colBounds.Height - totalTextH) / 2f;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                Vector2 size = font.MeasureString(line);
                float ty = startY + i * lineH;
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
                font.DrawString(sb, line, new Vector2(tx, ty), Color.White);
            }
        }

        private static List<string> WrapText(UIStyle.UIFont font, string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return lines;

            // Split on explicit newlines first, then word-wrap each segment.
            string[] paragraphs = text.Split('\n');
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrEmpty(paragraph))
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string[] words = paragraph.Split(' ');
                var current = new System.Text.StringBuilder();

                foreach (string word in words)
                {
                    if (string.IsNullOrEmpty(word)) continue;

                    string candidate = current.Length == 0 ? word : current + " " + word;
                    if (font.MeasureString(candidate).X <= maxWidth)
                    {
                        if (current.Length > 0) current.Append(' ');
                        current.Append(word);
                    }
                    else
                    {
                        if (current.Length > 0)
                        {
                            lines.Add(current.ToString());
                            current.Clear();
                        }
                        current.Append(word);
                    }
                }

                if (current.Length > 0)
                    lines.Add(current.ToString());
            }

            return lines;
        }
    }
}
