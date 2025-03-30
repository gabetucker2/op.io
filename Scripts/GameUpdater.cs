using Microsoft.Xna.Framework;
using System.Linq;

namespace op.io
{
    public static class GameUpdater
    {
        public static void Update(Core game, GameTime gameTime)
        {
            if (InputManager.IsInputActive("Exit"))
                game.Exit();

            //if (!(DebugModeHandler.DebugMode == 2) && ((DebugModeHandler.DebugMode == 1) != InputManager.IsInputActive("DebugMode")))
            //{
            //    DebugModeHandler.SetDebugMode(InputManager.IsInputActive("DebugMode"));
            //}

            if (BlockManager.DockingModeEnabled != InputManager.IsInputActive("DockingMode"))
            {
                BlockManager.DockingModeEnabled = InputManager.IsInputActive("DockingMode");
                DebugLogger.PrintUI($"Docking mode updated to {BlockManager.DockingModeEnabled}");
            }
            
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