using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace op.io
{
    public class ShapesManager
    {
        private List<Shape> _shapes;

        public ShapesManager()
        {
            _shapes = new List<Shape>();
        }

        public void AddShape(Vector2 position, string type, int width, int height, int sides, XnaColor color, XnaColor outlineColor, int outlineWidth, bool enableCollision, bool enablePhysics)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("Shape type cannot be null or empty.", nameof(type));
            if (type == "Circle" && width <= 0 && height <= 0)
                throw new ArgumentException("Circle must have a valid radius specified.", nameof(width));
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be greater than 0.", nameof(width));
            if (type == "Polygon" && sides < 3)
                throw new ArgumentException("Number of sides must be 3 or greater for polygons.", nameof(sides));
            if (outlineWidth < 0)
                throw new ArgumentException("Outline width must be non-negative.", nameof(outlineWidth));

            // Handle circle shapes by converting radius into width and height
            if (type == "Circle")
            {
                if (width <= 0 || height <= 0)
                    throw new ArgumentException("Circle must have a valid radius specified.", nameof(width));

                int radius = Math.Max(width, height) / 2;
                width = height = 2 * radius; // Enforce proper width and height for circles
            }

            // Handle rectangle shape explicitly
            if (type == "Rectangle")
            {
                _shapes.Add(new Shape(position, type, width, height, 0, color, outlineColor, outlineWidth, enableCollision, enablePhysics));
            }
            else if (type == "Polygon" || type == "Circle")
            {
                int size = Math.Max(width, height); // Use the larger dimension
                _shapes.Add(new Shape(position, type, size, size, sides, color, outlineColor, outlineWidth, enableCollision, enablePhysics));
            }
            else
            {
                throw new ArgumentException($"Unsupported shape type: {type}", nameof(type));
            }
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            foreach (var shape in _shapes)
            {
                shape.LoadContent(graphicsDevice);
            }
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0)
                throw new ArgumentException("Delta time cannot be negative.", nameof(deltaTime));

            foreach (var shape in _shapes)
            {
                shape.Update(deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");

            foreach (var shape in _shapes)
            {
                shape.Draw(spriteBatch, debugEnabled);
            }
        }

        public List<Shape> GetShapes()
        {
            return _shapes;
        }
    }

    public class Shape
    {
        public Vector2 Position { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Radius { get; private set; }
        public int Weight { get; set; }
        public string Type { get; set; }
        private int _sides;
        public int Sides { get { return _sides; } }
        private Microsoft.Xna.Framework.Color _color;
        private Microsoft.Xna.Framework.Color _outlineColor;
        private int _outlineWidth;
        public int OutlineWidth { get { return _outlineWidth; } }
        private Texture2D _texture;
        private bool _enableCollision;
        private bool _enablePhysics;

        public Shape(Vector2 position, string type, int width, int height, int sides, Microsoft.Xna.Framework.Color color, Microsoft.Xna.Framework.Color outlineColor, int outlineWidth, bool enableCollision, bool enablePhysics)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("Shape type cannot be null or empty.", nameof(type));
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be greater than 0.", nameof(width));
            if (sides < 0)
                throw new ArgumentException("Number of sides must be 0 or greater.", nameof(sides));
            if (outlineWidth < 0)
                throw new ArgumentException("Outline width must be non-negative.", nameof(outlineWidth));

            Position = position;
            Type = type;

            // Set width and height for all shapes
            Width = width;
            Height = height;

            // Specific handling for circles
            if (type == "Circle")
            {
                Radius = Math.Max(width, height) / 2;
            }

            // Specific handling for polygons
            if (type == "Polygon")
            {
                Radius = Math.Max(width, height) / 2;
            }

            _sides = sides;
            _color = color;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
            _enableCollision = enableCollision;
            _enablePhysics = enablePhysics;
            Weight = Math.Max(width, height);
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null.");

            // Use appropriate dimensions for texture creation
            int textureWidth = Type == "Rectangle" ? Width : Radius * 2;
            int textureHeight = Type == "Rectangle" ? Height : Radius * 2;

            _texture = new Texture2D(graphicsDevice, textureWidth, textureHeight);
            var data = new Microsoft.Xna.Framework.Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    if (IsPointInsideShape(x, y, textureWidth, textureHeight))
                    {
                        data[y * textureWidth + x] = IsOnOutline(x, y, textureWidth, textureHeight) ? _outlineColor : _color;
                    }
                    else
                    {
                        data[y * textureWidth + x] = Microsoft.Xna.Framework.Color.Transparent;
                    }
                }
            }

            _texture.SetData(data);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0)
                throw new ArgumentException("Delta time cannot be negative.", nameof(deltaTime));

            if (_enablePhysics)
            {
                // Add any desired physics behavior here
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (spriteBatch == null)
                throw new ArgumentNullException(nameof(spriteBatch), "SpriteBatch cannot be null.");
            if (_texture == null)
                throw new InvalidOperationException("Texture must be loaded before drawing.");

            Microsoft.Xna.Framework.Rectangle bounds;

            if (Type == "Rectangle")
            {
                bounds = new Microsoft.Xna.Framework.Rectangle(
                    (int)(Position.X - Width / 2),
                    (int)(Position.Y - Height / 2),
                    Width,
                    Height
                );
            }
            else
            {
                int diameter = Radius * 2;
                bounds = new Microsoft.Xna.Framework.Rectangle(
                    (int)(Position.X - Radius),
                    (int)(Position.Y - Radius),
                    diameter,
                    diameter
                );
            }

            spriteBatch.Draw(_texture, bounds, Microsoft.Xna.Framework.Color.White);

            if (debugEnabled)
            {
                DebugVisualizer.DrawDebugCircle(spriteBatch, Position);
            }
        }

        private bool IsPointInsideShape(int x, int y, int textureWidth, int textureHeight)
        {
            int centerX = textureWidth / 2;
            int centerY = textureHeight / 2;

            if (Type == "Circle")
            {
                float radius = Radius - (_outlineWidth > 0 ? 0.1f : 0f); // Reduce fill-border artifacting
                return Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2) <= radius * radius;
            }
            else if (Type == "Polygon")
            {
                // Adjust the radius for artifacting reduction
                float adjustedRadius = Radius - (_outlineWidth > 0 ? 0.1f : 0f);
                Vector2 point = new Vector2(x - centerX, y - centerY);
                return IsPointInsidePolygon(point, adjustedRadius);
            }
            else if (Type == "Rectangle")
            {
                return x >= 0 && x < Width && y >= 0 && y < Height;
            }

            return false;
        }

        public bool IsPointInsidePolygon(Vector2 point, float adjustedRadius)
        {
            float localX = point.X;
            float localY = point.Y;

            double angleIncrement = 2 * Math.PI / _sides;
            var vertices = new List<Vector2>();

            for (int i = 0; i < _sides; i++)
            {
                double angle = i * angleIncrement;
                vertices.Add(new Vector2(
                    (float)(adjustedRadius * Math.Cos(angle)),
                    (float)(adjustedRadius * Math.Sin(angle))
                ));
            }

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

        private bool IsOnOutline(int x, int y, int textureWidth, int textureHeight)
        {
            float distanceToEdge = DistanceToShapeEdge(x, y, textureWidth, textureHeight);

            if (Type == "Rectangle")
            {
                // Check if the point is within the outline bounds of the rectangle
                float left = _outlineWidth;
                float right = Width - _outlineWidth;
                float top = _outlineWidth;
                float bottom = Height - _outlineWidth;

                bool insideInnerRectangle = (x > left && x < right && y > top && y < bottom);
                bool insideOuterRectangle = (x >= 0 && x < Width && y >= 0 && y < Height);

                return insideOuterRectangle && !insideInnerRectangle;
            }

            // For other shapes
            return distanceToEdge <= _outlineWidth && distanceToEdge > 0;
        }

        private float DistanceToShapeEdge(int x, int y, int textureWidth, int textureHeight)
        {
            int centerX = textureWidth / 2;
            int centerY = textureHeight / 2;

            if (Type == "Circle")
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distanceToCenter = MathF.Sqrt(dx * dx + dy * dy);
                return MathF.Abs(distanceToCenter - Radius);
            }
            else if (Type == "Polygon")
            {
                return DistanceToPolygonEdge(x, y, centerX, centerY, Radius);
            }

            return float.MaxValue; // Rectangles do not use distance-based checks for edges
        }

        private float DistanceToPolygonEdge(int x, int y, int centerX, int centerY, int radius)
        {
            double angleIncrement = 2 * Math.PI / _sides;
            var points = new List<Vector2>();

            for (int i = 0; i < _sides; i++)
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
