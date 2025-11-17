using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameUpdater
    {
        public static void Update(GameTime gameTime)
        {
            Core.GAMETIME = (float)gameTime.TotalGameTime.TotalSeconds;
            Core.DELTATIME = (float)gameTime.ElapsedGameTime.TotalSeconds;

            DebugHelperFunctions.DeltaTimeZeroWarning();

            // Centralized switch polling
            SwitchStateScanner.Tick();

            // Process general actions (toggles, switches, etc.)
            ActionHandler.Tickwise_CheckActions();

            // Handle player transform
            Vector2 direction = InputManager.GetMoveVector();
            if (direction != Vector2.Zero)
            {
                ActionHandler.Move(Core.Instance.Player, direction, Core.Instance.Player.Speed);
            }
            Core.Instance.Player.Rotation = MouseFunctions.GetAngleToMouse(Core.Instance.Player.Position);

            // Update all GameObjects
            foreach (var gameObject in Core.Instance.GameObjects)
            {
                gameObject.Update();
            }

            if (Core.Instance.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects exist in the scene.");
            }

            // Apply physics step
            PhysicsManager.Update(Core.Instance.GameObjects);

            // Reset triggers
            TriggerManager.Tickwise_TriggerReset();

            // Assess "prev" switch state management mechanism
            ControlStateManager.Tickwise_PrevSwitchTrackUpdate();
        }
    }
}
