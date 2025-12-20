using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class BlankBlock
    {
        public const string BlockTitle = "Blank Block";
        public const int MinWidth = 30;
        public const int MinHeight = 0;

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds, float labelOpacity)
        {
            if (spriteBatch == null || contentBounds.Width <= 0 || contentBounds.Height <= 0)
            {
                return;
            }

            labelOpacity = MathHelper.Clamp(labelOpacity, 0f, 1f);
            if (labelOpacity <= 0f)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontH1;
            if (!font.IsAvailable)
            {
                return;
            }

            const string label = "Blank Block";
            Vector2 size = font.MeasureString(label);
            Vector2 position = new(
                contentBounds.X + (contentBounds.Width - size.X) / 2f,
                contentBounds.Y + (contentBounds.Height - size.Y) / 2f);

            Color color = UIStyle.MutedTextColor * labelOpacity;
            font.DrawString(spriteBatch, label, position, color);
        }
    }
}
