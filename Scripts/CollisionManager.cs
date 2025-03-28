using Microsoft.Xna.Framework;
using System;

namespace op.io
{
    public static class CollisionManager
    {
        public static bool CheckCollision(GameObject objA, GameObject objB)
        {
            if (objA.Shape == null || objB.Shape == null)
                return false;

            if (objA.Shape.Type == "Circle" && objB.Shape.Type == "Circle")
                return CircleCollision(objA, objB);

            if ((objA.Shape.Type == "Circle" && objB.Shape.Type == "Rectangle") ||
                (objA.Shape.Type == "Rectangle" && objB.Shape.Type == "Circle"))
                return CircleRectangleCollision(objA, objB);

            if (objA.Shape.Type == "Rectangle" && objB.Shape.Type == "Rectangle")
                return RectangleCollision(objA, objB);

            if (objA.Shape.Type == "Polygon" || objB.Shape.Type == "Polygon")
                return PolygonCollision(objA, objB);

            return false;
        }

        private static bool CircleCollision(GameObject objA, GameObject objB)
        {
            float distanceSquared = Vector2.DistanceSquared(objA.Position, objB.Position);
            float combinedRadius = objA.BoundingRadius + objB.BoundingRadius;
            return distanceSquared <= combinedRadius * combinedRadius;
        }

        private static bool CircleRectangleCollision(GameObject circleObj, GameObject rectObj)
        {
            Vector2 circleCenter = circleObj.Position;
            float circleRadius = circleObj.BoundingRadius;
            Vector2 rectCenter = rectObj.Position;
            int rectWidth = rectObj.Shape.Width;
            int rectHeight = rectObj.Shape.Height;

            float left = rectCenter.X - rectWidth / 2;
            float right = rectCenter.X + rectWidth / 2;
            float top = rectCenter.Y - rectHeight / 2;
            float bottom = rectCenter.Y + rectHeight / 2;

            float nearestX = Math.Clamp(circleCenter.X, left, right);
            float nearestY = Math.Clamp(circleCenter.Y, top, bottom);

            float distanceSquared = Vector2.DistanceSquared(circleCenter, new Vector2(nearestX, nearestY));

            return distanceSquared <= circleRadius * circleRadius;
        }

        private static bool RectangleCollision(GameObject objA, GameObject objB)
        {
            return objA.Position.X - objA.Shape.Width / 2 < objB.Position.X + objB.Shape.Width / 2 &&
                   objA.Position.X + objA.Shape.Width / 2 > objB.Position.X - objB.Shape.Width / 2 &&
                   objA.Position.Y - objA.Shape.Height / 2 < objB.Position.Y + objB.Shape.Height / 2 &&
                   objA.Position.Y + objA.Shape.Height / 2 > objB.Position.Y - objB.Shape.Height / 2;
        }

        private static bool PolygonCollision(GameObject objA, GameObject objB)
        {
            // Placeholder: Implement Separating Axis Theorem (SAT) for polygons
            return false;
        }
    }
}
