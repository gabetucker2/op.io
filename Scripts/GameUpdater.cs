using Microsoft.Xna.Framework;
using System.Linq;

namespace op.io
{
    public static class GameUpdater
    {
        public static void Update(Core game, GameTime gameTime)
        {
            if (InputManager.IsExitPressed())
                game.Exit();

            if (InputManager.IsDebugTogglePressed())
                DebugManager.ToggleDebugMode();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (deltaTime <= 0)
                deltaTime = 0.0001f;

            foreach (var gameObject in game.GameObjects)
            {
                gameObject.Update(deltaTime);
            }

            if (game.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("[WARNING] No GameObjects exist in the scene.");
            }

            game.PhysicsManager.ResolveCollisions(game.GameObjects, false);
        }
    }
}