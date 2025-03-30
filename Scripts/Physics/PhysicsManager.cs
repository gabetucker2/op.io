using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(List<GameObject> gameObjects, bool destroyOnCollision)
        {
            if (gameObjects == null)
            {
                DebugLogger.PrintError("PhysicsManager.ResolveCollisions failed: GameObjects list is null.");
                return;
            }

            for (int i = 0; i < gameObjects.Count; i++)
            {
                var objA = gameObjects[i];

                for (int j = i + 1; j < gameObjects.Count; j++)
                {
                    var objB = gameObjects[j];

                    if (!objA.IsCollidable || !objB.IsCollidable)
                        continue;

                    if (CollisionManager.CheckCollision(objA, objB))
                    {
                        if (!(objA.StaticPhysics && objB.StaticPhysics))
                            HandlePhysicsCollision(objA, objB);

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
        }

        private void HandlePhysicsCollision(GameObject objA, GameObject objB)
        {
            // First, ensure there's a collision
            if (!CollisionManager.CheckCollision(objA, objB))
                return;

            // Get collision normal vector
            Vector2 collisionNormal = GetCollisionNormal(objA, objB);

            if (collisionNormal == Vector2.Zero)
                return;

            // Define whether each object is static
            bool objAStatic = objA.StaticPhysics;
            bool objBStatic = objB.StaticPhysics;

            float massA = objAStatic ? float.PositiveInfinity : objA.Mass;
            float massB = objBStatic ? float.PositiveInfinity : objB.Mass;

            // Resolve based on static/dynamic status
            if (!objAStatic && !objBStatic)
            {
                float totalMass = massA + massB;
                objA.Position -= collisionNormal * (massB / totalMass);
                objB.Position += collisionNormal * (massA / totalMass);
            }
            else if (objAStatic && !objBStatic)
            {
                objB.Position += collisionNormal;
            }
            else if (!objAStatic && objBStatic)
            {
                objA.Position -= collisionNormal;
            }
            // both static objects don't move
        }

        private Vector2 GetCollisionNormal(GameObject objA, GameObject objB)
        {
            // Rectangle vs Circle special handling
            if (objA.Shape.Type == "Rectangle" && objB.Shape.Type == "Circle")
            {
                return RectangleCircleCollisionNormal(objA, objB);
            }
            else if (objA.Shape.Type == "Circle" && objB.Shape.Type == "Rectangle")
            {
                // Invert direction for Circle vs Rectangle
                return -RectangleCircleCollisionNormal(objB, objA);
            }
            else
            {
                // Default fallback: simple center-to-center (less accurate)
                Vector2 direction = objB.Position - objA.Position;
                if (direction != Vector2.Zero)
                    direction.Normalize();
                return direction;
            }
        }

        private Vector2 RectangleCircleCollisionNormal(GameObject rect, GameObject circle)
        {
            Vector2 rectHalfSize = new Vector2(rect.Shape.Width / 2f, rect.Shape.Height / 2f);
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