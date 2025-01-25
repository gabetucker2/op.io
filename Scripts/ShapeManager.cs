using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class ShapesManager
    {
        private List<Shape> _shapes;

        public ShapesManager()
        {
            _shapes = new List<Shape>();
        }

        public void AddShape(Vector2 position, string type, int size, int sides, Color color, Color outlineColor, int outlineWidth, bool enableCollision, bool enablePhysics)
        {
            _shapes.Add(new Shape(position, type, size, sides, color, outlineColor, outlineWidth, enableCollision, enablePhysics));
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            foreach (var shape in _shapes)
            {
                shape.LoadContent(graphicsDevice);
            }
        }

        public void Update(float deltaTime)
        {
            foreach (var shape in _shapes)
            {
                shape.Update(deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            foreach (var shape in _shapes)
            {
                shape.Draw(spriteBatch, debugEnabled);
            }
        }

        public List<Shape> GetShapes() => _shapes;
    }

    public class Shape
    {
        public Vector2 Position { get; set; }
        public int Size { get; set; }
        public int Weight { get; set; }
        public string Type { get; set; }
        private int _sides;
        private Color _color;
        private Color _outlineColor;
        private int _outlineWidth;
        private Texture2D _texture;
        private bool _enableCollision;
        private bool _enablePhysics;

        public Shape(Vector2 position, string type, int size, int sides, Color color, Color outlineColor, int outlineWidth, bool enableCollision, bool enablePhysics)
        {
            Position = position;
            Type = type;
            Size = size;
            _sides = sides;
            _color = color;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
            _enableCollision = enableCollision;
            _enablePhysics = enablePhysics;
            Weight = size;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, Size, Size);
            Color[] data = new Color[Size * Size];

            int filledPixels = 0;
            int outlinePixels = 0;
            int transparentPixels = 0;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (IsPointInsideShape(x, y))
                    {
                        if (IsOnOutline(x, y))
                        {
                            data[y * Size + x] = _outlineColor;
                            outlinePixels++;
                        }
                        else
                        {
                            data[y * Size + x] = _color;
                            filledPixels++;
                        }
                    }
                    else
                    {
                        data[y * Size + x] = Color.Transparent;
                        transparentPixels++;
                    }
                }
            }

            _texture.SetData(data);
        }

        public void Update(float deltaTime)
        {
            if (_enablePhysics)
            {
                // Add any desired physics behavior here, such as movement or collision response
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (_texture == null)
            {
                Console.WriteLine("Error: Texture not loaded for shape at position " + Position);
                return;
            }

            Rectangle bounds = new Rectangle((int)(Position.X - Size / 2), (int)(Position.Y - Size / 2), Size, Size);
            spriteBatch.Draw(_texture, bounds, Color.White);

            if (debugEnabled)
            {
                DebugVisualizer.DrawDebugCircle(spriteBatch, Position);
            }
        }

        private bool IsPointInsideShape(int x, int y)
        {
            int centerX = Size / 2;
            int centerY = Size / 2;

            if (Type == "Circle")
            {
                float radius = Size / 2f - (_outlineWidth > 0 ? 0.1f : 0f); // Adjust radius if outline exists
                return Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2) <= radius * radius;
            }
            else if (_sides >= 3) // Handle polygons as shapes with sides >= 3
            {
                Vector2 point = new Vector2(x - centerX, y - centerY); // Convert to local space
                return IsPointInsidePolygon(point);
            }

            return false;
        }

        public bool IsPointInsidePolygon(Vector2 point)
        {
            // Translate the point to local space
            float localX = point.X;
            float localY = point.Y;

            double angleIncrement = 2 * Math.PI / _sides;
            var vertices = new List<Vector2>();

            // Generate polygon vertices in local space
            for (int i = 0; i < _sides; i++)
            {
                double angle = i * angleIncrement;
                vertices.Add(new Vector2(
                    (float)(Size / 2 * Math.Cos(angle)),
                    (float)(Size / 2 * Math.Sin(angle))
                ));
            }

            // Ray-casting algorithm to count intersections
            int intersections = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % vertices.Count];

                if ((p1.Y > localY) != (p2.Y > localY))
                {
                    float intersectX = p1.X + (localY - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    if (localX < intersectX)
                    {
                        intersections++;
                    }
                }
            }

            return intersections % 2 != 0;
        }

        private bool IsOnOutline(int x, int y)
        {
            float distanceToEdge = DistanceToShapeEdge(x, y);
            return distanceToEdge <= _outlineWidth && distanceToEdge > 0;
        }

        private float DistanceToShapeEdge(int x, int y)
        {
            int centerX = Size / 2;
            int centerY = Size / 2;

            if (Type == "Circle")
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distanceToCenter = MathF.Sqrt(dx * dx + dy * dy);
                return MathF.Abs(distanceToCenter - Size / 2f);
            }
            else if (_sides >= 3) // Handle polygons
            {
                return DistanceToPolygonEdge(x, y, centerX, centerY, _sides, Size / 2);
            }

            return float.MaxValue;
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
