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
                throw new ArgumentNullException(nameof(gameObjects), "GameObjects list cannot be null.");
            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player object cannot be null.");

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
