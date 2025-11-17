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

            // Update switches
            SwitchUpdate();

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

        public static void SwitchUpdate()
        {
            if (Core.Instance.Player is not Agent player)
            {
                DebugLogger.PrintError("CRITICAL: Switch error - no Player initialized");
                return;
            }

            var modeHandlers = new List<(string key, Action<bool> applyState, bool selfPersists)>
            {
                ("DebugMode",   val =>
                {
                    if (DebugModeHandler.DEBUGENABLED != val)
                    {
                        DebugModeHandler.DEBUGENABLED = val;
                    }
                }, true),
                ("DockingMode", val => BlockManager.DockingModeEnabled = val, false),
                ("Crouch",      val => player.IsCrouching = val, false)
            };

            foreach (var (key, applyState, selfPersists) in modeHandlers)
            {
                bool liveState = InputManager.IsInputActive(key);
                bool cachedState;

                if (ControlStateManager.ContainsSwitchState(key))
                {
                    cachedState = ControlStateManager.GetSwitchState(key);
                }
                else
                {
                    cachedState = liveState;
                    ControlStateManager.UpdateSwitchState(key, liveState);
                }

                bool targetState = cachedState;

                if (liveState != cachedState)
                {
                    targetState = liveState;

                    if (!selfPersists)
                    {
                        ControlStateManager.SetSwitchState(key, liveState);
                    }

                    DebugLogger.PrintUI($"{key} switch updated from {cachedState} to {liveState}");
                }

                applyState(targetState);
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
