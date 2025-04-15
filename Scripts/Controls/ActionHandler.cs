using Microsoft.Xna.Framework;
using System.Windows.Forms;

namespace op.io
{
    public static class ActionHandler
    {
        public static void CheckActions()
        {
            // Exit Action
            if (InputManager.IsInputActive("Exit"))
            {
                DebugLogger.PrintUI("Exit action triggered.");
                Core.Instance.Exit();
            }

            // Debug Mode Handling
            bool currentDebugModeState = ControlStateManager.GetSwitchState("DebugMode");
            if (currentDebugModeState != DebugModeHandler.DEBUGMODE)
            {
                DebugLogger.PrintUI($"Debug mode state change detected: {currentDebugModeState} -> {!DebugModeHandler.DEBUGMODE}");
                DebugModeHandler.SetDebugMode(!DebugModeHandler.DEBUGMODE);
                ControlStateManager.SetSwitchState("DebugMode", DebugModeHandler.DEBUGMODE);
                DebugLogger.PrintUI($"Debug mode toggled to {DebugModeHandler.DEBUGMODE}");
            }
            else
            {
                DebugLogger.PrintUI("Debug mode state unchanged.");
            }

            // Docking Mode Handling
            bool dockingModeState = ControlStateManager.GetSwitchState("DockingMode");
            if (dockingModeState != BlockManager.DockingModeEnabled)
            {
                DebugLogger.PrintUI($"Docking mode state change detected: {BlockManager.DockingModeEnabled} -> {dockingModeState}");
                BlockManager.DockingModeEnabled = dockingModeState;
                DebugLogger.PrintUI($"Docking mode updated to {BlockManager.DockingModeEnabled}");
            }
            else
            {
                DebugLogger.PrintUI("Docking mode state unchanged.");
            }

            // Crouch Handling
            if (Core.Instance.Player is Agent player)
            {
                bool crouchState = ControlStateManager.GetSwitchState("Crouch");
                if (crouchState != player.IsCrouching)
                {
                    DebugLogger.PrintUI($"Crouch state change detected: {player.IsCrouching} -> {crouchState}");
                    player.IsCrouching = crouchState;
                    DebugLogger.PrintUI($"Crouch state updated to {player.IsCrouching}");
                }
                else
                {
                    DebugLogger.PrintUI("Crouch state unchanged.");
                }
            }
            else
            {
                DebugLogger.PrintError("Core.Instance.Player is null. Cannot update crouch state.");
            }

            // ReturnCursorToPlayer Handling
            if (InputManager.IsInputActive("ReturnCursorToPlayer"))
            {
                Vector2 playerPosition = BaseFunctions.GetGOGlobalScreenPosition(Core.Instance.Player);
                Cursor.Position = BaseFunctions.Vector2ToPoint(playerPosition);
                DebugLogger.PrintUI($"Cursor returned to player position: {playerPosition}");
            }
            else
            {
                DebugLogger.PrintUI("ReturnCursorToPlayer input not active.");
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
                DebugLogger.PrintWarning("Move aborted: Direction is zero.");
                return;
            }

            if (speed <= 0)
            {
                DebugLogger.PrintWarning($"Move aborted: Speed must be positive (received {speed})");
                return;
            }

            if (Core.DELTATIME <= 0)
            {
                DebugLogger.PrintWarning($"Move skipped: DeltaTime must be positive (received {Core.DELTATIME})");
                return;
            }

            // Normalize direction and apply speed for frame rate independence
            Vector2 normalizedDirection = Vector2.Normalize(direction);
            Vector2 velocity = normalizedDirection * speed * Core.DELTATIME;  // Using deltaTime for consistent speed

            gameObject.Position += velocity;  // Update position based on velocity
            //DebugLogger.PrintUI($"Moved GameObject (ID={gameObject.ID}) by {velocity}, new position: {gameObject.Position}");
        }
    }
}
