﻿using Microsoft.Xna.Framework;
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
        public Vector2[] Vertices { get; private set; }

        private Color _fillColor;
        private Color _outlineColor;
        private int _outlineWidth;
        private Texture2D _texture;
        private Vector2 _origin;

        public Shape(Vector2 position, string type, int width, int height, int sides, Color fillColor, Color outlineColor, int outlineWidth)
        {
            if (string.IsNullOrEmpty(type))
            {
                DebugLogger.PrintError("Shape type is null or empty. Defaulting to Rectangle.");
                type = "Rectangle";
            }

            if (width <= 0 || height <= 0)
            {
                DebugLogger.PrintError($"Invalid shape dimensions: Width={width}, Height={height}. Defaulting to 10x10.");
                width = height = 10;
            }

            if (outlineWidth < 0)
            {
                DebugLogger.PrintWarning($"Negative outline width ({outlineWidth}) detected. Defaulting to 0.");
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

            if (Type == "Polygon" && Sides >= 3)
                GeneratePolygonVertices();
        }

        private void GeneratePolygonVertices()
        {
            Vertices = new Vector2[Sides];
            float angleIncrement = MathF.Tau / Sides;

            for (int i = 0; i < Sides; i++)
            {
                float angle = angleIncrement * i;
                float x = (Width / 2f) * MathF.Cos(angle);
                float y = (Height / 2f) * MathF.Sin(angle);
                Vertices[i] = new Vector2(x, y);
            }
        }

        public Vector2[] GetTransformedVertices(Vector2 position, float rotation)
        {
            try
            {
                Vector2[] localVertices;

                switch (Type)
                {
                    case "Polygon":
                        if (Vertices == null || Vertices.Length != Sides)
                            GeneratePolygonVertices();
                        localVertices = Vertices;
                        break;

                    case "Rectangle":
                        float hw = Width / 2f;
                        float hh = Height / 2f;
                        localVertices = new Vector2[]
                        {
                    new Vector2(-hw, -hh),
                    new Vector2(hw, -hh),
                    new Vector2(hw, hh),
                    new Vector2(-hw, hh)
                        };
                        break;

                    case "Circle":
                        int circleSides = 16;
                        localVertices = new Vector2[circleSides];
                        float angleIncrement = MathF.Tau / circleSides;

                        for (int i = 0; i < circleSides; i++)
                        {
                            float angle = angleIncrement * i;
                            float x = (Width / 2f) * MathF.Cos(angle);
                            float y = (Height / 2f) * MathF.Sin(angle);
                            localVertices[i] = new Vector2(x, y);
                        }
                        break;

                    default:
                        DebugLogger.PrintError($"GetTransformedVertices called on unsupported shape type '{Type}'. Returning empty vertices.");
                        return Array.Empty<Vector2>();
                }

                Vector2[] transformedVertices = new Vector2[localVertices.Length];
                float cos = MathF.Cos(rotation);
                float sin = MathF.Sin(rotation);

                for (int i = 0; i < localVertices.Length; i++)
                {
                    Vector2 vertex = localVertices[i];
                    float rotatedX = vertex.X * cos - vertex.Y * sin;
                    float rotatedY = vertex.X * sin + vertex.Y * cos;
                    transformedVertices[i] = new Vector2(rotatedX + position.X, rotatedY + position.Y);
                }

                return transformedVertices;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in GetTransformedVertices: {ex.Message}");
                return Array.Empty<Vector2>();
            }
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                DebugLogger.PrintError("LoadContent failed: GraphicsDevice is null.");
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
                    bool isOutline = (x < _outlineWidth || x >= textureWidth - _outlineWidth || y < _outlineWidth || y >= textureHeight - _outlineWidth);

                    if (Type == "Rectangle")
                    {
                        data[y * textureWidth + x] = isOutline ? _outlineColor : _fillColor;
                    }
                    else if (Type == "Circle")
                    {
                        Vector2 center = new Vector2(textureWidth / 2f, textureHeight / 2f);
                        float dx = x - center.X;
                        float dy = y - center.Y;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);

                        float outer = MathF.Min(Width, Height) / 2f + _outlineWidth;
                        float inner = MathF.Min(Width, Height) / 2f;

                        bool isInside = dist <= inner;
                        bool isOutlineCircle = dist > inner && dist <= outer;

                        data[y * textureWidth + x] = isInside ? _fillColor : (isOutlineCircle ? _outlineColor : Color.Transparent);
                    }
                    else if (Type == "Polygon" && Sides >= 3)
                    {
                        bool inside = RenderPolygonPixel(x, y, textureWidth, textureHeight, Sides, MathF.Min(Width, Height) / 2f);
                        bool inOutline = !inside && RenderPolygonPixel(x, y, textureWidth, textureHeight, Sides, MathF.Min(Width, Height) / 2f + _outlineWidth);

                        data[y * textureWidth + x] = inside ? _fillColor : (inOutline ? _outlineColor : Color.Transparent);
                    }
                    else
                    {
                        DebugLogger.PrintError($"Invalid shape type: {Type} with {Sides} sides.  Cannot render texture.");

                    }
                }
            }

            _texture.SetData(data);
            _origin = new Vector2(textureWidth / 2f, textureHeight / 2f);
        }

        private bool RenderPolygonPixel(int x, int y, int textureWidth, int textureHeight, int sides, float radius)
        {
            Vector2 center = new Vector2(textureWidth / 2f, textureHeight / 2f);
            Vector2 point = new Vector2(x, y) - center;

            float angle = MathF.Atan2(point.Y, point.X);
            float distance = point.Length();

            float sectorAngle = MathF.Tau / sides;
            float halfSector = sectorAngle / 2;

            float rotatedAngle = (angle + MathF.Tau) % sectorAngle;
            float cornerDistance = MathF.Cos(halfSector) / MathF.Cos(rotatedAngle - halfSector);
            float maxDistance = radius * cornerDistance;

            return distance <= maxDistance;
        }

        public void Draw(SpriteBatch spriteBatch, float rotation = 0f)
        {
            if (_texture == null)
            {
                DebugLogger.PrintError($"Shape at {Position} attempted to draw without texture. Call LoadContent first.");
                return;
            }

            spriteBatch.Draw(_texture, Position, null, Color.White, rotation, _origin, 1f, SpriteEffects.None, 0f);
        }
    }
}
