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
            foreach (var gameObject in Core.InstanceCore.GameObjects)
            {
                gameObject.Update();
            }

            if (Core.InstanceCore.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects exist in the scene.");
            }

            // Resolve collisions
            Core.InstanceCore.PhysicsManager.ResolveCollisions(Core.InstanceCore.GameObjects, false);
        }
    }
}
