using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class TransparentBlock
    {
        public const string PanelTitle = "Transparent Block";

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null || contentBounds.Width <= 0 || contentBounds.Height <= 0)
            {
                return;
            }

            // Fill the content with the transparency key so the chroma key punches through.
            spriteBatch.Draw(
                texture: GetPixel(spriteBatch),
                destinationRectangle: contentBounds,
                color: Core.TransparentWindowColor);
        }

        private static Texture2D GetPixel(SpriteBatch spriteBatch)
        {
            // Reuse the graphics device to lazily create a 1x1 texture.
            if (_pixel == null)
            {
                _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }

            return _pixel;
        }

        private static Texture2D _pixel;
    }
}
