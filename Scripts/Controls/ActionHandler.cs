using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;
using System.Windows.Forms;

namespace op.io
{
    public static class ActionHandler
    {
        public static void Tickwise_CheckActions()
        {
            // Exit Action
            if (InputManager.IsInputActive("Exit"))
            {
                DebugLogger.PrintUI("Exit action triggered. Calling GameCloser.");
                Exit.CloseGame();
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
