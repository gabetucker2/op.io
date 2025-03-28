using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public class Shape
    {
        public Vector2 Position { get; set; }
        public string Type { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Sides { get; private set; }
        public Color FillColor => _fillColor;
        public Color OutlineColor => _outlineColor;
        public int OutlineWidth => _outlineWidth;

        private Color _fillColor;
        private Color _outlineColor;
        private int _outlineWidth;
        private Texture2D _texture;
        private Vector2 _origin;

        public Shape(Vector2 position, string type, int width, int height, int sides, Color fillColor, Color outlineColor, int outlineWidth)
        {
            if (string.IsNullOrEmpty(type))
            {
                DebugManager.PrintError("Shape type is null or empty. Defaulting to Rectangle.");
                type = "Rectangle";
            }

            if (width <= 0 || height <= 0)
            {
                DebugManager.PrintError($"Invalid shape dimensions: Width={width}, Height={height}. Defaulting to 10x10.");
                width = height = 10;
            }

            if (outlineWidth < 0)
            {
                DebugManager.PrintWarning($"Negative outline width ({outlineWidth}) detected. Defaulting to 0.");
                outlineWidth = 0;
            }

            Position = position;
            Type = type;
            Width = width;
            Height = height;
            Sides = sides;
            _fillColor = fillColor;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                DebugManager.PrintError("LoadContent failed: GraphicsDevice is null.");
                return;
            }

            int textureWidth = Width + 2 * _outlineWidth;
            int textureHeight = Height + 2 * _outlineWidth;
            _texture = new Texture2D(graphicsDevice, textureWidth, textureHeight);
            Color[] data = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    Vector2 center = new Vector2(textureWidth / 2f, textureHeight / 2f);
                    float dx = x - center.X;
                    float dy = y - center.Y;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    float outer = MathF.Min(Width, Height) / 2f + _outlineWidth;
                    float inner = MathF.Min(Width, Height) / 2f;

                    bool isInside = dist <= inner;
                    bool isOutline = dist > inner && dist <= outer;

                    if (Type == "Circle")
                        data[y * textureWidth + x] = isInside ? _fillColor : (isOutline ? _outlineColor : Color.Transparent);
                    else if (Type == "Polygon" && Sides >= 3)
                    {
                        bool inside = RenderPolygonPixel(dx, dy, Sides, inner);
                        bool inOutline = !inside && RenderPolygonPixel(dx, dy, Sides, outer);

                        data[y * textureWidth + x] = inside ? _fillColor :
                                                      (inOutline ? _outlineColor : Color.Transparent);
                    }
                    else
                        data[y * textureWidth + x] = _fillColor;
                }
            }

            _texture.SetData(data);
            _origin = new Vector2(textureWidth / 2f, textureHeight / 2f);

            DebugManager.PrintInfo($"Loaded texture for shape {Type} at {Position} [{Width}x{Height}]");
        }

        private bool RenderPolygonPixel(float dx, float dy, int sides, float radius)
        {
            float angle = MathF.Atan2(dy, dx);
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            float sectorAngle = MathF.Tau / sides;
            float halfSector = sectorAngle / 2;

            float rotatedAngle = (angle + MathF.Tau) % sectorAngle;

            float cornerDistance = MathF.Cos(halfSector) / MathF.Cos(rotatedAngle - halfSector);
            float maxDistance = radius * cornerDistance;

            return distance <= maxDistance;
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled, float rotation = 0f)
        {
            if (_texture == null)
            {
                DebugManager.PrintError($"Shape at {Position} attempted to draw without texture. Call LoadContent first.");
                return;
            }

            if (spriteBatch == null)
            {
                DebugManager.PrintError("Draw failed: SpriteBatch is null.");
                return;
            }

            if (Width <= 0 || Height <= 0)
            {
                DebugManager.PrintError($"Cannot draw shape with non-positive dimensions: Width={Width}, Height={Height}");
                return;
            }

            spriteBatch.Draw(
                _texture,
                position: Position,
                sourceRectangle: null,
                color: Color.White,
                rotation: Type == "Circle" ? 0f : rotation,
                origin: _origin,
                scale: 1f,
                effects: SpriteEffects.None,
                layerDepth: 0f
            );

            if (debugEnabled)
            {
                DebugManager.PrintDebug($"Drawing shape at {Position} with dimensions ({Width}, {Height}) and rotation {(Type == "Circle" ? 0f : rotation):F2} rad");
            }
        }
    }
}
