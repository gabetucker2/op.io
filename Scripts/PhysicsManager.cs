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

            for (int i = 0; i < gameObjects.Count; i++)
            {
                var objA = gameObjects[i];

                for (int j = i + 1; j < gameObjects.Count; j++)
                {
                    var objB = gameObjects[j];

                    if (objA.IsCollidable && objB.IsCollidable && CollisionManager.CheckCollision(objA, objB))
                    {
                        PhysicsResolver.HandleCollision(objA, objB);

                        if (destroyOnCollision)
                        {
                            if (objA.IsDestructible)
                            {
                                gameObjects.RemoveAt(i);
                                i--;
                                break;
                            }
                            if (objB.IsDestructible)
                            {
                                gameObjects.RemoveAt(j);
                                j--;
                            }
                        }
                    }
                }
            }

            foreach (var staticObject in staticObjects)
            {
                if (staticObject.IsCollidable && CollisionManager.CheckCollision(player, staticObject))
                {
                    PhysicsResolver.HandleCollision(player, staticObject);
                }
            }
        }
    }
}
