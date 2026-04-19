using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    internal static class FogOfWarManager
    {
        private readonly struct VisionSource
        {
            public VisionSource(Vector2 position, float radius)
            {
                Position = position;
                Radius = radius;
            }

            public Vector2 Position { get; }
            public float Radius { get; }
        }

        private static readonly List<VisionSource> VisionSources = new();

        private static readonly Color FogTerritoryColor = new(10, 14, 20, 255);
        private const float SightRadiusScale = 20f;

        private static readonly BlendState VisionCutoutBlend = new()
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.InverseSourceAlpha,
            AlphaBlendFunction = BlendFunction.Add
        };

        private static Texture2D _pixelTexture;
        private static Texture2D _visionMaskTexture;
        private static RenderTarget2D _fogRenderTarget;

        public static bool IsFogEnabled => Core.Instance?.Player != null;
        public static bool IsFogActive { get; private set; }
        public static int ActiveVisionSourceCount { get; private set; }
        public static float PlayerSightRadius { get; private set; }

        public static void Prepare(Matrix cameraTransform)
        {
            if (Core.Instance?.SpriteBatch == null || Core.Instance?.GraphicsDevice == null)
            {
                IsFogActive = false;
                ActiveVisionSourceCount = 0;
                PlayerSightRadius = 0f;
                return;
            }

            if (!CollectVisionSources())
            {
                IsFogActive = false;
                return;
            }

            GraphicsDevice graphicsDevice = Core.Instance.GraphicsDevice;
            int width = Math.Max(1, graphicsDevice.Viewport.Width);
            int height = Math.Max(1, graphicsDevice.Viewport.Height);
            EnsureResources(graphicsDevice, width, height);

            SpriteBatch spriteBatch = Core.Instance.SpriteBatch;
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
            Viewport previousViewport = graphicsDevice.Viewport;

            graphicsDevice.SetRenderTarget(_fogRenderTarget);
            graphicsDevice.Viewport = new Viewport(0, 0, _fogRenderTarget.Width, _fogRenderTarget.Height);
            graphicsDevice.Clear(Color.Transparent);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, _fogRenderTarget.Width, _fogRenderTarget.Height), FogTerritoryColor);
            spriteBatch.End();

            if (VisionSources.Count > 0)
            {
                float cameraScale = ExtractCameraScale(cameraTransform);
                spriteBatch.Begin(SpriteSortMode.Deferred, VisionCutoutBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                foreach (VisionSource source in VisionSources)
                {
                    Vector2 screenPos = Vector2.Transform(source.Position, cameraTransform);
                    float scaledRadius = source.Radius * cameraScale;
                    if (scaledRadius <= 0f)
                    {
                        continue;
                    }

                    int diameter = Math.Max(1, (int)MathF.Ceiling(scaledRadius * 2f));
                    Rectangle destination = new(
                        (int)MathF.Round(screenPos.X - scaledRadius),
                        (int)MathF.Round(screenPos.Y - scaledRadius),
                        diameter,
                        diameter);
                    spriteBatch.Draw(_visionMaskTexture, destination, Color.White);
                }

                spriteBatch.End();
            }

            if (previousTargets.Length > 0)
            {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
            else
            {
                graphicsDevice.SetRenderTarget(null);
            }

            graphicsDevice.Viewport = previousViewport;

            IsFogActive = true;
        }

        public static void DrawOverlay(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || _fogRenderTarget == null || _fogRenderTarget.IsDisposed || !IsFogActive)
            {
                return;
            }

            int width = spriteBatch.GraphicsDevice?.Viewport.Width ?? _fogRenderTarget.Width;
            int height = spriteBatch.GraphicsDevice?.Viewport.Height ?? _fogRenderTarget.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            spriteBatch.Draw(_fogRenderTarget, new Rectangle(0, 0, width, height), Color.White);
            spriteBatch.End();
        }

        public static bool IsWorldPositionVisible(Vector2 worldPosition, float visibleRadiusPadding = 0f)
        {
            Agent player = Core.Instance?.Player;
            if (player == null)
            {
                return true;
            }

            float sightRadius = MathF.Max(0f, player.BodyAttributes.Sight) * SightRadiusScale;
            if (sightRadius <= 0f)
            {
                return false;
            }

            float effectiveRadius = sightRadius + MathF.Max(0f, visibleRadiusPadding);
            float distanceSq = Vector2.DistanceSquared(worldPosition, player.Position);
            return distanceSq <= effectiveRadius * effectiveRadius;
        }

        private static bool CollectVisionSources()
        {
            VisionSources.Clear();
            ActiveVisionSourceCount = 0;
            PlayerSightRadius = 0f;

            Agent player = Core.Instance?.Player;
            if (player == null)
            {
                return false;
            }

            float sightRadius = MathF.Max(0f, player.BodyAttributes.Sight) * SightRadiusScale;
            PlayerSightRadius = sightRadius;

            if (sightRadius > 0f)
            {
                VisionSources.Add(new VisionSource(player.Position, sightRadius));
            }

            ActiveVisionSourceCount = VisionSources.Count;
            return true;
        }

        private static void EnsureResources(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (_pixelTexture == null || _pixelTexture.IsDisposed)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            if (_fogRenderTarget == null || _fogRenderTarget.IsDisposed ||
                _fogRenderTarget.Width != width || _fogRenderTarget.Height != height)
            {
                _fogRenderTarget?.Dispose();
                _fogRenderTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            }

            if (_visionMaskTexture == null || _visionMaskTexture.IsDisposed)
            {
                _visionMaskTexture = BuildVisionMaskTexture(graphicsDevice, 256);
            }
        }

        private static Texture2D BuildVisionMaskTexture(GraphicsDevice graphicsDevice, int diameter)
        {
            diameter = Math.Max(8, diameter);
            Texture2D texture = new(graphicsDevice, diameter, diameter);
            Color[] data = new Color[diameter * diameter];

            float radius = diameter * 0.5f;
            float center = (diameter - 1) * 0.5f;
            const float hardVisionRatio = 0.92f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distanceRatio = MathF.Sqrt(dx * dx + dy * dy) / radius;

                    float alpha;
                    if (distanceRatio >= 1f)
                    {
                        alpha = 0f;
                    }
                    else if (distanceRatio <= hardVisionRatio)
                    {
                        alpha = 1f;
                    }
                    else
                    {
                        float edgeRatio = (distanceRatio - hardVisionRatio) / (1f - hardVisionRatio);
                        edgeRatio = MathHelper.Clamp(edgeRatio, 0f, 1f);
                        alpha = 1f - (edgeRatio * edgeRatio * (3f - 2f * edgeRatio));
                    }

                    data[y * diameter + x] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
                }
            }

            texture.SetData(data);
            return texture;
        }

        private static float ExtractCameraScale(Matrix matrix)
        {
            Vector2 xBasis = new(matrix.M11, matrix.M12);
            Vector2 yBasis = new(matrix.M21, matrix.M22);
            float scaleX = xBasis.Length();
            float scaleY = yBasis.Length();
            float scale = (scaleX + scaleY) * 0.5f;
            return scale > 0f ? scale : 1f;
        }
    }
}
