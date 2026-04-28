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
        public Vector2 TextureWorldScale { get; private set; } = Vector2.One;

        internal bool HasCustomTexture => _customTextureData != null &&
            _customTextureWidth > 0 &&
            _customTextureHeight > 0;
        internal int CustomTextureWidth => _customTextureWidth;
        internal int CustomTextureHeight => _customTextureHeight;
        internal Color[] CustomTextureData => _customTextureData;

        private Color _fillColor;
        private Color _outlineColor;
        private int _outlineWidth;
        private Color[] _customTextureData;
        private int _customTextureWidth;
        private int _customTextureHeight;

        private ShapeRenderer _renderer;

        public Shape(string shapeType, int width, int height, int sides, Color fillColor, Color outlineColor, int outlineWidth, bool isPrototype = false)
        {
            // Validate ShapeType and dimensions early
            if (string.IsNullOrEmpty(shapeType) || (shapeType != "Rectangle" && shapeType != "Circle" && shapeType != "Polygon" && shapeType != "Texture"))
            {
                throw new ArgumentException("Invalid shape type. Valid types are Rectangle, Circle, Polygon, and Texture.");
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

        public static Shape CreateTextureShape(
            int width,
            int height,
            Color[] textureData,
            int textureWidth,
            int textureHeight,
            Vector2 textureWorldScale,
            bool isPrototype = false)
        {
            if (textureData == null || textureData.Length == 0)
            {
                throw new ArgumentException("Texture shape requires non-empty texture data.", nameof(textureData));
            }

            if (textureWidth <= 0 || textureHeight <= 0)
            {
                throw new ArgumentException("Texture shape requires positive texture dimensions.");
            }

            if (textureData.Length != textureWidth * textureHeight)
            {
                throw new ArgumentException("Texture shape pixel data length must match texture dimensions.", nameof(textureData));
            }

            Shape shape = new("Texture", width, height, 0, Color.White, Color.Transparent, 0, isPrototype)
            {
                _customTextureData = (Color[])textureData.Clone(),
                _customTextureWidth = textureWidth,
                _customTextureHeight = textureHeight,
                TextureWorldScale = new Vector2(
                    MathF.Max(textureWorldScale.X, 0.0001f),
                    MathF.Max(textureWorldScale.Y, 0.0001f))
            };
            return shape;
        }

        /// <summary>
        /// Updates dimensions and optional appearance data, then rebuilds the
        /// cached texture when a graphics device is available.
        /// </summary>
        public void UpdateDimensions(
            int width,
            int height,
            GraphicsDevice graphicsDevice,
            Color? fillColor = null,
            Color? outlineColor = null,
            int? outlineWidth = null)
        {
            if (width <= 0 || height <= 0)
                return;

            Color resolvedFillColor = fillColor ?? _fillColor;
            Color resolvedOutlineColor = outlineColor ?? _outlineColor;
            int resolvedOutlineWidth = outlineWidth.HasValue
                ? Math.Max(0, outlineWidth.Value)
                : _outlineWidth;

            bool dimensionsChanged = Width != width || Height != height;
            bool appearanceChanged =
                _fillColor != resolvedFillColor ||
                _outlineColor != resolvedOutlineColor ||
                _outlineWidth != resolvedOutlineWidth;

            if (!dimensionsChanged && !appearanceChanged)
                return;

            Width = width;
            Height = height;
            _fillColor = resolvedFillColor;
            _outlineColor = resolvedOutlineColor;
            _outlineWidth = resolvedOutlineWidth;

            if (graphicsDevice == null)
                return;

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
