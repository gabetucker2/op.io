using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(
            List<Shape> shapes,
            List<StaticObject> staticObjects,
            Player player,
            bool destroyOnCollision
        )
        {
            if (shapes == null)
                throw new ArgumentNullException(nameof(shapes), "Shapes list cannot be null.");

            if (staticObjects == null)
                throw new ArgumentNullException(nameof(staticObjects), "Static objects list cannot be null.");

            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player cannot be null.");

            // Handle collisions with dynamic shapes
            for (int i = 0; i < shapes.Count; i++)
            {
                Shape shape = shapes[i];

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

            // Handle collisions with static objects
            foreach (var staticObject in staticObjects)
            {
                if (CheckCollision(player, staticObject))
                {
                    HandleStaticCollision(player, staticObject);
                }
            }
        }

        private bool CheckCollision(Player player, Shape shape)
        {
            if (shape == null)
                throw new ArgumentNullException(nameof(shape), "Shape cannot be null.");

            if (shape.Type == "Rectangle")
            {
                return IsCircleOverlappingRectangle(player.Position, player.Radius, shape.Position, shape.Width, shape.Height);
            }

            if (shape.Type == "Circle")
            {
                float distanceSquared = Vector2.DistanceSquared(player.Position, shape.Position);
                float shapeBoundingRadius = shape.Radius - (shape.OutlineWidth > 0 ? 0.1f : 0f);
                float combinedRadius = player.Radius + shapeBoundingRadius;

                return distanceSquared <= combinedRadius * combinedRadius;
            }

            if (shape.Type == "Polygon")
            {
                return IsCircleOverlappingPolygon(player.Position, player.Radius, shape);
            }

            throw new ArgumentException($"Unsupported shape type: {shape.Type}", nameof(shape));
        }

        private bool CheckCollision(Player player, StaticObject staticObject)
        {
            if (staticObject == null)
                throw new ArgumentNullException(nameof(staticObject), "Static object cannot be null.");

            return IsCircleOverlappingRectangle(player.Position, player.Radius, staticObject.Position, staticObject.Width, staticObject.Height);
        }

        private void ApplyForces(Player player, Shape shape)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player cannot be null.");

            if (shape == null)
                throw new ArgumentNullException(nameof(shape), "Shape cannot be null.");

            Vector2 direction = shape.Position - player.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (player.Weight + shape.Weight) / (distance > 0.1f ? distance : 0.1f);

            player.Position -= direction * force * (shape.Weight / (player.Weight + shape.Weight));
            shape.Position += direction * force * (player.Weight / (player.Weight + shape.Weight));
        }

        private void HandleStaticCollision(Player player, StaticObject staticObject)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player), "Player cannot be null.");

            if (staticObject == null)
                throw new ArgumentNullException(nameof(staticObject), "Static object cannot be null.");

            Vector2 direction = player.Position - staticObject.Position;
            float distance = direction.Length();
            float overlap = (player.Radius + staticObject.BoundingRadius) - distance;

            if (distance > 0 && overlap > 0)
            {
                direction.Normalize();
                player.Position += direction * overlap;
            }
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

        private bool IsCircleOverlappingPolygon(Vector2 circleCenter, float circleRadius, Shape polygon)
        {
            if (polygon == null || polygon.Type != "Polygon")
                throw new ArgumentException("Shape must be a polygon.", nameof(polygon));

            float adjustedRadius = polygon.Radius - (polygon.OutlineWidth > 0 ? 0.1f : 0f);
            var vertices = GeneratePolygonVertices(polygon.Position, polygon.Sides, adjustedRadius);

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
            if (sides < 3)
                throw new ArgumentException("A polygon must have at least 3 sides.", nameof(sides));

            if (radius <= 0)
                throw new ArgumentException("Radius must be greater than 0.", nameof(radius));

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
