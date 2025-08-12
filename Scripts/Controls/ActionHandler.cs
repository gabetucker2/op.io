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
                DebugLogger.PrintUI("Exit action triggered.");
                Core.Instance.Exit();
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

        public static void SwitchUpdate() // Remove this function entirely because a lot of it is automatically managed?  Probably should make modehandlers somewhere more easily accessible and make this process more procedural rather than manually defined.  but prevState should work so first maybe check out whether it works now in the old iteration with the comparison i'm trying to replicate.  maybe i could even just use the trigger manager to do shit instead of this, using the new prevtickswitchstate function in the process.
        {
            if (Core.Instance.Player is Agent player)
            {
                var modeHandlers = new List<(string key, Action<bool> setCurrent)>
                {
                    ("DebugMode",   val => DebugModeHandler.DEBUGENABLED = val),
                    ("DockingMode", val => BlockManager.DockingModeEnabled = val),
                    ("Crouch",      val => player.IsCrouching = val)
                };

                foreach (var (key, setCurrent) in modeHandlers)
                {
                    bool prevState = ControlStateManager.GetPrevTickSwitchState(key);
                    bool newState = ControlStateManager.GetSwitchState(key);

                    if (prevState != newState) // !!!!! TODO: This is the crux of the problem, this is always false, ensure you fix it.
                    {
                        if (TriggerManager.GetTrigger(key))
                        {
                            setCurrent(newState);
                            ControlStateManager.SetSwitchState(key, newState);
                            DebugLogger.PrintUI($"{key} updated from {!newState} to {newState}");
                        }
                        else
                        {
                            DebugLogger.PrintUI($"{key} state unchanged.");
                        }
                    }
                }
            }
            else
            {
                DebugLogger.PrintError($"CRITICAL: Switch error - no Player initialized");
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
