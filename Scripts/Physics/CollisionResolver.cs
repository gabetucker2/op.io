using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class CollisionResolver
    {
        public static void ResolveCollisions(List<GameObject> gameObjects)
        {
            if (gameObjects == null)
            {
                DebugLogger.PrintError("CollisionResolver.ResolveCollisions failed: GameObjects list is null.");
                return;
            }

            // Loop through all game objects to resolve collisions
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var objA = gameObjects[i];

                for (int j = i + 1; j < gameObjects.Count; j++)
                {
                    var objB = gameObjects[j];

                    // Skip if either object is not collidable
                    if (!objA.IsCollidable || !objB.IsCollidable)
                        continue;

                    // Check if there is a collision between objA and objB
                    if (CollisionManager.TryGetCollision(objA, objB, out Vector2 mtv))
                    {
                        //DebugLogger.PrintPhysics($"Collision confirmed between ID={objA.ID} and ID={objB.ID}");

                        // If neither object is static, apply physics resolution
                        if (!(objA.StaticPhysics && objB.StaticPhysics))
                            HandlePhysicsCollision(objA, objB, mtv);

                        // Handle destruction of objA and objB
                        if (objA.IsDestructible)
                        {
                            DebugLogger.PrintPhysics($"Destroying GameObject ID={objA.ID} (destructible).");
                            gameObjects.RemoveAt(i);
                            GameObjectRegister.UnregisterGameObject(objA);
                            i--; // Adjust the index after removal
                            break; // Break out of the inner loop as objA is destroyed
                        }

                        if (objB.IsDestructible)
                        {
                            DebugLogger.PrintPhysics($"Destroying GameObject ID={objB.ID} (destructible).");
                            gameObjects.RemoveAt(j);
                            GameObjectRegister.UnregisterGameObject(objB);
                            j--; // Adjust the index after removal
                            break; // Break out of the inner loop as objB is destroyed
                        }
                    }
                }
            }
        }

        private static void HandlePhysicsCollision(GameObject objA, GameObject objB, Vector2 minimumTranslationVector)
        {
            if (minimumTranslationVector == Vector2.Zero)
                return;

            bool objAStatic = objA.StaticPhysics;
            bool objBStatic = objB.StaticPhysics;

            // Calculate mass for each object
            float massA = objAStatic ? float.PositiveInfinity : Math.Max(objA.Mass, 0.0001f);
            float massB = objBStatic ? float.PositiveInfinity : Math.Max(objB.Mass, 0.0001f);

            // If both objects are dynamic
            if (!objAStatic && !objBStatic)
            {
                // Both objects move based on their relative masses
                float totalMass = massA + massB;
                Vector2 moveA = minimumTranslationVector * (massB / totalMass);
                Vector2 moveB = minimumTranslationVector * (massA / totalMass);

                objA.Position -= moveA;
                objB.Position += moveB;

                //DebugLogger.PrintPhysics($"Resolved collision: Between ID={objA.ID} and ID={objB.ID}");
            }
            else if (objAStatic && !objBStatic)
            {
                // Static object doesn't move, dynamic object moves based on collision normal
                objB.Position += minimumTranslationVector;

                //DebugLogger.PrintPhysics($"Resolved collision: static A. ID={objB.ID}");
            }
            else if (!objAStatic && objBStatic)
            {
                // Static object doesn't move, dynamic object moves based on collision normal
                objA.Position -= minimumTranslationVector;

                //DebugLogger.PrintPhysics($"Resolved collision: static B. ID={objA.ID}");
            }
            else
            {
                DebugLogger.PrintWarning($"Two static object collision: ID={objA.ID} and ID={objB.ID}");
            }
            // both static objects don't move, no push-back
        }
    }
}
