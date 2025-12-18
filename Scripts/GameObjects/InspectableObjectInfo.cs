using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public sealed class InspectableObjectInfo
    {
        public InspectableObjectInfo(GameObject source)
        {
            Source = source;
            Refresh();
        }

        public GameObject Source { get; }
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Type { get; private set; }
        public Vector2 Position { get; private set; }
        public float Rotation { get; private set; }
        public float Mass { get; private set; }
        public bool IsCollidable { get; private set; }
        public bool IsDestructible { get; private set; }
        public bool StaticPhysics { get; private set; }
        public bool IsPlayer { get; private set; }
        public Shape Shape { get; private set; }
        public Color FillColor { get; private set; }
        public Color OutlineColor { get; private set; }
        public int OutlineWidth { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Sides { get; private set; }
        public bool IsValid { get; private set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"ID {Id}" : Name;

        public void Refresh()
        {
            if (Source == null || Source.Shape == null)
            {
                IsValid = false;
                return;
            }

            Id = Source.ID;
            Name = Source.Name;
            Type = Source.Type;
            Position = Source.Position;
            Rotation = Source.Rotation;
            Mass = Source.Mass;
            IsCollidable = Source.IsCollidable;
            IsDestructible = Source.IsDestructible;
            StaticPhysics = Source.StaticPhysics;
            Shape = Source.Shape;
            FillColor = Source.FillColor;
            OutlineColor = Source.OutlineColor;
            OutlineWidth = Source.OutlineWidth;
            Width = Shape.Width;
            Height = Shape.Height;
            Sides = Shape.Sides;

            if (Source is Agent agent)
            {
                IsPlayer = agent.IsPlayer;
            }
            else
            {
                IsPlayer = false;
            }

            IsValid = true;
        }

        public InspectableObjectInfo Clone()
        {
            InspectableObjectInfo copy = new(Source);
            copy.Refresh();
            return copy;
        }
    }

    public static class GameObjectInspector
    {
        public static InspectableObjectInfo FindHoveredObject(Vector2 gameCursorPosition)
        {
            List<GameObject> objects = GameObjectRegister.GetRegisteredGameObjects();
            if (objects == null || objects.Count == 0)
            {
                return null;
            }

            InspectableObjectInfo best = null;
            float bestScore = float.MaxValue;

            foreach (GameObject obj in objects)
            {
                if (!IsInspectable(obj))
                {
                    continue;
                }

                if (!IsPointInside(obj, gameCursorPosition, out float score))
                {
                    continue;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new InspectableObjectInfo(obj);
                }
            }

            return best;
        }

        private static bool IsInspectable(GameObject gameObject)
        {
            return gameObject != null &&
                gameObject.Shape != null &&
                !gameObject.Shape.IsPrototype;
        }

        private static bool IsPointInside(GameObject gameObject, Vector2 point, out float score)
        {
            score = float.MaxValue;

            Shape shape = gameObject.Shape;
            if (shape == null)
            {
                return false;
            }

            Vector2 delta = point - gameObject.Position;
            float radius = gameObject.BoundingRadius + shape.OutlineWidth + 2f;
            float radiusSq = radius * radius;
            float distanceSq = delta.LengthSquared();

            if (distanceSq > radiusSq)
            {
                return false;
            }

            bool inside = shape.ShapeType switch
            {
                "Rectangle" => PointInsideRectangle(delta, shape, gameObject.Rotation),
                "Circle" => PointInsideEllipse(delta, shape),
                "Polygon" => PointInsidePolygon(point, shape, gameObject),
                _ => PointInsideRectangle(delta, shape, gameObject.Rotation)
            };

            if (inside)
            {
                score = distanceSq;
            }

            return inside;
        }

        private static bool PointInsideRectangle(Vector2 localPoint, Shape shape, float rotation)
        {
            float cos = MathF.Cos(-rotation);
            float sin = MathF.Sin(-rotation);

            float x = localPoint.X * cos - localPoint.Y * sin;
            float y = localPoint.X * sin + localPoint.Y * cos;

            float halfWidth = (shape.Width / 2f) + shape.OutlineWidth;
            float halfHeight = (shape.Height / 2f) + shape.OutlineWidth;

            return MathF.Abs(x) <= halfWidth && MathF.Abs(y) <= halfHeight;
        }

        private static bool PointInsideEllipse(Vector2 localPoint, Shape shape)
        {
            float rx = (shape.Width / 2f) + shape.OutlineWidth;
            float ry = (shape.Height / 2f) + shape.OutlineWidth;

            if (rx <= 0 || ry <= 0)
            {
                return false;
            }

            float normalized = (localPoint.X * localPoint.X) / (rx * rx) +
                (localPoint.Y * localPoint.Y) / (ry * ry);

            return normalized <= 1.0f;
        }

        private static bool PointInsidePolygon(Vector2 point, Shape shape, GameObject gameObject)
        {
            Vector2[] vertices = shape.GetTransformedVertices(gameObject.Position, gameObject.Rotation);
            if (vertices == null || vertices.Length == 0)
            {
                return false;
            }

            bool inside = false;
            int count = vertices.Length;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 vi = vertices[i];
                Vector2 vj = vertices[j];

                bool intersects = ((vi.Y > point.Y) != (vj.Y > point.Y)) &&
                    (point.X < (vj.X - vi.X) * (point.Y - vi.Y) / (vj.Y - vi.Y + 0.0001f) + vi.X);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
