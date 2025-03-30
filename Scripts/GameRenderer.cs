using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class GameRenderer
    {
        public static void Draw(Core game, GameTime gameTime)
        {
            game.GraphicsDevice.Clear(game.BackgroundColor);
            game.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            foreach (var gameObject in game.GameObjects)
            {
                gameObject.Draw(game.SpriteBatch);
            }

            // Render debug direction line last to ensure visibility
            if (DebugModeHandler.IsDebugEnabled())
            {
                foreach (var gameObject in game.GameObjects)
                {
                    DebugVisualizer.DrawDebugCircle(game.SpriteBatch, gameObject);
                }
            }

            game.SpriteBatch.End();
        }
    }
}
