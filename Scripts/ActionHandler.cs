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

            if (deltaTime <= 0)
            {
                DebugLogger.PrintWarning($"Move skipped: DeltaTime must be positive (received {deltaTime})");
                return;
            }

            Vector2 force = direction * speed;
            gameObject.ApplyForce(force, deltaTime);

            //DebugLogger.Print($"Applied force {force} with deltaTime {deltaTime} to {gameObject.Shape?.Type ?? "UnknownObject"} at {gameObject.Position}");
        }
    }
}
