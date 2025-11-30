using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class BlankBlock
    {
        public const string PanelTitle = "Blank Block";
        public const int MinWidth = 0;
        public const int MinHeight = 0;

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null || contentBounds.Width <= 0 || contentBounds.Height <= 0)
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

            font.DrawString(spriteBatch, label, position, UIStyle.MutedTextColor);
        }
    }
}
