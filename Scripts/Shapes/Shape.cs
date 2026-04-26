using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public class Shape : IDisposable
    {
        public string ShapeType { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Sides { get; private set; }
        public Color FillColor => _fillColor;
        public Color OutlineColor => _outlineColor;
        public int OutlineWidth => _outlineWidth;
        public bool IsPrototype { get; set; } = false;
        public bool SkipHover { get; set; } = false;

        private Color _fillColor;
        private Color _outlineColor;
        private int _outlineWidth;

        private ShapeRenderer _renderer;

        public Shape(string shapeType, int width, int height, int sides, Color fillColor, Color outlineColor, int outlineWidth, bool isPrototype = false)
        {
            // Validate ShapeType and dimensions early
            if (string.IsNullOrEmpty(shapeType) || (shapeType != "Rectangle" && shapeType != "Circle" && shapeType != "Polygon"))
            {
                throw new ArgumentException("Invalid shape type. Valid types are Rectangle, Circle, and Polygon.");
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException($"Invalid shape dimensions: Width={width}, Height={height}. Must be positive.");
            }

            if (outlineWidth < 0)
            {
                outlineWidth = 0;  // Default negative outline width to 0
            }

            ShapeType = shapeType;
            Width = width;
            Height = height;
            Sides = sides;
            _fillColor = fillColor;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
            IsPrototype = isPrototype;

            // Initialize ShapeRenderer for this shape
            _renderer = new ShapeRenderer();
        }

        /// <summary>
        /// Updates width and height (e.g. when a circle's radius changes due to mass).
        /// Reloads the renderer so the visual matches the new dimensions.
        /// </summary>
        public void UpdateDimensions(int width, int height, GraphicsDevice graphicsDevice)
        {
            if (width <= 0 || height <= 0)
                return;
            Width = width;
            Height = height;
            _renderer.Dispose();
            _renderer = new ShapeRenderer();
            _renderer.LoadContent(this, graphicsDevice);
        }

        // This method uses ShapeRenderer to load the shape content
        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _renderer.LoadContent(this, graphicsDevice);
        }

        // This method is used to get the transformed vertices, no changes in logic here
        public Vector2[] GetTransformedVertices(Vector2 position, float rotation)
        {
            ShapeVertexGenerator generator = new ShapeVertexGenerator(ShapeType, Width, Height, Sides);
            Vector2[] vertices = generator.GetVertices();

            Vector2[] transformedVertices = new Vector2[vertices.Length];
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 vertex = vertices[i];
                float rotatedX = vertex.X * cos - vertex.Y * sin;
                float rotatedY = vertex.X * sin + vertex.Y * cos;
                transformedVertices[i] = new Vector2(rotatedX + position.X, rotatedY + position.Y);
            }

            return transformedVertices;
        }

        // Delegate the Draw call to ShapeRenderer
        public void Draw(SpriteBatch spriteBatch, GameObject GO)
        {
            _renderer.Draw(spriteBatch, GO);
        }

        public void DrawFlash(SpriteBatch spriteBatch, GameObject GO)
        {
            _renderer.DrawFlash(spriteBatch, GO);
        }

        public void DrawAt(SpriteBatch spriteBatch, Vector2 position, float rotation, bool applyWorldTint = false)
        {
            _renderer.DrawAt(spriteBatch, position, rotation, applyWorldTint);
        }

        public void DrawAt(SpriteBatch spriteBatch, Vector2 position, float rotation, Vector2 scale, bool applyWorldTint = false)
        {
            _renderer.DrawAt(spriteBatch, position, rotation, scale, applyWorldTint);
        }

        public void DrawAt(SpriteBatch spriteBatch, Vector2 position, float rotation, Vector2 scale, float opacity, bool applyWorldTint = false)
        {
            _renderer.DrawAt(spriteBatch, position, rotation, scale, opacity, applyWorldTint);
        }

        // Dispose method to clean up resources
        public void Dispose()
        {
            _renderer.Dispose();
        }
    }
}
