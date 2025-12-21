using Microsoft.Xna.Framework;
using System.Windows.Forms;
using op.io.UI.BlockScripts.Blocks;

namespace op.io
{
    public static class ActionHandler
    {
        private const bool ExitHotkeyEnabled = true;
        private static float _lastExitSuppressLogTime;

        public static void Tickwise_CheckActions()
        {
            // Suppress gameplay actions while the keybind overlay is active so captured keys (e.g., Escape) don't trigger game exits.
            if (ControlsBlock.IsRebindOverlayOpen())
            {
                return;
            }

            // Exit Action
            if (ExitHotkeyEnabled && InputManager.IsInputActive("Exit"))
            {
                DebugLogger.PrintUI("Exit hotkey triggered. Closing game.");
                Exit.CloseGame();
            }
            else if (!ExitHotkeyEnabled && InputManager.IsInputActive("Exit"))
            {
                if (Core.GAMETIME - _lastExitSuppressLogTime > 1.0f)
                {
                    _lastExitSuppressLogTime = Core.GAMETIME;
                    DebugLogger.PrintUI("Exit hotkey suppressed to keep the session running.");
                }
            }

            // ReturnCursorToPlayer Handling
            if (InputManager.IsInputActive("ReturnCursorToPlayer"))
            {
                Vector2 playerPosition = GameObjectFunctions.GetGOGlobalScreenPosition(Core.Instance.Player);
                Cursor.Position = TypeConversionFunctions.Vector2ToPoint(playerPosition);
                DebugLogger.PrintUI($"Cursor returned to player position: {playerPosition}");
            }
            else
            {
                DebugLogger.PrintUI("ReturnCursorToPlayer input not active.");
            }

            if (InputManager.IsInputActive(ControlKeyMigrations.PreviousConfigurationKey))
            {
                if (!ControlSetupsBlock.TryApplyPreviousSetup(allowWhileLocked: true))
                {
                    DockingSetupsBlock.TryApplyPreviousSetup(allowWhileLocked: true);
                }
            }
            else if (InputManager.IsInputActive(ControlKeyMigrations.NextConfigurationKey))
            {
                if (!ControlSetupsBlock.TryApplyNextSetup(allowWhileLocked: true))
                {
                    DockingSetupsBlock.TryApplyNextSetup(allowWhileLocked: true);
                }
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
