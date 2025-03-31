using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class GameUpdater
    {
        public static void Update(GameTime gameTime)
        {
            Core.gameTime = (float)gameTime.TotalGameTime.TotalSeconds;
            Core.deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            DebugHelperFunctions.DeltaTimeZeroWarning();

            // Process Actions
            ActionHandler.CheckActions();

            // Update all GameObjects
            foreach (var gameObject in Core.Instance.GameObjects)
            {
                gameObject.Update();
            }

            if (Core.Instance.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects exist in the scene.");
            }

            // Resolve collisions
            Core.Instance.PhysicsManager.ResolveCollisions(Core.Instance.GameObjects, false);
        }
    }
}
