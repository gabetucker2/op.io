using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public class ShapeRenderer : IDisposable
    {
        private Texture2D _texture;
        private Texture2D _flashTexture;
        private Vector2 _origin;

        public void LoadContent(Shape shape, GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                DebugLogger.PrintError("LoadContent failed: GraphicsDevice is null.");
                return;
            }

            int textureWidth = shape.Width + 2 * shape.OutlineWidth;
            int textureHeight = shape.Height + 2 * shape.OutlineWidth;
            _texture = new Texture2D(graphicsDevice, textureWidth, textureHeight);
            Color[] data = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    bool isOutline = (x < shape.OutlineWidth || x >= textureWidth - shape.OutlineWidth || y < shape.OutlineWidth || y >= textureHeight - shape.OutlineWidth);

                    if (shape.ShapeType == "Rectangle")
                    {
                        data[y * textureWidth + x] = isOutline ? shape.OutlineColor : shape.FillColor;
                    }
                    else if (shape.ShapeType == "Circle")
                    {
                        Vector2 center = new Vector2(textureWidth / 2f, textureHeight / 2f);
                        float dx = x - center.X;
                        float dy = y - center.Y;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);

                        float outer = MathF.Min(shape.Width, shape.Height) / 2f + shape.OutlineWidth;
                        float inner = MathF.Min(shape.Width, shape.Height) / 2f;

                        bool isInside = dist <= inner;
                        bool isOutlineCircle = dist > inner && dist <= outer;

                        data[y * textureWidth + x] = isInside ? shape.FillColor : (isOutlineCircle ? shape.OutlineColor : Color.Transparent);
                    }
                    else if (shape.ShapeType == "Polygon" && shape.Sides >= 3)
                    {
                        bool inside = RenderPolygonPixel(x, y, textureWidth, textureHeight, shape.Sides, MathF.Min(shape.Width, shape.Height) / 2f);
                        bool inOutline = !inside && RenderPolygonPixel(x, y, textureWidth, textureHeight, shape.Sides, MathF.Min(shape.Width, shape.Height) / 2f + shape.OutlineWidth);

                        data[y * textureWidth + x] = inside ? shape.FillColor : (inOutline ? shape.OutlineColor : Color.Transparent);
                    }
                    else
                    {
                        DebugLogger.PrintError($"Invalid shape type: {shape.ShapeType} with {shape.Sides} sides. Cannot render texture.");
                    }
                }
            }

            _texture.SetData(data);
            _origin = new Vector2(textureWidth / 2f, textureHeight / 2f);

            // White flash overlay texture: same shape but all opaque pixels become white
            Color[] flashData = new Color[textureWidth * textureHeight];
            for (int i = 0; i < flashData.Length; i++)
                flashData[i] = data[i].A > 0 ? Color.White : Color.Transparent;
            _flashTexture = new Texture2D(graphicsDevice, textureWidth, textureHeight);
            _flashTexture.SetData(flashData);
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

        public void Draw(SpriteBatch spriteBatch, GameObject GO)
        {
            if (_texture == null)
            {
                DebugLogger.PrintError("ShapeRenderer attempted to draw without texture. Call LoadContent first.");
                return;
            }

            spriteBatch.Draw(_texture, GO.Position, null, Color.White * GO.Opacity, GO.Rotation, _origin, 1f, SpriteEffects.None, 0f);
        }

        public void DrawFlash(SpriteBatch spriteBatch, GameObject GO)
        {
            if (_flashTexture == null || GO.HitFlash <= 0f) return;

            float fadeIn  = BulletManager.HitFlashFadeIn;
            float fadeOut = BulletManager.HitFlashFadeOut;
            float t = GO.HitFlash; // total → 0 over duration
            float alpha = t > fadeOut
                ? (fadeIn + fadeOut - t) / MathF.Max(fadeIn, 0.001f)   // fade-in phase
                : t / MathF.Max(fadeOut, 0.001f);                       // fade-out phase
            spriteBatch.Draw(_flashTexture, GO.Position, null, new Color((byte)255, (byte)255, (byte)255, (byte)(180 * alpha)), GO.Rotation, _origin, 1f, SpriteEffects.None, 0f);
        }

        public void DrawAt(SpriteBatch spriteBatch, Vector2 position, float rotation)
        {
            if (_texture == null)
            {
                DebugLogger.PrintError("ShapeRenderer attempted to draw without texture. Call LoadContent first.");
                return;
            }

            spriteBatch.Draw(_texture, position, null, Color.White, rotation, _origin, 1f, SpriteEffects.None, 0f);
        }

        public void DrawAt(SpriteBatch spriteBatch, Vector2 position, float rotation, Vector2 scale)
        {
            if (_texture == null)
            {
                DebugLogger.PrintError("ShapeRenderer attempted to draw without texture. Call LoadContent first.");
                return;
            }

            spriteBatch.Draw(_texture, position, null, Color.White, rotation, _origin, scale, SpriteEffects.None, 0f);
        }

        public void DrawAt(SpriteBatch spriteBatch, Vector2 position, float rotation, Vector2 scale, float opacity)
        {
            if (_texture == null)
            {
                DebugLogger.PrintError("ShapeRenderer attempted to draw without texture. Call LoadContent first.");
                return;
            }

            spriteBatch.Draw(_texture, position, null, Color.White * opacity, rotation, _origin, scale, SpriteEffects.None, 0f);
        }

        public void Dispose()
        {
            _texture?.Dispose();
            _flashTexture?.Dispose();
        }
    }
}
