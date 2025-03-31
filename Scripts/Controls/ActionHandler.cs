using Microsoft.Xna.Framework;

namespace op.io
{
    public static class ActionHandler
    {
        public static void CheckActions(Core game)
        {
            if (InputManager.IsInputActive("Exit"))
                game.Exit();

            // Debug Mode Handling (Toggle based on current state)
            bool currentDebugModeState = ControlStateManager.GetSwitchState("DebugMode");

            if (currentDebugModeState != DebugModeHandler.DEBUGMODE)
            {
                // Toggle DEBUGMODE to the opposite state
                DebugModeHandler.SetDebugMode(!DebugModeHandler.DEBUGMODE);

                // Update the switch state in ControlStateManager to match the toggled value
                ControlStateManager.SetSwitchState("DebugMode", DebugModeHandler.DEBUGMODE);

                DebugLogger.PrintUI($"Debug mode toggled to {DebugModeHandler.DEBUGMODE}");
            }

            // Docking Mode Handling
            if (ControlStateManager.GetSwitchState("DockingMode") != BlockManager.DockingModeEnabled)
            {
                BlockManager.DockingModeEnabled = ControlStateManager.GetSwitchState("DockingMode");
                DebugLogger.PrintUI($"Docking mode updated to {BlockManager.DockingModeEnabled}");
            }

            // Crouch Handling
            if (ControlStateManager.GetSwitchState("Crouch") != Player.Instance.IsCrouching)
            {
                Player.Instance.IsCrouching = ControlStateManager.GetSwitchState("Crouch");
                DebugLogger.PrintUI($"Crouch state updated to {Player.Instance.IsCrouching}");
            }
        }

        public static void Move(GameObject gameObject, Vector2 direction, float speed)
        {
            if (gameObject == null)
            {
                DebugLogger.PrintError("Move failed: GameObject is null.");
                return;
            }

            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                DebugLogger.PrintWarning($"Move aborted: Direction contains NaN values: {direction}");
                return;
            }

            if (direction == Vector2.Zero)
            {
                //DebugLogger.Print("Move skipped: Direction vector is zero.");
                return;
            }

            if (speed <= 0)
            {
                DebugLogger.PrintWarning($"Move aborted: Speed must be positive (received {speed})");
                return;
            }

            if (Core.deltaTime <= 0)
            {
                DebugLogger.PrintWarning($"Move skipped: DeltaTime must be positive (received {Core.deltaTime})");
                return;
            }

            Vector2 force = direction * speed;
            gameObject.ApplyForce(force);

            //DebugLogger.Print($"Applied force {force} with deltaTime {deltaTime} to {gameObject.Shape?.Type ?? "UnknownObject"} at {gameObject.Position}");
        }
    }
}
