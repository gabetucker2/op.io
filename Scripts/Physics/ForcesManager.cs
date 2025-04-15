using Microsoft.Xna.Framework;

namespace op.io
{
    public static class ForcesManager
    {
        /// <summary>
        /// Applies a force to a GameObject, adjusting its position based on mass and deltaTime.
        /// </summary>
        public static void ApplyForce(GameObject gameObject, Vector2 force)
        {
            if (gameObject == null)
            {
                DebugLogger.PrintError("ApplyForce failed: GameObject is null.");
                return;
            }

            if (force == Vector2.Zero)
            {
                DebugLogger.PrintDebug("ApplyForce skipped: Force vector is zero.");
                return;
            }

            if (Core.DELTATIME <= 0f)
            {
                DebugLogger.PrintWarning("ApplyForce skipped: deltaTime must be positive.");
                return;
            }

            if (gameObject.StaticPhysics)
            {
                DebugLogger.PrintDebug("ApplyForce skipped: GameObject is static.");
                return;
            }

            Vector2 acceleration = force / gameObject.Mass;
            gameObject.Position += acceleration * Core.DELTATIME;

            //DebugLogger.PrintDebug($"Applied force {force}, resulting acceleration {acceleration}, new position {gameObject.Position}");
        }
    }
}