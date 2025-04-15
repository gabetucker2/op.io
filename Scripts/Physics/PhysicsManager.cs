using System.Collections.Generic;

namespace op.io
{
    /// <summary>
    /// Centralized physics system coordinator.
    /// Delegates collision resolution and force logic.
    /// </summary>
    public class PhysicsManager
    {
        private static bool _initialized = false;

        /// <summary>
        /// Initializes core physics modules. Call once from GameInitializer.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                DebugLogger.PrintWarning("PhysicsManager already initialized. Skipping.");
                return;
            }

            DebugLogger.PrintPhysics("Initializing PhysicsManager...");
            // No explicit submodule init required yet.
            DebugLogger.PrintPhysics("PhysicsManager initialization complete.");
            _initialized = true;
        }

        /// <summary>
        /// Applies full physics simulation for the current frame.
        /// </summary>
        public static void Update(List<GameObject> gameObjects)
        {
            if (gameObjects == null)
            {
                DebugLogger.PrintError("PhysicsManager.Update failed: GameObjects list is null.");
                return;
            }

            CollisionResolver.ResolveCollisions(gameObjects);
            // Future: ForcesManager, Gravity, Friction, etc.
        }
    }
}