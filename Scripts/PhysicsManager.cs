using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(List<Shape> shapes, Player player, bool destroyOnCollision)
        {
            if (shapes == null)
                throw new ArgumentNullException(nameof(shapes), "Shapes list cannot be null.");

            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player cannot be null.");

            for (int i = 0; i < shapes.Count; i++)
            {
                Shape shape = shapes[i];

                // Check for collision using shape-specific logic
                if (CheckCollision(player, shape))
                {
                    ApplyForces(player, shape);

                    if (destroyOnCollision)
                    {
                        shapes.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
        }

        private bool CheckCollision(Player player, Shape shape)
        {
            if (shape == null)
                throw new ArgumentNullException(nameof(shape), "Shape cannot be null.");

            // Handle rectangle collisions
            if (shape.Type == "Rectangle")
            {
                return IsCircleOverlappingRectangle(player.Position, player.Radius, shape);
            }

            // Handle circle collisions
            if (shape.Type == "Circle")
            {
                float distanceSquared = Vector2.DistanceSquared(player.Position, shape.Position);
                float shapeBoundingRadius = shape.Radius - (shape.OutlineWidth > 0 ? 0.1f : 0f);
                float combinedRadius = player.Radius + shapeBoundingRadius;

                return distanceSquared <= combinedRadius * combinedRadius;
            }

            // Handle polygon collisions
            if (shape.Type == "Polygon")
            {
                return IsCircleOverlappingPolygon(player.Position, player.Radius, shape);
            }

            return false;
        }

        private void ApplyForces(Player player, Shape shape)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player cannot be null.");

            if (shape == null)
                throw new ArgumentNullException(nameof(shape), "Shape cannot be null.");

            // Calculate direction of force
            Vector2 direction = shape.Position - player.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (player.Weight + shape.Weight) / (distance > 0.1f ? distance : 0.1f);

            // Apply forces to the player and shape
            player.Position -= direction * force * (shape.Weight / (player.Weight + shape.Weight));
            shape.Position += direction * force * (player.Weight / (player.Weight + shape.Weight));
        }

        private bool IsCircleOverlappingRectangle(Vector2 circleCenter, float circleRadius, Shape rectangle)
        {
            if (rectangle == null || rectangle.Type != "Rectangle")
                throw new ArgumentException("Shape must be a rectangle.", nameof(rectangle));

            float left = rectangle.Position.X - rectangle.Width / 2;
            float right = rectangle.Position.X + rectangle.Width / 2;
            float top = rectangle.Position.Y - rectangle.Height / 2;
            float bottom = rectangle.Position.Y + rectangle.Height / 2;

            // Clamp the circle's center to the nearest point on the rectangle
            float nearestX = Math.Clamp(circleCenter.X, left, right);
            float nearestY = Math.Clamp(circleCenter.Y, top, bottom);

            // Calculate the distance from the circle's center to this point
            float distanceSquared = Vector2.DistanceSquared(circleCenter, new Vector2(nearestX, nearestY));

            // Check if the distance is less than or equal to the circle's radius squared
            return distanceSquared <= circleRadius * circleRadius;
        }

        private bool IsCircleOverlappingPolygon(Vector2 circleCenter, float circleRadius, Shape polygon)
        {
            if (polygon == null || polygon.Type != "Polygon")
                throw new ArgumentException("Shape must be a polygon.", nameof(polygon));

            float adjustedRadius = polygon.Radius - (polygon.OutlineWidth > 0 ? 0.1f : 0f);
            var vertices = GeneratePolygonVertices(polygon.Position, polygon.Sides, adjustedRadius);

            // Step 1: Check if the circle's center is inside the polygon
            int intersections = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 p1 = vertices[i];
                Vector2 p2 = vertices[(i + 1) % vertices.Count];

                if ((p1.Y > circleCenter.Y) != (p2.Y > circleCenter.Y)) // Check if circle center is between p1.Y and p2.Y
                {
                    float intersectX = p1.X + (circleCenter.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    if (circleCenter.X < intersectX)
                    {
                        intersections++;
                    }
                }
            }

            if (intersections % 2 != 0) // Circle's center is inside the polygon
            {
                return true;
            }

            // Step 2: Check if the circle overlaps any polygon edge
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 p1 = vertices[i];
                Vector2 p2 = vertices[(i + 1) % vertices.Count];

                if (CircleIntersectsSegment(circleCenter, circleRadius, p1, p2))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CircleIntersectsSegment(Vector2 circleCenter, float radius, Vector2 segA, Vector2 segB)
        {
            Vector2 seg = segB - segA;
            Vector2 pointToA = circleCenter - segA;

            float projection = Vector2.Dot(pointToA, seg) / seg.LengthSquared();
            projection = Math.Clamp(projection, 0, 1);

            Vector2 closestPoint = segA + projection * seg;
            float distanceSquared = Vector2.DistanceSquared(circleCenter, closestPoint);

            return distanceSquared <= radius * radius;
        }

        private List<Vector2> GeneratePolygonVertices(Vector2 center, int sides, float radius)
        {
            var vertices = new List<Vector2>();
            double angleIncrement = 2 * Math.PI / sides;

            for (int i = 0; i < sides; i++)
            {
                double angle = i * angleIncrement;
                vertices.Add(new Vector2(
                    center.X + (float)(radius * Math.Cos(angle)),
                    center.Y + (float)(radius * Math.Sin(angle))
                ));
            }

            return vertices;
        }
    }
}
