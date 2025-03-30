using Microsoft.Xna.Framework;
using System.Linq;

namespace op.io
{
    public static class GameUpdater
    {
        public static void Update(Core game, GameTime gameTime)
        {
            ActionHandler.CheckActions(game);

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (deltaTime <= 0)
                deltaTime = 0.0001f;
            Core.TimeSinceStart += deltaTime;

            foreach (var gameObject in game.GameObjects)
            {
                gameObject.Update(deltaTime);
            }

            if (game.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects exist in the scene.");
            }

            game.PhysicsManager.ResolveCollisions(game.GameObjects, false);
        }
    }
}
