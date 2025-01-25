using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class FarmShape
    {
        public Vector2 Position;
        public int Size;
        public int Weight;
        private string _shapeType;
        private int _sides;
        private Color _color;
        private Color _outlineColor;
        private int _outlineWidth;
        private Texture2D _texture;

        public FarmShape(Vector2 position, int size, string shapeType, int sides, Color color, int weight, Color outlineColor, int outlineWidth)
        {
            Position = position;
            Size = size;
            _shapeType = shapeType;
            _sides = sides;
            _color = color;
            Weight = weight;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, Size, Size);
            Color[] data = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (IsPointInsidePolygon(x, y, Size / 2, Size / 2, _sides, Size / 2))
                    {
                        if (IsOnOutline(x, y, Size / 2, Size / 2, _sides, Size / 2, _outlineWidth))
                            data[y * Size + x] = _outlineColor;
                        else
                            data[y * Size + x] = _color;
                    }
                    else
                    {
                        data[y * Size + x] = Color.Transparent;
                    }
                }
            }

            _texture.SetData(data);
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            spriteBatch.Draw(_texture, Position - new Vector2(Size / 2f), Color.White);

            if (debugEnabled)
            {
                DebugVisualizer.DrawDebugCircle(spriteBatch, Position);
            }
        }

        public bool IsPointInsidePolygon(Vector2 point)
        {
            // Translate the point to the polygon's local space
            float localX = point.X - Position.X;
            float localY = point.Y - Position.Y;

            double angleIncrement = 2 * Math.PI / _sides;
            var points = new List<Vector2>();

            for (int i = 0; i < _sides; i++)
            {
                double angle = i * angleIncrement;
                points.Add(new Vector2(
                    (float)(Size / 2 * Math.Cos(angle)),
                    (float)(Size / 2 * Math.Sin(angle))
                ));
            }

            int intersections = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];

                if ((p1.Y > localY) != (p2.Y > localY) &&
                    localX < (p2.X - p1.X) * (localY - p1.Y) / (p2.Y - p1.Y) + p1.X)
                {
                    intersections++;
                }
            }

            return intersections % 2 != 0;
        }

        public bool IsPointInsidePolygon(int x, int y, int centerX, int centerY, int sides, float radius)
        {
            double angleIncrement = 2 * Math.PI / sides;
            var points = new List<Vector2>();

            for (int i = 0; i < sides; i++)
            {
                double angle = i * angleIncrement;
                points.Add(new Vector2(
                    centerX + (float)(radius * Math.Cos(angle)),
                    centerY + (float)(radius * Math.Sin(angle))
                ));
            }

            int intersections = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];

                if ((p1.Y > y) != (p2.Y > y) &&
                    x < (p2.X - p1.X) * (y - p1.Y) / (p2.Y - p1.Y) + p1.X)
                {
                    intersections++;
                }
            }

            return intersections % 2 != 0;
        }

        private bool IsOnOutline(int x, int y, int centerX, int centerY, int sides, float radius, int outlineWidth)
        {
            float distanceToEdge = DistanceToPolygonEdge(x, y, centerX, centerY, sides, radius);
            return distanceToEdge <= outlineWidth && distanceToEdge > 0;
        }

        private float DistanceToPolygonEdge(int x, int y, int centerX, int centerY, int sides, float radius)
        {
            double angleIncrement = 2 * Math.PI / sides;
            var points = new List<Vector2>();

            for (int i = 0; i < sides; i++)
            {
                double angle = i * angleIncrement;
                points.Add(new Vector2(
                    centerX + (float)(radius * Math.Cos(angle)),
                    centerY + (float)(radius * Math.Sin(angle))
                ));
            }

            float minDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];

                float distance = PointToSegmentDistance(new Vector2(x, y), p1, p2);
                if (distance < minDistance)
                    minDistance = distance;
            }

            return minDistance;
        }

        private float PointToSegmentDistance(Vector2 point, Vector2 segA, Vector2 segB)
        {
            Vector2 seg = segB - segA;
            Vector2 pointToA = point - segA;

            float projection = Vector2.Dot(pointToA, seg) / seg.LengthSquared();
            projection = MathHelper.Clamp(projection, 0, 1);

            Vector2 closestPoint = segA + projection * seg;
            return Vector2.Distance(point, closestPoint);
        }
    }
}
