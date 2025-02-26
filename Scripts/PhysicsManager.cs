using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(
    List<GameObject> gameObjects,
    List<GameObject> staticObjects,
    GameObject player,
    bool destroyOnCollision
)
        {
            if (gameObjects == null)
                throw new ArgumentNullException(nameof(gameObjects), "GameObjects list cannot be null.");

            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player object cannot be null.");

            // Check collisions between dynamic objects
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var objA = gameObjects[i];

                for (int j = i + 1; j < gameObjects.Count; j++)
                {
                    var objB = gameObjects[j];

                    if (objA.IsCollidable && objB.IsCollidable && CheckCollision(objA, objB))
                    {
                        HandleCollision(objA, objB);

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

            // Check collisions with static objects
            foreach (var staticObject in staticObjects)
            {
                if (staticObject.IsCollidable && CheckCollision(player, staticObject))
                {
                    HandleCollision(player, staticObject);
                }
            }
        }

        private bool CheckCollision(GameObject objA, GameObject objB)
        {
            // Circle-to-circle collision
            if (objA.Shape.Type == "Circle" && objB.Shape.Type == "Circle")
            {
                float distanceSquared = Vector2.DistanceSquared(objA.Position, objB.Position);
                float combinedRadius = objA.BoundingRadius + objB.BoundingRadius;
                return distanceSquared <= combinedRadius * combinedRadius;
            }

            // Circle-to-rectangle collision
            if ((objA.Shape.Type == "Circle" && objB.Shape.Type == "Rectangle") ||
                (objA.Shape.Type == "Rectangle" && objB.Shape.Type == "Circle"))
            {
                var circleObj = objA.Shape.Type == "Circle" ? objA : objB;
                var rectObj = objA.Shape.Type == "Rectangle" ? objA : objB;

                return IsCircleOverlappingRectangle(
                    circleObj.Position,
                    circleObj.BoundingRadius,
                    rectObj.Position,
                    rectObj.Shape.Width,
                    rectObj.Shape.Height
                );
            }

            // Rectangle-to-rectangle collision
            if (objA.Shape.Type == "Rectangle" && objB.Shape.Type == "Rectangle")
            {
                return IsRectangleOverlappingRectangle(
                    objA.Position,
                    objA.Shape.Width,
                    objA.Shape.Height,
                    objB.Position,
                    objB.Shape.Width,
                    objB.Shape.Height
                );
            }

            // Circle-to-polygon or Polygon-to-polygon logic
            if (objA.Shape.Type == "Polygon" || objB.Shape.Type == "Polygon")
            {
                return IsPolygonColliding(objA, objB);
            }

            return false;
        }

        private void HandleCollision(GameObject objA, GameObject objB)
        {
            // Apply physics-based response for colliding objects
            Vector2 direction = objB.Position - objA.Position;
            float distance = direction.Length();

            if (distance == 0)
                return;

            direction.Normalize();

            float totalMass = objA.Mass + objB.Mass;
            float force = (objA.Mass * objB.Mass) / distance;

            // Apply forces proportional to each object's mass
            objA.Position -= direction * force * (objB.Mass / totalMass);
            objB.Position += direction * force * (objA.Mass / totalMass);
        }

        private bool IsCircleOverlappingRectangle(Vector2 circleCenter, float circleRadius, Vector2 rectCenter, int rectWidth, int rectHeight)
        {
            float left = rectCenter.X - rectWidth / 2;
            float right = rectCenter.X + rectWidth / 2;
            float top = rectCenter.Y - rectHeight / 2;
            float bottom = rectCenter.Y + rectHeight / 2;

            float nearestX = Math.Clamp(circleCenter.X, left, right);
            float nearestY = Math.Clamp(circleCenter.Y, top, bottom);

            float distanceSquared = Vector2.DistanceSquared(circleCenter, new Vector2(nearestX, nearestY));

            return distanceSquared <= circleRadius * circleRadius;
        }

        private bool IsRectangleOverlappingRectangle(Vector2 rectA, int widthA, int heightA, Vector2 rectB, int widthB, int heightB)
        {
            float leftA = rectA.X - widthA / 2;
            float rightA = rectA.X + widthA / 2;
            float topA = rectA.Y - heightA / 2;
            float bottomA = rectA.Y + heightA / 2;

            float leftB = rectB.X - widthB / 2;
            float rightB = rectB.X + widthB / 2;
            float topB = rectB.Y - heightB / 2;
            float bottomB = rectB.Y + heightB / 2;

            return leftA < rightB && rightA > leftB && topA < bottomB && bottomA > topB;
        }

        private bool IsPolygonColliding(GameObject objA, GameObject objB)
        {
            // Use separating axis theorem or similar approach for polygon collision
            // This is a placeholder; actual implementation depends on how polygon vertices are defined
            return false;
        }
    }
}
