using System;
using System.Collections.Generic;

namespace op.io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(
            List<GameObject> gameObjects,
            List<GameObject> staticObjects,
            GameObject player,
            bool destroyOnCollision)
        {
            if (gameObjects == null)
            {
                DebugLogger.PrintError("PhysicsManager.ResolveCollisions failed: GameObjects list is null.");
                return;
            }

            if (player == null)
            {
                DebugLogger.PrintError("PhysicsManager.ResolveCollisions failed: Player object is null.");
                return;
            }

            // Resolve collisions between dynamic gameObjects
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var objA = gameObjects[i];

                for (int j = i + 1; j < gameObjects.Count; j++)
                {
                    var objB = gameObjects[j];

                    if (objA.IsCollidable && objB.IsCollidable)
                    {
                        // Use the unified collision handling function from CollisionManager
                        bool removed = CollisionManager.HandleCollision(objA, objB, destroyOnCollision, gameObjects);

                        if (removed)
                        {
                            i--; // Adjust index if objA was removed
                            break;
                        }
                    }
                }
            }

            // Resolve collisions between player and static objects
            foreach (var staticObject in staticObjects)
            {
                if (staticObject.IsCollidable)
                {
                    CollisionManager.HandleCollision(player, staticObject, destroyOnCollision, gameObjects);
                }
            }
        }
    }
}
