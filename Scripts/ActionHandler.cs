using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class ActionHandler
    {
        public static void Move(GameObject gameObject, Vector2 direction, float speed, float deltaTime)
        {
            if (gameObject == null)
            {
                DebugManager.PrintError("Move failed: GameObject is null.");
                return;
            }

            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                DebugManager.PrintWarning($"Move aborted: Direction vector contains NaN values: {direction}");
                return;
            }

            if (direction == Vector2.Zero)
            {
                DebugManager.PrintDebug("Move skipped: Direction vector is zero.");
                return;
            }

            if (speed <= 0)
            {
                DebugManager.PrintWarning($"Move aborted: Speed must be positive but was {speed}");
                return;
            }

            if (deltaTime <= 0)
            {
                DebugManager.PrintWarning($"Move skipped: DeltaTime must be positive but was {deltaTime}");
                return;
            }

            Vector2 movement = direction * speed * deltaTime;
            gameObject.Position += movement;

            DebugManager.PrintDebug($"Moved GameObject by {movement} to new position {gameObject.Position}");
        }
    }
}
