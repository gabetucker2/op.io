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

            // Draw all static MapData objects (assuming these are stored in StaticObjects)
            foreach (var staticObject in game.StaticObjects)
            {
                staticObject.Draw(game.SpriteBatch, DebugModeHandler.IsDebugEnabled());
            }

            // Draw all dynamic GameObjects
            foreach (var gameObject in game.GameObjects)
            {
                gameObject.Draw(game.SpriteBatch, DebugModeHandler.IsDebugEnabled());
            }

            // Draw shapes managed by ShapesManager
            game.ShapesManager.Draw(game.SpriteBatch, DebugModeHandler.IsDebugEnabled());

            // Render debug circles last to ensure visibility
            if (DebugModeHandler.IsDebugEnabled())
            {
                // Draw debug circles for StaticObjects
                foreach (var staticObject in game.StaticObjects)
                {
                    DebugVisualizer.DrawDebugCircle(game.SpriteBatch, staticObject);
                }

                // Draw debug circles for GameObjects
                foreach (var gameObject in game.GameObjects)
                {
                    DebugVisualizer.DrawDebugCircle(game.SpriteBatch, gameObject);
                }
            }

            game.SpriteBatch.End();
        }
    }
}
