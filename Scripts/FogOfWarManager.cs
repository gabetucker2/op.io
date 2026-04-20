using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        internal struct FogVisualParameters
        {
            public Color BaseColor;
            public float StableWorldScale;
            public float BroadScale;
            public Vector3 BroadAmp;

            public float DetailMaskScale;
            public Vector3 SplotchDarkening;
            public int DetailSeed;
            public int SpeckCount;
            public int DotCount;
            public int DashCount;
            public float MarkSizeMin;
            public float MarkSizeMax;
            public float MarkBlur;
            public float MarkAngleVariance;

            public float WarpScaleA;
            public float WarpScaleB;
            public float WarpAmp;
            public float Macro1Scale;
            public float Macro2Scale;
            public float Macro3Scale;
            public float Amp1;
            public float Amp2;
            public float Amp3;
            public float FieldSmoothing;

            public float NearWidth;
            public float MidWidth;
            public float WideWidth;
            public float NearWeight;
            public float MidWeight;
            public float WideWeight;
            public Vector3 EdgeDarkColor;

            public float RevealNearWeight;
            public float RevealMidWeight;
            public Vector3 RevealLiftColor;

            public float BoundarySoftness;
            public int PolygonSides;
            public float PolygonRotation;
            public float PolygonNoiseBlend;
            public int PolygonToothCount;
            public float PolygonToothAmplitude;
            public float PolygonToothSharpness;

            public static FogVisualParameters CreateDefaults()
            {
                return new FogVisualParameters
                {
                    BaseColor = new Color(0, 0, 0, 255),
                    StableWorldScale = 0.00125f,
                    BroadScale = 0.35f,
                    BroadAmp = Vector3.Zero,

                    DetailMaskScale = 0.30f,
                    SplotchDarkening = Vector3.Zero,
                    DetailSeed = 19073,
                    SpeckCount = 380,
                    DotCount = 300,
                    DashCount = 84,
                    MarkSizeMin = 2f,
                    MarkSizeMax = 7f,
                    MarkBlur = 0.45f,
                    MarkAngleVariance = MathHelper.ToRadians(20f),

                    WarpScaleA = 0.20f,
                    WarpScaleB = 0.27f,
                    WarpAmp = 0.30f,
                    Macro1Scale = 0.36f,
                    Macro2Scale = 0.78f,
                    Macro3Scale = 1.35f,
                    // Ratios from the 1200x750 / radius 176 reference.
                    Amp1 = 0.24f,
                    Amp2 = 0.085f,
                    Amp3 = 0.030f,
                    FieldSmoothing = 0f,

                    // Ratios from reference: 16/50/110 at radius 176.
                    NearWidth = 0.091f,
                    MidWidth = 0.284f,
                    WideWidth = 0.625f,
                    NearWeight = 1.10f,
                    MidWeight = 0.72f,
                    WideWeight = 0.28f,
                    EdgeDarkColor = Vector3.Zero,

                    // Keep lift nearly off by default to avoid over-bright revealed content.
                    RevealNearWeight = 0f,
                    RevealMidWeight = 0f,
                    RevealLiftColor = new Vector3(8f / 255f, 12f / 255f, 18f / 255f),

                    BoundarySoftness = 0.012f,
                    // Micro polygon faceting: large frontier stays circle-like.
                    PolygonSides = 96,
                    PolygonRotation = 0f,
                    PolygonNoiseBlend = 0.010f,
                    PolygonToothCount = 288,
                    PolygonToothAmplitude = 0.022f,
                    PolygonToothSharpness = 5.6f
                };
            }
        }

        private enum MarkType
        {
            Speck,
            Dot,
            Dash
        }

        private const float SightRadiusScale = 20f;
        private const int FogBodyTextureSize = 1024;
        private const int FrontierTextureSize = 1536;
        private const int FrontierTextureSizeMoveMedium = 1152;
        private const int FrontierTextureSizeMoveFast = 896;
        private const int FrontierRuntimeTextureSize = FrontierTextureSizeMoveMedium;
        private const int FrontierDirectionBins = 8;
        private const int FrontierPhaseCount = 30;
        private const float FrontierFramesPerCentifoot = 30f;
        private const float FrontierDomainExtent = 1.35f;
        private const float FrontierMinUpdateIntervalSeconds = 1f / 30f;
        private const float FrontierFastUpdateIntervalSeconds = 1f / 90f;
        private const float FrontierMoveSpeedForMaxRateCentifootPerSecond = 80f;
        private const int EdgeNoiseTableSize = 512;
        private const int EdgeNoiseTableMask = EdgeNoiseTableSize - 1;
        private const int FrontierCacheMagic = 0x464F5743; // FOWC
        private const int FrontierCacheVersion = 3;
        private const int FrontierHeaderIntCount = 8;
        private const int FrontierCacheRadiusKey = 1000;
        private const string FrontierCacheFileName = "fow_frontier_cache.bin";
        private const int FogBodyCacheMagic = 0x464F5742; // FOWB
        private const int FogBodyCacheVersion = 1;
        private const string FogBodyCacheFileName = "fow_body_cache.bin";

        private static readonly List<VisionSource> VisionSources = new();
        private static readonly object FrontierComputeSync = new();

        private static readonly BlendState VisionCutoutBlend = new()
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.InverseSourceAlpha,
            AlphaBlendFunction = BlendFunction.Add
        };

        private sealed class FrontierVariant
        {
            public byte[] VisionMaskAlpha;
            public byte[] HiddenEdgeAlpha;
            public byte[] RevealLiftAlpha;
        }

        private readonly struct FrontierBuildRequest
        {
            public FrontierBuildRequest(
                int requestId,
                int textureSize,
                FogVisualParameters parameters,
                float referenceRadius,
                Vector2 referenceCenter,
                int hash,
                float animationPhase,
                int directionBin)
            {
                RequestId = requestId;
                TextureSize = textureSize;
                Parameters = parameters;
                ReferenceRadius = referenceRadius;
                ReferenceCenter = referenceCenter;
                Hash = hash;
                AnimationPhase = animationPhase;
                DirectionBin = directionBin;
            }

            public int RequestId { get; }
            public int TextureSize { get; }
            public FogVisualParameters Parameters { get; }
            public float ReferenceRadius { get; }
            public Vector2 ReferenceCenter { get; }
            public int Hash { get; }
            public float AnimationPhase { get; }
            public int DirectionBin { get; }
        }

        private sealed class FrontierBuildResult
        {
            public int RequestId;
            public int TextureSize;
            public int Hash;
            public float ReferenceRadius;
            public Vector2 ReferenceCenter;
            public float AnimationPhase;
            public int DirectionBin;
            public byte[] VisionMaskAlpha;
            public byte[] HiddenEdgeAlpha;
            public byte[] RevealLiftAlpha;
            public float BuildMilliseconds;
        }

        private sealed class FrontierCache
        {
            public int RadiusKey;
            public int Hash;
            public int TextureSize;
            public int DirectionBins;
            public int PhaseCount;
            public int LayerCount;
            public long DataOffsetBytes;
            public int LayerByteCount;
            public int VariantByteCount;
            public int DirectionBlockByteCount;
            public string FilePath;
            public FrontierVariant[] Variants;
            public bool IsDiskBacked => !string.IsNullOrWhiteSpace(FilePath);

            public void DisposeAll()
            {
                Variants = null;
            }
        }

        private sealed class TimedProgressReporter : IDisposable
        {
            private readonly string _label;
            private readonly int _totalWork;
            private readonly Stopwatch _stopwatch;
            private readonly Timer _timer;
            private int _completedWork;
            private int _isDisposed;

            public TimedProgressReporter(string label, int totalWork, TimeSpan interval)
            {
                _label = string.IsNullOrWhiteSpace(label) ? "FOW cache" : label;
                _totalWork = Math.Max(1, totalWork);
                _stopwatch = Stopwatch.StartNew();
                Console.WriteLine($"[{_label}] progress: 0.0% (0/{_totalWork}) elapsed 00:00:00");
                _timer = new Timer(_ => PrintStatus(isFinal: false), null, interval, interval);
            }

            public void Update(int completedWork)
            {
                int clamped = Math.Clamp(completedWork, 0, _totalWork);
                Interlocked.Exchange(ref _completedWork, clamped);
            }

            public void Complete()
            {
                Update(_totalWork);
                PrintStatus(isFinal: true);
                Dispose();
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                {
                    return;
                }

                _timer.Dispose();
                _stopwatch.Stop();
            }

            private void PrintStatus(bool isFinal)
            {
                if (Volatile.Read(ref _isDisposed) != 0)
                {
                    return;
                }

                int completed = Volatile.Read(ref _completedWork);
                float pct = (completed / (float)_totalWork) * 100f;
                TimeSpan elapsed = _stopwatch.Elapsed;
                string etaText = "--:--:--";
                if (completed > 0 && completed < _totalWork)
                {
                    double unitsPerSecond = completed / Math.Max(0.0001, elapsed.TotalSeconds);
                    if (unitsPerSecond > 0.0001)
                    {
                        double remainingSeconds = (_totalWork - completed) / unitsPerSecond;
                        etaText = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds)).ToString(@"hh\:mm\:ss");
                    }
                }

                string suffix = isFinal ? " [complete]" : string.Empty;
                Console.WriteLine(
                    $"[{_label}] progress: {pct:0.0}% ({completed}/{_totalWork}) elapsed {elapsed:hh\\:mm\\:ss} eta {etaText}{suffix}");
            }
        }

        private static Texture2D _fogBodyTexture;
        private static Texture2D _visionMaskTexture;
        private static Texture2D _hiddenEdgeTexture;
        private static Texture2D _revealLiftTexture;

        private static RenderTarget2D _fogRenderTarget;
        private static RenderTarget2D _revealLiftRenderTarget;

        private static int _fogBodyHash;
        private static FrontierCache _frontierCache;
        private static bool _hasMotionAnchor;
        private static Vector2 _lastMotionCenter;
        private static int _activeDirectionBin;
        private static float _phaseFrameCursor;
        private static int _activePhaseIndex;
        private static float _lastFrameMoveDistanceWorld;
        private static float _frontierTargetUpdateIntervalSeconds = FrontierMinUpdateIntervalSeconds;
        private static int _frontierActiveTextureSize = FrontierTextureSize;
        private static float _frontierBuildMillisecondsLast;
        private static float _frontierBuildMillisecondsAvg;
        private static bool _hasFrontierSample;
        private static Vector2 _frontierSampleCenter;
        private static float _frontierSampleRadius;
        private static int _frontierRuntimeHash;
        private static byte[] _visionAlphaBuffer;
        private static byte[] _hiddenAlphaBuffer;
        private static byte[] _revealAlphaBuffer;
        private static byte[] _diskReadBuffer;
        private static byte[] _zeroAlphaBuffer;
        private static float[] _frontierLocalXCache;
        private static float[] _frontierLocalYCache;
        private static float[] _frontierRadialCache;
        private static float[] _frontierAngleCache;
        private static int _frontierGeometryCacheSize;
        private static float[] _edgeNoiseWarpA;
        private static float[] _edgeNoiseWarpB;
        private static float[] _edgeNoiseMacro1;
        private static float[] _edgeNoiseMacro2;
        private static float[] _edgeNoiseMacro3;
        private static int _edgeNoiseSeedHash = int.MinValue;
        private static Task<FrontierBuildResult> _frontierBuildTask;
        private static bool _hasQueuedFrontierBuild;
        private static int _frontierBuildRequestId;
        private static int _frontierCommittedRequestId;
        private static Texture2D[] _visionDirectionTextures;
        private static Texture2D[] _hiddenDirectionTextures;
        private static Texture2D[] _revealDirectionTextures;

        private static FogVisualParameters _visualParameters = FogVisualParameters.CreateDefaults();

        internal static FogVisualParameters VisualParameters
        {
            get => _visualParameters;
            set
            {
                _visualParameters = SanitizeVisualParameters(value);
                _fogBodyHash = 0;
                _frontierCache?.DisposeAll();
                _frontierCache = null;
                DisposeFrontierRuntimeTextures();
                _hasMotionAnchor = false;
                _lastMotionCenter = Vector2.Zero;
                _activeDirectionBin = 0;
                _phaseFrameCursor = 0f;
                _activePhaseIndex = 0;
                _lastFrameMoveDistanceWorld = 0f;
                _frontierTargetUpdateIntervalSeconds = FrontierMinUpdateIntervalSeconds;
                _frontierActiveTextureSize = FrontierTextureSize;
                _frontierBuildMillisecondsLast = 0f;
                _frontierBuildMillisecondsAvg = 0f;
                _hasFrontierSample = false;
                _frontierSampleCenter = Vector2.Zero;
                _frontierSampleRadius = 0f;
                _frontierRuntimeHash = 0;
                _visionAlphaBuffer = null;
                _hiddenAlphaBuffer = null;
                _revealAlphaBuffer = null;
                _edgeNoiseSeedHash = int.MinValue;
                _hasQueuedFrontierBuild = false;
                _frontierBuildRequestId++;
                _frontierCommittedRequestId = _frontierBuildRequestId;
            }
        }

        public static bool IsFogEnabled => Core.Instance?.Player != null;
        public static bool IsFogActive { get; private set; }
        public static int ActiveVisionSourceCount { get; private set; }
        public static float PlayerSightRadius { get; private set; }
        public static bool FrontierCacheLoadedFromDisk => false;
        public static int ActiveFrontierDirectionBin => _activeDirectionBin;
        public static int ActiveFrontierPhaseIndex => _activePhaseIndex;
        public static int FrontierTextureResolution => FrontierTextureSize;
        public static int FrontierActiveTextureResolution => _frontierActiveTextureSize;
        public static int FrontierPhaseTotal => FrontierPhaseCount;
        public static float FrontierAnimationPhase => _phaseFrameCursor / MathF.Max(1f, FrontierPhaseCount);
        public static float FrontierFramesPerCentifootRate => FrontierFramesPerCentifoot;
        public static float FrontierBuildMsLast => _frontierBuildMillisecondsLast;
        public static float FrontierBuildMsAvg => _frontierBuildMillisecondsAvg;
        public static float FrontierTargetUpdateIntervalSeconds => _frontierTargetUpdateIntervalSeconds;
        public static bool FrontierBuildInFlight =>
            (_frontierBuildTask != null && !_frontierBuildTask.IsCompleted) || _hasQueuedFrontierBuild;

        public static void Prepare(Matrix cameraTransform)
        {
            if (Core.Instance?.GraphicsDevice == null || Core.Instance?.SpriteBatch == null)
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
            EnsureRenderTargets(graphicsDevice, width, height);
            EnsureFogBodyTexture(graphicsDevice);
            EnsureFrontierTextures(graphicsDevice, GetReferenceVisionRadius(), GetReferenceVisionCenter());

            if (!AreVisualResourcesReady())
            {
                IsFogActive = false;
                return;
            }

            SpriteBatch spriteBatch = Core.Instance.SpriteBatch;
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
            Viewport previousViewport = graphicsDevice.Viewport;
            IsFogActive = false;

            try
            {
                RenderFogTarget(graphicsDevice, spriteBatch, cameraTransform, width, height);
                IsFogActive = true;
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    graphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    graphicsDevice.SetRenderTarget(null);
                }

                graphicsDevice.Viewport = previousViewport;
            }
        }

        public static void DrawOverlay(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || !IsFogActive || _fogRenderTarget == null || _fogRenderTarget.IsDisposed)
            {
                return;
            }

            int width = spriteBatch.GraphicsDevice?.Viewport.Width ?? _fogRenderTarget.Width;
            int height = spriteBatch.GraphicsDevice?.Viewport.Height ?? _fogRenderTarget.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Rectangle destination = new Rectangle(0, 0, width, height);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            spriteBatch.Draw(_fogRenderTarget, destination, Color.White);
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

        private static void EnsureRenderTargets(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (_fogRenderTarget == null || _fogRenderTarget.IsDisposed ||
                _fogRenderTarget.Width != width || _fogRenderTarget.Height != height)
            {
                _fogRenderTarget?.Dispose();
                _fogRenderTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            }
            _revealLiftRenderTarget?.Dispose();
            _revealLiftRenderTarget = null;
        }

        private static void EnsureFogBodyTexture(GraphicsDevice graphicsDevice)
        {
            int desiredHash = ComputeFogBodyHash(_visualParameters);
            if (_fogBodyTexture == null || _fogBodyTexture.IsDisposed || _fogBodyHash != desiredHash)
            {
                _fogBodyTexture?.Dispose();
                _fogBodyTexture = BuildFogBodyTexture(graphicsDevice, FogBodyTextureSize, _visualParameters);
                _fogBodyHash = desiredHash;
            }
        }

        private static void EnsureFrontierTextures(GraphicsDevice graphicsDevice, float referenceRadius, Vector2 referenceCenter)
        {
            // Static circular vision mask only: no animated/position-variant frontier generation.
            int activeTextureSize = FrontierRuntimeTextureSize;
            _frontierActiveTextureSize = activeTextureSize;
            _frontierTargetUpdateIntervalSeconds = 0f;
            bool needsReveal = false;
            EnsureFrontierRuntimeTextures(graphicsDevice, activeTextureSize);
            if (_visionMaskTexture == null || _visionMaskTexture.IsDisposed)
            {
                return;
            }

            int desiredHash = HashCode.Combine(
                ComputeFrontierHash(_visualParameters, FrontierCacheRadiusKey),
                activeTextureSize);
            bool needsBuild = !_hasFrontierSample || _frontierRuntimeHash != desiredHash;
            if (needsBuild)
            {
                FrontierBuildRequest request = CreateFrontierBuildRequest(
                    activeTextureSize,
                    _visualParameters,
                    referenceRadius,
                    referenceCenter,
                    desiredHash,
                    animationPhase: 0f,
                    directionBin: 0);

                BuildFrontierTexturesProcedural(
                    request.TextureSize,
                    request.Parameters,
                    request.ReferenceRadius,
                    request.ReferenceCenter,
                    request.AnimationPhase,
                    request.DirectionBin,
                    out byte[] visionMaskAlpha,
                    out byte[] hiddenEdgeAlpha,
                    out byte[] revealLiftAlpha);

                CommitFrontierTextures(
                    needsReveal,
                    request.RequestId,
                    request.Hash,
                    request.ReferenceRadius,
                    request.ReferenceCenter,
                    visionMaskAlpha,
                    hiddenEdgeAlpha,
                    revealLiftAlpha,
                    _frontierBuildMillisecondsLast);
            }

            _hasQueuedFrontierBuild = false;
            _activeDirectionBin = 0;
            _activePhaseIndex = 0;
        }

        private static FrontierBuildRequest CreateFrontierBuildRequest(
            int textureSize,
            FogVisualParameters parameters,
            float referenceRadius,
            Vector2 referenceCenter,
            int hash,
            float animationPhase,
            int directionBin)
        {
            int requestId = Interlocked.Increment(ref _frontierBuildRequestId);
            return new FrontierBuildRequest(
                requestId,
                textureSize,
                parameters,
                MathF.Max(1f, referenceRadius),
                referenceCenter,
                hash,
                animationPhase,
                directionBin);
        }

        private static void StartFrontierBuildAsync(FrontierBuildRequest request)
        {
            _frontierBuildTask = Task.Run(() =>
            {
                long startTicks = Stopwatch.GetTimestamp();
                BuildFrontierTexturesProcedural(
                    request.TextureSize,
                    request.Parameters,
                    request.ReferenceRadius,
                    request.ReferenceCenter,
                    request.AnimationPhase,
                    request.DirectionBin,
                    out byte[] visionMaskAlpha,
                    out byte[] hiddenEdgeAlpha,
                    out byte[] revealLiftAlpha);

                float elapsedMs = (float)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);
                return new FrontierBuildResult
                {
                    RequestId = request.RequestId,
                    TextureSize = request.TextureSize,
                    Hash = request.Hash,
                    ReferenceRadius = request.ReferenceRadius,
                    ReferenceCenter = request.ReferenceCenter,
                    AnimationPhase = request.AnimationPhase,
                    DirectionBin = request.DirectionBin,
                    VisionMaskAlpha = visionMaskAlpha,
                    HiddenEdgeAlpha = hiddenEdgeAlpha,
                    RevealLiftAlpha = revealLiftAlpha,
                    BuildMilliseconds = elapsedMs
                };
            });
        }

        private static void TryCommitCompletedFrontierBuild(bool needsReveal, int desiredHash, int activeTextureSize)
        {
            if (_frontierBuildTask == null || !_frontierBuildTask.IsCompleted)
            {
                return;
            }

            Task<FrontierBuildResult> completedTask = _frontierBuildTask;
            _frontierBuildTask = null;

            FrontierBuildResult result = null;
            try
            {
                result = completedTask.Result;
            }
            catch
            {
                result = null;
            }

            if (result == null ||
                result.Hash != desiredHash ||
                result.RequestId < _frontierCommittedRequestId ||
                result.TextureSize != activeTextureSize)
            {
                return;
            }

            CommitFrontierTextures(
                needsReveal,
                result.RequestId,
                result.Hash,
                result.ReferenceRadius,
                result.ReferenceCenter,
                result.VisionMaskAlpha,
                result.HiddenEdgeAlpha,
                result.RevealLiftAlpha,
                result.BuildMilliseconds);
        }

        private static void CommitFrontierTextures(
            bool needsReveal,
            int requestId,
            int hash,
            float referenceRadius,
            Vector2 referenceCenter,
            byte[] visionMaskAlpha,
            byte[] hiddenEdgeAlpha,
            byte[] revealLiftAlpha,
            float buildMilliseconds)
        {
            UploadAlphaMask(_visionMaskTexture, visionMaskAlpha);

            _visionAlphaBuffer = visionMaskAlpha;
            _hiddenAlphaBuffer = hiddenEdgeAlpha;
            _revealAlphaBuffer = revealLiftAlpha;
            _hasFrontierSample = true;
            _frontierSampleCenter = referenceCenter;
            _frontierSampleRadius = referenceRadius;
            _frontierRuntimeHash = hash;
            _frontierCommittedRequestId = Math.Max(_frontierCommittedRequestId, requestId);
            _frontierBuildMillisecondsLast = buildMilliseconds;
            _frontierBuildMillisecondsAvg = _frontierBuildMillisecondsAvg <= 0f
                ? buildMilliseconds
                : MathHelper.Lerp(_frontierBuildMillisecondsAvg, buildMilliseconds, 0.12f);
        }

        private static FrontierCache BuildFrontierCache(float referenceRadius, int radiusKey, int hash)
        {
            int layerCount = 3;
            int layerByteCount = FrontierTextureSize * FrontierTextureSize;
            FrontierCache cache = new FrontierCache
            {
                RadiusKey = radiusKey,
                Hash = hash,
                TextureSize = FrontierTextureSize,
                DirectionBins = FrontierDirectionBins,
                PhaseCount = FrontierPhaseCount,
                LayerCount = layerCount,
                DataOffsetBytes = FrontierHeaderIntCount * sizeof(int),
                LayerByteCount = layerByteCount,
                VariantByteCount = layerByteCount * layerCount,
                DirectionBlockByteCount = layerByteCount * layerCount * FrontierPhaseCount,
                FilePath = null,
                Variants = new FrontierVariant[FrontierDirectionBins * FrontierPhaseCount]
            };

            for (int directionBin = 0; directionBin < FrontierDirectionBins; directionBin++)
            {
                for (int phaseIndex = 0; phaseIndex < FrontierPhaseCount; phaseIndex++)
                {
                    BuildFrontierTextures(
                        FrontierTextureSize,
                        _visualParameters,
                        referenceRadius,
                        directionBin,
                        phaseIndex,
                        out byte[] visionMaskAlpha,
                        out byte[] hiddenEdgeAlpha,
                        out byte[] revealLiftAlpha);

                    int index = (directionBin * FrontierPhaseCount) + phaseIndex;
                    cache.Variants[index] = new FrontierVariant
                    {
                        VisionMaskAlpha = visionMaskAlpha,
                        HiddenEdgeAlpha = hiddenEdgeAlpha,
                        RevealLiftAlpha = revealLiftAlpha
                    };
                }
            }

            return cache;
        }

        private static FrontierCache BuildFastFrontierFallbackCache(int hash)
        {
            int layerByteCount = FrontierTextureSize * FrontierTextureSize;
            return new FrontierCache
            {
                RadiusKey = FrontierCacheRadiusKey,
                Hash = hash,
                TextureSize = FrontierTextureSize,
                DirectionBins = FrontierDirectionBins,
                PhaseCount = FrontierPhaseCount,
                LayerCount = 2,
                DataOffsetBytes = FrontierHeaderIntCount * sizeof(int),
                LayerByteCount = layerByteCount,
                VariantByteCount = 0,
                DirectionBlockByteCount = 0,
                FilePath = null,
                Variants = Array.Empty<FrontierVariant>()
            };
        }

        private static bool TryLoadFrontierCacheFromDisk(int radiusKey, int hash, out FrontierCache cache)
        {
            cache = null;
            string outputPath = ResolveFrontierCacheOutputPath();
            if (TryReadFrontierCacheFile(outputPath, radiusKey, hash, out cache))
            {
                return true;
            }

            string projectPath = ResolveFrontierCacheProjectPath();
            if (!string.Equals(projectPath, outputPath, StringComparison.OrdinalIgnoreCase) &&
                TryReadFrontierCacheFile(projectPath, radiusKey, hash, out cache))
            {
                return true;
            }

            return false;
        }

        private static bool TryReadFrontierCacheFile(string filePath, int radiusKey, int hash, out FrontierCache cache)
        {
            cache = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                using BinaryReader reader = new BinaryReader(stream);

                int magic = reader.ReadInt32();
                int version = reader.ReadInt32();
                int textureSize = reader.ReadInt32();
                int directionBins = reader.ReadInt32();
                int phaseCount = reader.ReadInt32();
                int layerCount = reader.ReadInt32();
                int fileRadiusKey = reader.ReadInt32();
                int fileHash = reader.ReadInt32();

                if (magic != FrontierCacheMagic ||
                    version != FrontierCacheVersion ||
                    textureSize != FrontierTextureSize ||
                    directionBins != FrontierDirectionBins ||
                    phaseCount != FrontierPhaseCount ||
                    (layerCount != 2 && layerCount != 3) ||
                    fileRadiusKey != radiusKey ||
                    fileHash != hash)
                {
                    return false;
                }

                int layerByteCount = textureSize * textureSize;
                int variantByteCount = layerByteCount * layerCount;
                int directionBlockByteCount = variantByteCount * phaseCount;
                FrontierCache loaded = new FrontierCache
                {
                    RadiusKey = fileRadiusKey,
                    Hash = fileHash,
                    TextureSize = textureSize,
                    DirectionBins = directionBins,
                    PhaseCount = phaseCount,
                    LayerCount = layerCount,
                    DataOffsetBytes = FrontierHeaderIntCount * sizeof(int),
                    LayerByteCount = layerByteCount,
                    VariantByteCount = variantByteCount,
                    DirectionBlockByteCount = directionBlockByteCount,
                    FilePath = filePath,
                    Variants = null
                };

                cache = loaded;
                return true;
            }
            catch
            {
                cache?.DisposeAll();
                cache = null;
                return false;
            }
        }

        private static bool TryWriteFrontierCacheFile(
            string filePath,
            float referenceRadius,
            FogVisualParameters parameters,
            string progressLabel = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                int radiusKey = FrontierCacheRadiusKey;
                int hash = ComputeFrontierHash(parameters, radiusKey);
                int layerByteCount = FrontierTextureSize * FrontierTextureSize;
                bool includeRevealLayer = parameters.RevealNearWeight > 0f || parameters.RevealMidWeight > 0f;
                int layerCount = includeRevealLayer ? 3 : 2;

                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using FileStream stream = File.Create(filePath);
                using BinaryWriter writer = new BinaryWriter(stream);

                writer.Write(FrontierCacheMagic);
                writer.Write(FrontierCacheVersion);
                writer.Write(FrontierTextureSize);
                writer.Write(FrontierDirectionBins);
                writer.Write(FrontierPhaseCount);
                writer.Write(layerCount);
                writer.Write(radiusKey);
                writer.Write(hash);

                int totalVariants = FrontierDirectionBins * FrontierPhaseCount;
                int completedVariants = 0;
                using TimedProgressReporter progress = new TimedProgressReporter(
                    string.IsNullOrWhiteSpace(progressLabel) ? "Frontier cache" : progressLabel,
                    totalVariants,
                    TimeSpan.FromSeconds(10));

                for (int directionBin = 0; directionBin < FrontierDirectionBins; directionBin++)
                {
                    for (int phaseIndex = 0; phaseIndex < FrontierPhaseCount; phaseIndex++)
                    {
                        BuildFrontierTextures(
                            FrontierTextureSize,
                            parameters,
                            referenceRadius,
                            directionBin,
                            phaseIndex,
                            out byte[] visionMaskAlpha,
                            out byte[] hiddenEdgeAlpha,
                            out byte[] revealLiftAlpha);

                        if (visionMaskAlpha.Length != layerByteCount ||
                            hiddenEdgeAlpha.Length != layerByteCount ||
                            revealLiftAlpha.Length != layerByteCount)
                        {
                            return false;
                        }

                        writer.Write(visionMaskAlpha);
                        writer.Write(hiddenEdgeAlpha);
                        if (includeRevealLayer)
                        {
                            writer.Write(revealLiftAlpha);
                        }

                        completedVariants++;
                        progress.Update(completedVariants);
                    }
                }

                writer.Flush();
                progress.Complete();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveFrontierCacheOutputPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Data", FrontierCacheFileName);
        }

        private static string ResolveFrontierCacheProjectPath()
        {
            try
            {
                string projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return ResolveFrontierCacheOutputPath();
                }

                return Path.Combine(projectRoot, "Data", FrontierCacheFileName);
            }
            catch
            {
                return ResolveFrontierCacheOutputPath();
            }
        }

        private static string ResolveFogBodyCacheOutputPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Data", FogBodyCacheFileName);
        }

        private static string ResolveFogBodyCacheProjectPath()
        {
            try
            {
                string projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return ResolveFogBodyCacheOutputPath();
                }

                return Path.Combine(projectRoot, "Data", FogBodyCacheFileName);
            }
            catch
            {
                return ResolveFogBodyCacheOutputPath();
            }
        }

        private static Texture2D TryLoadFogBodyTextureFromDisk(GraphicsDevice graphicsDevice, int size, int expectedHash)
        {
            string outputPath = ResolveFogBodyCacheOutputPath();
            if (TryReadFogBodyCacheFile(outputPath, graphicsDevice, size, expectedHash, out Texture2D outputTexture))
            {
                return outputTexture;
            }

            string projectPath = ResolveFogBodyCacheProjectPath();
            if (!string.Equals(projectPath, outputPath, StringComparison.OrdinalIgnoreCase) &&
                TryReadFogBodyCacheFile(projectPath, graphicsDevice, size, expectedHash, out Texture2D projectTexture))
            {
                return projectTexture;
            }

            return null;
        }

        private static bool TryReadFogBodyCacheFile(
            string filePath,
            GraphicsDevice graphicsDevice,
            int size,
            int expectedHash,
            out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                using BinaryReader reader = new BinaryReader(stream);

                int magic = reader.ReadInt32();
                int version = reader.ReadInt32();
                int textureSize = reader.ReadInt32();
                int fileHash = reader.ReadInt32();
                if (magic != FogBodyCacheMagic || version != FogBodyCacheVersion || textureSize != size || fileHash != expectedHash)
                {
                    return false;
                }

                int pixelCount = size * size;
                int byteCount = pixelCount * 4;
                byte[] bytes = reader.ReadBytes(byteCount);
                if (bytes.Length != byteCount)
                {
                    return false;
                }

                Color[] pixels = new Color[pixelCount];
                for (int i = 0; i < pixelCount; i++)
                {
                    int b = i * 4;
                    pixels[i] = new Color(bytes[b], bytes[b + 1], bytes[b + 2], bytes[b + 3]);
                }

                texture = new Texture2D(graphicsDevice, size, size, false, SurfaceFormat.Color);
                texture.SetData(pixels);
                return true;
            }
            catch
            {
                texture?.Dispose();
                texture = null;
                return false;
            }
        }

        private static bool TryWriteFogBodyCacheFile(
            string filePath,
            int size,
            FogVisualParameters parameters,
            string progressLabel = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using TimedProgressReporter progress = new TimedProgressReporter(
                    string.IsNullOrWhiteSpace(progressLabel) ? "Body cache" : progressLabel,
                    3,
                    TimeSpan.FromSeconds(10));

                int hash = ComputeFogBodyHash(parameters);
                progress.Update(1);
                Color[] pixels = BuildFogBodyPixels(size, parameters);
                progress.Update(2);

                using FileStream stream = File.Create(filePath);
                using BinaryWriter writer = new BinaryWriter(stream);

                writer.Write(FogBodyCacheMagic);
                writer.Write(FogBodyCacheVersion);
                writer.Write(size);
                writer.Write(hash);

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    writer.Write(c.R);
                    writer.Write(c.G);
                    writer.Write(c.B);
                    writer.Write(c.A);
                }

                writer.Flush();
                progress.Complete();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCopyCacheFile(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                return false;
            }

            if (!File.Exists(sourcePath))
            {
                return false;
            }

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                string directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureFrontierRuntimeTextures(GraphicsDevice graphicsDevice, int textureSize)
        {
            int size = Math.Max(256, textureSize);
            bool visionInvalid = _visionMaskTexture == null || _visionMaskTexture.IsDisposed ||
                _visionMaskTexture.Width != size || _visionMaskTexture.Height != size;
            if (!visionInvalid && _hiddenEdgeTexture == null && _revealLiftTexture == null)
            {
                return;
            }

            lock (FrontierComputeSync)
            {
                _visionMaskTexture?.Dispose();
                _hiddenEdgeTexture?.Dispose();
                _revealLiftTexture?.Dispose();

                _visionMaskTexture = new Texture2D(graphicsDevice, size, size, false, SurfaceFormat.Alpha8);
                _hiddenEdgeTexture = null;
                _revealLiftTexture = null;

                int pixelCount = size * size;
                if (_visionAlphaBuffer == null || _visionAlphaBuffer.Length != pixelCount)
                {
                    _visionAlphaBuffer = new byte[pixelCount];
                }

                if (_hiddenAlphaBuffer == null || _hiddenAlphaBuffer.Length != pixelCount)
                {
                    _hiddenAlphaBuffer = new byte[pixelCount];
                }

                _revealAlphaBuffer = null;
            }

            _hasFrontierSample = false;
        }

        private static void DisposeFrontierRuntimeTextures()
        {
            lock (FrontierComputeSync)
            {
                DisposeTextureArray(_visionDirectionTextures);
                DisposeTextureArray(_hiddenDirectionTextures);
                DisposeTextureArray(_revealDirectionTextures);
                _visionDirectionTextures = null;
                _hiddenDirectionTextures = null;
                _revealDirectionTextures = null;
                _visionMaskTexture?.Dispose();
                _hiddenEdgeTexture?.Dispose();
                _revealLiftTexture?.Dispose();
                _visionMaskTexture = null;
                _hiddenEdgeTexture = null;
                _revealLiftTexture = null;
                _diskReadBuffer = null;
                _zeroAlphaBuffer = null;
                _visionAlphaBuffer = null;
                _hiddenAlphaBuffer = null;
                _revealAlphaBuffer = null;
                _hasFrontierSample = false;
                _frontierSampleCenter = Vector2.Zero;
                _frontierSampleRadius = 0f;
                _frontierAngleCache = null;
                _frontierGeometryCacheSize = 0;
            }
        }

        private static void UploadDirectionTextures(int directionBin)
        {
            if (_frontierCache == null ||
                _visionDirectionTextures == null ||
                _hiddenDirectionTextures == null)
            {
                return;
            }

            if (_frontierCache.IsDiskBacked)
            {
                UploadDirectionTexturesFromDisk(_frontierCache, directionBin);
                return;
            }

            if (_frontierCache.Variants == null || _frontierCache.Variants.Length == 0)
            {
                UploadDirectionTexturesFallback(directionBin);
                return;
            }

            int directionOffset = directionBin * FrontierPhaseCount;
            for (int phaseIndex = 0; phaseIndex < FrontierPhaseCount; phaseIndex++)
            {
                int variantIndex = directionOffset + phaseIndex;
                if (variantIndex < 0 || variantIndex >= _frontierCache.Variants.Length)
                {
                    continue;
                }

                FrontierVariant variant = _frontierCache.Variants[variantIndex];
                if (variant == null)
                {
                    continue;
                }

                UploadAlphaMask(_visionDirectionTextures[phaseIndex], variant.VisionMaskAlpha);
                UploadAlphaMask(_hiddenDirectionTextures[phaseIndex], variant.HiddenEdgeAlpha);
                if (_revealDirectionTextures != null && phaseIndex < _revealDirectionTextures.Length)
                {
                    UploadAlphaMask(_revealDirectionTextures[phaseIndex], variant.RevealLiftAlpha);
                }
            }
        }

        private static void UploadDirectionTexturesFallback(int directionBin)
        {
            if (_visionDirectionTextures == null || _hiddenDirectionTextures == null)
            {
                return;
            }

            bool hasReveal = _revealDirectionTextures != null && _revealDirectionTextures.Length == FrontierPhaseCount;
            int size = _visionDirectionTextures[0]?.Width ?? FrontierTextureSize;
            int pixelCount = size * size;
            byte[] vision = new byte[pixelCount];
            byte[] hidden = new byte[pixelCount];
            byte[] reveal = hasReveal ? new byte[pixelCount] : null;

            float center = (size - 1) * 0.5f;
            float invRadius = FrontierDomainExtent / MathF.Max(1f, size * 0.5f);
            float nearWidth = MathF.Max(0.004f, _visualParameters.NearWidth);
            float midWidth = MathF.Max(nearWidth, _visualParameters.MidWidth);
            float wideWidth = MathF.Max(midWidth, _visualParameters.WideWidth);
            float boundarySoftness = MathF.Max(0.001f, _visualParameters.BoundarySoftness);
            float dirPhase = (MathHelper.TwoPi * PositiveModulo(directionBin, FrontierDirectionBins)) / FrontierDirectionBins;
            bool hasRevealWeights = _visualParameters.RevealNearWeight > 0f || _visualParameters.RevealMidWeight > 0f;

            for (int phaseIndex = 0; phaseIndex < FrontierPhaseCount; phaseIndex++)
            {
                Array.Clear(vision, 0, pixelCount);
                Array.Clear(hidden, 0, pixelCount);
                if (hasReveal && reveal != null)
                {
                    Array.Clear(reveal, 0, pixelCount);
                }

                float phaseT = phaseIndex / (float)Math.Max(1, FrontierPhaseCount);
                float phaseOffset = phaseT * MathHelper.TwoPi;

                for (int y = 0; y < size; y++)
                {
                    float py = (y - center) * invRadius;
                    int rowStart = y * size;

                    for (int x = 0; x < size; x++)
                    {
                        float px = (x - center) * invRadius;
                        float radial = MathF.Sqrt((px * px) + (py * py));
                        float angle = MathF.Atan2(py, px);

                        float perturb =
                            (MathF.Sin((angle * 2.0f) + dirPhase + phaseOffset) * 0.055f) +
                            (MathF.Sin((angle * 5.0f) + (dirPhase * 0.6f) + (phaseOffset * 1.8f)) * 0.022f);

                        float d = (radial - 1f) + perturb;
                        float absD = MathF.Abs(d);
                        float hiddenMask = d >= 0f ? 1f : 0f;
                        float visibleMask = 1f - hiddenMask;

                        float bandNear = EvaluateBand(absD, nearWidth);
                        float bandMid = EvaluateBand(absD, midWidth);
                        float bandWide = EvaluateBand(absD, wideWidth);

                        float cutoutAlpha = 1f - SmoothStep(-boundarySoftness, boundarySoftness, d);
                        float edgeDarkStrength = hiddenMask *
                            ((bandNear * _visualParameters.NearWeight) +
                             (bandMid * _visualParameters.MidWeight) +
                             (bandWide * _visualParameters.WideWeight));

                        float revealLiftStrength = hasRevealWeights
                            ? (visibleMask *
                               ((bandNear * _visualParameters.RevealNearWeight) +
                                (bandMid * _visualParameters.RevealMidWeight)))
                            : 0f;

                        float radialFrameFade = 1f - SmoothStep(FrontierDomainExtent - 0.18f, FrontierDomainExtent, radial);
                        cutoutAlpha *= radialFrameFade;
                        edgeDarkStrength *= radialFrameFade;
                        revealLiftStrength *= radialFrameFade;

                        int idx = rowStart + x;
                        vision[idx] = ToByte(cutoutAlpha);
                        hidden[idx] = ToByte(edgeDarkStrength);
                        if (hasReveal && reveal != null)
                        {
                            reveal[idx] = ToByte(revealLiftStrength);
                        }
                    }
                }

                UploadAlphaMask(_visionDirectionTextures[phaseIndex], vision);
                UploadAlphaMask(_hiddenDirectionTextures[phaseIndex], hidden);
                if (hasReveal && _revealDirectionTextures != null)
                {
                    UploadAlphaMask(_revealDirectionTextures[phaseIndex], reveal);
                }
            }
        }

        private static void UploadDirectionTexturesFromDisk(FrontierCache cache, int directionBin)
        {
            if (cache == null || !cache.IsDiskBacked || string.IsNullOrWhiteSpace(cache.FilePath) || !File.Exists(cache.FilePath))
            {
                return;
            }

            int safeDirection = PositiveModulo(directionBin, FrontierDirectionBins);
            int layerByteCount = cache.LayerByteCount;
            if (layerByteCount <= 0)
            {
                return;
            }

            EnsureDiskReadBuffer(layerByteCount);
            EnsureZeroAlphaBuffer(layerByteCount);

            try
            {
                using FileStream stream = File.OpenRead(cache.FilePath);
                using BinaryReader reader = new BinaryReader(stream);

                long directionStart = cache.DataOffsetBytes + ((long)safeDirection * cache.DirectionBlockByteCount);
                for (int phaseIndex = 0; phaseIndex < FrontierPhaseCount; phaseIndex++)
                {
                    long phaseStart = directionStart + ((long)phaseIndex * cache.VariantByteCount);
                    stream.Position = phaseStart;

                    ReadIntoBuffer(reader, _diskReadBuffer, layerByteCount);
                    UploadAlphaMask(_visionDirectionTextures[phaseIndex], _diskReadBuffer);

                    ReadIntoBuffer(reader, _diskReadBuffer, layerByteCount);
                    UploadAlphaMask(_hiddenDirectionTextures[phaseIndex], _diskReadBuffer);

                    if (cache.LayerCount >= 3 && _revealDirectionTextures != null && phaseIndex < _revealDirectionTextures.Length)
                    {
                        ReadIntoBuffer(reader, _diskReadBuffer, layerByteCount);
                        UploadAlphaMask(_revealDirectionTextures[phaseIndex], _diskReadBuffer);
                    }
                    else if (_revealDirectionTextures != null && phaseIndex < _revealDirectionTextures.Length)
                    {
                        UploadAlphaMask(_revealDirectionTextures[phaseIndex], _zeroAlphaBuffer);
                    }
                }
            }
            catch
            {
                // Keep current textures if disk streaming fails.
            }
        }

        private static Texture2D ResolvePhaseTexture(Texture2D[] textureSet, int phaseIndex)
        {
            if (textureSet == null || textureSet.Length == 0)
            {
                return null;
            }

            int safeIndex = PositiveModulo(phaseIndex, textureSet.Length);
            return textureSet[safeIndex];
        }

        private static void DisposeTextureArray(Texture2D[] textures)
        {
            if (textures == null)
            {
                return;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                textures[i]?.Dispose();
            }
        }

        private static void UploadAlphaMask(Texture2D destinationTexture, byte[] alphaMask)
        {
            if (destinationTexture == null || destinationTexture.IsDisposed || alphaMask == null || alphaMask.Length == 0)
            {
                return;
            }

            destinationTexture.SetData(alphaMask);
        }

        private static void EnsureDiskReadBuffer(int size)
        {
            if (_diskReadBuffer == null || _diskReadBuffer.Length != size)
            {
                _diskReadBuffer = new byte[size];
            }
        }

        private static void EnsureZeroAlphaBuffer(int size)
        {
            if (_zeroAlphaBuffer == null || _zeroAlphaBuffer.Length != size)
            {
                _zeroAlphaBuffer = new byte[size];
            }
        }

        private static void ReadIntoBuffer(BinaryReader reader, byte[] buffer, int size)
        {
            int total = 0;
            while (total < size)
            {
                int read = reader.Read(buffer, total, size - total);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected EOF while reading FOW cache.");
                }

                total += read;
            }
        }

        private static void UpdateFrontierVariantSelection(Vector2 referenceCenter, float referenceRadius)
        {
            if (!_hasMotionAnchor)
            {
                _lastMotionCenter = referenceCenter;
                _hasMotionAnchor = true;
                _activeDirectionBin = 0;
                _activePhaseIndex = 0;
                _phaseFrameCursor = 0f;
                _lastFrameMoveDistanceWorld = 0f;
                return;
            }

            Vector2 delta = referenceCenter - _lastMotionCenter;
            float moveDistance = delta.Length();
            _lastFrameMoveDistanceWorld = moveDistance;
            if (moveDistance > 0.001f)
            {
                float angle = MathF.Atan2(delta.Y, delta.X);
                _activeDirectionBin = QuantizeDirectionBin(angle);

                float moveCentifoot = CentifootUnits.WorldToCentifoot(moveDistance);
                if (moveCentifoot > 0f)
                {
                    _phaseFrameCursor += moveCentifoot * FrontierFramesPerCentifoot;
                    int phaseFrame = (int)MathF.Floor(_phaseFrameCursor);
                    _activePhaseIndex = PositiveModulo(phaseFrame, FrontierPhaseCount);
                }
            }

            _lastMotionCenter = referenceCenter;
        }

        private static bool AreVisualResourcesReady()
        {
            return _fogRenderTarget != null && !_fogRenderTarget.IsDisposed &&
                _fogBodyTexture != null && !_fogBodyTexture.IsDisposed &&
                _visionMaskTexture != null && !_visionMaskTexture.IsDisposed;
        }

        private static float GetReferenceVisionRadius()
        {
            if (VisionSources.Count <= 0)
            {
                return MathF.Max(1f, PlayerSightRadius);
            }

            float total = 0f;
            for (int i = 0; i < VisionSources.Count; i++)
            {
                total += VisionSources[i].Radius;
            }

            return MathF.Max(1f, total / VisionSources.Count);
        }

        private static Vector2 GetReferenceVisionCenter()
        {
            if (VisionSources.Count <= 0)
            {
                return Vector2.Zero;
            }

            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < VisionSources.Count; i++)
            {
                sum += VisionSources[i].Position;
            }

            return sum / VisionSources.Count;
        }

        private static void RenderFogTarget(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Matrix cameraTransform, int width, int height)
        {
            graphicsDevice.SetRenderTarget(_fogRenderTarget);
            graphicsDevice.Viewport = new Viewport(0, 0, width, height);
            graphicsDevice.Clear(Color.Transparent);

            Rectangle destination = new Rectangle(0, 0, width, height);
            Rectangle source = BuildFogBodySourceRectangle(
                cameraTransform,
                width,
                height,
                _fogBodyTexture.Width,
                _fogBodyTexture.Height,
                _visualParameters.StableWorldScale * _visualParameters.DetailMaskScale);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            spriteBatch.Draw(_fogBodyTexture, destination, source, Color.White);
            spriteBatch.End();

            if (VisionSources.Count <= 0)
            {
                return;
            }

            float cameraScale = ExtractCameraScale(cameraTransform);
            Vector2 maskOrigin = new Vector2(_visionMaskTexture.Width * 0.5f, _visionMaskTexture.Height * 0.5f);

            spriteBatch.Begin(SpriteSortMode.Deferred, VisionCutoutBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            for (int i = 0; i < VisionSources.Count; i++)
            {
                DrawVisionTexture(spriteBatch, _visionMaskTexture, VisionSources[i], cameraTransform, cameraScale, maskOrigin, Color.White);
            }
            spriteBatch.End();
        }

        private static void RenderRevealLiftTarget(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Matrix cameraTransform, int width, int height)
        {
            graphicsDevice.SetRenderTarget(_revealLiftRenderTarget);
            graphicsDevice.Viewport = new Viewport(0, 0, width, height);
            graphicsDevice.Clear(Color.Transparent);

            if ((_visualParameters.RevealNearWeight <= 0f && _visualParameters.RevealMidWeight <= 0f) || VisionSources.Count <= 0)
            {
                return;
            }

            if (_revealLiftTexture == null || _revealLiftTexture.IsDisposed)
            {
                return;
            }

            float cameraScale = ExtractCameraScale(cameraTransform);
            Vector2 maskOrigin = new Vector2(_revealLiftTexture.Width * 0.5f, _revealLiftTexture.Height * 0.5f);
            Color liftTint = ToColor(_visualParameters.RevealLiftColor, 1f);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            for (int i = 0; i < VisionSources.Count; i++)
            {
                DrawVisionTexture(spriteBatch, _revealLiftTexture, VisionSources[i], cameraTransform, cameraScale, maskOrigin, liftTint);
            }
            spriteBatch.End();
        }

        private static void DrawVisionTexture(
            SpriteBatch spriteBatch,
            Texture2D texture,
            VisionSource source,
            Matrix cameraTransform,
            float cameraScale,
            Vector2 origin,
            Color tint)
        {
            if (texture == null || texture.IsDisposed)
            {
                return;
            }

            float scaledRadius = source.Radius * cameraScale;
            if (scaledRadius <= 0f)
            {
                return;
            }

            Vector2 screenPosition = Vector2.Transform(source.Position, cameraTransform);
            float drawScale = ((scaledRadius * 2f) * FrontierDomainExtent) / texture.Width;

            spriteBatch.Draw(
                texture,
                screenPosition,
                null,
                tint,
                0f,
                origin,
                drawScale,
                SpriteEffects.None,
                0f);
        }
        private static Rectangle BuildFogBodySourceRectangle(
            Matrix cameraTransform,
            int viewportWidth,
            int viewportHeight,
            int textureWidth,
            int textureHeight,
            float worldToUvScale)
        {
            float cameraScale = MathF.Max(0.0001f, ExtractCameraScale(cameraTransform));
            float texelsPerWorldX = textureWidth * MathF.Max(0.00001f, worldToUvScale);
            float texelsPerWorldY = textureHeight * MathF.Max(0.00001f, worldToUvScale);

            float worldPerScreenPixel = 1f / cameraScale;
            int sourceWidth = Math.Max(1, (int)MathF.Ceiling(viewportWidth * worldPerScreenPixel * texelsPerWorldX));
            int sourceHeight = Math.Max(1, (int)MathF.Ceiling(viewportHeight * worldPerScreenPixel * texelsPerWorldY));

            float worldOriginX = -cameraTransform.M41 / cameraScale;
            float worldOriginY = -cameraTransform.M42 / cameraScale;

            int sourceX = PositiveModulo((int)MathF.Floor(worldOriginX * texelsPerWorldX), Math.Max(1, textureWidth));
            int sourceY = PositiveModulo((int)MathF.Floor(worldOriginY * texelsPerWorldY), Math.Max(1, textureHeight));

            return new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight);
        }

        private static float ExtractCameraScale(Matrix matrix)
        {
            Vector2 xBasis = new Vector2(matrix.M11, matrix.M12);
            Vector2 yBasis = new Vector2(matrix.M21, matrix.M22);
            float scaleX = xBasis.Length();
            float scaleY = yBasis.Length();
            float scale = (scaleX + scaleY) * 0.5f;
            return scale > 0f ? scale : 1f;
        }

        private static Texture2D BuildFogBodyTexture(GraphicsDevice graphicsDevice, int size, FogVisualParameters parameters)
        {
            int textureSize = Math.Max(256, size);
            Color[] pixels = BuildFogBodyPixels(textureSize, parameters);
            Texture2D texture = new Texture2D(graphicsDevice, textureSize, textureSize, false, SurfaceFormat.Color);
            texture.SetData(pixels);
            return texture;
        }

        private static Color[] BuildFogBodyPixels(int textureSize, FogVisualParameters parameters)
        {
            int pixelCount = textureSize * textureSize;
            Color[] pixels = new Color[pixelCount];
            Array.Fill(pixels, Color.Black);
            return pixels;
        }

        private static float[] BuildDetailMask(FogVisualParameters parameters, int textureSize)
        {
            float[] mask = new float[textureSize * textureSize];
            Random random = new Random(parameters.DetailSeed);
            float speckWidthMin = MathF.Max(1f, parameters.MarkSizeMin);
            float speckWidthMax = MathF.Max(speckWidthMin + 0.1f, parameters.MarkSizeMax);
            float speckHeightMin = 1f;
            float speckHeightMax = 2f;
            float dashMin = 5f;
            float dashMax = 10f;
            float dotRadiusMin = 0.9f;
            float dotRadiusMax = 1.8f;

            for (int i = 0; i < Math.Max(0, parameters.SpeckCount); i++)
            {
                float cx = NextFloat(random) * textureSize;
                float cy = NextFloat(random) * textureSize;
                float w = MathHelper.Lerp(speckWidthMin, speckWidthMax, NextFloat(random));
                float h = MathHelper.Lerp(speckHeightMin, speckHeightMax, NextFloat(random));
                float angle = (NextFloat(random) - 0.5f) * 2f * parameters.MarkAngleVariance;
                float intensity = MathHelper.Lerp(0.42f, 0.90f, NextFloat(random));
                RasterizeMark(mask, textureSize, cx, cy, w, h, angle, intensity, MarkType.Speck);
            }

            for (int i = 0; i < Math.Max(0, parameters.DotCount); i++)
            {
                float cx = NextFloat(random) * textureSize;
                float cy = NextFloat(random) * textureSize;
                float radius = MathHelper.Lerp(dotRadiusMin, dotRadiusMax, NextFloat(random));
                float intensity = MathHelper.Lerp(0.35f, 0.75f, NextFloat(random));
                RasterizeMark(mask, textureSize, cx, cy, radius * 2f, radius * 2f, 0f, intensity, MarkType.Dot);
            }

            for (int i = 0; i < Math.Max(0, parameters.DashCount); i++)
            {
                float cx = NextFloat(random) * textureSize;
                float cy = NextFloat(random) * textureSize;
                float length = MathHelper.Lerp(dashMin, dashMax, NextFloat(random));
                float width = 1f;
                float angle = (NextFloat(random) - 0.5f) * 2f * parameters.MarkAngleVariance;
                float intensity = MathHelper.Lerp(0.45f, 1.00f, NextFloat(random));
                RasterizeMark(mask, textureSize, cx, cy, length, width, angle, intensity, MarkType.Dash);
            }

            if (parameters.MarkBlur > 0f)
            {
                ApplySeparableBlur(mask, textureSize, parameters.MarkBlur);
            }

            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = Saturate(mask[i] * 0.85f);
            }

            return mask;
        }
        private static void RasterizeMark(
            float[] mask,
            int textureSize,
            float centerX,
            float centerY,
            float width,
            float height,
            float angle,
            float intensity,
            MarkType type)
        {
            float halfW = MathF.Max(0.5f, width * 0.5f);
            float halfH = MathF.Max(0.5f, height * 0.5f);
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            int minX = (int)MathF.Floor(centerX - halfW - 2f);
            int maxX = (int)MathF.Ceiling(centerX + halfW + 2f);
            int minY = (int)MathF.Floor(centerY - halfH - 2f);
            int maxY = (int)MathF.Ceiling(centerY + halfH + 2f);

            float invRx = 1f / MathF.Max(0.1f, halfW);
            float invRy = 1f / MathF.Max(0.1f, halfH);

            for (int y = minY; y <= maxY; y++)
            {
                int wrappedY = PositiveModulo(y, textureSize);
                int rowStart = wrappedY * textureSize;
                float dy = y - centerY;

                for (int x = minX; x <= maxX; x++)
                {
                    int wrappedX = PositiveModulo(x, textureSize);
                    float dx = x - centerX;

                    float localX = (dx * cos) + (dy * sin);
                    float localY = (-dx * sin) + (dy * cos);

                    float nx = localX * invRx;
                    float ny = localY * invRy;
                    float d = (nx * nx) + (ny * ny);

                    if (type == MarkType.Dash)
                    {
                        float dashLen = MathF.Abs(localX) * invRx;
                        float dashWidth = MathF.Abs(localY) * invRy;
                        d = (dashLen * dashLen * 0.52f) + (dashWidth * dashWidth * 1.18f);
                    }

                    if (d > 1.55f)
                    {
                        continue;
                    }

                    float coverage = 1f - SmoothStep(0.82f, 1.22f, d);
                    if (coverage <= 0f)
                    {
                        continue;
                    }

                    int index = rowStart + wrappedX;
                    float value = intensity * coverage;
                    if (value > mask[index])
                    {
                        mask[index] = value;
                    }
                }
            }
        }

        private static void ApplySeparableBlur(float[] values, int size, float radius)
        {
            int kernelRadius = Math.Max(1, (int)MathF.Ceiling(radius));
            int kernelLength = (kernelRadius * 2) + 1;
            float sigma = MathF.Max(0.35f, radius);

            float[] kernel = new float[kernelLength];
            float kernelSum = 0f;
            for (int i = -kernelRadius; i <= kernelRadius; i++)
            {
                float weight = MathF.Exp(-(i * i) / (2f * sigma * sigma));
                kernel[i + kernelRadius] = weight;
                kernelSum += weight;
            }

            if (kernelSum <= 0f)
            {
                return;
            }

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= kernelSum;
            }

            float[] temp = new float[values.Length];

            for (int y = 0; y < size; y++)
            {
                int rowStart = y * size;
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int k = -kernelRadius; k <= kernelRadius; k++)
                    {
                        int sx = PositiveModulo(x + k, size);
                        sum += values[rowStart + sx] * kernel[k + kernelRadius];
                    }

                    temp[rowStart + x] = sum;
                }
            }

            for (int y = 0; y < size; y++)
            {
                int rowStart = y * size;
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int k = -kernelRadius; k <= kernelRadius; k++)
                    {
                        int sy = PositiveModulo(y + k, size);
                        sum += temp[(sy * size) + x] * kernel[k + kernelRadius];
                    }

                    values[rowStart + x] = sum;
                }
            }
        }

        private static void EnsureFrontierGeometryCache(int textureSize)
        {
            if (_frontierGeometryCacheSize == textureSize &&
                _frontierLocalXCache != null &&
                _frontierLocalYCache != null &&
                _frontierRadialCache != null)
            {
                return;
            }

            int size = Math.Max(256, textureSize);
            int pixelCount = size * size;
            _frontierLocalXCache = new float[pixelCount];
            _frontierLocalYCache = new float[pixelCount];
            _frontierRadialCache = new float[pixelCount];
            _frontierAngleCache = new float[pixelCount];
            _frontierGeometryCacheSize = size;

            float center = (size - 1) * 0.5f;
            float invRadius = FrontierDomainExtent / MathF.Max(1f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                float py = (y - center) * invRadius;
                int rowStart = y * size;
                for (int x = 0; x < size; x++)
                {
                    float px = (x - center) * invRadius;
                    int idx = rowStart + x;
                    _frontierLocalXCache[idx] = px;
                    _frontierLocalYCache[idx] = py;
                    _frontierRadialCache[idx] = MathF.Sqrt((px * px) + (py * py));
                    _frontierAngleCache[idx] = MathF.Atan2(py, px);
                }
            }
        }

        private static void EnsureEdgeNoiseTables(int detailSeed)
        {
            if (_edgeNoiseSeedHash == detailSeed &&
                _edgeNoiseWarpA != null &&
                _edgeNoiseWarpB != null &&
                _edgeNoiseMacro1 != null &&
                _edgeNoiseMacro2 != null &&
                _edgeNoiseMacro3 != null)
            {
                return;
            }

            int pixelCount = EdgeNoiseTableSize * EdgeNoiseTableSize;
            _edgeNoiseWarpA = new float[pixelCount];
            _edgeNoiseWarpB = new float[pixelCount];
            _edgeNoiseMacro1 = new float[pixelCount];
            _edgeNoiseMacro2 = new float[pixelCount];
            _edgeNoiseMacro3 = new float[pixelCount];

            int seedWarpA = detailSeed + 101;
            int seedWarpB = detailSeed + 131;
            int seedMacro1 = detailSeed + 211;
            int seedMacro2 = detailSeed + 307;
            int seedMacro3 = detailSeed + 401;
            for (int y = 0; y < EdgeNoiseTableSize; y++)
            {
                int rowStart = y * EdgeNoiseTableSize;
                for (int x = 0; x < EdgeNoiseTableSize; x++)
                {
                    int idx = rowStart + x;
                    _edgeNoiseWarpA[idx] = Hash2D(x, y, seedWarpA);
                    _edgeNoiseWarpB[idx] = Hash2D(x, y, seedWarpB);
                    _edgeNoiseMacro1[idx] = Hash2D(x, y, seedMacro1);
                    _edgeNoiseMacro2[idx] = Hash2D(x, y, seedMacro2);
                    _edgeNoiseMacro3[idx] = Hash2D(x, y, seedMacro3);
                }
            }

            _edgeNoiseSeedHash = detailSeed;
        }

        private static float SampleEdgeNoise(float[] table, float x, float y)
        {
            int x0 = (int)MathF.Floor(x);
            int y0 = (int)MathF.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float tx = x - x0;
            float ty = y - y0;
            float sx = SmoothStep01(tx);
            float sy = SmoothStep01(ty);

            int wx0 = x0 & EdgeNoiseTableMask;
            int wy0 = y0 & EdgeNoiseTableMask;
            int wx1 = x1 & EdgeNoiseTableMask;
            int wy1 = y1 & EdgeNoiseTableMask;

            float n00 = table[(wy0 * EdgeNoiseTableSize) + wx0];
            float n10 = table[(wy0 * EdgeNoiseTableSize) + wx1];
            float n01 = table[(wy1 * EdgeNoiseTableSize) + wx0];
            float n11 = table[(wy1 * EdgeNoiseTableSize) + wx1];

            float nx0 = MathHelper.Lerp(n00, n10, sx);
            float nx1 = MathHelper.Lerp(n01, n11, sx);
            return MathHelper.Lerp(nx0, nx1, sy);
        }

        private static void BuildFrontierTexturesProcedural(
            int textureSize,
            FogVisualParameters parameters,
            float referenceRadius,
            Vector2 referenceCenter,
            float animationPhase,
            int directionBin,
            out byte[] visionMaskAlpha,
            out byte[] hiddenEdgeAlpha,
            out byte[] revealLiftAlpha)
        {
            long buildStartTicks = Stopwatch.GetTimestamp();
            lock (FrontierComputeSync)
            {
                int size = Math.Max(256, textureSize);
                int pixelCount = size * size;
                EnsureFrontierGeometryCache(size);
                visionMaskAlpha = _visionAlphaBuffer != null && _visionAlphaBuffer.Length == pixelCount
                    ? _visionAlphaBuffer
                    : new byte[pixelCount];
                hiddenEdgeAlpha = _hiddenAlphaBuffer != null && _hiddenAlphaBuffer.Length == pixelCount
                    ? _hiddenAlphaBuffer
                    : new byte[pixelCount];
                revealLiftAlpha = _revealAlphaBuffer != null && _revealAlphaBuffer.Length == pixelCount
                    ? _revealAlphaBuffer
                    : new byte[pixelCount];

                float refRadiusSafe = MathF.Max(1f, referenceRadius);

                for (int index = 0; index < pixelCount; index++)
                {
                    float radial = _frontierRadialCache[index];
                    if (radial >= FrontierDomainExtent)
                    {
                        visionMaskAlpha[index] = 0;
                        hiddenEdgeAlpha[index] = 0;
                        revealLiftAlpha[index] = 0;
                        continue;
                    }

                    float baseDistanceWorld = (radial - 1f) * refRadiusSafe;
                    float d = baseDistanceWorld;

                    // Flat binary frontier edge: no smoothing/no animation/no textured border.
                    float cutoutAlpha = d <= 0f ? 1f : 0f;
                    visionMaskAlpha[index] = ToByte(cutoutAlpha);
                    hiddenEdgeAlpha[index] = 0;
                    revealLiftAlpha[index] = 0;
                }
            }

            double elapsedMs = (Stopwatch.GetTimestamp() - buildStartTicks) * 1000.0 / Stopwatch.Frequency;
            _frontierBuildMillisecondsLast = (float)elapsedMs;
            _frontierBuildMillisecondsAvg = _frontierBuildMillisecondsAvg <= 0f
                ? _frontierBuildMillisecondsLast
                : MathHelper.Lerp(_frontierBuildMillisecondsAvg, _frontierBuildMillisecondsLast, 0.12f);
        }

        private static void BuildFrontierTextures(
            int textureSize,
            FogVisualParameters parameters,
            float referenceRadius,
            int directionBin,
            int phaseIndex,
            out byte[] visionMaskAlpha,
            out byte[] hiddenEdgeAlpha,
            out byte[] revealLiftAlpha)
        {
            EnsureEdgeNoiseTables(parameters.DetailSeed);
            int size = Math.Max(256, textureSize);
            visionMaskAlpha = new byte[size * size];
            hiddenEdgeAlpha = new byte[size * size];
            revealLiftAlpha = new byte[size * size];

            float center = (size - 1) * 0.5f;
            float invRadius = FrontierDomainExtent / MathF.Max(1f, size * 0.5f);
            float refRadiusSafe = MathF.Max(1f, referenceRadius);
            float nearWidthWorld = MathF.Max(0.25f, parameters.NearWidth * refRadiusSafe);
            float midWidthWorld = MathF.Max(nearWidthWorld, parameters.MidWidth * refRadiusSafe);
            float wideWidthWorld = MathF.Max(midWidthWorld, parameters.WideWidth * refRadiusSafe);
            float boundarySoftnessWorld = MathF.Max(0.1f, parameters.BoundarySoftness * refRadiusSafe);
            float directionAngle = (MathHelper.TwoPi * directionBin) / FrontierDirectionBins;
            Vector2 directionUnit = new Vector2(MathF.Cos(directionAngle), MathF.Sin(directionAngle));
            float phaseT = phaseIndex / (float)Math.Max(1, FrontierPhaseCount);

            for (int y = 0; y < size; y++)
            {
                float py = (y - center) * invRadius;
                int rowStart = y * size;

                for (int x = 0; x < size; x++)
                {
                    float px = (x - center) * invRadius;
                    Vector2 p = new Vector2(px, py);
                    float radial = p.Length();

                    float baseDistanceWorld = (radial - 1f) * refRadiusSafe;
                    float perturbed = baseDistanceWorld + EvaluateEdgeOffsetWorld(p, directionUnit, phaseT, refRadiusSafe, parameters);
                    float smoothed = perturbed;

                    if (parameters.FieldSmoothing > 0f)
                    {
                        const float smoothStep = 0.015f;
                        float n1 = ((p + new Vector2(smoothStep, 0f)).Length() - 1f) * refRadiusSafe +
                            EvaluateEdgeOffsetWorld(p + new Vector2(smoothStep, 0f), directionUnit, phaseT, refRadiusSafe, parameters);
                        float n2 = ((p + new Vector2(-smoothStep, 0f)).Length() - 1f) * refRadiusSafe +
                            EvaluateEdgeOffsetWorld(p + new Vector2(-smoothStep, 0f), directionUnit, phaseT, refRadiusSafe, parameters);
                        float n3 = ((p + new Vector2(0f, smoothStep)).Length() - 1f) * refRadiusSafe +
                            EvaluateEdgeOffsetWorld(p + new Vector2(0f, smoothStep), directionUnit, phaseT, refRadiusSafe, parameters);
                        float n4 = ((p + new Vector2(0f, -smoothStep)).Length() - 1f) * refRadiusSafe +
                            EvaluateEdgeOffsetWorld(p + new Vector2(0f, -smoothStep), directionUnit, phaseT, refRadiusSafe, parameters);
                        float avg = (perturbed + n1 + n2 + n3 + n4) / 5f;
                        smoothed = MathHelper.Lerp(perturbed, avg, parameters.FieldSmoothing);
                    }

                    float absD = MathF.Abs(smoothed);
                    float hiddenMaskValue = smoothed >= 0f ? 1f : 0f;
                    float visibleMaskValue = 1f - hiddenMaskValue;

                    float bandNear = EvaluateBand(absD, nearWidthWorld);
                    float bandMid = EvaluateBand(absD, midWidthWorld);
                    float bandWide = EvaluateBand(absD, wideWidthWorld);

                    float cutoutAlpha = 1f - SmoothStep(-boundarySoftnessWorld, boundarySoftnessWorld, smoothed);

                    float edgeDarkStrength = hiddenMaskValue *
                        ((bandNear * parameters.NearWeight) +
                        (bandMid * parameters.MidWeight) +
                        (bandWide * parameters.WideWeight));

                    float revealLiftStrength = visibleMaskValue *
                        ((bandNear * parameters.RevealNearWeight) +
                        (bandMid * parameters.RevealMidWeight));

                    // Force masks to fade out before square corners to prevent framed-square artifacts.
                    float radialFrameFade = 1f - SmoothStep(FrontierDomainExtent - 0.18f, FrontierDomainExtent, radial);
                    cutoutAlpha *= radialFrameFade;
                    edgeDarkStrength *= radialFrameFade;
                    revealLiftStrength *= radialFrameFade;

                    int index = rowStart + x;
                    byte cutoutByte = ToByte(cutoutAlpha);
                    byte darkByte = ToByte(edgeDarkStrength);
                    byte liftByte = ToByte(revealLiftStrength);

                    visionMaskAlpha[index] = cutoutByte;
                    hiddenEdgeAlpha[index] = darkByte;
                    revealLiftAlpha[index] = liftByte;
                }
            }
        }

        private static float EvaluateEdgeOffsetWorld(
            float worldX,
            float worldY,
            float edgeDomainScale,
            float referenceRadius,
            FogVisualParameters parameters)
        {
            // Keep edge noise world-anchored and low-frequency so the frontier evolves with movement
            // without UV swimming or high-frequency chatter.
            float baseX = worldX * edgeDomainScale;
            float baseY = worldY * edgeDomainScale;

            float warpAX = (baseX * parameters.WarpScaleA) + 13.37f;
            float warpAY = (baseY * parameters.WarpScaleA) + 5.11f;
            float warpBX = (baseX * parameters.WarpScaleB) + 7.73f;
            float warpBY = (baseY * parameters.WarpScaleB) + 19.19f;
            float warpX = SampleEdgeNoise(_edgeNoiseWarpA, warpAX, warpAY);
            float warpY = SampleEdgeNoise(_edgeNoiseWarpB, warpBX, warpBY);

            float warpedX = baseX + ((warpX - 0.5f) * parameters.WarpAmp);
            float warpedY = baseY + ((warpY - 0.5f) * parameters.WarpAmp);
            float macro1 = SampleEdgeNoise(_edgeNoiseMacro1, warpedX * parameters.Macro1Scale, warpedY * parameters.Macro1Scale);
            float macro2 = SampleEdgeNoise(_edgeNoiseMacro2, baseX * parameters.Macro2Scale, baseY * parameters.Macro2Scale);
            float macro3 = SampleEdgeNoise(_edgeNoiseMacro3, baseX * parameters.Macro3Scale, baseY * parameters.Macro3Scale);

            return
                ((macro1 - 0.5f) * (parameters.Amp1 * referenceRadius)) +
                ((macro2 - 0.5f) * (parameters.Amp2 * referenceRadius)) +
                ((macro3 - 0.5f) * (parameters.Amp3 * referenceRadius));
        }

        private static float EvaluateEdgeOffsetWorld(
            Vector2 worldPos,
            float referenceRadius,
            FogVisualParameters parameters)
        {
            if (_edgeNoiseWarpA == null || _edgeNoiseSeedHash != parameters.DetailSeed)
            {
                EnsureEdgeNoiseTables(parameters.DetailSeed);
            }

            float edgeDomainScale = parameters.StableWorldScale * 64f;
            return EvaluateEdgeOffsetWorld(worldPos.X, worldPos.Y, edgeDomainScale, referenceRadius, parameters);
        }

        private static float EvaluateEdgeOffsetWorld(
            Vector2 normalizedLocalPos,
            Vector2 directionUnit,
            float phaseT,
            float referenceRadius,
            FogVisualParameters parameters)
        {
            Vector2 phaseOffsetWorld = directionUnit * (phaseT * referenceRadius * 0.02f);
            Vector2 worldPos = (normalizedLocalPos * referenceRadius) + phaseOffsetWorld;
            return EvaluateEdgeOffsetWorld(worldPos, referenceRadius, parameters);
        }
        private static float EvaluateBand(float absoluteDistance, float width)
        {
            if (width <= 0f)
            {
                return 0f;
            }

            return Saturate(1f - (absoluteDistance / width));
        }

        private static float EvaluateLayeredSquareOffsetWorld(
            float localX,
            float localY,
            float angle,
            float worldX,
            float worldY,
            Vector2 referenceCenter,
            float referenceRadius,
            float animationPhase,
            int directionBin,
            int seed,
            int squareCount,
            float amplitudeWorld,
            float hardness)
        {
            float radiusSafe = MathF.Max(1f, referenceRadius);
            int baseSquares = Math.Max(16, squareCount);
            float hard01 = Saturate((hardness - 1f) / 9f);
            float feather = MathHelper.Lerp(0.34f, 0.16f, hard01);
            float dirAngle = (MathHelper.TwoPi * PositiveModulo(directionBin, FrontierDirectionBins)) / FrontierDirectionBins;
            float dirX = MathF.Cos(dirAngle);
            float dirY = MathF.Sin(dirAngle);
            float positionPhase = (referenceCenter.X * 0.0019f) + (referenceCenter.Y * -0.0013f);
            float localDirectionBias = (localX * dirX) + (localY * dirY);

            float totalOffset = 0f;
            float maxAccumulated = MathF.Max(0.0001f, amplitudeWorld);
            for (int layer = 0; layer < 3; layer++)
            {
                float layerT = layer / 2f;
                int layerSquareCount = Math.Max(12, (int)MathF.Round(baseSquares * MathHelper.Lerp(0.72f, 1.24f, layerT)));
                float angularFrequency = layerSquareCount / MathHelper.TwoPi;
                float layerSpeed = MathHelper.Lerp(0.55f, 1.06f, layerT);
                float layerAmplitude = amplitudeWorld * MathHelper.Lerp(0.45f, 0.82f, layerT);
                maxAccumulated += layerAmplitude;

                float layerPhase =
                    (animationPhase * layerSpeed) +
                    (positionPhase * (0.55f + (layer * 0.22f))) +
                    (localDirectionBias * (0.82f + (layer * 0.18f))) +
                    (layer * 0.73f);

                float cellCoord = (angle * angularFrequency) + layerPhase;
                int centerCell = (int)MathF.Floor(cellCoord);

                float layerMask = 0f;
                for (int neighbor = -1; neighbor <= 1; neighbor++)
                {
                    int cellIndex = centerCell + neighbor;
                    float jitterA = Hash2D(cellIndex, layer + 1, seed ^ unchecked((int)0x9E3779B9));
                    float jitterB = Hash2D(cellIndex, layer + 19, seed ^ unchecked((int)0x7F4A7C15));
                    float jitterC = Hash2D(cellIndex, layer + 43, seed ^ unchecked((int)0x85EBCA6B));

                    float centerAngle = ((cellIndex + 0.5f) / layerSquareCount) * MathHelper.TwoPi;
                    centerAngle += (jitterA - 0.5f) * (MathHelper.TwoPi / layerSquareCount) * 0.38f;
                    float deltaAngle = WrapAngleRadians(angle - centerAngle);
                    float tangentWorld = deltaAngle * radiusSafe;

                    float pulse =
                        0.5f + (0.5f * MathF.Sin(
                            (animationPhase * MathHelper.Lerp(2.2f, 3.5f, layerT)) +
                            (jitterB * MathHelper.TwoPi) +
                            ((worldX * dirX + worldY * dirY) * 0.0046f) +
                            (layer * 1.31f)));

                    float squareCenterRadial =
                        (amplitudeWorld * 0.32f) +
                        (layerAmplitude * (0.42f + (0.58f * pulse)));

                    float radialWorld = ((MathF.Sqrt((localX * localX) + (localY * localY)) - 1f) * radiusSafe) - squareCenterRadial;

                    // Rotate tangent/radial frame so a corner tends to face inward toward the reveal center.
                    float u = (tangentWorld + radialWorld) * 0.70710677f;
                    float v = (tangentWorld - radialWorld) * 0.70710677f;

                    float halfExtentWorld = radiusSafe * (MathHelper.TwoPi / layerSquareCount) * MathHelper.Lerp(0.54f, 0.78f, jitterC);
                    halfExtentWorld = MathF.Max(0.8f, halfExtentWorld);
                    float squareMetric = MathF.Max(MathF.Abs(u), MathF.Abs(v)) / halfExtentWorld;
                    float squareCoverage = 1f - SmoothStep(1f - feather, 1f + feather, squareMetric);
                    if (squareCoverage > layerMask)
                    {
                        layerMask = squareCoverage;
                    }
                }

                totalOffset += layerMask * layerAmplitude;
            }

            return MathHelper.Clamp(totalOffset, 0f, maxAccumulated);
        }

        private static int ComputeFogBodyHash(FogVisualParameters parameters)
        {
            HashCode hash = new HashCode();
            hash.Add(parameters.BaseColor.PackedValue);
            hash.Add(parameters.BroadScale);
            hash.Add(parameters.BroadAmp.X);
            hash.Add(parameters.BroadAmp.Y);
            hash.Add(parameters.BroadAmp.Z);
            hash.Add(parameters.SplotchDarkening.X);
            hash.Add(parameters.SplotchDarkening.Y);
            hash.Add(parameters.SplotchDarkening.Z);
            hash.Add(parameters.DetailSeed);
            hash.Add(parameters.SpeckCount);
            hash.Add(parameters.DotCount);
            hash.Add(parameters.DashCount);
            hash.Add(parameters.MarkSizeMin);
            hash.Add(parameters.MarkSizeMax);
            hash.Add(parameters.MarkBlur);
            hash.Add(parameters.MarkAngleVariance);
            return hash.ToHashCode();
        }

        private static int ComputeFrontierHash(FogVisualParameters parameters, int radiusKey)
        {
            HashCode hash = new HashCode();
            hash.Add(radiusKey);
            hash.Add(parameters.StableWorldScale);
            hash.Add(parameters.WarpScaleA);
            hash.Add(parameters.WarpScaleB);
            hash.Add(parameters.WarpAmp);
            hash.Add(parameters.Macro1Scale);
            hash.Add(parameters.Macro2Scale);
            hash.Add(parameters.Macro3Scale);
            hash.Add(parameters.Amp1);
            hash.Add(parameters.Amp2);
            hash.Add(parameters.Amp3);
            hash.Add(parameters.FieldSmoothing);
            hash.Add(parameters.NearWidth);
            hash.Add(parameters.MidWidth);
            hash.Add(parameters.WideWidth);
            hash.Add(parameters.NearWeight);
            hash.Add(parameters.MidWeight);
            hash.Add(parameters.WideWeight);
            hash.Add(parameters.RevealNearWeight);
            hash.Add(parameters.RevealMidWeight);
            hash.Add(parameters.BoundarySoftness);
            hash.Add(parameters.PolygonSides);
            hash.Add(parameters.PolygonRotation);
            hash.Add(parameters.PolygonNoiseBlend);
            hash.Add(parameters.PolygonToothCount);
            hash.Add(parameters.PolygonToothAmplitude);
            hash.Add(parameters.PolygonToothSharpness);
            hash.Add(parameters.DetailSeed);
            return hash.ToHashCode();
        }

        internal static bool HandleCommandLinePrecompute(string[] args)
        {
            // Pregeneration path intentionally disabled: FOW runs fully procedural at runtime.
            return false;
        }

        private static FogVisualParameters SanitizeVisualParameters(FogVisualParameters parameters)
        {
            FogVisualParameters defaults = FogVisualParameters.CreateDefaults();

            if (parameters.BaseColor.PackedValue == 0u)
            {
                parameters.BaseColor = defaults.BaseColor;
            }

            parameters.BaseColor = new Color(parameters.BaseColor.R, parameters.BaseColor.G, parameters.BaseColor.B, (byte)255);
            parameters.StableWorldScale = ClampPositiveFinite(parameters.StableWorldScale, defaults.StableWorldScale, 0.0001f, 0.4f);
            parameters.BroadScale = ClampPositiveFinite(parameters.BroadScale, defaults.BroadScale, 0.01f, 10f);
            parameters.BroadAmp = ClampVector3Finite(parameters.BroadAmp, defaults.BroadAmp, Vector3.Zero, new Vector3(0.2f, 0.2f, 0.2f));

            parameters.DetailMaskScale = ClampPositiveFinite(parameters.DetailMaskScale, defaults.DetailMaskScale, 0.1f, 20f);
            parameters.SplotchDarkening = ClampVector3Finite(parameters.SplotchDarkening, defaults.SplotchDarkening, Vector3.Zero, new Vector3(0.35f, 0.35f, 0.35f));
            parameters.DetailSeed = parameters.DetailSeed == 0 ? defaults.DetailSeed : parameters.DetailSeed;
            parameters.SpeckCount = ClampInt(parameters.SpeckCount, defaults.SpeckCount, 0, 4000);
            parameters.DotCount = ClampInt(parameters.DotCount, defaults.DotCount, 0, 4000);
            parameters.DashCount = ClampInt(parameters.DashCount, defaults.DashCount, 0, 2000);
            parameters.MarkSizeMin = ClampPositiveFinite(parameters.MarkSizeMin, defaults.MarkSizeMin, 0.25f, 20f);
            parameters.MarkSizeMax = ClampPositiveFinite(parameters.MarkSizeMax, defaults.MarkSizeMax, parameters.MarkSizeMin, 40f);
            parameters.MarkBlur = ClampFinite(parameters.MarkBlur, defaults.MarkBlur, 0f, 4f);
            parameters.MarkAngleVariance = ClampFinite(parameters.MarkAngleVariance, defaults.MarkAngleVariance, 0f, MathHelper.PiOver2);

            parameters.WarpScaleA = ClampPositiveFinite(parameters.WarpScaleA, defaults.WarpScaleA, 0.01f, 10f);
            parameters.WarpScaleB = ClampPositiveFinite(parameters.WarpScaleB, defaults.WarpScaleB, 0.01f, 10f);
            parameters.WarpAmp = ClampFinite(parameters.WarpAmp, defaults.WarpAmp, 0f, 6f);
            parameters.Macro1Scale = ClampPositiveFinite(parameters.Macro1Scale, defaults.Macro1Scale, 0.01f, 10f);
            parameters.Macro2Scale = ClampPositiveFinite(parameters.Macro2Scale, defaults.Macro2Scale, 0.01f, 10f);
            parameters.Macro3Scale = ClampPositiveFinite(parameters.Macro3Scale, defaults.Macro3Scale, 0.01f, 10f);
            parameters.Amp1 = ClampFinite(parameters.Amp1, defaults.Amp1, 0f, 2f);
            parameters.Amp2 = ClampFinite(parameters.Amp2, defaults.Amp2, 0f, 2f);
            parameters.Amp3 = ClampFinite(parameters.Amp3, defaults.Amp3, 0f, 2f);
            parameters.FieldSmoothing = ClampFinite(parameters.FieldSmoothing, defaults.FieldSmoothing, 0f, 1f);

            parameters.NearWidth = ClampPositiveFinite(parameters.NearWidth, defaults.NearWidth, 0.002f, 2f);
            parameters.MidWidth = ClampPositiveFinite(parameters.MidWidth, defaults.MidWidth, parameters.NearWidth, 2f);
            parameters.WideWidth = ClampPositiveFinite(parameters.WideWidth, defaults.WideWidth, parameters.MidWidth, 2.5f);
            parameters.NearWeight = ClampFinite(parameters.NearWeight, defaults.NearWeight, 0f, 5f);
            parameters.MidWeight = ClampFinite(parameters.MidWeight, defaults.MidWeight, 0f, 5f);
            parameters.WideWeight = ClampFinite(parameters.WideWeight, defaults.WideWeight, 0f, 5f);
            parameters.EdgeDarkColor = ClampVector3Finite(parameters.EdgeDarkColor, defaults.EdgeDarkColor, Vector3.Zero, Vector3.One);

            parameters.RevealNearWeight = ClampFinite(parameters.RevealNearWeight, defaults.RevealNearWeight, 0f, 1f);
            parameters.RevealMidWeight = ClampFinite(parameters.RevealMidWeight, defaults.RevealMidWeight, 0f, 1f);
            parameters.RevealLiftColor = ClampVector3Finite(parameters.RevealLiftColor, defaults.RevealLiftColor, Vector3.Zero, Vector3.One);
            parameters.BoundarySoftness = ClampPositiveFinite(parameters.BoundarySoftness, defaults.BoundarySoftness, 0.001f, 0.15f);
            parameters.PolygonSides = ClampInt(parameters.PolygonSides, defaults.PolygonSides, 6, 256);
            parameters.PolygonRotation = ClampFinite(parameters.PolygonRotation, defaults.PolygonRotation, -MathHelper.TwoPi, MathHelper.TwoPi);
            parameters.PolygonNoiseBlend = ClampFinite(parameters.PolygonNoiseBlend, defaults.PolygonNoiseBlend, 0f, 1f);
            parameters.PolygonToothCount = ClampInt(parameters.PolygonToothCount, defaults.PolygonToothCount, 8, 1024);
            parameters.PolygonToothAmplitude = ClampFinite(parameters.PolygonToothAmplitude, defaults.PolygonToothAmplitude, 0f, 0.2f);
            parameters.PolygonToothSharpness = ClampFinite(parameters.PolygonToothSharpness, defaults.PolygonToothSharpness, 1f, 10f);

            return parameters;
        }
        private static float FbmNoise(Vector2 uv, int octaves, int seed)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(uv * frequency, seed + (i * 73)) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            if (total <= 0f)
            {
                return 0.5f;
            }

            return sum / total;
        }

        private static float ValueNoise(Vector2 uv, int seed)
        {
            int x0 = (int)MathF.Floor(uv.X);
            int y0 = (int)MathF.Floor(uv.Y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float tx = uv.X - x0;
            float ty = uv.Y - y0;
            float sx = SmoothStep01(tx);
            float sy = SmoothStep01(ty);

            float n00 = Hash2D(x0, y0, seed);
            float n10 = Hash2D(x1, y0, seed);
            float n01 = Hash2D(x0, y1, seed);
            float n11 = Hash2D(x1, y1, seed);

            float nx0 = MathHelper.Lerp(n00, n10, sx);
            float nx1 = MathHelper.Lerp(n01, n11, sx);
            return MathHelper.Lerp(nx0, nx1, sy);
        }

        private static float Hash2D(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8DA6B343u;
                h ^= (uint)y * 0xD8163841u;
                h ^= (uint)seed * 0xCB1AB31Fu;
                h ^= h >> 13;
                h *= 0x85EBCA6Bu;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
            {
                return 0;
            }

            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static float PositiveModulo(float value, float modulus)
        {
            if (modulus <= 0f)
            {
                return 0f;
            }

            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }

        private static float WrapAngleRadians(float angleRadians)
        {
            return MathHelper.WrapAngle(angleRadians);
        }

        private static int QuantizeDirectionBin(float angleRadians)
        {
            float normalized = angleRadians / MathHelper.TwoPi;
            normalized -= MathF.Floor(normalized);
            int bin = (int)MathF.Floor(normalized * FrontierDirectionBins) % FrontierDirectionBins;
            if (bin < 0)
            {
                bin += FrontierDirectionBins;
            }

            return bin;
        }

        private static int ClampInt(int value, int fallback, int min, int max)
        {
            int resolved = value < min ? fallback : value;
            if (resolved < min) return min;
            if (resolved > max) return max;
            return resolved;
        }

        private static float ClampPositiveFinite(float value, float fallback, float min, float max)
        {
            float resolved = (!float.IsFinite(value) || value <= 0f) ? fallback : value;
            return MathHelper.Clamp(resolved, min, max);
        }

        private static float ClampFinite(float value, float fallback, float min, float max)
        {
            float resolved = !float.IsFinite(value) ? fallback : value;
            return MathHelper.Clamp(resolved, min, max);
        }

        private static Vector3 ClampVector3(Vector3 value)
        {
            return new Vector3(
                MathHelper.Clamp(value.X, 0f, 1f),
                MathHelper.Clamp(value.Y, 0f, 1f),
                MathHelper.Clamp(value.Z, 0f, 1f));
        }

        private static Vector3 ClampVector3Finite(Vector3 value, Vector3 fallback, Vector3 min, Vector3 max)
        {
            float x = !float.IsFinite(value.X) ? fallback.X : value.X;
            float y = !float.IsFinite(value.Y) ? fallback.Y : value.Y;
            float z = !float.IsFinite(value.Z) ? fallback.Z : value.Z;

            return new Vector3(
                MathHelper.Clamp(x, min.X, max.X),
                MathHelper.Clamp(y, min.Y, max.Y),
                MathHelper.Clamp(z, min.Z, max.Z));
        }

        private static float Saturate(float value)
        {
            return MathHelper.Clamp(value, 0f, 1f);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (edge1 <= edge0)
            {
                return value < edge0 ? 0f : 1f;
            }

            float t = Saturate((value - edge0) / (edge1 - edge0));
            return (t * t) * (3f - (2f * t));
        }

        private static float SmoothStep01(float value)
        {
            float t = Saturate(value);
            return (t * t) * (3f - (2f * t));
        }

        private static float NextFloat(Random random)
        {
            return (float)random.NextDouble();
        }

        private static byte ToByte(float value)
        {
            return (byte)MathF.Round(Saturate(value) * 255f);
        }

        private static Color ToColor(Vector3 rgb, float alpha)
        {
            Vector3 c = ClampVector3(rgb);
            return new Color(
                (byte)MathF.Round(c.X * 255f),
                (byte)MathF.Round(c.Y * 255f),
                (byte)MathF.Round(c.Z * 255f),
                (byte)MathF.Round(Saturate(alpha) * 255f));
        }
    }
}
