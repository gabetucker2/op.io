using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class GameBlock
    {
        public const string PanelTitle = "Game";
        public const int MinWidth = 30;
        public const int MinHeight = 0;

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds, Texture2D worldTexture)
        {
            if (spriteBatch == null || worldTexture == null)
            {
                return;
            }

            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(worldTexture, contentBounds, Color.White);
        }
    }
}
