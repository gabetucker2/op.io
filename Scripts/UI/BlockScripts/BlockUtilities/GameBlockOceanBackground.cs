using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Procedural ocean background for the Game block panel.
    /// World-anchored seamless tile drawn with camera transform.
    /// </summary>
    internal static class GameBlockOceanBackground
    {
        private const float Tau = MathF.PI * 2f;
        private const int TileResolution = 1024;
        private const float DefaultDownwardSpeed = 0.032f;
        private const float DefaultBackgroundVariationStrength = 0.055f;
        private const float DefaultWarpStrengthX = 0.022f;
        private const float DefaultWarpStrengthY = 0.020f;
        private const float DefaultCrestBrightness = 1.24f;
        private const float DefaultWaveSet1Strength = 0.86f;
        private const float DefaultWaveSet2Strength = 0.56f;
        private const float DefaultWaveSet3Strength = 0.38f;
        private const int FieldTextureResolution = 320;
        private const int AnimationLoopFrameCount = 12;
        private const float DetailAnimationCycleSeconds = 3.10f;
        private const float DetailAnimationSpanSeconds = 0.30f;
        private static readonly ParallelOptions TileBuildParallelOptions = new()
        {
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
        };
        private static readonly RasterizerState OceanScissorRasterizerState = new()
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };
        private static readonly Vector3 DefaultBaseColor = new(40f / 255f, 176f / 255f, 206f / 255f);

        private static Texture2D _fullscreenPixel;
        private static Effect _oceanShader;
        private static Texture2D _fieldTextureA;
        private static Texture2D _fieldTextureB;
        private static Texture2D _fieldTextureC;
        private static bool _shaderReady;
        private static Texture2D _tileTexture;
        private static Texture2D[] _animationLoopTextures;
        private static Color[] _tilePixels;
        private static int _tileWidth;
        private static int _tileHeight;
        private static float _generatedFieldScale = float.NaN;
        private static int _generatedParameterHash;
        private static float _animationLoopFieldScale = float.NaN;
        private static int _animationLoopParameterHash = int.MinValue;
        private static bool _animationLoopReady;
        private static float _lastResolvedFieldScale = 1f;
        private static float _lastCameraZoom = 1f;
        private static int _lastPanelWidth;
        private static int _lastPanelHeight;
        private static string _shaderStatus = "GPU ocean shader disabled; using CPU ocean path.";
        private static Task<GeneratedTileLoop> _pendingLoopBuildTask;
        private static int _pendingLoopBuildHash = int.MinValue;
        private static float _lastDrawTimeSeconds;
        private static int _fieldWidth;
        private static int _fieldHeight;
        private static float[] _fieldN1;
        private static float[] _fieldN2;
        private static float[] _fieldN3;
        private static float[] _fieldSeg;
        private static float[] _fieldSeg2;
        private static float[] _fieldRand;
        private static float[] _fieldRand2;
        private static float[] _fieldPack1;
        private static float[] _fieldPack2;
        private static float[] _fieldPack3;
        private static float _staticCacheFieldScale = float.NaN;
        private static float[] _staticRefX;
        private static float[] _staticRefY;
        private static float[] _staticWaveXBase;
        private static float[] _staticWaveYBase;
        private static float[] _staticFieldX;
        private static float[] _staticFieldY;
        private static float[] _staticN1;
        private static float[] _staticN2;
        private static float[] _staticN3;
        private static float[] _staticRand;
        private static float[] _staticRand2;
        private static float[] _staticP1;
        private static float[] _staticP2;
        private static float[] _staticP3;
        private static float[] _staticPacketMix;
        private static float[] _staticBody;
        private static float[] _staticXs;
        private static float[] _staticYsBase;
        private static bool _loggedShaderWorldSpan;
        private static readonly VertexPositionColorTexture[] ShaderQuadVertices = new VertexPositionColorTexture[6];

        private sealed class GeneratedTileFrame
        {
            public Color[] Pixels { get; init; }
            public float FieldScale { get; init; }
            public int ParameterHash { get; init; }
        }

        private sealed class GeneratedTileLoop
        {
            public Color[][] Frames { get; init; }
            public float FieldScale { get; init; }
            public int ParameterHash { get; init; }
        }

        // Live tunables.
        public static Vector3 BaseColor { get; set; } = DefaultBaseColor;
        public static float TimeScale { get; set; } = 0.72f;
        public static float DownwardSpeed { get; set; } = DefaultDownwardSpeed;
        public static float BackgroundVariationStrength { get; set; } = DefaultBackgroundVariationStrength;
        public static float WarpStrengthX { get; set; } = DefaultWarpStrengthX;
        public static float WarpStrengthY { get; set; } = DefaultWarpStrengthY;
        public static float CrestBrightness { get; set; } = DefaultCrestBrightness;
        public static float CrestThickness { get; set; } = 0.30f;
        public static float CrestSegmentation { get; set; } = 0.64f;
        public static float CrestDensity { get; set; } = 0.58f;
        public static float WaveSet1Strength { get; set; } = DefaultWaveSet1Strength;
        public static float WaveSet2Strength { get; set; } = DefaultWaveSet2Strength;
        public static float WaveSet3Strength { get; set; } = DefaultWaveSet3Strength;
        public static float PanelScale { get; set; } = 1.30f;
        public static float ZoomInfluence { get; set; } = 0.72f;
        public static float DetailScale { get; set; } = 1f;

        // Compatibility/debug fields retained (not used to downscale this path).
        public static float RenderScale { get; set; } = 1f;
        public static int MaxRenderPixels { get; set; } = int.MaxValue;

        public static bool ShaderReady => _shaderReady;
        public static bool UsingShaderPath => _shaderReady;
        public static string ShaderStatus => _shaderStatus;
        public static string BaseColorRgb =>
            $"{ClampToByte(BaseColor.X)}, {ClampToByte(BaseColor.Y)}, {ClampToByte(BaseColor.Z)}";
        public static float LastResolvedRenderScale => _lastResolvedFieldScale;
        public static float LastCameraZoom => _lastCameraZoom;
        public static string RenderTextureResolution =>
            _lastPanelWidth > 0 && _lastPanelHeight > 0
                ? $"{_lastPanelWidth}x{_lastPanelHeight}"
                : $"{Math.Max(1, _tileWidth)}x{Math.Max(1, _tileHeight)}";

        public static void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
        {
            EnsureFullscreenPixel(graphicsDevice);
            EnsureShaderResources(graphicsDevice, content);
            if (_shaderReady)
            {
                _shaderStatus = $"GPU ocean shader active ({FieldTextureResolution} field textures, world-space procedural).";
                return;
            }

            bool created = EnsureResources(graphicsDevice);
            if (created || float.IsNaN(_generatedFieldScale))
            {
                float fieldScale = ResolveFieldScale();
                int parameterHash = ComputeGenerationHash(fieldScale);
                BuildProceduralTile(fieldScale, ResolveDetailAnimationTime(0f), parameterHash);
                QueueLoopBuild(fieldScale, parameterHash);
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle panelBounds, float timeSeconds)
        {
            Draw(spriteBatch, panelBounds, timeSeconds, BlockManager.CameraZoom, BlockManager.GetCameraTransform());
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle panelBounds, float timeSeconds, float cameraZoom)
        {
            Draw(spriteBatch, panelBounds, timeSeconds, cameraZoom, BlockManager.GetCameraTransform());
        }

        public static void Draw(
            SpriteBatch spriteBatch,
            Rectangle panelBounds,
            float timeSeconds,
            float cameraZoom,
            Matrix cameraTransform)
        {
            if (spriteBatch == null || panelBounds.Width <= 0 || panelBounds.Height <= 0)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            _lastCameraZoom = Math.Max(0.05f, cameraZoom);
            _lastDrawTimeSeconds = Math.Max(0f, timeSeconds);
            float fieldScale = ResolveFieldScale();
            _lastResolvedFieldScale = fieldScale;
            _lastPanelWidth = Math.Max(1, panelBounds.Width);
            _lastPanelHeight = Math.Max(1, panelBounds.Height);

            EnsureFullscreenPixel(graphicsDevice);
            if (_shaderReady && _oceanShader != null && _fieldTextureA != null && _fieldTextureB != null && _fieldTextureC != null)
            {
                DrawWorldAnchoredShader(spriteBatch, panelBounds, timeSeconds, cameraTransform, fieldScale);
                _shaderStatus =
                    $"GPU ocean shader active ({FieldTextureResolution} field textures, fieldScale {fieldScale:0.00}, zoom {_lastCameraZoom:0.00})";
                return;
            }

            bool created = EnsureResources(graphicsDevice);
            float detailAnimationTime = ResolveDetailAnimationTime(timeSeconds);
            int parameterHash = ComputeGenerationHash(fieldScale);
            TryApplyPendingLoopBuild();
            bool fallbackChanged =
                float.IsNaN(_generatedFieldScale) ||
                MathF.Abs(_generatedFieldScale - fieldScale) > 0.01f ||
                _generatedParameterHash != parameterHash;
            if (created || fallbackChanged)
            {
                BuildProceduralTile(fieldScale, detailAnimationTime, parameterHash);
            }

            bool loopMatchesCurrent =
                _animationLoopReady &&
                !float.IsNaN(_animationLoopFieldScale) &&
                MathF.Abs(_animationLoopFieldScale - fieldScale) <= 0.01f &&
                _animationLoopParameterHash == parameterHash;
            if (!loopMatchesCurrent)
            {
                QueueLoopBuild(fieldScale, parameterHash);
            }

            if (_tileTexture == null || _tileTexture.IsDisposed)
            {
                return;
            }

            DrawWorldAnchoredTiles(spriteBatch, panelBounds, timeSeconds, cameraTransform);
            _shaderStatus =
                _pendingLoopBuildTask != null && !_pendingLoopBuildTask.IsCompleted
                    ? $"CPU ocean loop building ({_tileWidth}x{_tileHeight}, fieldScale {fieldScale:0.00}, zoom {_lastCameraZoom:0.00}, fallback active)"
                    : loopMatchesCurrent
                        ? $"CPU ocean loop active ({AnimationLoopFrameCount} frames, {_tileWidth}x{_tileHeight}, fieldScale {fieldScale:0.00}, zoom {_lastCameraZoom:0.00})"
                        : $"CPU ocean fallback active ({_tileWidth}x{_tileHeight}, fieldScale {fieldScale:0.00}, zoom {_lastCameraZoom:0.00})";
        }

        private static void DrawWorldAnchoredShader(
            SpriteBatch spriteBatch,
            Rectangle panelBounds,
            float timeSeconds,
            Matrix cameraTransform,
            float fieldScale)
        {
            if (spriteBatch == null ||
                _oceanShader == null ||
                _fieldTextureA == null ||
                _fieldTextureB == null ||
                _fieldTextureC == null ||
                _fullscreenPixel == null ||
                _fullscreenPixel.IsDisposed)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            Rectangle scissorRect = BuildViewportClampedScissorRectangle(panelBounds, graphicsDevice.Viewport);
            Rectangle previousScissor = graphicsDevice.ScissorRectangle;
            Rectangle destinationRectangle = panelBounds;

            float scaledTime = ResolveScaledAnimationTime(timeSeconds);
            float backgroundFactor = Math.Clamp(BackgroundVariationStrength / DefaultBackgroundVariationStrength, 0f, 2f);
            float warpXFactor = Math.Clamp(WarpStrengthX / DefaultWarpStrengthX, 0f, 3f);
            float warpYFactor = Math.Clamp(WarpStrengthY / DefaultWarpStrengthY, 0f, 3f);
            float crestBrightnessFactor = CrestBrightness <= 0f
                ? 0f
                : Math.Clamp(CrestBrightness / DefaultCrestBrightness, 0f, 2f);
            float waveSet1Factor = Math.Clamp(WaveSet1Strength / DefaultWaveSet1Strength, 0f, 3f);
            float waveSet2Factor = Math.Clamp(WaveSet2Strength / DefaultWaveSet2Strength, 0f, 3f);
            float waveSet3Factor = Math.Clamp(WaveSet3Strength / DefaultWaveSet3Strength, 0f, 3f);

            _oceanShader.Parameters["BaseColor"]?.SetValue(new Vector4(Vector3.Clamp(BaseColor, Vector3.Zero, Vector3.One), 1f));
            _oceanShader.Parameters["TimeSeconds"]?.SetValue(scaledTime);
            _oceanShader.Parameters["FieldScale"]?.SetValue(Math.Clamp(fieldScale, 0.20f, 12f));
            _oceanShader.Parameters["BackgroundVariationStrength"]?.SetValue(backgroundFactor);
            _oceanShader.Parameters["WarpStrengthX"]?.SetValue(warpXFactor);
            _oceanShader.Parameters["WarpStrengthY"]?.SetValue(warpYFactor);
            _oceanShader.Parameters["CrestBrightness"]?.SetValue(crestBrightnessFactor);
            _oceanShader.Parameters["CrestThickness"]?.SetValue(Math.Clamp(CrestThickness, 0f, 1f));
            _oceanShader.Parameters["CrestSegmentation"]?.SetValue(Math.Clamp(CrestSegmentation, 0f, 1f));
            _oceanShader.Parameters["CrestDensity"]?.SetValue(Math.Clamp(CrestDensity, 0f, 1f));
            _oceanShader.Parameters["WaveSet1Strength"]?.SetValue(waveSet1Factor);
            _oceanShader.Parameters["WaveSet2Strength"]?.SetValue(waveSet2Factor);
            _oceanShader.Parameters["WaveSet3Strength"]?.SetValue(waveSet3Factor);
            _oceanShader.Parameters["WorldPatternUnits"]?.SetValue((float)TileResolution);
            _oceanShader.Parameters["FieldTextureResolution"]?.SetValue((float)FieldTextureResolution);
            _oceanShader.Parameters["CameraOffset"]?.SetValue(BlockManager.CameraOffset);
            _oceanShader.Parameters["RenderCenter"]?.SetValue(BlockManager.GetCameraRenderCenter());
            _oceanShader.Parameters["CameraZoom"]?.SetValue(_lastCameraZoom);
            _oceanShader.Parameters["FieldsA"]?.SetValue(_fieldTextureA);
            _oceanShader.Parameters["FieldsB"]?.SetValue(_fieldTextureB);
            _oceanShader.Parameters["FieldsC"]?.SetValue(_fieldTextureC);
            _oceanShader.Parameters["MatrixTransform"]?.SetValue(
                Matrix.CreateOrthographicOffCenter(
                    0f,
                    graphicsDevice.Viewport.Width,
                    graphicsDevice.Viewport.Height,
                    0f,
                    0f,
                    1f));

            if (!_loggedShaderWorldSpan)
            {
                Vector2 renderCenter = BlockManager.GetCameraRenderCenter();
                float zoom = Math.Max(0.05f, _lastCameraZoom);
                float minX = ((panelBounds.Left - renderCenter.X) / zoom) + BlockManager.CameraOffset.X + renderCenter.X;
                float maxX = ((panelBounds.Right - renderCenter.X) / zoom) + BlockManager.CameraOffset.X + renderCenter.X;
                float minY = ((panelBounds.Top - renderCenter.Y) / zoom) + BlockManager.CameraOffset.Y + renderCenter.Y;
                float maxY = ((panelBounds.Bottom - renderCenter.Y) / zoom) + BlockManager.CameraOffset.Y + renderCenter.Y;
                float patternSpanX = ((maxX - minX) / TileResolution) * Math.Clamp(fieldScale, 0.20f, 12f);
                float patternSpanY = ((maxY - minY) / TileResolution) * Math.Clamp(fieldScale, 0.20f, 12f);
                DebugLogger.Print(
                    $"GameBlockOceanBackground: shader world span=({maxX - minX:0.00}, {maxY - minY:0.00}), " +
                    $"pattern span=({patternSpanX:0.000}, {patternSpanY:0.000}), fieldScale={fieldScale:0.00}, zoom={_lastCameraZoom:0.00}");
                _loggedShaderWorldSpan = true;
            }

            graphicsDevice.ScissorRectangle = scissorRect;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState = OceanScissorRasterizerState;

            float left = destinationRectangle.Left;
            float top = destinationRectangle.Top;
            float right = destinationRectangle.Right;
            float bottom = destinationRectangle.Bottom;
            Color color = Color.White;

            ShaderQuadVertices[0] = new VertexPositionColorTexture(new Vector3(left, top, 0f), color, new Vector2(0f, 0f));
            ShaderQuadVertices[1] = new VertexPositionColorTexture(new Vector3(right, top, 0f), color, new Vector2(1f, 0f));
            ShaderQuadVertices[2] = new VertexPositionColorTexture(new Vector3(left, bottom, 0f), color, new Vector2(0f, 1f));
            ShaderQuadVertices[3] = new VertexPositionColorTexture(new Vector3(right, top, 0f), color, new Vector2(1f, 0f));
            ShaderQuadVertices[4] = new VertexPositionColorTexture(new Vector3(right, bottom, 0f), color, new Vector2(1f, 1f));
            ShaderQuadVertices[5] = new VertexPositionColorTexture(new Vector3(left, bottom, 0f), color, new Vector2(0f, 1f));

            foreach (EffectPass pass in _oceanShader.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, ShaderQuadVertices, 0, 2);
            }

            graphicsDevice.ScissorRectangle = previousScissor;
        }

        private static bool EnsureResources(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return false;
            }

            EnsureFullscreenPixel(graphicsDevice);
            bool fullscreenPixelCreated = _fullscreenPixel != null && !_fullscreenPixel.IsDisposed && _tileTexture == null;

            bool sizeChanged = _tileTexture == null ||
                _tileTexture.IsDisposed ||
                _tileWidth != TileResolution ||
                _tileHeight != TileResolution;

            if (!sizeChanged)
            {
                return fullscreenPixelCreated;
            }

            _tileTexture?.Dispose();
            _tileTexture = new Texture2D(graphicsDevice, TileResolution + 1, TileResolution + 1, false, SurfaceFormat.Color);
            DisposeAnimationLoopTextures();
            _animationLoopTextures = new Texture2D[AnimationLoopFrameCount];
            for (int i = 0; i < AnimationLoopFrameCount; i++)
            {
                _animationLoopTextures[i] = new Texture2D(graphicsDevice, TileResolution + 1, TileResolution + 1, false, SurfaceFormat.Color);
            }
            _tilePixels = new Color[TileResolution * TileResolution];
            _tileWidth = TileResolution;
            _tileHeight = TileResolution;
            _generatedFieldScale = float.NaN;
            _animationLoopFieldScale = float.NaN;
            _animationLoopParameterHash = int.MinValue;
            _animationLoopReady = false;
            return true;
        }

        private static void EnsureFullscreenPixel(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_fullscreenPixel == null || _fullscreenPixel.IsDisposed)
            {
                _fullscreenPixel?.Dispose();
                _fullscreenPixel = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _fullscreenPixel.SetData(new[] { Color.White });
            }
        }

        private static void EnsureShaderResources(GraphicsDevice graphicsDevice, ContentManager content)
        {
            if (graphicsDevice == null || content == null || _shaderReady)
            {
                return;
            }

            try
            {
                _oceanShader ??= content.Load<Effect>("Effects/GameBlockOcean");
                EnsureShaderFieldTextures(graphicsDevice);
                if (_oceanShader != null && _fieldTextureA != null && _fieldTextureB != null && _fieldTextureC != null)
                {
                    _shaderReady = true;
                    DebugLogger.Print("GameBlockOceanBackground: GPU ocean shader loaded.");
                }
            }
            catch (Exception ex)
            {
                _shaderReady = false;
                _shaderStatus = $"GPU ocean shader unavailable; using CPU ocean path. {ex.Message}";
                DebugLogger.PrintWarning($"GameBlockOceanBackground: GPU ocean shader unavailable. {ex.Message}");
            }
        }

        private static void EnsureShaderFieldTextures(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null || (_fieldTextureA != null && !_fieldTextureA.IsDisposed))
            {
                return;
            }

            float[] n1 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 10, 1f, 1f, 0f, 0f));
            float[] n2 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 20, 1.6f, 1.4f, 0.11f, 0.07f));
            float[] n3 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 30, 2.1f, 2.0f, 0.31f, 0.17f));
            float[] seg = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 40, 1.25f, 0.95f, 0.23f, 0.41f));
            float[] seg2 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 50, 2.3f, 1.2f, 0.05f, 0.19f));
            float[] rand = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 60, 1.8f, 1.7f, 0.13f, 0.29f));
            float[] rand2 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 70, 3.2f, 2.8f, 0.09f, 0.17f));
            float[] pack1 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 90, 0.9f, 1.0f, 0.17f, 0.23f));
            float[] pack2 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 91, 1.4f, 1.3f, 0.41f, 0.05f));
            float[] pack3 = NormalizeFieldForTexture(BuildFbmField(FieldTextureResolution, FieldTextureResolution, 92, 2.0f, 1.8f, 0.09f, 0.31f));

            _fieldTextureA?.Dispose();
            _fieldTextureB?.Dispose();
            _fieldTextureC?.Dispose();
            _fieldTextureA = BuildPackedFieldTexture(graphicsDevice, FieldTextureResolution, FieldTextureResolution, n1, n2, n3, seg);
            _fieldTextureB = BuildPackedFieldTexture(graphicsDevice, FieldTextureResolution, FieldTextureResolution, seg2, rand, rand2, pack1);
            _fieldTextureC = BuildPackedFieldTexture(graphicsDevice, FieldTextureResolution, FieldTextureResolution, pack2, pack3, null, null);
        }

        private static Texture2D BuildPackedFieldTexture(
            GraphicsDevice graphicsDevice,
            int width,
            int height,
            float[] red,
            float[] green,
            float[] blue,
            float[] alpha)
        {
            int displayWidth = width + 1;
            int displayHeight = height + 1;
            Texture2D texture = new(graphicsDevice, displayWidth, displayHeight, false, SurfaceFormat.Color);
            Color[] pixels = new Color[displayWidth * displayHeight];

            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * width;
                int destinationRow = y * displayWidth;
                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = sourceRow + x;
                    pixels[destinationRow + x] = new Color(
                        ClampToByte(red != null && sourceIndex < red.Length ? red[sourceIndex] : 0f),
                        ClampToByte(green != null && sourceIndex < green.Length ? green[sourceIndex] : 0f),
                        ClampToByte(blue != null && sourceIndex < blue.Length ? blue[sourceIndex] : 0f),
                        ClampToByte(alpha != null && sourceIndex < alpha.Length ? alpha[sourceIndex] : 1f));
                }

                pixels[destinationRow + width] = pixels[destinationRow];
            }

            int lastRow = height * displayWidth;
            Array.Copy(pixels, 0, pixels, lastRow, displayWidth);
            texture.SetData(pixels);
            return texture;
        }

        private static float[] NormalizeFieldForTexture(float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<float>();
            }

            float[] normalized = new float[values.Length];
            GetValueRange(values, out float minValue, out float maxValue);
            for (int i = 0; i < values.Length; i++)
            {
                normalized[i] = NormalizeValue(values[i], minValue, maxValue);
            }

            return normalized;
        }

        private static float ResolveFieldScale()
        {
            float panelScale = Math.Clamp(PanelScale, 0.10f, 12f);
            float detail = Math.Clamp(DetailScale, 0.35f, 2f);
            return Math.Clamp(panelScale * detail, 0.20f, 12f);
        }

        private static void BuildProceduralTile(float fieldScale, float animationTime, int parameterHash)
        {
            if (_tileTexture == null || _tileTexture.IsDisposed || _tilePixels == null)
            {
                return;
            }

            GeneratedTileFrame frame = GenerateProceduralTile(fieldScale, animationTime, parameterHash);
            ApplyGeneratedTile(frame);
        }

        private static GeneratedTileFrame GenerateProceduralTile(float fieldScale, float animationTime, int parameterHash)
        {
            EnsureReferenceFields();
            EnsureStaticSamplingCache(fieldScale);

            int width = _tileWidth;
            int height = _tileHeight;
            Vector3 baseColor = Vector3.Clamp(BaseColor, Vector3.Zero, Vector3.One);
            float bgVariation = Math.Clamp(BackgroundVariationStrength, 0f, 1f);
            float scale = Math.Clamp(fieldScale, 0.20f, 12f);

            float[] heights = new float[width * height];
            float[] backgroundFields = new float[width * height];
            Parallel.For(0, height, TileBuildParallelOptions, y =>
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    float h = computeOceanHeight(index, scale, animationTime);
                    heights[index] = h;
                    backgroundFields[index] = computeBackgroundField(index, scale, animationTime);
                }
            });

            float[] slopes = new float[width * height];

            Parallel.For(0, height, TileBuildParallelOptions, y =>
            {
                int yPrev = y == 0 ? height - 1 : y - 1;
                int yNext = y == height - 1 ? 0 : y + 1;
                int row = y * width;
                int rowPrev = yPrev * width;
                int rowNext = yNext * width;

                for (int x = 0; x < width; x++)
                {
                    int xPrev = x == 0 ? width - 1 : x - 1;
                    int xNext = x == width - 1 ? 0 : x + 1;
                    int index = row + x;

                    float hx = (heights[row + xNext] - heights[row + xPrev]) * 0.5f;
                    float hy = (heights[rowNext + x] - heights[rowPrev + x]) * 0.5f;
                    slopes[index] = MathF.Sqrt((hx * hx) + (hy * hy));
                }
            });
            Color[] pixels = new Color[width * height];

            Parallel.For(0, height, TileBuildParallelOptions, y =>
            {
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    float hn = Saturate(0.44f + (0.26f * heights[index]));
                    float sn = Saturate((slopes[index] - 0.015f) / 0.115f);
                    float backgroundField = Saturate(0.5f + (0.5f * backgroundFields[index]));
                    float crestMask = computeCrestMask(
                        index,
                        scale,
                        animationTime,
                        hn,
                        sn,
                        _staticPacketMix[index],
                        Math.Clamp(CrestThickness, 0f, 1f),
                        Math.Clamp(CrestSegmentation, 0f, 1f),
                        Math.Clamp(CrestDensity, 0f, 1f),
                        Math.Max(0f, CrestBrightness));

                    Vector3 color = applyOceanColor(baseColor, backgroundField, hn, sn, crestMask, bgVariation);
                    pixels[index] = new Color(color);
                }
            });

            return new GeneratedTileFrame
            {
                Pixels = pixels,
                FieldScale = scale,
                ParameterHash = parameterHash
            };
        }

        private static GeneratedTileLoop GenerateProceduralLoop(float fieldScale, int parameterHash)
        {
            Color[][] frames = new Color[AnimationLoopFrameCount][];
            for (int i = 0; i < AnimationLoopFrameCount; i++)
            {
                float animationTime = ResolveLoopFrameAnimationTime(i);
                frames[i] = GenerateProceduralTile(fieldScale, animationTime, parameterHash).Pixels;
            }

            return new GeneratedTileLoop
            {
                Frames = frames,
                FieldScale = Math.Clamp(fieldScale, 0.20f, 12f),
                ParameterHash = parameterHash
            };
        }

        private static void ApplyGeneratedTile(GeneratedTileFrame frame)
        {
            if (frame?.Pixels == null || _tileTexture == null || _tileTexture.IsDisposed)
            {
                return;
            }

            _tilePixels = frame.Pixels;
            _tileTexture.SetData(BuildDisplayTexturePixels(_tilePixels, _tileWidth, _tileHeight));
            _generatedFieldScale = frame.FieldScale;
            _generatedParameterHash = frame.ParameterHash;
        }

        private static void ApplyGeneratedLoop(GeneratedTileLoop loop)
        {
            if (loop?.Frames == null || _animationLoopTextures == null || _animationLoopTextures.Length != AnimationLoopFrameCount)
            {
                return;
            }

            for (int i = 0; i < AnimationLoopFrameCount; i++)
            {
                Texture2D texture = _animationLoopTextures[i];
                if (texture == null || texture.IsDisposed || loop.Frames[i] == null)
                {
                    continue;
                }

                texture.SetData(BuildDisplayTexturePixels(loop.Frames[i], _tileWidth, _tileHeight));
            }

            _animationLoopFieldScale = loop.FieldScale;
            _animationLoopParameterHash = loop.ParameterHash;
            _animationLoopReady = true;
        }

        private static void QueueLoopBuild(float fieldScale, int parameterHash)
        {
            if (_pendingLoopBuildTask != null && !_pendingLoopBuildTask.IsCompleted)
            {
                return;
            }

            if (_pendingLoopBuildHash == parameterHash)
            {
                return;
            }

            EnsureReferenceFields();
            _pendingLoopBuildHash = parameterHash;
            _pendingLoopBuildTask = Task.Run(() => GenerateProceduralLoop(fieldScale, parameterHash));
        }

        private static void TryApplyPendingLoopBuild()
        {
            if (_pendingLoopBuildTask == null || !_pendingLoopBuildTask.IsCompleted)
            {
                return;
            }

            try
            {
                ApplyGeneratedLoop(_pendingLoopBuildTask.Result);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"GameBlockOceanBackground: ocean animation loop build failed. {ex.Message}");
            }
            finally
            {
                _pendingLoopBuildTask = null;
                _pendingLoopBuildHash = int.MinValue;
            }
        }

        private static float computeOceanHeight(int index, float scale, float animationTime)
        {
            float wave1Factor = Math.Clamp(WaveSet1Strength / DefaultWaveSet1Strength, 0f, 3f);
            float wave2Factor = Math.Clamp(WaveSet2Strength / DefaultWaveSet2Strength, 0f, 3f);
            float wave3Factor = Math.Clamp(WaveSet3Strength / DefaultWaveSet3Strength, 0f, 3f);

            float h =
                (wave1Factor * _staticP1[index] * waveSet1(_staticWaveXBase[index], _staticWaveYBase[index], scale, animationTime, _staticN2[index], _staticRand[index])) +
                (wave2Factor * _staticP2[index] * waveSet2(_staticWaveXBase[index], _staticWaveYBase[index], scale, animationTime, _staticN3[index], _staticRand2[index])) +
                (wave3Factor * _staticP3[index] * waveSet3(_staticWaveXBase[index], _staticWaveYBase[index], scale, animationTime, _staticRand[index], _staticN1[index]));

            return h + _staticBody[index];
        }

        private static float computeBackgroundField(int index, float scale, float animationTime)
        {
            float refX = _staticRefX[index];
            float refY = _staticRefY[index];
            float x1 = refX + (0.020f * SinTau((1f * refY) - (0.02f * animationTime)));
            float y1 = refY - (0.018f * animationTime) + (0.018f * SinTau((1f * refX) + (0.03f * animationTime)));
            float f =
                (0.50f * SinTau((1f * x1) + (1f * y1) + 0.07f)) +
                (0.28f * SinTau((-1f * x1) + (1f * y1) + 0.31f)) +
                (0.17f * SinTau((2f * x1) - (1f * y1) + 0.63f)) +
                (0.11f * SinTau((-2f * x1) - (1f * y1) + 0.18f));

            return f + (0.18f * f * f) - (0.10f * f * f * f);
        }

        private static float packetMask1(float x, float y)
        {
            return 0.42f + (0.62f * packetMask(_fieldPack1, x + 0.03f, y - 0.02f, 0.95f, 0.78f, 0.34f, 0.32f));
        }

        private static float packetMask2(float x, float y)
        {
            return 0.34f + (0.70f * packetMask(_fieldPack2, x - 0.01f, y + 0.04f, 1.18f, 0.96f, 0.40f, 0.30f));
        }

        private static float packetMask3(float x, float y)
        {
            return 0.28f + (0.78f * packetMask(_fieldPack3, x + 0.05f, y + 0.01f, 1.48f, 1.24f, 0.42f, 0.30f));
        }

        private static float waveSet1(float x, float y, float scale, float animationTime, float n2, float rand)
        {
            int swellFrequency = ResolveWaveFrequency(1.8f, scale);
            int ridgeFrequency = ResolveWaveFrequency(3.0f, scale);
            float lateralPhase1 =
                (0.08f * SinTau(x + (0.07f * SinTau(y + (0.11f * n2))))) +
                (0.08f * (n2 - 0.5f));
            float lateralPhase2 =
                (0.05f * SinTau(x + y + (0.13f * rand))) +
                (0.06f * (rand - 0.5f));
            return
                (0.90f * SinTau((swellFrequency * (y - (0.11f * animationTime))) + lateralPhase1)) +
                (0.28f * SinTau((ridgeFrequency * (y - (0.13f * animationTime))) + lateralPhase2 + 0.17f));
        }

        private static float waveSet2(float x, float y, float scale, float animationTime, float n3, float rand2)
        {
            int swellFrequency = ResolveWaveFrequency(2.4f, scale);
            int ridgeFrequency = ResolveWaveFrequency(4.1f, scale);
            float lateralPhase1 =
                (0.10f * SinTau(x + (0.60f * y) + (0.09f * n3))) +
                (0.08f * (n3 - 0.5f));
            float lateralPhase2 =
                (0.07f * SinTau(x - y + (0.07f * rand2))) +
                (0.05f * (rand2 - 0.5f));
            return
                (0.50f * SinTau((swellFrequency * (y - (0.15f * animationTime))) + lateralPhase1 + 0.31f)) +
                (0.22f * SinTau((ridgeFrequency * (y - (0.17f * animationTime))) + lateralPhase2 + 0.61f));
        }

        private static float waveSet3(float x, float y, float scale, float animationTime, float rand, float n1)
        {
            int swellFrequency = ResolveWaveFrequency(3.1f, scale);
            int ridgeFrequency = ResolveWaveFrequency(5.2f, scale);
            float lateralPhase1 =
                (0.11f * SinTau(x + (0.80f * y) + (0.08f * rand))) +
                (0.06f * (rand - 0.5f));
            float lateralPhase2 =
                (0.06f * SinTau(x - (0.50f * y) + (0.10f * n1))) +
                (0.04f * (n1 - 0.5f));
            return
                (0.28f * SinTau((swellFrequency * (y - (0.19f * animationTime))) + lateralPhase1 + 0.11f)) +
                (0.15f * SinTau((ridgeFrequency * (y - (0.22f * animationTime))) + lateralPhase2 + 0.74f));
        }

        private static float computeCrestMask(
            int index,
            float scale,
            float animationTime,
            float hn,
            float sn,
            float packetMix,
            float crestThickness,
            float crestSegmentation,
            float crestDensity,
            float crestBrightness)
        {
            float refX = _staticRefX[index];
            float refY = _staticRefY[index];
            float xs = _staticXs[index];
            float ys = Wrap01(_staticYsBase[index] - (0.05f * animationTime));
            float n3 = _staticN3[index];
            float rand = _staticRand[index];

            float segTex =
                (0.40f * SampleField(_fieldSeg, (xs * 1.25f) + 0.03f, (ys * 0.60f) + 0.02f)) +
                (0.26f * SampleField(_fieldSeg2, (xs * 1.85f) - 0.01f, (ys * 0.76f) + 0.01f)) +
                (0.18f * SampleField(_fieldRand, (xs * 2.40f) + 0.02f, (ys * 1.15f) - 0.04f)) +
                (0.16f * packetMix);
            segTex = Saturate(segTex);

            float localCenter = 0.64f + (0.020f * (SampleField(_fieldRand2, xs * 1.6f, ys * 1.4f) - 0.5f));
            float widthScale = Lerp(1.18f, 0.90f, crestThickness);
            float localWidth = (0.040f + (0.015f * SampleField(_fieldRand, xs * 1.9f, ys * 1.7f))) * widthScale;
            float crestBand = MathF.Exp(-MathF.Pow((hn - localCenter) / MathF.Max(0.0001f, localWidth), 2f));

            float slopeGate = MathF.Pow(Saturate((sn - 0.10f) / 0.40f), 0.92f);
            float segThreshold = Lerp(0.22f, 0.18f, crestDensity);
            float segSoftness = Lerp(0.50f, 0.34f, crestSegmentation);
            float segGate = 0.50f + (0.50f * SmoothStep(segThreshold, segThreshold + MathF.Max(0.12f, segSoftness), segTex));

            float elongationTex = SampleField(_fieldPack1, xs * 1.35f, ys * 0.52f);
            float horizontalBias = 0.60f + (0.40f * Saturate(elongationTex));

            float intensityTex =
                (0.45f * SampleField(_fieldRand, (xs * 2.2f) + 0.01f, (ys * 1.6f) + 0.03f)) +
                (0.30f * SampleField(_fieldRand2, (xs * 3.1f) - 0.02f, (ys * 2.4f) - 0.01f)) +
                (0.25f * SampleField(_fieldPack1, (xs * 1.3f) + 0.02f, (ys * 1.1f) + 0.01f));
            float intensity = 0.88f + (0.24f * Saturate(intensityTex));

            float rawCrest = crestBand * slopeGate;
            float segmentedCrest = rawCrest * segGate * horizontalBias;
            float underCrest = rawCrest * (0.22f + (0.14f * packetMix));
            float contour = ((0.72f * segmentedCrest) + (0.38f * underCrest)) * intensity;
            contour = Math.Clamp(contour, 0f, 1.35f);
            contour *= Lerp(0.96f, 1.03f, packetMix);

            // A single repeated ocean tile can still reveal its repeat boundary if
            // bright crest energy lands exactly on the tile edges. Suppress only the
            // foam layer in a narrow periodic margin so the water body remains
            // continuous while seam-aligned white lines disappear.
            float seamDistanceX = MathF.Min(refX, 1f - refX);
            float seamDistanceY = MathF.Min(refY, 1f - refY);
            float seamFadeX = SmoothStep(0.012f, 0.045f, seamDistanceX);
            float seamFadeY = SmoothStep(0.012f, 0.045f, seamDistanceY);
            contour *= seamFadeX * seamFadeY;

            float brightnessFactor = crestBrightness <= 0f
                ? 0f
                : Math.Clamp(crestBrightness / DefaultCrestBrightness, 0f, 2f);

            return Saturate(contour * brightnessFactor);
        }

        private static Vector3 applyOceanColor(
            Vector3 baseColor,
            float backgroundField,
            float hn,
            float sn,
            float crestMask,
            float backgroundVariationStrength)
        {
            float backgroundFactor = Math.Clamp(
                backgroundVariationStrength / DefaultBackgroundVariationStrength,
                0f,
                2f);
            float centeredBackground = backgroundField - 0.5f;
            float compressedBackground =
                centeredBackground >= 0f
                    ? centeredBackground * 0.42f
                    : centeredBackground * 0.18f;
            float backgroundLuminance = 0.982f + ((0.040f * backgroundFactor) * compressedBackground);
            float waveLuminance = 1.0f + (0.011f * (hn - 0.5f)) + (0.009f * (sn - 0.5f));
            Vector3 water = Vector3.Clamp(baseColor * (backgroundLuminance * waveLuminance), Vector3.Zero, Vector3.One);
            return Vector3.Lerp(water, Vector3.One, Saturate(0.97f * crestMask));
        }

        private static void EnsureReferenceFields()
        {
            if (_fieldWidth == _tileWidth &&
                _fieldHeight == _tileHeight &&
                _fieldN1 != null &&
                _fieldPack3 != null)
            {
                return;
            }

            _fieldWidth = _tileWidth;
            _fieldHeight = _tileHeight;
            _fieldN1 = BuildFbmField(_fieldWidth, _fieldHeight, 10, 1f, 1f, 0f, 0f);
            _fieldN2 = BuildFbmField(_fieldWidth, _fieldHeight, 20, 1.6f, 1.4f, 0.11f, 0.07f);
            _fieldN3 = BuildFbmField(_fieldWidth, _fieldHeight, 30, 2.1f, 2.0f, 0.31f, 0.17f);
            _fieldSeg = BuildFbmField(_fieldWidth, _fieldHeight, 40, 1.25f, 0.95f, 0.23f, 0.41f);
            _fieldSeg2 = BuildFbmField(_fieldWidth, _fieldHeight, 50, 2.3f, 1.2f, 0.05f, 0.19f);
            _fieldRand = BuildFbmField(_fieldWidth, _fieldHeight, 60, 1.8f, 1.7f, 0.13f, 0.29f);
            _fieldRand2 = BuildFbmField(_fieldWidth, _fieldHeight, 70, 3.2f, 2.8f, 0.09f, 0.17f);
            _fieldPack1 = BuildFbmField(_fieldWidth, _fieldHeight, 90, 0.9f, 1.0f, 0.17f, 0.23f);
            _fieldPack2 = BuildFbmField(_fieldWidth, _fieldHeight, 91, 1.4f, 1.3f, 0.41f, 0.05f);
            _fieldPack3 = BuildFbmField(_fieldWidth, _fieldHeight, 92, 2.0f, 1.8f, 0.09f, 0.31f);
        }

        private static void EnsureStaticSamplingCache(float fieldScale)
        {
            float scale = Math.Clamp(fieldScale, 0.20f, 12f);
            if (_staticRefX != null &&
                _staticRefX.Length == _tileWidth * _tileHeight &&
                !float.IsNaN(_staticCacheFieldScale) &&
                MathF.Abs(_staticCacheFieldScale - scale) <= 0.0001f)
            {
                return;
            }

            int width = _tileWidth;
            int height = _tileHeight;
            int count = width * height;
            _staticRefX = new float[count];
            _staticRefY = new float[count];
            _staticWaveXBase = new float[count];
            _staticWaveYBase = new float[count];
            _staticFieldX = new float[count];
            _staticFieldY = new float[count];
            _staticN1 = new float[count];
            _staticN2 = new float[count];
            _staticN3 = new float[count];
            _staticRand = new float[count];
            _staticRand2 = new float[count];
            _staticP1 = new float[count];
            _staticP2 = new float[count];
            _staticP3 = new float[count];
            _staticPacketMix = new float[count];
            _staticBody = new float[count];
            _staticXs = new float[count];
            _staticYsBase = new float[count];

            float warpFactorX = Math.Clamp(WarpStrengthX / DefaultWarpStrengthX, 0f, 3f);
            float warpFactorY = Math.Clamp(WarpStrengthY / DefaultWarpStrengthY, 0f, 3f);

            Parallel.For(0, height, TileBuildParallelOptions, y =>
            {
                float v = (float)y / height;
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    float u = (float)x / width;
                    MapReferenceAxes(u, v, out float refX, out float refY);
                    float fieldX = refX;
                    float fieldY = refY;
                    float n1 = SampleField(_fieldN1, fieldX, fieldY);
                    float n2 = SampleField(_fieldN2, fieldX, fieldY);
                    float n3 = SampleField(_fieldN3, fieldX, fieldY);
                    float rand = SampleField(_fieldRand, fieldX, fieldY);
                    float rand2 = SampleField(_fieldRand2, fieldX, fieldY);
                    float warpX = warpFactorX *
                        ((0.036f * (n1 - 0.5f)) + (0.020f * (n3 - 0.5f)) + (0.010f * SinTau(refY + (0.17f * n2))));
                    float warpY = warpFactorY *
                        ((0.018f * (n2 - 0.5f)) + (0.010f * (rand2 - 0.5f)));
                    float packetX = fieldX + warpX;
                    float packetY = fieldY + warpY;
                    float p1 = packetMask1(packetX, packetY);
                    float p2 = packetMask2(packetX, packetY);
                    float p3 = packetMask3(packetX, packetY);

                    _staticRefX[index] = refX;
                    _staticRefY[index] = refY;
                    _staticWaveXBase[index] = refX + warpX;
                    _staticWaveYBase[index] = refY + warpY;
                    _staticFieldX[index] = fieldX;
                    _staticFieldY[index] = fieldY;
                    _staticN1[index] = n1;
                    _staticN2[index] = n2;
                    _staticN3[index] = n3;
                    _staticRand[index] = rand;
                    _staticRand2[index] = rand2;
                    _staticP1[index] = p1;
                    _staticP2[index] = p2;
                    _staticP3[index] = p3;
                    _staticPacketMix[index] = Saturate((p1 + p2 + p3) / 3f);
                    _staticBody[index] =
                        (0.16f * SampleField(_fieldN1, packetX * 0.75f, packetY * 0.72f)) +
                        (0.09f * SampleField(_fieldN2, (packetX * 0.95f) + 0.05f, (packetY * 0.88f) - 0.02f)) +
                        (0.04f * SampleField(_fieldN3, (packetX * 1.20f) - 0.03f, (packetY * 1.05f) + 0.04f));
                    _staticXs[index] = fieldX + (0.07f * (n2 - 0.5f)) + (0.04f * (rand - 0.5f));
                    _staticYsBase[index] = fieldY + (0.06f * (n1 - 0.5f)) + (0.03f * (rand2 - 0.5f));
                }
            });

            _staticCacheFieldScale = scale;
        }

        private static float[] BuildFbmField(int width, int height, int seed, float scaleX, float scaleY, float offsetX, float offsetY)
        {
            float[] octave1 = BuildPeriodicValueNoiseField(width, height, 4, 4, seed + 1, scaleX, scaleY, offsetX, offsetY);
            float[] octave2 = BuildPeriodicValueNoiseField(width, height, 8, 8, seed + 2, scaleX, scaleY, offsetX, offsetY);
            float[] octave3 = BuildPeriodicValueNoiseField(width, height, 16, 16, seed + 3, scaleX, scaleY, offsetX, offsetY);
            float[] octave4 = BuildPeriodicValueNoiseField(width, height, 32, 32, seed + 4, scaleX, scaleY, offsetX, offsetY);
            float[] field = new float[width * height];

            Parallel.For(0, field.Length, index =>
            {
                field[index] =
                    (0.45f * octave1[index]) +
                    (0.27f * octave2[index]) +
                    (0.17f * octave3[index]) +
                    (0.11f * octave4[index]);
            });

            return field;
        }

        private static float[] BuildPeriodicValueNoiseField(
            int width,
            int height,
            int gridX,
            int gridY,
            int seed,
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY)
        {
            float[] field = new float[width * height];
            float[] grid = new float[gridX * gridY];
            Random random = new(seed);
            for (int i = 0; i < grid.Length; i++)
            {
                grid[i] = (float)random.NextDouble();
            }

            Parallel.For(0, height, y =>
            {
                float v = (float)y / height;
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float xf = (((u * scaleX) + offsetX) * gridX);
                    float yf = (((v * scaleY) + offsetY) * gridY);

                    int x0Floor = FastFloor(xf);
                    int y0Floor = FastFloor(yf);
                    int x0 = PositiveMod(x0Floor, gridX);
                    int y0 = PositiveMod(y0Floor, gridY);
                    int x1 = (x0 + 1) % gridX;
                    int y1 = (y0 + 1) % gridY;

                    float tx = SmoothStep01(xf - x0Floor);
                    float ty = SmoothStep01(yf - y0Floor);

                    float v00 = grid[(y0 * gridX) + x0];
                    float v10 = grid[(y0 * gridX) + x1];
                    float v01 = grid[(y1 * gridX) + x0];
                    float v11 = grid[(y1 * gridX) + x1];

                    float a = Lerp(v00, v10, tx);
                    float b = Lerp(v01, v11, tx);
                    field[row + x] = Lerp(a, b, ty);
                }
            });

            return field;
        }

        private static float packetMask(
            float[] field,
            float x,
            float y,
            float scaleX,
            float scaleY,
            float threshold,
            float softness)
        {
            float value = SampleField(field, x * scaleX, y * scaleY);
            float masked = Saturate((value - threshold) / MathF.Max(0.001f, softness));
            return SmoothStep01(masked);
        }

        private static float SampleField(float[] field, float x, float y)
        {
            if (field == null || _fieldWidth <= 0 || _fieldHeight <= 0)
            {
                return 0.5f;
            }

            float wrappedX = Wrap01(x) * _fieldWidth;
            float wrappedY = Wrap01(y) * _fieldHeight;
            int x0Floor = FastFloor(wrappedX);
            int y0Floor = FastFloor(wrappedY);
            int x0 = PositiveMod(x0Floor, _fieldWidth);
            int y0 = PositiveMod(y0Floor, _fieldHeight);
            int x1 = (x0 + 1) % _fieldWidth;
            int y1 = (y0 + 1) % _fieldHeight;
            float tx = wrappedX - x0Floor;
            float ty = wrappedY - y0Floor;

            float v00 = field[(y0 * _fieldWidth) + x0];
            float v10 = field[(y0 * _fieldWidth) + x1];
            float v01 = field[(y1 * _fieldWidth) + x0];
            float v11 = field[(y1 * _fieldWidth) + x1];

            float a = Lerp(v00, v10, tx);
            float b = Lerp(v01, v11, tx);
            return Lerp(a, b, ty);
        }

        private static void GetValueRange(float[] values, out float minValue, out float maxValue)
        {
            minValue = float.MaxValue;
            maxValue = float.MinValue;

            if (values == null || values.Length == 0)
            {
                minValue = 0f;
                maxValue = 1f;
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                if (value < minValue)
                {
                    minValue = value;
                }

                if (value > maxValue)
                {
                    maxValue = value;
                }
            }

            if (minValue == float.MaxValue || maxValue == float.MinValue)
            {
                minValue = 0f;
                maxValue = 1f;
            }
        }

        private static float NormalizeValue(float value, float minValue, float maxValue)
        {
            float range = maxValue - minValue;
            if (range <= 0.000001f)
            {
                return 0.5f;
            }

            return Saturate((value - minValue) / range);
        }

        private static int ComputeGenerationHash(float fieldScale)
        {
            HashCode hash = new();
            hash.Add(MathF.Round(Math.Clamp(fieldScale, 0.20f, 12f) * 1000f));
            hash.Add(ClampToByte(BaseColor.X));
            hash.Add(ClampToByte(BaseColor.Y));
            hash.Add(ClampToByte(BaseColor.Z));
            hash.Add(MathF.Round(Math.Clamp(TimeScale, 0f, 10f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(DownwardSpeed, 0f, 5f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(BackgroundVariationStrength, 0f, 1f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(WarpStrengthX, 0f, 1f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(WarpStrengthY, 0f, 1f) * 1000f));
            hash.Add(MathF.Round(Math.Max(0f, CrestBrightness) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(CrestThickness, 0f, 1f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(CrestSegmentation, 0f, 1f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(CrestDensity, 0f, 1f) * 1000f));
            hash.Add(MathF.Round(Math.Max(0f, WaveSet1Strength) * 1000f));
            hash.Add(MathF.Round(Math.Max(0f, WaveSet2Strength) * 1000f));
            hash.Add(MathF.Round(Math.Max(0f, WaveSet3Strength) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(PanelScale, 0.10f, 12f) * 1000f));
            hash.Add(MathF.Round(Math.Clamp(DetailScale, 0.35f, 2f) * 1000f));
            return hash.ToHashCode();
        }

        private static float ResolveScaledAnimationTime(float timeSeconds)
        {
            float speedFactor = DefaultDownwardSpeed <= 0f
                ? 1f
                : Math.Clamp(DownwardSpeed / DefaultDownwardSpeed, 0f, 4f);
            return Math.Max(0f, timeSeconds) * Math.Max(0f, TimeScale) * speedFactor;
        }

        private static float ResolveDetailAnimationPhase(float timeSeconds)
        {
            return Wrap01(ResolveScaledAnimationTime(timeSeconds) / DetailAnimationCycleSeconds);
        }

        private static float ResolveDetailAnimationTime(float timeSeconds)
        {
            return ResolveLoopAnimationTimeFromPhase(ResolveDetailAnimationPhase(timeSeconds));
        }

        private static float ResolveLoopFrameAnimationTime(int frameIndex)
        {
            float phase = frameIndex / (float)AnimationLoopFrameCount;
            return ResolveLoopAnimationTimeFromPhase(phase);
        }

        private static float ResolveLoopAnimationTimeFromPhase(float phase)
        {
            float triangle = 1f - MathF.Abs((2f * Wrap01(phase)) - 1f);
            return triangle * DetailAnimationSpanSeconds;
        }

        private static float ResolveWorldScrollOffset(float timeSeconds)
        {
            return ResolveScaledAnimationTime(timeSeconds) * _tileHeight * DefaultDownwardSpeed;
        }

        private static void DrawWorldAnchoredTiles(
            SpriteBatch spriteBatch,
            Rectangle panelBounds,
            float timeSeconds,
            Matrix cameraTransform)
        {
            if (_tileTexture == null || _tileTexture.IsDisposed || _tileWidth <= 0 || _tileHeight <= 0)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            if (!TryGetVisibleWorldBounds(cameraTransform, panelBounds, out float minX, out float maxX, out float minY, out float maxY))
            {
                return;
            }

            float scrollOffset = ResolveWorldScrollOffset(timeSeconds);

            Texture2D currentTexture = _tileTexture;
            Texture2D nextTexture = null;
            float blendToNext = 0f;
            if (_animationLoopReady &&
                _animationLoopTextures != null &&
                _animationLoopTextures.Length == AnimationLoopFrameCount &&
                _animationLoopParameterHash == _generatedParameterHash &&
                MathF.Abs(_animationLoopFieldScale - _generatedFieldScale) <= 0.01f)
            {
                float framePosition = ResolveDetailAnimationPhase(timeSeconds) * AnimationLoopFrameCount;
                int frameIndex = PositiveMod((int)MathF.Floor(framePosition), AnimationLoopFrameCount);
                int nextFrameIndex = (frameIndex + 1) % AnimationLoopFrameCount;
                currentTexture = _animationLoopTextures[frameIndex] ?? _tileTexture;
                nextTexture = _animationLoopTextures[nextFrameIndex];
                blendToNext = framePosition - MathF.Floor(framePosition);
            }

            bool blendingFrames =
                nextTexture != null &&
                !nextTexture.IsDisposed &&
                currentTexture != null &&
                !currentTexture.IsDisposed &&
                blendToNext > 0.001f;
            Rectangle previousScissor = graphicsDevice.ScissorRectangle;
            Rectangle scissorRect = BuildViewportClampedScissorRectangle(panelBounds, graphicsDevice.Viewport);

            graphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                OceanScissorRasterizerState,
                null,
                cameraTransform);

            DrawTiledOceanTexture(spriteBatch, currentTexture, minX, maxX, minY, maxY, scrollOffset, 1f);
            if (blendingFrames)
            {
                DrawTiledOceanTexture(spriteBatch, nextTexture, minX, maxX, minY, maxY, scrollOffset, blendToNext);
            }

            spriteBatch.End();
            graphicsDevice.ScissorRectangle = previousScissor;
        }

        private static Color[] BuildDisplayTexturePixels(Color[] sourcePixels, int width, int height)
        {
            if (sourcePixels == null || width <= 0 || height <= 0)
            {
                return Array.Empty<Color>();
            }

            int displayWidth = width + 1;
            int displayHeight = height + 1;
            Color[] pixels = new Color[displayWidth * displayHeight];

            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * width;
                int destRow = y * displayWidth;
                for (int x = 0; x < width; x++)
                {
                    pixels[destRow + x] = sourcePixels[sourceRow + x];
                }

                pixels[destRow + width] = sourcePixels[sourceRow];
            }

            int lastRow = height * displayWidth;
            for (int x = 0; x < width; x++)
            {
                pixels[lastRow + x] = sourcePixels[x];
            }

            pixels[lastRow + width] = sourcePixels[0];
            return pixels;
        }

        private static void DrawTiledOceanTexture(
            SpriteBatch spriteBatch,
            Texture2D texture,
            float minX,
            float maxX,
            float minY,
            float maxY,
            float scrollOffset,
            float alpha)
        {
            if (spriteBatch == null || texture == null || texture.IsDisposed || alpha <= 0f)
            {
                return;
            }

            int startTileX = (int)MathF.Floor(minX / _tileWidth) - 1;
            int endTileX = (int)MathF.Ceiling(maxX / _tileWidth) + 1;
            int startTileY = (int)MathF.Floor((minY - scrollOffset) / _tileHeight) - 1;
            int endTileY = (int)MathF.Ceiling((maxY - scrollOffset) / _tileHeight) + 1;
            Color tint = Color.White * Math.Clamp(alpha, 0f, 1f);
            Rectangle sourceRectangle = new(0, 0, _tileWidth, _tileHeight);

            for (int tileY = startTileY; tileY <= endTileY; tileY++)
            {
                float worldY = (tileY * _tileHeight) + scrollOffset;
                for (int tileX = startTileX; tileX <= endTileX; tileX++)
                {
                    Vector2 worldPosition = new(tileX * _tileWidth, worldY);
                    spriteBatch.Draw(
                        texture,
                        worldPosition,
                        sourceRectangle,
                        tint,
                        0f,
                        Vector2.Zero,
                        Vector2.One,
                        SpriteEffects.None,
                        0f);
                }
            }
        }

        private static Rectangle BuildViewportClampedScissorRectangle(Rectangle bounds, Viewport viewport)
        {
            int left = Math.Clamp(bounds.Left, viewport.X, viewport.X + viewport.Width);
            int top = Math.Clamp(bounds.Top, viewport.Y, viewport.Y + viewport.Height);
            int right = Math.Clamp(bounds.Right, viewport.X, viewport.X + viewport.Width);
            int bottom = Math.Clamp(bounds.Bottom, viewport.Y, viewport.Y + viewport.Height);
            int width = Math.Max(1, right - left);
            int height = Math.Max(1, bottom - top);
            return new Rectangle(left, top, width, height);
        }

        private static bool TryGetVisibleWorldBounds(
            Matrix cameraTransform,
            Rectangle panelBounds,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            Matrix inverse = Matrix.Invert(cameraTransform);

            Vector2 topLeft = Vector2.Transform(new Vector2(panelBounds.Left, panelBounds.Top), inverse);
            Vector2 topRight = Vector2.Transform(new Vector2(panelBounds.Right, panelBounds.Top), inverse);
            Vector2 bottomLeft = Vector2.Transform(new Vector2(panelBounds.Left, panelBounds.Bottom), inverse);
            Vector2 bottomRight = Vector2.Transform(new Vector2(panelBounds.Right, panelBounds.Bottom), inverse);

            minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
            maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
            minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
            maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

            return !(float.IsNaN(minX) || float.IsNaN(maxX) || float.IsNaN(minY) || float.IsNaN(maxY));
        }

        private static float SinTau(float value) => MathF.Sin(Tau * value);
        private static int ResolveWaveFrequency(float baseFrequency, float scale)
        {
            float scaled = MathF.Max(0.50f, baseFrequency * Math.Clamp(scale, 0.50f, 2.20f));
            return Math.Max(1, (int)MathF.Round(scaled));
        }

        private static float PeriodicSin(float kxBase, float kyBase, float scale, float x, float y, float phase)
        {
            float sx = kxBase * Math.Max(0.10f, scale);
            float sy = kyBase * Math.Max(0.10f, scale);

            float absX = MathF.Abs(sx);
            float absY = MathF.Abs(sy);
            int lowX = (int)MathF.Floor(absX);
            int lowY = (int)MathF.Floor(absY);
            int highX = lowX + 1;
            int highY = lowY + 1;
            float blendX = absX - lowX;
            float blendY = absY - lowY;
            int signX = sx >= 0f ? 1 : -1;
            int signY = sy >= 0f ? 1 : -1;

            float s00 = SinTau((signX * lowX * x) + (signY * lowY * y) + phase);
            float s10 = SinTau((signX * highX * x) + (signY * lowY * y) + phase);
            float s01 = SinTau((signX * lowX * x) + (signY * highY * y) + phase);
            float s11 = SinTau((signX * highX * x) + (signY * highY * y) + phase);

            float sxBlendLow = Lerp(s00, s10, blendX);
            float sxBlendHigh = Lerp(s01, s11, blendX);
            return Lerp(sxBlendLow, sxBlendHigh, blendY);
        }
        private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);
        private static void MapReferenceAxes(float x, float y, out float mappedX, out float mappedY)
        {
            // Keep the reference script's native axis convention so its dominant Y
            // wave terms remain horizontal crests on screen. Rotating these axes
            // turns the crest field into vertical stripe columns.
            mappedX = x;
            mappedY = y;
        }

        private static float SmoothStep01(float value)
        {
            float x = Saturate(value);
            return x * x * (3f - (2f * x));
        }

        private static float Wrap01(float value) => value - MathF.Floor(value);
        private static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0f, 1f));
        private static int FastFloor(float value) => (int)MathF.Floor(value);

        private static int PositiveMod(int value, int modulus)
        {
            if (modulus <= 0)
            {
                return 0;
            }

            int remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (edge1 <= edge0)
            {
                return value >= edge1 ? 1f : 0f;
            }

            float x = Saturate((value - edge0) / (edge1 - edge0));
            return x * x * (3f - (2f * x));
        }

        private static int ClampToByte(float value)
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            return (int)MathF.Round(clamped * 255f);
        }

        private static void DisposeAnimationLoopTextures()
        {
            if (_animationLoopTextures == null)
            {
                return;
            }

            for (int i = 0; i < _animationLoopTextures.Length; i++)
            {
                _animationLoopTextures[i]?.Dispose();
            }

            _animationLoopTextures = null;
        }
    }
}
