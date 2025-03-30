using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace op.io
{
    public static class CollisionManager
    {
        public static bool CheckCollision(GameObject objA, GameObject objB)
        {
            if (objA.Shape == null || objB.Shape == null)
                return false;

            return objA.Shape.Type switch
            {
                "Circle" when objB.Shape.Type == "Circle" => CircleCollision(objA, objB),
                "Circle" when objB.Shape.Type == "Rectangle" => CircleRectangleCollision(objA, objB),
                "Rectangle" when objB.Shape.Type == "Circle" => CircleRectangleCollision(objB, objA),
                "Rectangle" when objB.Shape.Type == "Rectangle" => RectangleCollision(objA, objB),
                _ when objA.Shape.Type == "Polygon" || objB.Shape.Type == "Polygon" => PolygonCollision(objA, objB),
                _ => false
            };
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
            return SATCollision(
                objA.Shape.GetTransformedVertices(objA.Position, objA.Rotation),
                objB.Shape.GetTransformedVertices(objB.Position, objB.Rotation)
            );
        }

        private static bool SATCollision(Vector2[] polyA, Vector2[] polyB)
        {
            return !HasSeparatingAxis(polyA, polyB) && !HasSeparatingAxis(polyB, polyA);
        }

        private static bool HasSeparatingAxis(Vector2[] verticesA, Vector2[] verticesB)
        {
            int countA = verticesA.Length;

            for (int i = 0; i < countA; i++)
            {
                Vector2 edge = verticesA[(i + 1) % countA] - verticesA[i];
                Vector2 axis = new Vector2(-edge.Y, edge.X);
                axis.Normalize();

                float minA, maxA, minB, maxB;
                ProjectVertices(axis, verticesA, out minA, out maxA);
                ProjectVertices(axis, verticesB, out minB, out maxB);

                if (maxA < minB || maxB < minA)
                    return true;
            }

            return false;
        }

        private static void ProjectVertices(Vector2 axis, Vector2[] vertices, out float min, out float max)
        {
            min = max = 0f;

            try
            {
                if (vertices == null || vertices.Length == 0)
                    DebugLogger.PrintError($"Vertices array is null or empty.");

                float dot = Vector2.Dot(axis, vertices[0]);
                min = dot;
                max = dot;

                for (int i = 1; i < vertices.Length; i++)
                {
                    dot = Vector2.Dot(vertices[i], axis);
                    if (dot < min) min = dot;
                    if (dot > max) max = dot;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in ProjectVertices: {ex.Message}");
            }
        }

    }
}
