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
        }
    }
}
