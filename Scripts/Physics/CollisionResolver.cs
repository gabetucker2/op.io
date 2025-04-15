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
                    if (CollisionManager.CheckCollision(objA, objB))
                    {
                        //DebugLogger.PrintPhysics($"Collision confirmed between ID={objA.ID} and ID={objB.ID}");

                        // If neither object is static, apply physics resolution
                        if (!(objA.StaticPhysics && objB.StaticPhysics))
                            HandlePhysicsCollision(objA, objB);

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

        private static void HandlePhysicsCollision(GameObject objA, GameObject objB)
        {
            if (!CollisionManager.CheckCollision(objA, objB))
                return;

            // Get collision normal vector
            Vector2 collisionNormal = GetCollisionNormal(objA, objB);

            if (collisionNormal == Vector2.Zero)
            {
                DebugLogger.PrintWarning($"Zero collision normal between ID={objA.ID} and ID={objB.ID}. Skipping resolution.");
                return;
            }

            bool objAStatic = objA.StaticPhysics;
            bool objBStatic = objB.StaticPhysics;

            // Calculate mass for each object
            float massA = objAStatic ? float.PositiveInfinity : objA.Mass;
            float massB = objBStatic ? float.PositiveInfinity : objB.Mass;

            // If both objects are dynamic
            if (!objAStatic && !objBStatic)
            {
                // Both objects move based on their relative masses
                float totalMass = massA + massB;
                Vector2 moveA = collisionNormal * (massB / totalMass);
                Vector2 moveB = collisionNormal * (massA / totalMass);

                objA.Position -= moveA;
                objB.Position += moveB;

                //DebugLogger.PrintPhysics($"Resolved collision: Between ID={objA.ID} and ID={objB.ID}");
            }
            else if (objAStatic && !objBStatic)
            {
                // Static object doesn't move, dynamic object moves based on collision normal
                objB.Position += collisionNormal;

                //DebugLogger.PrintPhysics($"Resolved collision: static A. ID={objB.ID}");
            }
            else if (!objAStatic && objBStatic)
            {
                // Static object doesn't move, dynamic object moves based on collision normal
                objA.Position -= collisionNormal;

                //DebugLogger.PrintPhysics($"Resolved collision: static B. ID={objA.ID}");
            }
            else
            {
                DebugLogger.PrintWarning($"Two static object collision: ID={objA.ID} and ID={objB.ID}");
            }
            // both static objects don't move, no push-back
        }

        private static Vector2 GetCollisionNormal(GameObject objA, GameObject objB)
        {
            if (objA.Shape.ShapeType == "Rectangle" && objB.Shape.ShapeType == "Circle")
                return RectangleCircleCollisionNormal(objA, objB);

            if (objA.Shape.ShapeType == "Circle" && objB.Shape.ShapeType == "Rectangle")
                return -RectangleCircleCollisionNormal(objB, objA);

            Vector2 direction = objB.Position - objA.Position;
            if (direction != Vector2.Zero)
                direction.Normalize();
            else
                DebugLogger.PrintWarning($"Collision fallback: zero vector between ID={objA.ID} and ID={objB.ID}");

            return direction;
        }

        private static Vector2 RectangleCircleCollisionNormal(GameObject rect, GameObject circle)
        {
            Vector2 rectHalfSize = new(rect.Shape.Width / 2f, rect.Shape.Height / 2f);
            Vector2 rectCenter = rect.Position;
            Vector2 circleCenter = circle.Position;

            Vector2 delta = circleCenter - rectCenter;
            Vector2 clamped = Vector2.Clamp(delta, -rectHalfSize, rectHalfSize);
            Vector2 closest = rectCenter + clamped;
            Vector2 normal = circleCenter - closest;

            if (normal != Vector2.Zero)
                normal.Normalize();

            return normal;
        }
    }
}
