using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Seed-driven chunked land overlay rendered over the ocean background.
    /// Chunks are regenerated from seed + chunk coordinates, so only the seed
    /// needs to persist between sessions.
    /// </summary>
    internal static class GameBlockTerrainBackground
    {
        private const int DefaultTerrainWorldSeed = 1337;
        private const float ChunkWorldSize = 1024f;
        private const int ChunkTextureResolution = 128;
        private const int ChunkGuardResolution = 16;
        private const int MaxNewChunkBuildsPerFrame = 2;
        private const float PreloadChunkMarginMultiplier = 1.25f;
        private const float RetainExtraChunkMultiplier = 1.5f;
        private const float TerrainFeatureScaleMultiplier = 3f;
        private const int TerrainContourResolutionMultiplier = 1;
        private const int TerrainVisualTextureOversample = 0;
        private const int MaxTerrainVisualTextureAxis = 4096;
        private const float TerrainCollisionActivationMarginWorldUnits = 256f;
        private const float TerrainCollisionDynamicProbeMarginWorldUnits = 128f;
        private const float TerrainColliderSpatialCellSizeWorldUnits = 256f;
        private const float TerrainCollisionVelocityLeadSeconds = 0.35f;
        private const float TerrainCollisionHullSampleSpacingWorldUnits = 8f;
        private const int MaxTerrainColliderRecordsPerRefresh = 16384;
        private const float TerrainCollisionShellThicknessCentifoot = 0.08f;
        private const float TerrainVisualLoopSimplifyToleranceCells = 1.15f;
        private const float TerrainNaturalCoastJitterCells = 0.42f;
        private const float TerrainNaturalCoastMaxSegmentCells = 4.25f;
        private const float TerrainNaturalCoastBoundaryLockCells = 2.25f;
        private const int TerrainNaturalCoastMaxPointCount = 120;
        private static readonly float TerrainOctogonalCornerCutCells = 0f;
        private const float TerrainMinVisualLoopAreaCells = 180f;
        private const float TerrainMinVisualLoopMajorAxisCells = 14f;
        private const float TerrainMinVisualLoopNarrowAxisCells = 6f;
        private const int TerrainVisualSmoothIterations = 1;
        private const int TerrainSmoothLoopPointThreshold = 360;
        private const int TerrainHeavyLoopPointThreshold = 384;
        private const int TerrainDrawLayer = -1000;
        private const float TerrainStaticMass = 1000000f;
        private const float TerrainWorldSizeWorldUnits = 8192f;
        private const float TerrainWorldBoundaryThicknessWorldUnits = 256f;
        private const float DefaultPixelsPerTerrainUnit = 22f;
        private const int SpawnAnchorSearchCellRadius = 8;
        private const float PreferredSpawnFieldMargin = 0.12f;

        private const float SeaLevel = 0.05f;
        private const byte Land = 1;
        private const byte Water = 0;
        private const int BaseMinLandComponentArea = 260;
        private const int BaseMinWaterComponentArea = 120;
        private const float ArchipelagoCellSize = 4.8f;
        private const float ArchipelagoMacroCellSize = ArchipelagoCellSize * 4.6f;
        private const float IslandCellPresenceThreshold = 0.38f;
        private const float IslandMainRadiusBase = 1.18f;
        private const float IslandMainRadiusRange = 0.38f;
        private const float IslandMainJitterInset = 0.18f;
        private const float IslandMainJitterRange = 0.64f;
        private const int IslandSatelliteMaxCount = 2;
        private const float ArchipelagoSubstrateCellSize = 7.2f;
        private const float ArchipelagoEnclosureCellSize = ArchipelagoCellSize * 1.65f;
        private const float ArchipelagoBarrierCellSize = ArchipelagoCellSize * 1.15f;
        private const float ArchipelagoKarstCellSize = ArchipelagoCellSize * 0.75f;
        private const float ArchipelagoLandformCellSize = ArchipelagoCellSize * 2.35f;
        private const float ArchipelagoLandformCenterJitter = 0.22f;
        private const int LagoonOpeningMinCount = 1;
        private const int LagoonOpeningMaxCount = 2;
        private const float LagoonBasinCutStrength = 0.76f;
        private const float RegionalTidalChannelCutStrength = 0.68f;

        private static readonly int MaxConcurrentChunkBuilds = 2;
        private static readonly Dictionary<ChunkKey, TerrainChunkRecord> ResidentChunks = new();
        private static readonly Dictionary<ChunkKey, Task<GeneratedChunkData>> PendingChunks = new();
        private static readonly List<TerrainVisualObjectRecord> ResidentTerrainVisualObjects = new();
        private static readonly List<TerrainColliderObjectRecord> ResidentTerrainColliderObjects = new();
        private static readonly List<TerrainCollisionLoopRecord> ResidentTerrainCollisionLoops = new();
        private static readonly Dictionary<long, List<TerrainColliderObjectRecord>> TerrainColliderRecordCells = new();
        private static readonly List<long> TerrainColliderRecordCellKeys = new();
        private static readonly Stack<List<TerrainColliderObjectRecord>> AvailableTerrainColliderRecordCellLists = new();
        private static readonly HashSet<TerrainColliderObjectRecord> ActiveTerrainColliderRecords = new();
        private static readonly HashSet<TerrainColliderObjectRecord> DesiredTerrainColliderRecords = new();
        private static readonly List<TerrainColliderObjectRecord> TerrainColliderDeactivateScratch = new();
        private static readonly VertexPositionColor[] TerrainBoundaryFillVertices = new VertexPositionColor[24];
        private static readonly RasterizerState TerrainVectorRasterizerState = new()
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };

        private static bool _settingsLoaded;
        private static bool _terrainWorldObjectsDirty = true;
        private static int _terrainWorldSeed = DefaultTerrainWorldSeed;
        private static int _residentTerrainComponentCount;
        private static int _residentTerrainColliderCount;
        private static int _residentTerrainVisualTriangleCount;
        private static int _activeTerrainColliderCount;
        private static int _terrainColliderActivationCandidateCount;
        private static int _terrainSpawnRelocationCount;
        private static int _terrainCollisionIntrusionCorrectionCount;
        private static Vector2 _terrainSeedAnchorCentifoot = Vector2.Zero;
        private static bool _terrainWorldBoundsInitialized;
        private static TerrainWorldBounds _terrainWorldBounds;
        private static float _lastPreloadMarginWorldUnits = ChunkWorldSize * PreloadChunkMarginMultiplier;
        private static ChunkBounds _lastVisibleChunkWindow;
        private static ChunkBounds _lastMaterializedChunkWindow;
        private static ChunkBounds _lastTerrainColliderChunkWindow;
        private static ChunkKey _lastCenterChunk = new(0, 0);
        private static Vector2 _lastTerrainStreamingFocusWorldPosition = Vector2.Zero;
        private static int _terrainPendingCriticalChunkCount;
        private static BasicEffect _terrainVectorEffect;
        private static Task<TerrainMaterializationResult> _terrainMaterializationTask;
        private static int _terrainMaterializationRequestId;
        private static int _terrainDiscardedStaleMaterializationCount;
        private static double _lastTerrainMaterializationMilliseconds;
        private static uint _lastAppliedTerrainColorPacked;
        private static bool _residentTerrainVertexColorValid;
        private static bool _startupVisibleTerrainReady;
        private static string _terrainStartupReadinessSummary = "startup terrain pending";

        public static bool IsActive => true;
        public static int TerrainWorldSeed => _terrainWorldSeed;
        public static int TerrainResidentChunkCount => ResidentChunks.Count;
        public static int TerrainResidentComponentCount => _residentTerrainComponentCount;
        public static int TerrainResidentEdgeLoopCount => _residentTerrainComponentCount;
        public static int TerrainResidentColliderCount => _residentTerrainColliderCount;
        public static int TerrainResidentVisualTriangleCount => _residentTerrainVisualTriangleCount;
        public static int TerrainActiveColliderCount => _activeTerrainColliderCount;
        public static int TerrainColliderActivationCandidateCount => _terrainColliderActivationCandidateCount;
        public static int TerrainSpawnRelocationCount => _terrainSpawnRelocationCount;
        public static int TerrainCollisionIntrusionCorrectionCount => _terrainCollisionIntrusionCorrectionCount;
        public static int TerrainPendingChunkCount => PendingChunks.Count;
        public static int TerrainPendingCriticalChunkCount => _terrainPendingCriticalChunkCount;
        public static bool TerrainChunkBuildsInFlight => PendingChunks.Count > 0;
        public static bool TerrainMaterializationInFlight => _terrainMaterializationTask != null;
        public static bool TerrainMaterializationRestartPending => _terrainMaterializationTask != null && _terrainWorldObjectsDirty;
        public static int TerrainDiscardedStaleMaterializationCount => _terrainDiscardedStaleMaterializationCount;
        public static double TerrainLastMaterializationMilliseconds => _lastTerrainMaterializationMilliseconds;
        public static bool TerrainStartupVisibleTerrainReady => _startupVisibleTerrainReady;
        public static string TerrainStartupReadinessSummary => _terrainStartupReadinessSummary;
        public static float TerrainChunkWorldSize => ChunkWorldSize;
        public static float TerrainFeatureWorldScaleMultiplier => TerrainFeatureScaleMultiplier;
        public static float TerrainArchipelagoMacroCellSize => ArchipelagoMacroCellSize;
        public static float TerrainArchipelagoSubstrateCellSize => ArchipelagoSubstrateCellSize;
        public static float TerrainArchipelagoEnclosureCellSize => ArchipelagoEnclosureCellSize;
        public static float TerrainArchipelagoLandformCellSize => ArchipelagoLandformCellSize;
        public static string TerrainGenerationPipeline => "macro mask > lithology > fractures > pre-flood terrain > karst dissolution > flooding > erosion > sediment > reef growth > classification";
        public static string TerrainLandformSelectionMode => "layered geological processes";
        public static string TerrainLagoonOpeningTarget => $"{LagoonOpeningMinCount}-{LagoonOpeningMaxCount}";
        public static float TerrainLagoonBasinCutStrength => LagoonBasinCutStrength;
        public static float TerrainRegionalTidalChannelCutStrength => RegionalTidalChannelCutStrength;
        public static int TerrainContourResolutionMultiplierSetting => TerrainContourResolutionMultiplier;
        public static int TerrainTargetVisualTextureOversample => TerrainVisualTextureOversample;
        public static float TerrainOctogonalCornerCutCellRatio => TerrainOctogonalCornerCutCells;
        public static int TerrainDrawLayerSetting => TerrainDrawLayer;
        public static float TerrainPreloadMarginWorldUnits => _lastPreloadMarginWorldUnits;
        public static string TerrainWorldBoundsSummary =>
            $"{CentifootUnits.FormatDistance(_terrainWorldBounds.MinX)}, {CentifootUnits.FormatDistance(_terrainWorldBounds.MinY)} -> {CentifootUnits.FormatDistance(_terrainWorldBounds.MaxX)}, {CentifootUnits.FormatDistance(_terrainWorldBounds.MaxY)}";
        public static string TerrainSeedAnchor =>
            $"{CentifootUnits.FormatNumber(_terrainSeedAnchorCentifoot.X)}, {CentifootUnits.FormatNumber(_terrainSeedAnchorCentifoot.Y)} {CentifootUnits.UnitAbbreviation}";
        public static string TerrainStreamingFocus => CentifootUnits.FormatVector2(_lastTerrainStreamingFocusWorldPosition);
        public static string TerrainStreamingLandformSignature => BuildLandformDebugSignature(_lastTerrainStreamingFocusWorldPosition);
        public static string TerrainCenterChunk => $"{_lastCenterChunk.X}, {_lastCenterChunk.Y}";
        public static string TerrainVisibleChunkWindow =>
            $"{_lastVisibleChunkWindow.MinChunkX}..{_lastVisibleChunkWindow.MaxChunkX}, {_lastVisibleChunkWindow.MinChunkY}..{_lastVisibleChunkWindow.MaxChunkY}";
        public static string TerrainColliderChunkWindow =>
            $"{_lastTerrainColliderChunkWindow.MinChunkX}..{_lastTerrainColliderChunkWindow.MaxChunkX}, {_lastTerrainColliderChunkWindow.MinChunkY}..{_lastTerrainColliderChunkWindow.MaxChunkY}";

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            LoadSettingsIfNeeded();
            EnsureTerrainVectorEffect(graphicsDevice);
            _terrainWorldObjectsDirty = true;
            _startupVisibleTerrainReady = false;
            _terrainStartupReadinessSummary = "startup terrain pending";
        }

        public static bool PrepareStartupVisibleTerrain(GraphicsDevice graphicsDevice, Rectangle panelBounds, Matrix cameraTransform)
        {
            if (graphicsDevice == null || panelBounds.Width <= 0 || panelBounds.Height <= 0)
            {
                _terrainStartupReadinessSummary = "startup terrain pending: invalid viewport";
                return false;
            }

            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();
            EnsureTerrainVectorEffect(graphicsDevice);

            if (!TryResolveTerrainStreamingWindows(cameraTransform, panelBounds, out TerrainStreamingWindowSet windows))
            {
                _terrainStartupReadinessSummary = "startup terrain pending: camera bounds unavailable";
                return false;
            }

            ApplyTerrainStreamingWindowState(windows);
            TryPromoteCompletedChunks(windows.RetainChunkWindow);
            if (!BuildResidentChunksSynchronously(windows.VisibleChunkWindow))
            {
                _terrainStartupReadinessSummary = "startup terrain pending: visible chunk build failed";
                return false;
            }

            PruneResidentChunks(windows.RetainChunkWindow);
            _terrainPendingCriticalChunkCount = CountPendingChunksInBounds(windows.VisibleChunkWindow);
            if (_terrainPendingCriticalChunkCount > 0)
            {
                _terrainStartupReadinessSummary = $"startup terrain pending: {_terrainPendingCriticalChunkCount} visible chunks still queued";
                return false;
            }

            ChunkBounds startupMaterializedWindow = windows.VisibleChunkWindow;
            TerrainMaterializationResult result;
            if (TryBuildCombinedResidentMask(startupMaterializedWindow, out CombinedResidentMask residentMask))
            {
                result = BuildTerrainMaterializationResult(
                    residentMask,
                    ++_terrainMaterializationRequestId,
                    BuildChunkWorldBounds(startupMaterializedWindow),
                    startupMaterializedWindow,
                    startupMaterializedWindow);
            }
            else
            {
                result = BuildTerrainMaterializationResult(
                    new CombinedResidentMask(Array.Empty<byte>(), 0, 0, startupMaterializedWindow.MinChunkX, startupMaterializedWindow.MinChunkY),
                    ++_terrainMaterializationRequestId,
                    BuildChunkWorldBounds(startupMaterializedWindow),
                    startupMaterializedWindow,
                    startupMaterializedWindow);
            }

            _terrainMaterializationTask = null;
            _lastMaterializedChunkWindow = startupMaterializedWindow;
            _lastTerrainColliderChunkWindow = startupMaterializedWindow;
            _terrainWorldObjectsDirty = false;
            ApplyTerrainMaterializationResult(result);
            _startupVisibleTerrainReady = true;
            _terrainStartupReadinessSummary = $"startup terrain ready: {FormatChunkBounds(startupMaterializedWindow)} visible";
            return true;
        }

        private static void EnsureTerrainVectorEffect(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_terrainVectorEffect == null ||
                !ReferenceEquals(_terrainVectorEffect.GraphicsDevice, graphicsDevice))
            {
                _terrainVectorEffect?.Dispose();
                _terrainVectorEffect = new BasicEffect(graphicsDevice)
                {
                    TextureEnabled = false,
                    VertexColorEnabled = true,
                    LightingEnabled = false
                };
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle panelBounds, Matrix cameraTransform)
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

            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();

            if (!TryResolveTerrainStreamingWindows(cameraTransform, panelBounds, out TerrainStreamingWindowSet windows))
            {
                return;
            }

            ApplyTerrainStreamingWindowState(windows);
            if (!ChunkBoundsEqual(_lastMaterializedChunkWindow, windows.TerrainObjectChunkWindow) ||
                !ChunkBoundsEqual(_lastTerrainColliderChunkWindow, windows.TerrainColliderChunkWindow))
            {
                _lastMaterializedChunkWindow = windows.TerrainObjectChunkWindow;
                _lastTerrainColliderChunkWindow = windows.TerrainColliderChunkWindow;
                _terrainWorldObjectsDirty = true;
            }

            TryPromoteCompletedChunks(windows.RetainChunkWindow);
            QueueChunkBuilds(windows.PreloadChunkWindow, windows.VisibleChunkWindow);
            PruneResidentChunks(windows.RetainChunkWindow);
            _terrainPendingCriticalChunkCount = CountPendingChunksInBounds(windows.VisibleChunkWindow);
            RefreshResidentTerrainWorldObjects(
                graphicsDevice,
                windows.TerrainObjectChunkWindow,
                windows.TerrainColliderChunkWindow,
                windows.VisibleChunkWindow);
            UpdateStartupVisibleTerrainReadiness(windows.VisibleChunkWindow);
            DrawTerrainWorldBoundaryFill(
                spriteBatch,
                panelBounds,
                cameraTransform,
                windows.CameraMinX,
                windows.CameraMaxX,
                windows.CameraMinY,
                windows.CameraMaxY);
            DrawResidentTerrainVisuals(
                spriteBatch,
                panelBounds,
                cameraTransform,
                windows.CameraMinX,
                windows.CameraMaxX,
                windows.CameraMinY,
                windows.CameraMaxY);
            UpdateActiveTerrainColliders(windows.CameraMinX, windows.CameraMaxX, windows.CameraMinY, windows.CameraMaxY);
        }

        private static void LoadSettingsIfNeeded()
        {
            if (_settingsLoaded)
            {
                return;
            }

            _terrainWorldSeed = DatabaseFetch.GetSetting("GeneralSettings", "Value", "SettingKey", "TerrainWorldSeed", DefaultTerrainWorldSeed);
            _terrainSeedAnchorCentifoot = ResolveSeedAnchorCentifoot(_terrainWorldSeed);
            _settingsLoaded = true;
        }

        private static void EnsureTerrainWorldBoundsInitialized()
        {
            if (_terrainWorldBoundsInitialized)
            {
                return;
            }

            Vector2 center = Core.Instance?.Player?.Position ?? Vector2.Zero;
            float halfSize = TerrainWorldSizeWorldUnits * 0.5f;
            _terrainWorldBounds = new TerrainWorldBounds(
                center.X - halfSize,
                center.Y - halfSize,
                TerrainWorldSizeWorldUnits,
                TerrainWorldSizeWorldUnits);
            _terrainWorldBoundsInitialized = true;
        }

        private static float ResolvePreloadMarginWorldUnits(
            float minX,
            float maxX,
            float minY,
            float maxY,
            float maxVisionRadiusWorldUnits)
        {
            float cameraSpan = MathF.Max(maxX - minX, maxY - minY);
            float sightRadius = MathF.Max(MathF.Max(0f, FogOfWarManager.PlayerSightRadius), maxVisionRadiusWorldUnits);
            return MathF.Max(ChunkWorldSize * PreloadChunkMarginMultiplier, MathF.Max(sightRadius, cameraSpan * 0.75f));
        }

        private static Vector2 ResolveTerrainStreamingFocusWorldPosition(float minX, float maxX, float minY, float maxY)
        {
            Vector2 playerPosition = Core.Instance?.Player?.Position ?? Vector2.Zero;
            if (IsFiniteVector(playerPosition))
            {
                return playerPosition;
            }

            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        private static void ExpandTerrainStreamingWorldBoundsForCameraAndVision(
            ref float streamMinX,
            ref float streamMaxX,
            ref float streamMinY,
            ref float streamMaxY,
            float cameraMinX,
            float cameraMaxX,
            float cameraMinY,
            float cameraMaxY,
            ref float maxVisionRadiusWorldUnits)
        {
            IncludeWorldBounds(ref streamMinX, ref streamMaxX, ref streamMinY, ref streamMaxY, cameraMinX, cameraMaxX, cameraMinY, cameraMaxY);

            if (FogOfWarManager.TryGetVisionWorldBounds(
                0f,
                out _,
                out float visionMinX,
                out float visionMaxX,
                out float visionMinY,
                out float visionMaxY,
                out float visionMaxRadius))
            {
                IncludeWorldBounds(ref streamMinX, ref streamMaxX, ref streamMinY, ref streamMaxY, visionMinX, visionMaxX, visionMinY, visionMaxY);
                maxVisionRadiusWorldUnits = MathF.Max(maxVisionRadiusWorldUnits, visionMaxRadius);
            }
        }

        private static void IncludeWorldBounds(
            ref float targetMinX,
            ref float targetMaxX,
            ref float targetMinY,
            ref float targetMaxY,
            float sourceMinX,
            float sourceMaxX,
            float sourceMinY,
            float sourceMaxY)
        {
            if (!float.IsFinite(sourceMinX) ||
                !float.IsFinite(sourceMaxX) ||
                !float.IsFinite(sourceMinY) ||
                !float.IsFinite(sourceMaxY))
            {
                return;
            }

            targetMinX = MathF.Min(targetMinX, sourceMinX);
            targetMaxX = MathF.Max(targetMaxX, sourceMaxX);
            targetMinY = MathF.Min(targetMinY, sourceMinY);
            targetMaxY = MathF.Max(targetMaxY, sourceMaxY);
        }

        private static bool TryResolveTerrainStreamingWindows(
            Matrix cameraTransform,
            Rectangle panelBounds,
            out TerrainStreamingWindowSet windows)
        {
            windows = default;
            if (!TryGetVisibleWorldBounds(
                cameraTransform,
                panelBounds,
                out float cameraMinX,
                out float cameraMaxX,
                out float cameraMinY,
                out float cameraMaxY))
            {
                return false;
            }

            Vector2 terrainStreamingFocus = ResolveTerrainStreamingFocusWorldPosition(cameraMinX, cameraMaxX, cameraMinY, cameraMaxY);
            BuildTerrainStreamingWorldBounds(
                terrainStreamingFocus,
                cameraMinX,
                cameraMaxX,
                cameraMinY,
                cameraMaxY,
                out float streamMinX,
                out float streamMaxX,
                out float streamMinY,
                out float streamMaxY);
            float maxVisionRadiusWorldUnits = MathF.Max(0f, FogOfWarManager.PlayerSightRadius);
            ExpandTerrainStreamingWorldBoundsForCameraAndVision(
                ref streamMinX,
                ref streamMaxX,
                ref streamMinY,
                ref streamMaxY,
                cameraMinX,
                cameraMaxX,
                cameraMinY,
                cameraMaxY,
                ref maxVisionRadiusWorldUnits);
            terrainStreamingFocus = ResolveTerrainStreamingFocusWorldPosition(streamMinX, streamMaxX, streamMinY, streamMaxY);

            float preloadMarginWorldUnits = ResolvePreloadMarginWorldUnits(
                streamMinX,
                streamMaxX,
                streamMinY,
                streamMaxY,
                maxVisionRadiusWorldUnits);
            float retainMarginWorldUnits = preloadMarginWorldUnits + (ChunkWorldSize * RetainExtraChunkMultiplier);

            ChunkBounds visibleChunkWindow = BuildChunkBounds(streamMinX, streamMaxX, streamMinY, streamMaxY);
            ChunkBounds preloadChunkWindow = BuildChunkBounds(
                streamMinX - preloadMarginWorldUnits,
                streamMaxX + preloadMarginWorldUnits,
                streamMinY - preloadMarginWorldUnits,
                streamMaxY + preloadMarginWorldUnits);
            ChunkBounds terrainObjectChunkWindow = preloadChunkWindow;
            ChunkBounds terrainColliderChunkWindow = BuildChunkBounds(
                streamMinX - TerrainCollisionActivationMarginWorldUnits,
                streamMaxX + TerrainCollisionActivationMarginWorldUnits,
                streamMinY - TerrainCollisionActivationMarginWorldUnits,
                streamMaxY + TerrainCollisionActivationMarginWorldUnits);
            ChunkBounds retainChunkWindow = BuildChunkBounds(
                streamMinX - retainMarginWorldUnits,
                streamMaxX + retainMarginWorldUnits,
                streamMinY - retainMarginWorldUnits,
                streamMaxY + retainMarginWorldUnits);

            windows = new TerrainStreamingWindowSet(
                cameraMinX,
                cameraMaxX,
                cameraMinY,
                cameraMaxY,
                terrainStreamingFocus,
                preloadMarginWorldUnits,
                ClampChunkBoundsToTerrainWorld(visibleChunkWindow),
                ClampChunkBoundsToTerrainWorld(preloadChunkWindow),
                ClampChunkBoundsToTerrainWorld(terrainObjectChunkWindow),
                ClampChunkBoundsToTerrainWorld(terrainColliderChunkWindow),
                ClampChunkBoundsToTerrainWorld(retainChunkWindow));
            return true;
        }

        private static void ApplyTerrainStreamingWindowState(TerrainStreamingWindowSet windows)
        {
            _lastTerrainStreamingFocusWorldPosition = windows.StreamingFocusWorldPosition;
            _lastPreloadMarginWorldUnits = windows.PreloadMarginWorldUnits;
            _lastVisibleChunkWindow = windows.VisibleChunkWindow;
            _lastCenterChunk = BuildChunkKey(windows.StreamingFocusWorldPosition.X, windows.StreamingFocusWorldPosition.Y);
        }

        private static void BuildTerrainStreamingWorldBounds(
            Vector2 focusWorldPosition,
            float visibleMinX,
            float visibleMaxX,
            float visibleMinY,
            float visibleMaxY,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            float spanX = MathF.Max(ChunkWorldSize, visibleMaxX - visibleMinX);
            float spanY = MathF.Max(ChunkWorldSize, visibleMaxY - visibleMinY);
            float halfSpanX = spanX * 0.5f;
            float halfSpanY = spanY * 0.5f;

            minX = focusWorldPosition.X - halfSpanX;
            maxX = focusWorldPosition.X + halfSpanX;
            minY = focusWorldPosition.Y - halfSpanY;
            maxY = focusWorldPosition.Y + halfSpanY;
        }

        private static bool HasResidentChunkInBounds(ChunkBounds bounds)
        {
            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (bounds.Contains(entry.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPendingChunkInBounds(ChunkBounds bounds)
        {
            foreach (KeyValuePair<ChunkKey, Task<GeneratedChunkData>> entry in PendingChunks)
            {
                if (bounds.Contains(entry.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountPendingChunksInBounds(ChunkBounds bounds)
        {
            int count = 0;
            foreach (KeyValuePair<ChunkKey, Task<GeneratedChunkData>> entry in PendingChunks)
            {
                if (bounds.Contains(entry.Key))
                {
                    count++;
                }
            }

            return count;
        }

        private static void QueueChunkBuilds(ChunkBounds preloadChunkWindow, ChunkBounds visibleChunkWindow)
        {
            List<ChunkBuildCandidate> missingChunks = new();

            for (int chunkY = preloadChunkWindow.MinChunkY; chunkY <= preloadChunkWindow.MaxChunkY; chunkY++)
            {
                for (int chunkX = preloadChunkWindow.MinChunkX; chunkX <= preloadChunkWindow.MaxChunkX; chunkX++)
                {
                    ChunkKey key = new(chunkX, chunkY);
                    if (ResidentChunks.ContainsKey(key) || PendingChunks.ContainsKey(key))
                    {
                        continue;
                    }

                    bool visible = visibleChunkWindow.Contains(key);
                    int dx = chunkX - _lastCenterChunk.X;
                    int dy = chunkY - _lastCenterChunk.Y;
                    int distanceSq = (dx * dx) + (dy * dy);
                    missingChunks.Add(new ChunkBuildCandidate(key, visible, distanceSq));
                }
            }

            missingChunks.Sort(static (left, right) =>
            {
                int visibleCompare = right.IsVisible.CompareTo(left.IsVisible);
                if (visibleCompare != 0)
                {
                    return visibleCompare;
                }

                return left.DistanceSq.CompareTo(right.DistanceSq);
            });

            int availableBuildSlots = Math.Min(
                MaxNewChunkBuildsPerFrame,
                Math.Max(0, MaxConcurrentChunkBuilds - PendingChunks.Count));
            if (availableBuildSlots <= 0)
            {
                return;
            }

            int buildsQueued = 0;
            for (int i = 0; i < missingChunks.Count && buildsQueued < availableBuildSlots; i++)
            {
                ChunkKey key = missingChunks[i].Key;
                PendingChunks[key] = Task.Run(() => BuildChunkData(key));
                buildsQueued++;
            }
        }

        private static bool BuildResidentChunksSynchronously(ChunkBounds chunkWindow)
        {
            for (int chunkY = chunkWindow.MinChunkY; chunkY <= chunkWindow.MaxChunkY; chunkY++)
            {
                for (int chunkX = chunkWindow.MinChunkX; chunkX <= chunkWindow.MaxChunkX; chunkX++)
                {
                    ChunkKey key = new(chunkX, chunkY);
                    if (ResidentChunks.ContainsKey(key))
                    {
                        continue;
                    }

                    if (PendingChunks.TryGetValue(key, out Task<GeneratedChunkData> pendingTask))
                    {
                        try
                        {
                            GeneratedChunkData pendingChunkData = pendingTask.GetAwaiter().GetResult();
                            PendingChunks.Remove(key);
                            PromoteChunk(pendingChunkData);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            PendingChunks.Remove(key);
                            DebugLogger.PrintWarning($"GameBlockTerrainBackground: startup chunk build {key.X},{key.Y} failed. {ex.GetBaseException().Message}");
                            return false;
                        }
                    }

                    try
                    {
                        PromoteChunk(BuildChunkData(key));
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.PrintWarning($"GameBlockTerrainBackground: startup chunk build {key.X},{key.Y} failed. {ex.GetBaseException().Message}");
                        return false;
                    }
                }
            }

            return true;
        }

        private static void TryPromoteCompletedChunks(ChunkBounds retainChunkWindow)
        {
            List<ChunkKey> completed = new();

            foreach (KeyValuePair<ChunkKey, Task<GeneratedChunkData>> entry in PendingChunks)
            {
                Task<GeneratedChunkData> task = entry.Value;
                if (!task.IsCompleted)
                {
                    continue;
                }

                completed.Add(entry.Key);

                if (task.IsFaulted)
                {
                    DebugLogger.PrintWarning($"GameBlockTerrainBackground: chunk build {entry.Key.X},{entry.Key.Y} failed. {task.Exception?.GetBaseException().Message}");
                    continue;
                }

                if (task.IsCanceled)
                {
                    continue;
                }

                GeneratedChunkData chunkData = task.Result;
                if (!retainChunkWindow.Contains(chunkData.Key))
                {
                    continue;
                }

                PromoteChunk(chunkData);
            }

            for (int i = 0; i < completed.Count; i++)
            {
                PendingChunks.Remove(completed[i]);
            }
        }

        private static void PromoteChunk(GeneratedChunkData chunkData)
        {
            if (chunkData == null)
            {
                return;
            }

            DisposeResidentChunk(chunkData.Key);
            ResidentChunks[chunkData.Key] = new TerrainChunkRecord(chunkData.LandMask, chunkData.HasLand);
            if (_lastMaterializedChunkWindow.Contains(chunkData.Key))
            {
                _terrainWorldObjectsDirty = true;
            }
        }

        private static void DisposeResidentChunk(ChunkKey key)
        {
            if (!ResidentChunks.TryGetValue(key, out TerrainChunkRecord existing))
            {
                return;
            }

            ResidentChunks.Remove(key);
            if (_lastMaterializedChunkWindow.Contains(key))
            {
                _terrainWorldObjectsDirty = true;
            }
        }

        private static void PruneResidentChunks(ChunkBounds retainChunkWindow)
        {
            List<ChunkKey> staleKeys = new();

            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (!retainChunkWindow.Contains(entry.Key))
                {
                    staleKeys.Add(entry.Key);
                }
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                DisposeResidentChunk(staleKeys[i]);
            }
        }

        private static GeneratedChunkData BuildChunkData(ChunkKey key)
        {
            EnsureTerrainWorldBoundsInitialized();
            if (!TerrainWorldContainsChunk(key))
            {
                return new GeneratedChunkData(
                    key,
                    new byte[ChunkTextureResolution * ChunkTextureResolution],
                    false);
            }

            int paddedResolution = ChunkTextureResolution + (ChunkGuardResolution * 2);
            float sampleStepWorldUnits = ChunkWorldSize / ChunkTextureResolution;
            float originX = (key.X * ChunkWorldSize) - (ChunkGuardResolution * sampleStepWorldUnits);
            float originY = (key.Y * ChunkWorldSize) - (ChunkGuardResolution * sampleStepWorldUnits);

            byte[] mask = new byte[paddedResolution * paddedResolution];
            float pixelsPerTerrainUnit = ChunkTextureResolution / CentifootUnits.WorldToCentifoot(ChunkWorldSize);
            float densityScale = pixelsPerTerrainUnit / DefaultPixelsPerTerrainUnit;
            int minLandComponentArea = Math.Max(64, (int)MathF.Round(BaseMinLandComponentArea * densityScale * densityScale));
            int minWaterComponentArea = Math.Max(48, (int)MathF.Round(BaseMinWaterComponentArea * densityScale * densityScale));

            for (int y = 0; y < paddedResolution; y++)
            {
                float worldY = originY + ((y + 0.5f) * sampleStepWorldUnits);

                for (int x = 0; x < paddedResolution; x++)
                {
                    float worldX = originX + ((x + 0.5f) * sampleStepWorldUnits);
                    mask[Index(x, y, paddedResolution)] = SampleTerrainMaskAtWorldPosition(worldX, worldY);
                }
            }

            byte[] cleaned = MajoritySmooth(mask, paddedResolution, paddedResolution, 1);
            cleaned = RemoveSmallComponents(cleaned, paddedResolution, paddedResolution, Land, minLandComponentArea, Water);
            cleaned = RemoveSmallComponents(cleaned, paddedResolution, paddedResolution, Water, minWaterComponentArea, Land);

            byte[] landMask = new byte[ChunkTextureResolution * ChunkTextureResolution];
            bool hasLand = false;
            for (int y = 0; y < ChunkTextureResolution; y++)
            {
                for (int x = 0; x < ChunkTextureResolution; x++)
                {
                    int paddedX = x + ChunkGuardResolution;
                    int paddedY = y + ChunkGuardResolution;
                    int paddedIndex = Index(paddedX, paddedY, paddedResolution);
                    if (cleaned[paddedIndex] != Land)
                    {
                        continue;
                    }

                    landMask[Index(x, y, ChunkTextureResolution)] = Land;
                    hasLand = true;
                }
            }

            return new GeneratedChunkData(key, landMask, hasLand);
        }

        private static float ResolveTerrainCentifootX(float worldX)
        {
            return (CentifootUnits.WorldToCentifoot(worldX) / TerrainFeatureScaleMultiplier) + _terrainSeedAnchorCentifoot.X;
        }

        private static float ResolveTerrainCentifootY(float worldY)
        {
            return (CentifootUnits.WorldToCentifoot(worldY) / TerrainFeatureScaleMultiplier) + _terrainSeedAnchorCentifoot.Y;
        }

        internal static Vector2 ResolveNearestTerrainFreeWorldPosition(
            Vector2 worldPosition,
            float clearanceRadiusWorldUnits,
            float maxSearchDistanceWorldUnits = 640f)
        {
            LoadSettingsIfNeeded();

            float clearanceRadius = MathF.Max(0f, clearanceRadiusWorldUnits);
            if (!OverlapsTerrainAtWorldPosition(worldPosition, clearanceRadius))
            {
                return worldPosition;
            }

            float radialStep = MathF.Max(12f, clearanceRadius * 0.35f);
            for (float distance = radialStep; distance <= maxSearchDistanceWorldUnits; distance += radialStep)
            {
                int sampleCount = Math.Max(12, (int)MathF.Ceiling(MathF.Tau * distance / radialStep));
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = (sampleIndex / (float)sampleCount) * MathF.Tau;
                    Vector2 candidate = new(
                        worldPosition.X + (MathF.Cos(angle) * distance),
                        worldPosition.Y + (MathF.Sin(angle) * distance));
                    if (!OverlapsTerrainAtWorldPosition(candidate, clearanceRadius))
                    {
                        return candidate;
                    }
                }
            }

            return worldPosition;
        }

        internal static void SetTerrainSpawnRelocationCount(int relocationCount)
        {
            _terrainSpawnRelocationCount = Math.Max(0, relocationCount);
        }

        internal static void ResolveDynamicTerrainIntrusions(IReadOnlyList<GameObject> gameObjects)
        {
            _terrainCollisionIntrusionCorrectionCount = 0;

            if (gameObjects == null || gameObjects.Count == 0)
            {
                return;
            }

            LoadSettingsIfNeeded();

            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject == null ||
                    !gameObject.DynamicPhysics ||
                    !gameObject.IsCollidable ||
                    gameObject.Shape == null)
                {
                    continue;
                }

                if (!OverlapsTerrainAtWorldPosition(gameObject.Position, gameObject.BoundingRadius) ||
                    !OverlapsTerrainAtCollisionHull(gameObject, gameObject.Position))
                {
                    continue;
                }

                float escapeProbeRadius = ResolveTerrainEscapeProbeRadius(gameObject);
                Vector2 resolvedPosition = ResolveTerrainIntrusionPosition(
                    gameObject,
                    gameObject.Position,
                    gameObject.PreviousPosition,
                    escapeProbeRadius);
                if (Vector2.DistanceSquared(resolvedPosition, gameObject.Position) < 0.25f)
                {
                    continue;
                }

                Vector2 originalPosition = gameObject.Position;
                Vector2 terrainEscapeDirection = ResolveTerrainCollisionEscapeDirection(
                    originalPosition,
                    resolvedPosition,
                    escapeProbeRadius);
                Vector2 reflectedPhysicsVelocity = ResolveTerrainCollisionPhysicsVelocity(
                    gameObject,
                    terrainEscapeDirection);

                gameObject.Position = resolvedPosition;
                gameObject.PreviousPosition = resolvedPosition;
                gameObject.PhysicsVelocity = reflectedPhysicsVelocity;
                if (gameObject is Agent agent)
                {
                    agent.MovementVelocity = RemoveTerrainInwardVelocity(
                        agent.MovementVelocity,
                        terrainEscapeDirection);
                }

                _terrainCollisionIntrusionCorrectionCount++;
            }
        }

        private static Vector2 ResolveTerrainCollisionEscapeDirection(
            Vector2 originalPosition,
            Vector2 resolvedPosition,
            float clearanceRadiusWorldUnits)
        {
            Vector2 escapeDirection = resolvedPosition - originalPosition;
            if (IsFiniteVector(escapeDirection) && escapeDirection.LengthSquared() > 0.0001f)
            {
                escapeDirection.Normalize();
                return escapeDirection;
            }

            escapeDirection = EstimateTerrainEscapeDirection(originalPosition, clearanceRadiusWorldUnits);
            return IsFiniteVector(escapeDirection) && escapeDirection.LengthSquared() > 0.0001f
                ? Vector2.Normalize(escapeDirection)
                : Vector2.Zero;
        }

        private static Vector2 ResolveTerrainCollisionPhysicsVelocity(
            GameObject gameObject,
            Vector2 terrainEscapeDirection)
        {
            Vector2 currentPhysicsVelocity = gameObject?.PhysicsVelocity ?? Vector2.Zero;
            if (!IsFiniteVector(currentPhysicsVelocity))
            {
                currentPhysicsVelocity = Vector2.Zero;
            }

            if (gameObject == null ||
                terrainEscapeDirection == Vector2.Zero ||
                Core.DELTATIME <= 0f)
            {
                return currentPhysicsVelocity;
            }

            float currentOutwardSpeed = Vector2.Dot(currentPhysicsVelocity, terrainEscapeDirection);
            if (currentOutwardSpeed > 0.0001f)
            {
                return currentPhysicsVelocity;
            }

            Vector2 frameVelocity = (gameObject.Position - gameObject.PreviousPosition) / Core.DELTATIME;
            if (!IsFiniteVector(frameVelocity))
            {
                return currentPhysicsVelocity;
            }

            float inwardSpeed = Vector2.Dot(frameVelocity, terrainEscapeDirection);
            if (inwardSpeed >= -1f)
            {
                return currentPhysicsVelocity;
            }

            // Match the regular static-wall impulse model: input movement remains separate,
            // so fallback terrain collision uses the same momentum-transfer coefficient.
            float targetOutwardSpeed = -CollisionResolver.CollisionBounceMomentumTransfer * inwardSpeed;
            if (targetOutwardSpeed <= 0.0001f)
            {
                return currentPhysicsVelocity;
            }

            return currentPhysicsVelocity + (terrainEscapeDirection * targetOutwardSpeed);
        }

        private static Vector2 RemoveTerrainInwardVelocity(
            Vector2 velocity,
            Vector2 terrainEscapeDirection)
        {
            if (!IsFiniteVector(velocity) ||
                terrainEscapeDirection == Vector2.Zero)
            {
                return Vector2.Zero;
            }

            float inwardSpeed = Vector2.Dot(velocity, terrainEscapeDirection);
            return inwardSpeed < 0f
                ? velocity - (terrainEscapeDirection * inwardSpeed)
                : velocity;
        }

        internal static bool TryBuildTerrainInspectionSnapshot(
            Vector2 worldPosition,
            bool requireTerrainHit,
            out TerrainInspectionSnapshot snapshot)
        {
            snapshot = default;
            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();

            if (requireTerrainHit)
            {
                if (!FogOfWarManager.IsWorldPositionVisible(worldPosition, visibleRadiusPadding: 2f) ||
                    !OverlapsTerrainAtWorldPosition(worldPosition, clearanceRadiusWorldUnits: 1f))
                {
                    return false;
                }
            }

            bool boundary = OverlapsTerrainWorldBoundary(worldPosition, clearanceRadiusWorldUnits: 0f);
            float fieldValue = boundary ? float.NaN : SampleTerrainFieldAtWorldPosition(worldPosition);
            bool isLand = boundary || fieldValue > SeaLevel || OverlapsTerrainAtWorldPosition(worldPosition, clearanceRadiusWorldUnits: 1f);

            snapshot = new TerrainInspectionSnapshot(
                worldPosition,
                boundary,
                isLand,
                fieldValue,
                ResolveTerrainColor(),
                TerrainWorldSeed,
                TerrainResidentChunkCount,
                TerrainResidentComponentCount,
                TerrainResidentEdgeLoopCount,
                TerrainResidentColliderCount,
                TerrainActiveColliderCount,
                TerrainResidentVisualTriangleCount,
                TerrainPendingChunkCount,
                TerrainColliderActivationCandidateCount,
                TerrainChunkBuildsInFlight,
                TerrainMaterializationInFlight,
                TerrainMaterializationRestartPending,
                TerrainLastMaterializationMilliseconds,
                TerrainChunkWorldSize,
                TerrainFeatureWorldScaleMultiplier,
                TerrainPreloadMarginWorldUnits,
                TerrainOctogonalCornerCutCellRatio,
                TerrainWorldBoundsSummary,
                TerrainSeedAnchor,
                TerrainCenterChunk,
                TerrainVisibleChunkWindow,
                TerrainColliderChunkWindow);
            return true;
        }

        private static bool OverlapsTerrainAtWorldPosition(Vector2 worldPosition, float clearanceRadiusWorldUnits)
        {
            EnsureTerrainWorldBoundsInitialized();
            if (OverlapsTerrainWorldBoundary(worldPosition, clearanceRadiusWorldUnits))
            {
                return true;
            }

            if (ResidentTerrainCollisionLoops.Count > 0)
            {
                return OverlapsResidentTerrainGeometry(worldPosition, clearanceRadiusWorldUnits);
            }

            return OverlapsTerrainFieldAtWorldPosition(worldPosition, clearanceRadiusWorldUnits);
        }

        private static bool OverlapsTerrainFieldAtWorldPosition(Vector2 worldPosition, float clearanceRadiusWorldUnits)
        {
            if (IsTerrainLandAtWorldPosition(worldPosition))
            {
                return true;
            }

            if (clearanceRadiusWorldUnits <= 1f)
            {
                return false;
            }

            int sampleCount = Math.Max(8, (int)MathF.Ceiling(MathF.Tau * clearanceRadiusWorldUnits / 28f));
            float innerRadius = clearanceRadiusWorldUnits * 0.6f;
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float angle = (sampleIndex / (float)sampleCount) * MathF.Tau;
                Vector2 outerSample = new(
                    worldPosition.X + (MathF.Cos(angle) * clearanceRadiusWorldUnits),
                    worldPosition.Y + (MathF.Sin(angle) * clearanceRadiusWorldUnits));
                if (IsTerrainLandAtWorldPosition(outerSample))
                {
                    return true;
                }

                Vector2 innerSample = new(
                    worldPosition.X + (MathF.Cos(angle) * innerRadius),
                    worldPosition.Y + (MathF.Sin(angle) * innerRadius));
                if (IsTerrainLandAtWorldPosition(innerSample))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsResidentTerrainGeometry(Vector2 worldPosition, float clearanceRadiusWorldUnits)
        {
            float radius = MathF.Max(0f, clearanceRadiusWorldUnits);
            float radiusSq = radius * radius;
            for (int loopIndex = 0; loopIndex < ResidentTerrainCollisionLoops.Count; loopIndex++)
            {
                TerrainCollisionLoopRecord loop = ResidentTerrainCollisionLoops[loopIndex];
                if (loop?.Points == null ||
                    loop.Points.Count < 3 ||
                    !loop.Bounds.Intersects(
                        worldPosition.X - radius,
                        worldPosition.X + radius,
                        worldPosition.Y - radius,
                        worldPosition.Y + radius))
                {
                    continue;
                }

                if (PointInsidePolygon(worldPosition, loop.Points))
                {
                    return true;
                }

                if (radius <= 1f)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < loop.Points.Count; pointIndex++)
                {
                    Vector2 start = loop.Points[pointIndex];
                    Vector2 end = loop.Points[(pointIndex + 1) % loop.Points.Count];
                    if (DistancePointToSegmentSquared(worldPosition, start, end) <= radiusSq)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool OverlapsTerrainAtCollisionHull(GameObject gameObject, Vector2 worldPosition)
        {
            if (gameObject?.Shape == null || !IsFiniteVector(worldPosition))
            {
                return false;
            }

            if (OverlapsTerrainAtWorldPosition(worldPosition, clearanceRadiusWorldUnits: 0f))
            {
                return true;
            }

            Vector2[] vertices;
            try
            {
                vertices = gameObject.Shape.GetTransformedVertices(worldPosition, gameObject.Rotation);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning(
                    $"Terrain collision hull probe failed for ID={gameObject.ID}, Name={gameObject.Name}: {ex.Message}");
                return OverlapsTerrainAtWorldPosition(worldPosition, ResolveTerrainEscapeProbeRadius(gameObject));
            }

            if (vertices == null || vertices.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                if (OverlapsTerrainAtWorldPosition(vertices[i], clearanceRadiusWorldUnits: 0f))
                {
                    return true;
                }
            }

            return TerrainCollisionHullEdgesOverlapTerrain(vertices);
        }

        private static bool TerrainCollisionHullEdgesOverlapTerrain(Vector2[] vertices)
        {
            int vertexCount = vertices?.Length ?? 0;
            if (vertexCount < 2)
            {
                return false;
            }

            for (int i = 0; i < vertexCount; i++)
            {
                Vector2 start = vertices[i];
                Vector2 end = vertices[(i + 1) % vertexCount];
                Vector2 segment = end - start;
                float segmentLength = segment.Length();
                if (!float.IsFinite(segmentLength) || segmentLength <= TerrainCollisionHullSampleSpacingWorldUnits)
                {
                    continue;
                }

                int interiorSampleCount = Math.Max(
                    1,
                    (int)MathF.Ceiling(segmentLength / TerrainCollisionHullSampleSpacingWorldUnits) - 1);
                for (int sampleIndex = 1; sampleIndex <= interiorSampleCount; sampleIndex++)
                {
                    float t = sampleIndex / (float)(interiorSampleCount + 1);
                    Vector2 sample = Vector2.Lerp(start, end, t);
                    if (OverlapsTerrainAtWorldPosition(sample, clearanceRadiusWorldUnits: 0f))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float ResolveTerrainEscapeProbeRadius(GameObject gameObject)
        {
            if (gameObject?.Shape == null)
            {
                return 12f;
            }

            float radius = 0f;
            Vector2[] vertices = gameObject.Shape.GetTransformedVertices(Vector2.Zero, gameObject.Rotation);
            for (int i = 0; i < vertices.Length; i++)
            {
                float distanceSq = vertices[i].LengthSquared();
                if (float.IsFinite(distanceSq) && distanceSq > radius * radius)
                {
                    radius = MathF.Sqrt(distanceSq);
                }
            }

            return MathF.Max(12f, radius);
        }

        private static byte SampleTerrainMaskAtWorldPosition(float worldX, float worldY)
        {
            if (!TerrainWorldContainsPoint(worldX, worldY))
            {
                return Land;
            }

            float terrainX = ResolveTerrainCentifootX(worldX);
            float terrainY = ResolveTerrainCentifootY(worldY);
            return WorldField(terrainX, terrainY, _terrainWorldSeed) > SeaLevel ? Land : Water;
        }

        private static bool IsTerrainLandAtWorldPosition(Vector2 worldPosition)
        {
            return SampleTerrainMaskAtWorldPosition(worldPosition.X, worldPosition.Y) == Land;
        }

        private static Vector2 ResolveTerrainIntrusionPosition(
            GameObject gameObject,
            Vector2 worldPosition,
            Vector2 previousWorldPosition,
            float clearanceRadiusWorldUnits)
        {
            if (!OverlapsTerrainAtCollisionHull(gameObject, worldPosition))
            {
                return worldPosition;
            }

            if (!OverlapsTerrainAtCollisionHull(gameObject, previousWorldPosition) &&
                Vector2.DistanceSquared(worldPosition, previousWorldPosition) > 0.25f)
            {
                Vector2 safePosition = previousWorldPosition;
                Vector2 blockedPosition = worldPosition;
                for (int iteration = 0; iteration < 14; iteration++)
                {
                    Vector2 midpoint = Vector2.Lerp(safePosition, blockedPosition, 0.5f);
                    if (OverlapsTerrainAtCollisionHull(gameObject, midpoint))
                    {
                        blockedPosition = midpoint;
                    }
                    else
                    {
                        safePosition = midpoint;
                    }
                }

                Vector2 retreatDirection = safePosition - blockedPosition;
                if (retreatDirection.LengthSquared() <= 0.0001f)
                {
                    retreatDirection = EstimateTerrainEscapeDirection(worldPosition, clearanceRadiusWorldUnits);
                }
                else
                {
                    retreatDirection.Normalize();
                }

                Vector2 edgeResolvedPosition = AdvanceCollisionHullOutOfTerrain(
                    gameObject,
                    safePosition,
                    retreatDirection,
                    clearanceRadiusWorldUnits,
                    MathF.Max(2f, clearanceRadiusWorldUnits * 0.12f),
                    18);
                if (!OverlapsTerrainAtCollisionHull(gameObject, edgeResolvedPosition))
                {
                    return edgeResolvedPosition;
                }
            }

            Vector2 escapeDirection = EstimateTerrainEscapeDirection(worldPosition, clearanceRadiusWorldUnits);
            if (escapeDirection != Vector2.Zero)
            {
                Vector2 projectedPosition = AdvanceCollisionHullOutOfTerrain(
                    gameObject,
                    worldPosition,
                    escapeDirection,
                    clearanceRadiusWorldUnits,
                    MathF.Max(2f, clearanceRadiusWorldUnits * 0.12f),
                    48);
                if (!OverlapsTerrainAtCollisionHull(gameObject, projectedPosition))
                {
                    return projectedPosition;
                }
            }

            return ResolveNearestTerrainFreeCollisionHullPosition(
                gameObject,
                worldPosition,
                clearanceRadiusWorldUnits,
                MathF.Max(180f, clearanceRadiusWorldUnits * 7f));
        }

        private static Vector2 EstimateTerrainEscapeDirection(Vector2 worldPosition, float clearanceRadiusWorldUnits)
        {
            if (TryEstimateTerrainWorldBoundaryEscapeDirection(
                worldPosition,
                clearanceRadiusWorldUnits,
                out Vector2 boundaryDirection))
            {
                return boundaryDirection;
            }

            if (TryEstimateResidentTerrainEscapeDirection(
                worldPosition,
                clearanceRadiusWorldUnits,
                out Vector2 residentGeometryDirection))
            {
                return residentGeometryDirection;
            }

            Vector2 accumulatedDirection = Vector2.Zero;

            Vector2 fieldGradient = SampleTerrainFieldGradient(worldPosition, MathF.Max(2f, clearanceRadiusWorldUnits * 0.18f));
            if (fieldGradient.LengthSquared() > 0.0001f)
            {
                accumulatedDirection -= Vector2.Normalize(fieldGradient) * 2f;
            }

            if (clearanceRadiusWorldUnits > 1f)
            {
                int sampleCount = Math.Max(12, (int)MathF.Ceiling(MathF.Tau * clearanceRadiusWorldUnits / 16f));
                float innerRadius = clearanceRadiusWorldUnits * 0.55f;
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = (sampleIndex / (float)sampleCount) * MathF.Tau;
                    Vector2 radialDirection = new(MathF.Cos(angle), MathF.Sin(angle));

                    Vector2 outerSample = worldPosition + (radialDirection * clearanceRadiusWorldUnits);
                    if (IsTerrainLandAtWorldPosition(outerSample))
                    {
                        accumulatedDirection -= radialDirection;
                    }

                    Vector2 innerSample = worldPosition + (radialDirection * innerRadius);
                    if (IsTerrainLandAtWorldPosition(innerSample))
                    {
                        accumulatedDirection -= radialDirection * 0.5f;
                    }
                }
            }

            if (accumulatedDirection.LengthSquared() <= 0.0001f)
            {
                return Vector2.Zero;
            }

            accumulatedDirection.Normalize();
            return accumulatedDirection;
        }

        private static bool TryEstimateResidentTerrainEscapeDirection(
            Vector2 worldPosition,
            float clearanceRadiusWorldUnits,
            out Vector2 direction)
        {
            direction = Vector2.Zero;
            if (ResidentTerrainCollisionLoops.Count == 0)
            {
                return false;
            }

            float radius = MathF.Max(0f, clearanceRadiusWorldUnits);
            float radiusSq = radius * radius;
            float bestDistanceSq = float.MaxValue;
            Vector2 bestDirection = Vector2.Zero;

            for (int loopIndex = 0; loopIndex < ResidentTerrainCollisionLoops.Count; loopIndex++)
            {
                TerrainCollisionLoopRecord loop = ResidentTerrainCollisionLoops[loopIndex];
                if (loop?.Points == null ||
                    loop.Points.Count < 3 ||
                    !loop.Bounds.Intersects(
                        worldPosition.X - radius,
                        worldPosition.X + radius,
                        worldPosition.Y - radius,
                        worldPosition.Y + radius))
                {
                    continue;
                }

                bool inside = PointInsidePolygon(worldPosition, loop.Points);
                bool overlapping = inside;
                for (int pointIndex = 0; pointIndex < loop.Points.Count; pointIndex++)
                {
                    Vector2 start = loop.Points[pointIndex];
                    Vector2 end = loop.Points[(pointIndex + 1) % loop.Points.Count];
                    Vector2 nearest = NearestPointOnSegment(worldPosition, start, end);
                    float distanceSq = Vector2.DistanceSquared(worldPosition, nearest);
                    if (!inside && distanceSq > radiusSq)
                    {
                        continue;
                    }

                    overlapping = true;
                    if (distanceSq >= bestDistanceSq)
                    {
                        continue;
                    }

                    Vector2 candidateDirection = inside
                        ? nearest - worldPosition
                        : worldPosition - nearest;
                    if (candidateDirection.LengthSquared() <= 0.0001f)
                    {
                        continue;
                    }

                    bestDistanceSq = distanceSq;
                    bestDirection = candidateDirection;
                }

                if (!overlapping)
                {
                    continue;
                }
            }

            if (bestDirection.LengthSquared() <= 0.0001f)
            {
                return false;
            }

            direction = Vector2.Normalize(bestDirection);
            return true;
        }

        private static bool OverlapsTerrainWorldBoundary(Vector2 worldPosition, float clearanceRadiusWorldUnits)
        {
            float radius = MathF.Max(0f, clearanceRadiusWorldUnits);
            return worldPosition.X - radius < _terrainWorldBounds.MinX ||
                worldPosition.X + radius > _terrainWorldBounds.MaxX ||
                worldPosition.Y - radius < _terrainWorldBounds.MinY ||
                worldPosition.Y + radius > _terrainWorldBounds.MaxY;
        }

        private static bool TryEstimateTerrainWorldBoundaryEscapeDirection(
            Vector2 worldPosition,
            float clearanceRadiusWorldUnits,
            out Vector2 direction)
        {
            direction = Vector2.Zero;
            if (!OverlapsTerrainWorldBoundary(worldPosition, clearanceRadiusWorldUnits))
            {
                return false;
            }

            float radius = MathF.Max(0f, clearanceRadiusWorldUnits);
            if (worldPosition.X - radius < _terrainWorldBounds.MinX)
            {
                direction.X += 1f;
            }
            if (worldPosition.X + radius > _terrainWorldBounds.MaxX)
            {
                direction.X -= 1f;
            }
            if (worldPosition.Y - radius < _terrainWorldBounds.MinY)
            {
                direction.Y += 1f;
            }
            if (worldPosition.Y + radius > _terrainWorldBounds.MaxY)
            {
                direction.Y -= 1f;
            }

            if (direction.LengthSquared() <= 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }

        private static Vector2 SampleTerrainFieldGradient(Vector2 worldPosition, float sampleOffsetWorldUnits)
        {
            float offset = MathF.Max(1f, sampleOffsetWorldUnits);
            float sampleRight = SampleTerrainFieldAtWorldPosition(new Vector2(worldPosition.X + offset, worldPosition.Y));
            float sampleLeft = SampleTerrainFieldAtWorldPosition(new Vector2(worldPosition.X - offset, worldPosition.Y));
            float sampleDown = SampleTerrainFieldAtWorldPosition(new Vector2(worldPosition.X, worldPosition.Y + offset));
            float sampleUp = SampleTerrainFieldAtWorldPosition(new Vector2(worldPosition.X, worldPosition.Y - offset));
            return new Vector2(sampleRight - sampleLeft, sampleDown - sampleUp);
        }

        private static float SampleTerrainFieldAtWorldPosition(Vector2 worldPosition)
        {
            float terrainX = ResolveTerrainCentifootX(worldPosition.X);
            float terrainY = ResolveTerrainCentifootY(worldPosition.Y);
            return WorldField(terrainX, terrainY, _terrainWorldSeed);
        }

        private static Vector2 AdvanceCollisionHullOutOfTerrain(
            GameObject gameObject,
            Vector2 startPosition,
            Vector2 direction,
            float clearanceRadiusWorldUnits,
            float stepWorldUnits,
            int maxIterations)
        {
            if (direction.LengthSquared() <= 0.0001f)
            {
                return startPosition;
            }

            direction.Normalize();
            Vector2 candidate = startPosition;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                if (!OverlapsTerrainAtCollisionHull(gameObject, candidate))
                {
                    return candidate;
                }

                candidate += direction * stepWorldUnits;
            }

            return candidate;
        }

        private static Vector2 ResolveNearestTerrainFreeCollisionHullPosition(
            GameObject gameObject,
            Vector2 worldPosition,
            float clearanceRadiusWorldUnits,
            float maxSearchDistanceWorldUnits)
        {
            LoadSettingsIfNeeded();

            if (!OverlapsTerrainAtCollisionHull(gameObject, worldPosition))
            {
                return worldPosition;
            }

            float radialStep = MathF.Max(12f, clearanceRadiusWorldUnits * 0.35f);
            for (float distance = radialStep; distance <= maxSearchDistanceWorldUnits; distance += radialStep)
            {
                int sampleCount = Math.Max(12, (int)MathF.Ceiling(MathF.Tau * distance / radialStep));
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = (sampleIndex / (float)sampleCount) * MathF.Tau;
                    Vector2 candidate = new(
                        worldPosition.X + (MathF.Cos(angle) * distance),
                        worldPosition.Y + (MathF.Sin(angle) * distance));
                    if (!OverlapsTerrainAtCollisionHull(gameObject, candidate))
                    {
                        return candidate;
                    }
                }
            }

            return worldPosition;
        }

        private static void DrawResidentTerrainVisuals(
            SpriteBatch spriteBatch,
            Rectangle panelBounds,
            Matrix cameraTransform,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (spriteBatch == null || ResidentTerrainVisualObjects.Count == 0)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            EnsureTerrainVectorEffect(graphicsDevice);
            if (_terrainVectorEffect == null)
            {
                return;
            }

            Color terrainColor = ResolveTerrainColor();
            ApplyTerrainColorToResidentVertices(terrainColor);

            Rectangle viewportBounds = graphicsDevice.Viewport.Bounds;
            Rectangle clippedPanelBounds = Rectangle.Intersect(panelBounds, viewportBounds);
            if (clippedPanelBounds.Width <= 0 || clippedPanelBounds.Height <= 0)
            {
                return;
            }

            Rectangle previousScissor = graphicsDevice.ScissorRectangle;
            Viewport viewport = graphicsDevice.Viewport;
            _terrainVectorEffect.World = cameraTransform;
            _terrainVectorEffect.View = Matrix.Identity;
            _terrainVectorEffect.Projection = Matrix.CreateOrthographicOffCenter(
                0f,
                viewport.Width,
                viewport.Height,
                0f,
                0f,
                1f);

            graphicsDevice.ScissorRectangle = clippedPanelBounds;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState = TerrainVectorRasterizerState;

            foreach (EffectPass pass in _terrainVectorEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                for (int i = 0; i < ResidentTerrainVisualObjects.Count; i++)
                {
                    TerrainVisualObjectRecord record = ResidentTerrainVisualObjects[i];
                    if (record == null ||
                        record.FillVertices == null ||
                        record.FillPrimitiveCount <= 0 ||
                        !record.Bounds.Intersects(minX, maxX, minY, maxY))
                    {
                        continue;
                    }

                    graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        record.FillVertices,
                        0,
                        record.FillPrimitiveCount);
                }
            }

            graphicsDevice.ScissorRectangle = previousScissor;
        }

        private static void DrawTerrainWorldBoundaryFill(
            SpriteBatch spriteBatch,
            Rectangle panelBounds,
            Matrix cameraTransform,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (spriteBatch == null)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            EnsureTerrainVectorEffect(graphicsDevice);
            if (_terrainVectorEffect == null)
            {
                return;
            }

            Color terrainColor = ResolveTerrainColor();
            int vertexCount = BuildTerrainBoundaryFillVertices(minX, maxX, minY, maxY, terrainColor);
            if (vertexCount <= 0)
            {
                return;
            }

            Rectangle viewportBounds = graphicsDevice.Viewport.Bounds;
            Rectangle clippedPanelBounds = Rectangle.Intersect(panelBounds, viewportBounds);
            if (clippedPanelBounds.Width <= 0 || clippedPanelBounds.Height <= 0)
            {
                return;
            }

            Rectangle previousScissor = graphicsDevice.ScissorRectangle;
            Viewport viewport = graphicsDevice.Viewport;
            _terrainVectorEffect.World = cameraTransform;
            _terrainVectorEffect.View = Matrix.Identity;
            _terrainVectorEffect.Projection = Matrix.CreateOrthographicOffCenter(
                0f,
                viewport.Width,
                viewport.Height,
                0f,
                0f,
                1f);

            graphicsDevice.ScissorRectangle = clippedPanelBounds;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState = TerrainVectorRasterizerState;

            foreach (EffectPass pass in _terrainVectorEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    TerrainBoundaryFillVertices,
                    0,
                    vertexCount / 3);
            }

            graphicsDevice.ScissorRectangle = previousScissor;
        }

        private static Color ResolveTerrainColor()
        {
            AmbienceSettings.Initialize();
            return AmbienceSettings.TerrainColor;
        }

        private static void ApplyTerrainColorToResidentVertices(Color terrainColor)
        {
            uint packedColor = terrainColor.PackedValue;
            if (_residentTerrainVertexColorValid && _lastAppliedTerrainColorPacked == packedColor)
            {
                return;
            }

            for (int recordIndex = 0; recordIndex < ResidentTerrainVisualObjects.Count; recordIndex++)
            {
                VertexPositionColor[] vertices = ResidentTerrainVisualObjects[recordIndex]?.FillVertices;
                if (vertices == null)
                {
                    continue;
                }

                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    vertices[vertexIndex].Color = terrainColor;
                }
            }

            _lastAppliedTerrainColorPacked = packedColor;
            _residentTerrainVertexColorValid = true;
        }

        private static int BuildTerrainBoundaryFillVertices(
            float minX,
            float maxX,
            float minY,
            float maxY,
            Color terrainColor)
        {
            EnsureTerrainWorldBoundsInitialized();

            int vertexCount = 0;
            TerrainWorldBounds bounds = _terrainWorldBounds;

            AppendTerrainFillRectangle(minX, minY, MathF.Min(maxX, bounds.MinX), maxY, terrainColor, ref vertexCount);
            AppendTerrainFillRectangle(MathF.Max(minX, bounds.MaxX), minY, maxX, maxY, terrainColor, ref vertexCount);

            float innerLeft = MathF.Max(minX, bounds.MinX);
            float innerRight = MathF.Min(maxX, bounds.MaxX);
            if (innerRight > innerLeft)
            {
                AppendTerrainFillRectangle(innerLeft, minY, innerRight, MathF.Min(maxY, bounds.MinY), terrainColor, ref vertexCount);
                AppendTerrainFillRectangle(innerLeft, MathF.Max(minY, bounds.MaxY), innerRight, maxY, terrainColor, ref vertexCount);
            }

            return vertexCount;
        }

        private static void AppendTerrainFillRectangle(
            float left,
            float top,
            float right,
            float bottom,
            Color color,
            ref int vertexCount)
        {
            if (right <= left || bottom <= top || vertexCount + 6 > TerrainBoundaryFillVertices.Length)
            {
                return;
            }

            TerrainBoundaryFillVertices[vertexCount++] = new VertexPositionColor(new Vector3(left, top, 0f), color);
            TerrainBoundaryFillVertices[vertexCount++] = new VertexPositionColor(new Vector3(right, top, 0f), color);
            TerrainBoundaryFillVertices[vertexCount++] = new VertexPositionColor(new Vector3(right, bottom, 0f), color);
            TerrainBoundaryFillVertices[vertexCount++] = new VertexPositionColor(new Vector3(left, top, 0f), color);
            TerrainBoundaryFillVertices[vertexCount++] = new VertexPositionColor(new Vector3(right, bottom, 0f), color);
            TerrainBoundaryFillVertices[vertexCount++] = new VertexPositionColor(new Vector3(left, bottom, 0f), color);
        }

        private static void UpdateActiveTerrainColliders(float minX, float maxX, float minY, float maxY)
        {
            _ = minX;
            _ = maxX;
            _ = minY;
            _ = maxY;

            if (Core.Instance?.GameObjects == null || Core.Instance.StaticObjects == null)
            {
                DeactivateActiveTerrainColliders();
                _activeTerrainColliderCount = 0;
                _terrainColliderActivationCandidateCount = 0;
                return;
            }

            DesiredTerrainColliderRecords.Clear();

            List<GameObject> gameObjects = Core.Instance.GameObjects;
            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject == null ||
                    !gameObject.DynamicPhysics ||
                    !gameObject.IsCollidable ||
                    gameObject.Shape == null)
                {
                    continue;
                }

                if (!TryBuildTerrainActivationBounds(
                    gameObject.Position,
                    gameObject.BoundingRadius,
                    ResolveTerrainActivationVelocity(gameObject),
                    out TerrainWorldBounds activationBounds))
                {
                    continue;
                }

                CollectTerrainColliderRecordsInBounds(activationBounds, DesiredTerrainColliderRecords);
            }

            IReadOnlyList<Bullet> bullets = BulletManager.GetBullets();
            for (int i = 0; i < bullets.Count; i++)
            {
                Bullet bullet = bullets[i];
                if (bullet == null ||
                    bullet.IsDying ||
                    bullet.IsBarrelLocked ||
                    bullet.Shape == null)
                {
                    continue;
                }

                if (!TryBuildTerrainActivationBounds(
                    bullet.Position,
                    bullet.BoundingRadius,
                    bullet.Velocity,
                    out TerrainWorldBounds activationBounds))
                {
                    continue;
                }

                CollectTerrainColliderRecordsInBounds(activationBounds, DesiredTerrainColliderRecords);
            }

            foreach (TerrainColliderObjectRecord record in DesiredTerrainColliderRecords)
            {
                if (!record.IsCollisionActive)
                {
                    SetTerrainColliderActive(record, true);
                }
            }

            TerrainColliderDeactivateScratch.Clear();
            foreach (TerrainColliderObjectRecord record in ActiveTerrainColliderRecords)
            {
                if (!DesiredTerrainColliderRecords.Contains(record))
                {
                    TerrainColliderDeactivateScratch.Add(record);
                }
            }

            for (int i = 0; i < TerrainColliderDeactivateScratch.Count; i++)
            {
                SetTerrainColliderActive(TerrainColliderDeactivateScratch[i], false);
            }

            _terrainColliderActivationCandidateCount = DesiredTerrainColliderRecords.Count;
            _activeTerrainColliderCount = ActiveTerrainColliderRecords.Count;
            DesiredTerrainColliderRecords.Clear();
            TerrainColliderDeactivateScratch.Clear();
        }

        private static void SetTerrainColliderActive(TerrainColliderObjectRecord record, bool isActive)
        {
            if (record == null || Core.Instance?.GameObjects == null || Core.Instance.StaticObjects == null)
            {
                return;
            }

            if (isActive)
            {
                GameObject colliderObject = record.EnsureColliderObjectCreated(CreateTerrainColliderObject);
                if (colliderObject == null)
                {
                    return;
                }

                if (!Core.Instance.StaticObjects.Contains(colliderObject))
                {
                    Core.Instance.StaticObjects.Add(colliderObject);
                }

                if (!Core.Instance.GameObjects.Contains(colliderObject))
                {
                    Core.Instance.GameObjects.Add(colliderObject);
                }
            }
            else
            {
                GameObject colliderObject = record.ColliderObject;
                if (colliderObject != null)
                {
                    Core.Instance.StaticObjects.Remove(colliderObject);
                    Core.Instance.GameObjects.Remove(colliderObject);
                }
            }

            record.IsCollisionActive = isActive;
            if (isActive)
            {
                ActiveTerrainColliderRecords.Add(record);
            }
            else
            {
                ActiveTerrainColliderRecords.Remove(record);
            }
        }

        private static void DeactivateActiveTerrainColliders()
        {
            TerrainColliderDeactivateScratch.Clear();
            foreach (TerrainColliderObjectRecord record in ActiveTerrainColliderRecords)
            {
                TerrainColliderDeactivateScratch.Add(record);
            }

            for (int i = 0; i < TerrainColliderDeactivateScratch.Count; i++)
            {
                SetTerrainColliderActive(TerrainColliderDeactivateScratch[i], false);
            }

            TerrainColliderDeactivateScratch.Clear();
        }

        private static Vector2 ResolveTerrainActivationVelocity(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return Vector2.Zero;
            }

            Vector2 velocity = gameObject.PhysicsVelocity;
            if (gameObject is Agent agent)
            {
                velocity += agent.MovementVelocity;
            }

            return IsFiniteVector(velocity) ? velocity : Vector2.Zero;
        }

        private static bool TryBuildTerrainActivationBounds(
            Vector2 position,
            float radius,
            Vector2 velocity,
            out TerrainWorldBounds bounds)
        {
            bounds = default;
            if (!IsFiniteVector(position))
            {
                return false;
            }

            float resolvedRadius = MathF.Max(2f, radius);
            if (!float.IsFinite(resolvedRadius))
            {
                return false;
            }

            float velocityLead = IsFiniteVector(velocity)
                ? MathF.Min(ChunkWorldSize, velocity.Length() * TerrainCollisionVelocityLeadSeconds)
                : 0f;
            float probeRadius = resolvedRadius + TerrainCollisionDynamicProbeMarginWorldUnits + velocityLead;
            bounds = new TerrainWorldBounds(
                position.X - probeRadius,
                position.Y - probeRadius,
                probeRadius * 2f,
                probeRadius * 2f);
            return true;
        }

        private static void CollectTerrainColliderRecordsInBounds(
            TerrainWorldBounds bounds,
            HashSet<TerrainColliderObjectRecord> output)
        {
            if (output == null ||
                !TryGetTerrainColliderRecordCellRange(bounds, out int minCellX, out int maxCellX, out int minCellY, out int maxCellY))
            {
                return;
            }

            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long cellKey = ComposeTerrainColliderCellKey(cellX, cellY);
                    if (!TerrainColliderRecordCells.TryGetValue(cellKey, out List<TerrainColliderObjectRecord> records))
                    {
                        continue;
                    }

                    for (int i = 0; i < records.Count; i++)
                    {
                        TerrainColliderObjectRecord record = records[i];
                        if (record != null &&
                            record.Bounds.Intersects(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY))
                        {
                            output.Add(record);
                        }
                    }
                }
            }
        }

        private static void RefreshResidentTerrainWorldObjects(
            GraphicsDevice graphicsDevice,
            ChunkBounds materializedChunkWindow,
            ChunkBounds colliderChunkWindow,
            ChunkBounds criticalChunkWindow)
        {
            _ = graphicsDevice;
            TryApplyCompletedTerrainMaterialization();

            if (!_terrainWorldObjectsDirty)
            {
                return;
            }

            if (_terrainMaterializationTask != null)
            {
                return;
            }

            if (Core.Instance?.GameObjects == null || Core.Instance.StaticObjects == null)
            {
                _terrainWorldObjectsDirty = false;
                return;
            }

            if (HasPendingChunkInBounds(criticalChunkWindow))
            {
                return;
            }

            if (!TryBuildCombinedResidentMask(materializedChunkWindow, out CombinedResidentMask residentMask))
            {
                if (HasPendingChunkInBounds(materializedChunkWindow))
                {
                    return;
                }

                StartTerrainMaterialization(
                    new CombinedResidentMask(Array.Empty<byte>(), 0, 0, materializedChunkWindow.MinChunkX, materializedChunkWindow.MinChunkY),
                    BuildChunkWorldBounds(colliderChunkWindow),
                    materializedChunkWindow,
                    colliderChunkWindow);
                return;
            }

            StartTerrainMaterialization(
                residentMask,
                BuildChunkWorldBounds(colliderChunkWindow),
                materializedChunkWindow,
                colliderChunkWindow);
        }

        private static void UpdateStartupVisibleTerrainReadiness(ChunkBounds visibleChunkWindow)
        {
            if (_startupVisibleTerrainReady)
            {
                return;
            }

            if (_terrainPendingCriticalChunkCount > 0)
            {
                _terrainStartupReadinessSummary = $"startup terrain pending: {_terrainPendingCriticalChunkCount} visible chunks still queued";
                return;
            }

            if (_terrainMaterializationTask != null)
            {
                _terrainStartupReadinessSummary = "startup terrain pending: visible terrain materialization in flight";
                return;
            }

            if (_terrainWorldObjectsDirty)
            {
                _terrainStartupReadinessSummary = "startup terrain pending: visible terrain materialization queued";
                return;
            }

            _startupVisibleTerrainReady = true;
            _terrainStartupReadinessSummary = $"startup terrain ready: {FormatChunkBounds(visibleChunkWindow)} visible";
        }

        private static void StartTerrainMaterialization(
            CombinedResidentMask residentMask,
            TerrainWorldBounds colliderWorldBounds,
            ChunkBounds materializedChunkWindow,
            ChunkBounds colliderChunkWindow)
        {
            int requestId = ++_terrainMaterializationRequestId;
            _terrainWorldObjectsDirty = false;
            _terrainMaterializationTask = Task.Run(() => BuildTerrainMaterializationResult(
                residentMask,
                requestId,
                colliderWorldBounds,
                materializedChunkWindow,
                colliderChunkWindow));
        }

        private static void TryApplyCompletedTerrainMaterialization()
        {
            Task<TerrainMaterializationResult> task = _terrainMaterializationTask;
            if (task == null || !task.IsCompleted)
            {
                return;
            }

            _terrainMaterializationTask = null;

            if (task.IsFaulted)
            {
                DebugLogger.PrintWarning($"GameBlockTerrainBackground: terrain materialization failed. {task.Exception?.GetBaseException().Message}");
                _terrainWorldObjectsDirty = true;
                return;
            }

            if (task.IsCanceled)
            {
                _terrainWorldObjectsDirty = true;
                return;
            }

            TerrainMaterializationResult result = task.Result;
            if (_terrainWorldObjectsDirty || !IsTerrainMaterializationResultCurrent(result))
            {
                _terrainDiscardedStaleMaterializationCount++;
                _terrainWorldObjectsDirty = true;
                return;
            }

            ApplyTerrainMaterializationResult(result);
        }

        private static bool IsTerrainMaterializationResultCurrent(TerrainMaterializationResult result)
        {
            return result != null &&
                result.RequestId == _terrainMaterializationRequestId &&
                ChunkBoundsEqual(result.MaterializedChunkWindow, _lastMaterializedChunkWindow) &&
                ChunkBoundsEqual(result.ColliderChunkWindow, _lastTerrainColliderChunkWindow);
        }

        private static TerrainMaterializationResult BuildTerrainMaterializationResult(
            CombinedResidentMask residentMask,
            int requestId)
        {
            ChunkBounds materializedChunkWindow = BuildResidentMaskChunkBounds(residentMask);
            TerrainWorldBounds colliderWorldBounds = new(
                residentMask.MinChunkX * ChunkWorldSize,
                residentMask.MinChunkY * ChunkWorldSize,
                residentMask.Width * (ChunkWorldSize / ChunkTextureResolution),
                residentMask.Height * (ChunkWorldSize / ChunkTextureResolution));
            return BuildTerrainMaterializationResult(
                residentMask,
                requestId,
                colliderWorldBounds,
                materializedChunkWindow,
                materializedChunkWindow);
        }

        private static TerrainMaterializationResult BuildTerrainMaterializationResult(
            CombinedResidentMask residentMask,
            int requestId,
            TerrainWorldBounds colliderWorldBounds)
        {
            ChunkBounds materializedChunkWindow = BuildResidentMaskChunkBounds(residentMask);
            return BuildTerrainMaterializationResult(
                residentMask,
                requestId,
                colliderWorldBounds,
                materializedChunkWindow,
                BuildChunkBounds(
                    colliderWorldBounds.MinX,
                    colliderWorldBounds.MaxX,
                    colliderWorldBounds.MinY,
                    colliderWorldBounds.MaxY));
        }

        private static TerrainMaterializationResult BuildTerrainMaterializationResult(
            CombinedResidentMask residentMask,
            int requestId,
            TerrainWorldBounds colliderWorldBounds,
            ChunkBounds materializedChunkWindow,
            ChunkBounds colliderChunkWindow)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            TerrainMaterializationResult result = new(requestId, materializedChunkWindow, colliderChunkWindow);
            float sampleStepWorldUnits = ChunkWorldSize / ChunkTextureResolution;
            float worldLeft = residentMask.MinChunkX * ChunkWorldSize;
            float worldTop = residentMask.MinChunkY * ChunkWorldSize;

            List<TerrainLoop> singleLoop = new(1);
            List<RefinedTerrainComponent> refinedComponents = BuildRefinedResidentComponents(
                residentMask,
                worldLeft,
                worldTop,
                sampleStepWorldUnits);

            for (int componentIndex = 0; componentIndex < refinedComponents.Count; componentIndex++)
            {
                RefinedTerrainComponent component = refinedComponents[componentIndex];
                if (component == null ||
                    component.Mask.Length == 0 ||
                    component.Width <= 0 ||
                    component.Height <= 0 ||
                    component.SampleStepWorldUnits <= 0f)
                {
                    continue;
                }

                List<TerrainLoop> rawLoops = BuildTerrainLoops(
                    component.Mask,
                    component.Width,
                    component.Height);
                List<TerrainLoop> visualLoops = PrepareTerrainLoops(
                    rawLoops,
                    TerrainVisualLoopSimplifyToleranceCells,
                    includeInnerLoops: false,
                    smoothIterations: TerrainVisualSmoothIterations);

                for (int loopIndex = 0; loopIndex < visualLoops.Count; loopIndex++)
                {
                    TerrainLoop preparedLoop = visualLoops[loopIndex];
                    TerrainLoop loop = NaturalizeTerrainLoop(
                        preparedLoop,
                        component.WorldLeft,
                        component.WorldTop,
                        component.SampleStepWorldUnits);
                    if (loop?.Points == null || loop.Points.Count < 3)
                    {
                        continue;
                    }

                    singleLoop.Clear();
                    singleLoop.Add(loop);
                    TerrainWorldBounds loopBounds = BuildTerrainLoopBounds(
                        loop,
                        component.WorldLeft,
                        component.WorldTop,
                        component.SampleStepWorldUnits);
                    if (!IsTerrainLoopWorldBoundsPlausible(loopBounds))
                    {
                        continue;
                    }

                    VertexPositionColor[] visualVertices = BuildTerrainFillVertices(
                        singleLoop,
                        component.WorldLeft,
                        component.WorldTop,
                        component.SampleStepWorldUnits);
                    if (visualVertices.Length == 0)
                    {
                        continue;
                    }

                    result.VisualObjects.Add(new TerrainVisualObjectRecord(
                        loopBounds,
                        visualVertices));
                    result.CollisionLoops.Add(new TerrainCollisionLoopRecord(
                        loopBounds,
                        BuildTerrainWorldLoop(loop, component.WorldLeft, component.WorldTop, component.SampleStepWorldUnits)));
                    result.ComponentCount++;
                    result.VisualTriangleCount += visualVertices.Length / 3;

                    if (loopBounds.Intersects(
                        colliderWorldBounds.MinX,
                        colliderWorldBounds.MaxX,
                        colliderWorldBounds.MinY,
                        colliderWorldBounds.MaxY))
                    {
                        SpawnTerrainLoopColliders(
                            singleLoop,
                            component.WorldLeft,
                            component.WorldTop,
                            component.SampleStepWorldUnits,
                            result,
                            colliderWorldBounds);
                    }
                }
            }

            AddTerrainWorldBoundaryColliders(result, colliderWorldBounds);

            stopwatch.Stop();
            result.BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private static ChunkBounds BuildResidentMaskChunkBounds(CombinedResidentMask residentMask)
        {
            int chunkSpanX = Math.Max(1, (int)MathF.Ceiling(residentMask.Width / (float)ChunkTextureResolution));
            int chunkSpanY = Math.Max(1, (int)MathF.Ceiling(residentMask.Height / (float)ChunkTextureResolution));
            return new ChunkBounds(
                residentMask.MinChunkX,
                residentMask.MinChunkX + chunkSpanX - 1,
                residentMask.MinChunkY,
                residentMask.MinChunkY + chunkSpanY - 1);
        }

        private static void ApplyTerrainMaterializationResult(TerrainMaterializationResult result)
        {
            if (result == null)
            {
                return;
            }

            ClearResidentTerrainWorldObjects();

            ResidentTerrainVisualObjects.AddRange(result.VisualObjects);
            ResidentTerrainCollisionLoops.AddRange(result.CollisionLoops);
            for (int i = 0; i < result.ColliderObjects.Count; i++)
            {
                TerrainColliderObjectRecord record = result.ColliderObjects[i];
                ResidentTerrainColliderObjects.Add(record);
                AddTerrainColliderRecordToSpatialIndex(record);
            }

            _residentTerrainComponentCount = result.ComponentCount;
            _residentTerrainColliderCount = result.ColliderCount;
            _residentTerrainVisualTriangleCount = result.VisualTriangleCount;
            _lastTerrainMaterializationMilliseconds = result.BuildMilliseconds;
            _residentTerrainVertexColorValid = false;
        }

        private static bool TryBuildCombinedResidentMask(ChunkBounds chunkBounds, out CombinedResidentMask residentMask)
        {
            residentMask = default;

            bool foundLandChunk = false;
            int minChunkX = int.MaxValue;
            int minChunkY = int.MaxValue;
            int maxChunkX = int.MinValue;
            int maxChunkY = int.MinValue;

            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (!chunkBounds.Contains(entry.Key))
                {
                    continue;
                }

                if (!entry.Value.HasLand || entry.Value.LandMask.Length == 0)
                {
                    continue;
                }

                foundLandChunk = true;
                minChunkX = Math.Min(minChunkX, entry.Key.X);
                minChunkY = Math.Min(minChunkY, entry.Key.Y);
                maxChunkX = Math.Max(maxChunkX, entry.Key.X);
                maxChunkY = Math.Max(maxChunkY, entry.Key.Y);
            }

            if (!foundLandChunk)
            {
                return false;
            }

            int maskWidth = ((maxChunkX - minChunkX) + 1) * ChunkTextureResolution;
            int maskHeight = ((maxChunkY - minChunkY) + 1) * ChunkTextureResolution;
            byte[] mask = new byte[maskWidth * maskHeight];

            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (!chunkBounds.Contains(entry.Key))
                {
                    continue;
                }

                TerrainChunkRecord chunk = entry.Value;
                if (!chunk.HasLand || chunk.LandMask.Length == 0)
                {
                    continue;
                }

                int offsetX = (entry.Key.X - minChunkX) * ChunkTextureResolution;
                int offsetY = (entry.Key.Y - minChunkY) * ChunkTextureResolution;

                for (int row = 0; row < ChunkTextureResolution; row++)
                {
                    Array.Copy(
                        chunk.LandMask,
                        row * ChunkTextureResolution,
                        mask,
                        Index(offsetX, offsetY + row, maskWidth),
                        ChunkTextureResolution);
                }
            }

            residentMask = new CombinedResidentMask(mask, maskWidth, maskHeight, minChunkX, minChunkY);
            return true;
        }

        private static void CollectConnectedComponent(
            byte[] mask,
            int width,
            int height,
            int start,
            byte[] visited,
            List<int> queue,
            List<int> componentCells,
            out TerrainComponentBounds bounds)
        {
            queue.Clear();
            componentCells.Clear();
            queue.Add(start);
            visited[start] = 1;

            int minX = start % width;
            int maxX = minX;
            int minY = start / width;
            int maxY = minY;

            for (int queueIndex = 0; queueIndex < queue.Count; queueIndex++)
            {
                int current = queue[queueIndex];
                componentCells.Add(current);

                int x = current % width;
                int y = current / width;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);

                TryEnqueue(x + 1, y);
                TryEnqueue(x - 1, y);
                TryEnqueue(x, y + 1);
                TryEnqueue(x, y - 1);
            }

            bounds = new TerrainComponentBounds(minX, maxX, minY, maxY);

            void TryEnqueue(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                int index = Index(x, y, width);
                if (visited[index] != 0 || mask[index] != Land)
                {
                    return;
                }

                visited[index] = 1;
                queue.Add(index);
            }
        }

        private static List<TerrainLoop> PrepareTerrainLoops(
            IReadOnlyList<TerrainLoop> rawLoops,
            float baseSimplifyToleranceCells,
            bool includeInnerLoops,
            int smoothIterations)
        {
            List<TerrainLoop> preparedLoops = new();
            if (rawLoops == null || rawLoops.Count == 0)
            {
                return preparedLoops;
            }

            for (int loopIndex = 0; loopIndex < rawLoops.Count; loopIndex++)
            {
                TerrainLoop rawLoop = rawLoops[loopIndex];
                if (rawLoop?.Points == null || rawLoop.Points.Count < 3)
                {
                    continue;
                }

                float signedArea = ComputeLoopSignedArea(rawLoop.Points);
                bool isOuterLoop = signedArea > 0f;
                if (!includeInnerLoops && !isOuterLoop)
                {
                    continue;
                }

                if (!IsTerrainLoopLargeEnough(rawLoop.Points))
                {
                    continue;
                }

                float simplifyTolerance = ResolveTerrainLoopSimplifyTolerance(
                    rawLoop.Points.Count,
                    baseSimplifyToleranceCells);
                List<Vector2> simplifiedLoop = SimplifyLoop(rawLoop.Points, simplifyTolerance);
                if (!IsTerrainLoopLargeEnough(simplifiedLoop))
                {
                    continue;
                }

                simplifiedLoop = RemoveSharpTerrainLoopArtifacts(simplifiedLoop);
                if (!IsTerrainLoopLargeEnough(simplifiedLoop))
                {
                    continue;
                }

                int resolvedSmoothIterations = ResolveTerrainLoopSmoothIterations(
                    simplifiedLoop.Count,
                    smoothIterations);
                if (resolvedSmoothIterations > 0)
                {
                    simplifiedLoop = SmoothLoop(simplifiedLoop, resolvedSmoothIterations);
                }

                simplifiedLoop = CollapseCollinearLoop(simplifiedLoop);
                if (simplifiedLoop.Count >= 3 && IsTerrainLoopLargeEnough(simplifiedLoop))
                {
                    preparedLoops.Add(new TerrainLoop(simplifiedLoop));
                }
            }

            return preparedLoops;
        }

        private static TerrainLoop NaturalizeTerrainLoop(
            TerrainLoop loop,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits)
        {
            if (loop?.Points == null ||
                loop.Points.Count < 3 ||
                sampleStepWorldUnits <= 0f ||
                loop.Points.Count > TerrainNaturalCoastMaxPointCount)
            {
                return loop;
            }

            List<Vector2> densified = DensifyTerrainLoop(loop.Points, TerrainNaturalCoastMaxSegmentCells);
            if (densified.Count < 3 ||
                densified.Count > TerrainNaturalCoastMaxPointCount)
            {
                return loop;
            }

            float originalArea = ComputeLoopSignedArea(densified);
            if (MathF.Abs(originalArea) <= 0.001f)
            {
                return loop;
            }

            float boundaryLockDistance = sampleStepWorldUnits * TerrainNaturalCoastBoundaryLockCells;
            List<Vector2> naturalized = new(densified.Count);
            for (int pointIndex = 0; pointIndex < densified.Count; pointIndex++)
            {
                Vector2 localPoint = densified[pointIndex];
                Vector2 worldPoint = new(
                    worldLeft + (localPoint.X * sampleStepWorldUnits),
                    worldTop + (localPoint.Y * sampleStepWorldUnits));

                if (TrySnapTerrainWorldBoundary(ref worldPoint, boundaryLockDistance))
                {
                    naturalized.Add(new Vector2(
                        (worldPoint.X - worldLeft) / sampleStepWorldUnits,
                        (worldPoint.Y - worldTop) / sampleStepWorldUnits));
                    continue;
                }

                Vector2 previous = densified[(pointIndex - 1 + densified.Count) % densified.Count];
                Vector2 next = densified[(pointIndex + 1) % densified.Count];
                Vector2 previousWorld = new(
                    worldLeft + (previous.X * sampleStepWorldUnits),
                    worldTop + (previous.Y * sampleStepWorldUnits));
                Vector2 nextWorld = new(
                    worldLeft + (next.X * sampleStepWorldUnits),
                    worldTop + (next.Y * sampleStepWorldUnits));
                Vector2 tangent = nextWorld - previousWorld;
                if (tangent.LengthSquared() <= 0.0001f)
                {
                    naturalized.Add(localPoint);
                    continue;
                }

                tangent.Normalize();
                Vector2 normal = new(tangent.Y, -tangent.X);
                float terrainX = ResolveTerrainCentifootX(worldPoint.X);
                float terrainY = ResolveTerrainCentifootY(worldPoint.Y);
                float broadNoise = Fbm(terrainX * 1.75f, terrainY * 1.75f, _terrainWorldSeed + 6101, 4);
                float fineNoise = Fbm(terrainX * 4.4f, terrainY * 4.4f, _terrainWorldSeed + 6102, 3) * 0.55f;
                float ridgeNoise = (RidgedFbm(terrainX * 2.35f, terrainY * 2.35f, _terrainWorldSeed + 6103, 3) - 0.5f) * 0.35f;
                float offsetCells = Math.Clamp(broadNoise + fineNoise + ridgeNoise, -1f, 1f) * TerrainNaturalCoastJitterCells;
                Vector2 adjustedWorld = worldPoint + (normal * offsetCells * sampleStepWorldUnits);

                naturalized.Add(new Vector2(
                    (adjustedWorld.X - worldLeft) / sampleStepWorldUnits,
                    (adjustedWorld.Y - worldTop) / sampleStepWorldUnits));
            }

            naturalized = SanitizeLoopPoints(naturalized);
            if (naturalized.Count < 3 ||
                MathF.Sign(ComputeLoopSignedArea(naturalized)) != MathF.Sign(originalArea))
            {
                return loop;
            }

            return new TerrainLoop(naturalized);
        }

        private static List<Vector2> DensifyTerrainLoop(IReadOnlyList<Vector2> points, float maxSegmentLengthCells)
        {
            List<Vector2> densified = new();
            if (points == null || points.Count == 0)
            {
                return densified;
            }

            float maxSegment = MathF.Max(0.5f, maxSegmentLengthCells);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[(i + 1) % points.Count];
                AddLoopPoint(densified, start);

                float distance = Vector2.Distance(start, end);
                int insertedPointCount = Math.Max(0, (int)MathF.Floor(distance / maxSegment));
                for (int insertedPointIndex = 1; insertedPointIndex <= insertedPointCount; insertedPointIndex++)
                {
                    float t = insertedPointIndex / (float)(insertedPointCount + 1);
                    AddLoopPoint(densified, Vector2.Lerp(start, end, t));
                }
            }

            return densified;
        }

        private static bool TrySnapTerrainWorldBoundary(ref Vector2 worldPoint, float snapDistanceWorldUnits)
        {
            EnsureTerrainWorldBoundsInitialized();
            float snapDistance = MathF.Max(0f, snapDistanceWorldUnits);
            bool snapped = false;

            if (MathF.Abs(worldPoint.X - _terrainWorldBounds.MinX) <= snapDistance)
            {
                worldPoint.X = _terrainWorldBounds.MinX;
                snapped = true;
            }
            else if (MathF.Abs(worldPoint.X - _terrainWorldBounds.MaxX) <= snapDistance)
            {
                worldPoint.X = _terrainWorldBounds.MaxX;
                snapped = true;
            }

            if (MathF.Abs(worldPoint.Y - _terrainWorldBounds.MinY) <= snapDistance)
            {
                worldPoint.Y = _terrainWorldBounds.MinY;
                snapped = true;
            }
            else if (MathF.Abs(worldPoint.Y - _terrainWorldBounds.MaxY) <= snapDistance)
            {
                worldPoint.Y = _terrainWorldBounds.MaxY;
                snapped = true;
            }

            return snapped;
        }

        private static bool IsTerrainLoopWorldBoundsPlausible(TerrainWorldBounds bounds)
        {
            if (!float.IsFinite(bounds.MinX) ||
                !float.IsFinite(bounds.MaxX) ||
                !float.IsFinite(bounds.MinY) ||
                !float.IsFinite(bounds.MaxY))
            {
                return false;
            }

            float width = MathF.Max(0f, bounds.MaxX - bounds.MinX);
            float height = MathF.Max(0f, bounds.MaxY - bounds.MinY);
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            float narrowAxis = MathF.Min(width, height);
            float majorAxis = MathF.Max(width, height);
            if (narrowAxis < 36f)
            {
                return false;
            }

            float elongation = majorAxis / MathF.Max(1f, narrowAxis);
            return narrowAxis >= 72f || elongation <= 5.5f;
        }

        private static bool IsTerrainLoopLargeEnough(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
            {
                return false;
            }

            float area = MathF.Abs(ComputeLoopSignedArea(points));
            if (area < TerrainMinVisualLoopAreaCells)
            {
                return false;
            }

            if (!TryGetLoopCellExtents(points, out float width, out float height))
            {
                return false;
            }

            float majorAxis = MathF.Max(width, height);
            float narrowAxis = MathF.Min(width, height);
            return majorAxis >= TerrainMinVisualLoopMajorAxisCells &&
                narrowAxis >= TerrainMinVisualLoopNarrowAxisCells;
        }

        private static bool TryGetLoopCellExtents(
            IReadOnlyList<Vector2> points,
            out float width,
            out float height)
        {
            width = 0f;
            height = 0f;
            if (points == null || points.Count == 0)
            {
                return false;
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                {
                    return false;
                }

                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
            }

            width = MathF.Max(0f, maxX - minX);
            height = MathF.Max(0f, maxY - minY);
            return width > 0f && height > 0f;
        }

        private static float ResolveTerrainLoopSimplifyTolerance(int pointCount, float baseToleranceCells)
        {
            if (pointCount <= 96)
            {
                return baseToleranceCells;
            }

            float complexityFactor = MathF.Sqrt(pointCount / 128f) - 1f;
            float adaptiveBoost = MathF.Max(0f, complexityFactor) * baseToleranceCells * 0.65f;
            if (pointCount > TerrainHeavyLoopPointThreshold)
            {
                float oversizedFactor = MathF.Min(4f, (pointCount - TerrainHeavyLoopPointThreshold) / 256f);
                adaptiveBoost += oversizedFactor * baseToleranceCells * 0.35f;
            }

            adaptiveBoost = MathF.Min(2.6f, adaptiveBoost);
            return baseToleranceCells + adaptiveBoost;
        }

        private static int ResolveTerrainLoopSmoothIterations(int pointCount, int requestedIterations)
        {
            if (requestedIterations <= 0)
            {
                return 0;
            }

            return pointCount > TerrainSmoothLoopPointThreshold
                ? Math.Min(1, requestedIterations)
                : requestedIterations;
        }

        private static List<Vector2> RemoveSharpTerrainLoopArtifacts(IReadOnlyList<Vector2> points)
        {
            List<Vector2> current = CollapseCollinearLoop(points);
            if (current.Count < 4)
            {
                return current;
            }

            bool changed;
            int guard = 0;
            do
            {
                changed = false;
                List<Vector2> next = new(current.Count);
                for (int i = 0; i < current.Count; i++)
                {
                    int count = current.Count;
                    Vector2 previous = current[(i - 1 + count) % count];
                    Vector2 point = current[i];
                    Vector2 following = current[(i + 1) % count];

                    if (IsSharpTerrainArtifact(previous, point, following))
                    {
                        changed = true;
                        continue;
                    }

                    next.Add(point);
                }

                if (next.Count < 3)
                {
                    break;
                }

                current = CollapseCollinearLoop(next);
                guard++;
            }
            while (changed && current.Count >= 4 && guard < 4);

            return current;
        }

        private static bool IsSharpTerrainArtifact(Vector2 previous, Vector2 point, Vector2 following)
        {
            Vector2 toPrevious = previous - point;
            Vector2 toFollowing = following - point;
            float previousLength = toPrevious.Length();
            float followingLength = toFollowing.Length();
            if (previousLength <= 0.001f || followingLength <= 0.001f)
            {
                return true;
            }

            Vector2 previousDirection = toPrevious / previousLength;
            Vector2 followingDirection = toFollowing / followingLength;
            float angleCosine = MathHelper.Clamp(Vector2.Dot(previousDirection, followingDirection), -1f, 1f);
            if (angleCosine <= 0.84f)
            {
                return false;
            }

            float bridgeLength = Vector2.Distance(previous, following);
            if (bridgeLength <= 0.001f)
            {
                return true;
            }

            float spikeHeight = MathF.Sqrt(DistancePointToSegmentSquared(point, previous, following));
            float shorterAdjacent = MathF.Min(previousLength, followingLength);
            return spikeHeight <= MathF.Max(1.35f, bridgeLength * 0.55f) ||
                shorterAdjacent <= 3.5f;
        }

        private static float ResolveTerrainOctogonalCornerCutCells(IReadOnlyList<Vector2> points)
        {
            if (TerrainOctogonalCornerCutCells <= 0f)
            {
                return 0f;
            }

            if (points == null || points.Count < 3)
            {
                return TerrainOctogonalCornerCutCells;
            }

            if (!TryGetLoopCellExtents(points, out float width, out float height))
            {
                return TerrainOctogonalCornerCutCells;
            }

            float narrowAxis = MathF.Min(width, height);
            if (narrowAxis <= 0f)
            {
                return TerrainOctogonalCornerCutCells;
            }

            float areaScale = MathF.Sqrt(MathF.Abs(ComputeLoopSignedArea(points))) * 0.026f;
            float widthScale = narrowAxis * 0.038f;
            float scaledCut = MathF.Max(TerrainOctogonalCornerCutCells, MathF.Min(areaScale, widthScale));
            float maxCut = MathF.Min(4.5f, narrowAxis * 0.14f);
            return MathHelper.Clamp(scaledCut, TerrainOctogonalCornerCutCells, MathF.Max(TerrainOctogonalCornerCutCells, maxCut));
        }

        private static VertexPositionColor[] BuildTerrainFillVertices(
            IReadOnlyList<TerrainLoop> loops,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits)
        {
            if (loops == null || loops.Count == 0)
            {
                return Array.Empty<VertexPositionColor>();
            }

            List<VertexPositionColor> fillVertices = new();
            Color fillColor = Color.Black;

            for (int loopIndex = 0; loopIndex < loops.Count; loopIndex++)
            {
                TerrainLoop loop = loops[loopIndex];
                if (loop?.Points == null || loop.Points.Count < 3)
                {
                    continue;
                }

                List<Vector2> worldLoop = new(loop.Points.Count);
                for (int pointIndex = 0; pointIndex < loop.Points.Count; pointIndex++)
                {
                    Vector2 point = loop.Points[pointIndex];
                    worldLoop.Add(new Vector2(
                        worldLeft + (point.X * sampleStepWorldUnits),
                        worldTop + (point.Y * sampleStepWorldUnits)));
                }

                if (!TryTriangulateSimpleLoop(worldLoop, out List<Vector2> triangleVertices))
                {
                    continue;
                }

                for (int vertexIndex = 0; vertexIndex < triangleVertices.Count; vertexIndex++)
                {
                    Vector2 vertex = triangleVertices[vertexIndex];
                    fillVertices.Add(new VertexPositionColor(new Vector3(vertex, 0f), fillColor));
                }
            }

            return fillVertices.ToArray();
        }

        private static List<Vector2> BuildTerrainWorldLoop(
            TerrainLoop loop,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits)
        {
            List<Vector2> worldLoop = new();
            if (loop?.Points == null || sampleStepWorldUnits <= 0f)
            {
                return worldLoop;
            }

            for (int pointIndex = 0; pointIndex < loop.Points.Count; pointIndex++)
            {
                Vector2 point = loop.Points[pointIndex];
                worldLoop.Add(new Vector2(
                    worldLeft + (point.X * sampleStepWorldUnits),
                    worldTop + (point.Y * sampleStepWorldUnits)));
            }

            return worldLoop;
        }

        private static TerrainWorldBounds BuildTerrainLoopBounds(
            TerrainLoop loop,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits)
        {
            if (loop?.Points == null || loop.Points.Count == 0)
            {
                return new TerrainWorldBounds(worldLeft, worldTop, 0f, 0f);
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < loop.Points.Count; i++)
            {
                Vector2 point = loop.Points[i];
                float worldX = worldLeft + (point.X * sampleStepWorldUnits);
                float worldY = worldTop + (point.Y * sampleStepWorldUnits);
                minX = MathF.Min(minX, worldX);
                maxX = MathF.Max(maxX, worldX);
                minY = MathF.Min(minY, worldY);
                maxY = MathF.Max(maxY, worldY);
            }

            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY))
            {
                return new TerrainWorldBounds(worldLeft, worldTop, 0f, 0f);
            }

            return new TerrainWorldBounds(minX, minY, maxX - minX, maxY - minY);
        }

        private static bool TryTriangulateSimpleLoop(
            IReadOnlyList<Vector2> loopPoints,
            out List<Vector2> triangleVertices)
        {
            triangleVertices = new List<Vector2>();
            if (loopPoints == null || loopPoints.Count < 3)
            {
                return false;
            }

            List<Vector2> polygon = SanitizeLoopPoints(loopPoints);
            if (polygon.Count < 3)
            {
                return false;
            }

            bool isClockwise = ComputeLoopSignedArea(polygon) > 0f;
            List<int> remainingIndices = new(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                remainingIndices.Add(i);
            }

            int guardLimit = polygon.Count * polygon.Count;
            int guard = 0;
            while (remainingIndices.Count > 2 && guard < guardLimit)
            {
                bool earFound = false;
                for (int i = 0; i < remainingIndices.Count; i++)
                {
                    int previousIndex = remainingIndices[(i - 1 + remainingIndices.Count) % remainingIndices.Count];
                    int currentIndex = remainingIndices[i];
                    int nextIndex = remainingIndices[(i + 1) % remainingIndices.Count];

                    Vector2 previous = polygon[previousIndex];
                    Vector2 current = polygon[currentIndex];
                    Vector2 next = polygon[nextIndex];

                    if (!IsConvexLoopVertex(previous, current, next, isClockwise))
                    {
                        continue;
                    }

                    bool containsOtherPoint = false;
                    for (int other = 0; other < remainingIndices.Count; other++)
                    {
                        int otherIndex = remainingIndices[other];
                        if (otherIndex == previousIndex || otherIndex == currentIndex || otherIndex == nextIndex)
                        {
                            continue;
                        }

                        if (PointInsideTriangle(polygon[otherIndex], previous, current, next))
                        {
                            containsOtherPoint = true;
                            break;
                        }
                    }

                    if (containsOtherPoint)
                    {
                        continue;
                    }

                    triangleVertices.Add(previous);
                    triangleVertices.Add(current);
                    triangleVertices.Add(next);
                    remainingIndices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    triangleVertices.Clear();
                    return false;
                }

                guard++;
            }

            return triangleVertices.Count >= 3;
        }

        private static List<Vector2> SanitizeLoopPoints(IReadOnlyList<Vector2> loopPoints)
        {
            List<Vector2> sanitized = new();
            if (loopPoints == null)
            {
                return sanitized;
            }

            for (int i = 0; i < loopPoints.Count; i++)
            {
                Vector2 point = loopPoints[i];
                if (sanitized.Count > 0 &&
                    Vector2.DistanceSquared(sanitized[sanitized.Count - 1], point) <= 0.0001f)
                {
                    continue;
                }

                sanitized.Add(point);
            }

            if (sanitized.Count >= 2 &&
                Vector2.DistanceSquared(sanitized[0], sanitized[sanitized.Count - 1]) <= 0.0001f)
            {
                sanitized.RemoveAt(sanitized.Count - 1);
            }

            return sanitized;
        }

        private static bool IsConvexLoopVertex(Vector2 previous, Vector2 current, Vector2 next, bool isClockwise)
        {
            float cross = CrossProduct(current - previous, next - current);
            return isClockwise ? cross > 0.0001f : cross < -0.0001f;
        }

        private static bool PointInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = CrossProduct(b - a, point - a);
            float bc = CrossProduct(c - b, point - b);
            float ca = CrossProduct(a - c, point - c);

            bool hasNegative = ab < -0.0001f || bc < -0.0001f || ca < -0.0001f;
            bool hasPositive = ab > 0.0001f || bc > 0.0001f || ca > 0.0001f;
            return !(hasNegative && hasPositive);
        }

        private static float ComputeLoopSignedArea(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
            {
                return 0f;
            }

            float signedArea = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                signedArea += (current.X * next.Y) - (next.X * current.Y);
            }

            return signedArea * 0.5f;
        }

        private static float CrossProduct(Vector2 left, Vector2 right)
        {
            return (left.X * right.Y) - (left.Y * right.X);
        }

        private static int ResolveTerrainVisualOversample(int componentWidth, int componentHeight)
        {
            int maxOversampleByWidth = Math.Max(1, MaxTerrainVisualTextureAxis / Math.Max(1, componentWidth));
            int maxOversampleByHeight = Math.Max(1, MaxTerrainVisualTextureAxis / Math.Max(1, componentHeight));
            return Math.Max(1, Math.Min(TerrainVisualTextureOversample, Math.Min(maxOversampleByWidth, maxOversampleByHeight)));
        }

        private static List<RefinedTerrainComponent> BuildRefinedResidentComponents(
            CombinedResidentMask residentMask,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits)
        {
            List<RefinedTerrainComponent> components = new();
            if (residentMask.Mask.Length == 0 ||
                residentMask.Width <= 0 ||
                residentMask.Height <= 0 ||
                sampleStepWorldUnits <= 0f)
            {
                return components;
            }

            byte[] visited = new byte[residentMask.Mask.Length];
            List<int> queue = new();
            List<int> componentCells = new();

            for (int index = 0; index < residentMask.Mask.Length; index++)
            {
                if (visited[index] != 0 || residentMask.Mask[index] != Land)
                {
                    continue;
                }

                CollectConnectedComponent(
                    residentMask.Mask,
                    residentMask.Width,
                    residentMask.Height,
                    index,
                    visited,
                    queue,
                    componentCells,
                    out TerrainComponentBounds bounds);

                int componentWidth = (bounds.MaxX - bounds.MinX) + 1;
                int componentHeight = (bounds.MaxY - bounds.MinY) + 1;
                if (componentWidth <= 0 || componentHeight <= 0)
                {
                    continue;
                }

                byte[] componentMask = new byte[componentWidth * componentHeight];
                for (int i = 0; i < componentCells.Count; i++)
                {
                    int cellIndex = componentCells[i];
                    int cellX = cellIndex % residentMask.Width;
                    int cellY = cellIndex / residentMask.Width;
                    componentMask[Index(cellX - bounds.MinX, cellY - bounds.MinY, componentWidth)] = Land;
                }

                RefinedTerrainComponent refinedComponent = BuildRefinedTerrainComponent(
                    componentMask,
                    componentWidth,
                    componentHeight,
                    worldLeft + (bounds.MinX * sampleStepWorldUnits),
                    worldTop + (bounds.MinY * sampleStepWorldUnits),
                    sampleStepWorldUnits);
                if (refinedComponent != null)
                {
                    components.Add(refinedComponent);
                }
            }

            return components;
        }

        private static RefinedTerrainComponent BuildRefinedTerrainComponent(
            byte[] componentMask,
            int componentWidth,
            int componentHeight,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits)
        {
            if (componentMask == null ||
                componentMask.Length == 0 ||
                componentWidth <= 0 ||
                componentHeight <= 0)
            {
                return null;
            }

            int resolutionMultiplier = ResolveRefinedContourResolutionMultiplier(componentWidth, componentHeight);
            if (resolutionMultiplier <= 1)
            {
                return new RefinedTerrainComponent(
                    (byte[])componentMask.Clone(),
                    componentWidth,
                    componentHeight,
                    worldLeft,
                    worldTop,
                    sampleStepWorldUnits);
            }

            int contourMarginCells = Math.Max(2, resolutionMultiplier + 1);
            int refinedWidth = (componentWidth * resolutionMultiplier) + (contourMarginCells * 2);
            int refinedHeight = (componentHeight * resolutionMultiplier) + (contourMarginCells * 2);
            float refinedStepWorldUnits = sampleStepWorldUnits / resolutionMultiplier;
            float refinedWorldLeft = worldLeft - (contourMarginCells * refinedStepWorldUnits);
            float refinedWorldTop = worldTop - (contourMarginCells * refinedStepWorldUnits);

            byte[] refinedMask = new byte[refinedWidth * refinedHeight];
            for (int y = 0; y < refinedHeight; y++)
            {
                float sampleWorldY = refinedWorldTop + ((y + 0.5f) * refinedStepWorldUnits);

                for (int x = 0; x < refinedWidth; x++)
                {
                    float sampleWorldX = refinedWorldLeft + ((x + 0.5f) * refinedStepWorldUnits);
                    refinedMask[Index(x, y, refinedWidth)] = SampleTerrainMaskAtWorldPosition(sampleWorldX, sampleWorldY);
                }
            }

            if (!TryFindRefinedAnchorIndex(
                componentMask,
                componentWidth,
                componentHeight,
                refinedMask,
                refinedWidth,
                refinedHeight,
                resolutionMultiplier,
                contourMarginCells,
                out int anchorIndex))
            {
                return null;
            }

            byte[] visited = new byte[refinedMask.Length];
            List<int> queue = new();
            List<int> componentCells = new();
            CollectConnectedComponent(
                refinedMask,
                refinedWidth,
                refinedHeight,
                anchorIndex,
                visited,
                queue,
                componentCells,
                out TerrainComponentBounds refinedBounds);

            int croppedWidth = refinedBounds.MaxX - refinedBounds.MinX + 1;
            int croppedHeight = refinedBounds.MaxY - refinedBounds.MinY + 1;
            if (croppedWidth <= 0 || croppedHeight <= 0)
            {
                return null;
            }

            byte[] croppedMask = new byte[croppedWidth * croppedHeight];
            for (int i = 0; i < componentCells.Count; i++)
            {
                int refinedIndex = componentCells[i];
                int refinedX = refinedIndex % refinedWidth;
                int refinedY = refinedIndex / refinedWidth;
                int localX = refinedX - refinedBounds.MinX;
                int localY = refinedY - refinedBounds.MinY;
                croppedMask[Index(localX, localY, croppedWidth)] = Land;
            }

            return new RefinedTerrainComponent(
                croppedMask,
                croppedWidth,
                croppedHeight,
                refinedWorldLeft + (refinedBounds.MinX * refinedStepWorldUnits),
                refinedWorldTop + (refinedBounds.MinY * refinedStepWorldUnits),
                refinedStepWorldUnits);
        }

        private static int ResolveRefinedContourResolutionMultiplier(int componentWidth, int componentHeight)
        {
            int maxAxis = Math.Max(componentWidth, componentHeight);
            int cellArea = Math.Max(0, componentWidth) * Math.Max(0, componentHeight);
            if (maxAxis > 256 || cellArea > 32768)
            {
                return 1;
            }

            return Math.Max(1, TerrainContourResolutionMultiplier);
        }

        private static bool TryFindRefinedAnchorIndex(
            byte[] componentMask,
            int componentWidth,
            int componentHeight,
            byte[] refinedMask,
            int refinedWidth,
            int refinedHeight,
            int resolutionMultiplier,
            int contourMarginCells,
            out int anchorIndex)
        {
            anchorIndex = -1;

            for (int y = 0; y < componentHeight; y++)
            {
                for (int x = 0; x < componentWidth; x++)
                {
                    if (componentMask[Index(x, y, componentWidth)] != Land)
                    {
                        continue;
                    }

                    int refinedAnchorX = contourMarginCells + (x * resolutionMultiplier) + (resolutionMultiplier / 2);
                    int refinedAnchorY = contourMarginCells + (y * resolutionMultiplier) + (resolutionMultiplier / 2);
                    int searchRadius = Math.Max(2, resolutionMultiplier * 2);

                    for (int radius = 0; radius <= searchRadius; radius++)
                    {
                        for (int offsetY = -radius; offsetY <= radius; offsetY++)
                        {
                            for (int offsetX = -radius; offsetX <= radius; offsetX++)
                            {
                                int candidateX = refinedAnchorX + offsetX;
                                int candidateY = refinedAnchorY + offsetY;
                                if (candidateX < 0 ||
                                    candidateY < 0 ||
                                    candidateX >= refinedWidth ||
                                    candidateY >= refinedHeight)
                                {
                                    continue;
                                }

                                int candidateIndex = Index(candidateX, candidateY, refinedWidth);
                                if (refinedMask[candidateIndex] != Land)
                                {
                                    continue;
                                }

                                anchorIndex = candidateIndex;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static Texture2D BuildTerrainVisualTexture(
            GraphicsDevice graphicsDevice,
            List<TerrainLoop> rawLoops,
            int componentWidth,
            int componentHeight,
            int visualOversample)
        {
            if (graphicsDevice == null ||
                rawLoops == null ||
                rawLoops.Count == 0 ||
                componentWidth <= 0 ||
                componentHeight <= 0)
            {
                return null;
            }

            List<TerrainLoop> visualLoops = new(rawLoops.Count);
            for (int i = 0; i < rawLoops.Count; i++)
            {
                List<Vector2> simplifiedLoop = SimplifyLoop(
                    rawLoops[i].Points,
                    TerrainVisualLoopSimplifyToleranceCells);
                List<Vector2> smoothedLoop = SmoothLoop(simplifiedLoop, TerrainVisualSmoothIterations);
                if (smoothedLoop.Count >= 3)
                {
                    visualLoops.Add(new TerrainLoop(smoothedLoop));
                }
            }

            if (visualLoops.Count == 0)
            {
                return null;
            }

            int textureWidth = Math.Max(1, componentWidth * visualOversample);
            int textureHeight = Math.Max(1, componentHeight * visualOversample);
            Color[] pixels = RasterizeTerrainLoops(visualLoops, textureWidth, textureHeight, visualOversample);
            Texture2D texture = new(graphicsDevice, textureWidth, textureHeight, false, SurfaceFormat.Color);
            texture.SetData(pixels);
            return texture;
        }

        private static Color[] RasterizeTerrainLoops(
            IReadOnlyList<TerrainLoop> loops,
            int textureWidth,
            int textureHeight,
            int visualOversample)
        {
            Color[] pixels = new Color[textureWidth * textureHeight];
            List<float> intersections = new();
            float maxLocalX = textureWidth / (float)visualOversample;

            for (int y = 0; y < textureHeight; y++)
            {
                intersections.Clear();
                float sampleY = (y + 0.5f) / visualOversample;

                for (int loopIndex = 0; loopIndex < loops.Count; loopIndex++)
                {
                    CollectScanlineIntersections(loops[loopIndex].Points, sampleY, intersections);
                }

                if (intersections.Count < 2)
                {
                    continue;
                }

                intersections.Sort();
                for (int intersectionIndex = 0; intersectionIndex + 1 < intersections.Count; intersectionIndex += 2)
                {
                    float spanStart = MathF.Max(0f, intersections[intersectionIndex]);
                    float spanEnd = MathF.Min(maxLocalX, intersections[intersectionIndex + 1]);
                    if (spanEnd <= spanStart)
                    {
                        continue;
                    }

                    int startPixel = Math.Max(0, (int)MathF.Floor(spanStart * visualOversample));
                    int endPixel = Math.Min(textureWidth - 1, (int)MathF.Ceiling(spanEnd * visualOversample) - 1);

                    for (int x = startPixel; x <= endPixel; x++)
                    {
                        float pixelLeft = x / (float)visualOversample;
                        float pixelRight = (x + 1f) / visualOversample;
                        float coverage = MathF.Min(pixelRight, spanEnd) - MathF.Max(pixelLeft, spanStart);
                        if (coverage <= 0f)
                        {
                            continue;
                        }

                        byte alpha = (byte)Math.Clamp(
                            (int)MathF.Round((coverage * visualOversample) * byte.MaxValue),
                            0,
                            byte.MaxValue);
                        int pixelIndex = (y * textureWidth) + x;
                        if (alpha > pixels[pixelIndex].A)
                        {
                            pixels[pixelIndex] = new Color((byte)0, (byte)0, (byte)0, alpha);
                        }
                    }
                }
            }

            return pixels;
        }

        private static void CollectScanlineIntersections(IReadOnlyList<Vector2> loop, float sampleY, List<float> intersections)
        {
            if (loop == null || loop.Count < 2 || intersections == null)
            {
                return;
            }

            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 start = loop[i];
                Vector2 end = loop[(i + 1) % loop.Count];
                bool crossesScanline = (start.Y <= sampleY && end.Y > sampleY) ||
                    (end.Y <= sampleY && start.Y > sampleY);
                if (!crossesScanline)
                {
                    continue;
                }

                float t = (sampleY - start.Y) / (end.Y - start.Y);
                intersections.Add(start.X + ((end.X - start.X) * t));
            }
        }

        private static List<TerrainLoop> BuildTerrainLoops(byte[] componentMask, int width, int height)
        {
            List<TerrainLoop> loops = new();
            if (componentMask == null || componentMask.Length == 0 || width <= 0 || height <= 0)
            {
                return loops;
            }

            Dictionary<GridVertex, List<GridVertex>> nextVerticesByStart = new();
            int boundaryEdgeCount = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (componentMask[Index(x, y, width)] != Land)
                    {
                        continue;
                    }

                    if (y == 0 || componentMask[Index(x, y - 1, width)] != Land)
                    {
                        AddBoundaryEdge(new GridVertex(x, y), new GridVertex(x + 1, y));
                    }

                    if (x == width - 1 || componentMask[Index(x + 1, y, width)] != Land)
                    {
                        AddBoundaryEdge(new GridVertex(x + 1, y), new GridVertex(x + 1, y + 1));
                    }

                    if (y == height - 1 || componentMask[Index(x, y + 1, width)] != Land)
                    {
                        AddBoundaryEdge(new GridVertex(x + 1, y + 1), new GridVertex(x, y + 1));
                    }

                    if (x == 0 || componentMask[Index(x - 1, y, width)] != Land)
                    {
                        AddBoundaryEdge(new GridVertex(x, y + 1), new GridVertex(x, y));
                    }
                }
            }

            HashSet<DirectedGridEdge> visitedEdges = new();
            foreach (KeyValuePair<GridVertex, List<GridVertex>> entry in nextVerticesByStart)
            {
                List<GridVertex> outgoingVertices = entry.Value;
                for (int outgoingIndex = 0; outgoingIndex < outgoingVertices.Count; outgoingIndex++)
                {
                    DirectedGridEdge initialEdge = new(entry.Key, outgoingVertices[outgoingIndex]);
                    if (visitedEdges.Contains(initialEdge))
                    {
                        continue;
                    }

                    List<Vector2> collapsed = TraceTerrainBoundaryLoop(
                        nextVerticesByStart,
                        entry.Key,
                        outgoingVertices[outgoingIndex],
                        visitedEdges,
                        boundaryEdgeCount);
                    if (collapsed.Count >= 3)
                    {
                        loops.Add(new TerrainLoop(collapsed));
                    }
                }
            }

            return loops;

            void AddBoundaryEdge(GridVertex start, GridVertex end)
            {
                if (!nextVerticesByStart.TryGetValue(start, out List<GridVertex> outgoingVertices))
                {
                    outgoingVertices = new List<GridVertex>();
                    nextVerticesByStart[start] = outgoingVertices;
                }

                if (outgoingVertices.Contains(end))
                {
                    return;
                }

                outgoingVertices.Add(end);
                boundaryEdgeCount++;
            }
        }

        private static List<Vector2> TraceTerrainBoundaryLoop(
            IReadOnlyDictionary<GridVertex, List<GridVertex>> nextVerticesByStart,
            GridVertex start,
            GridVertex end,
            HashSet<DirectedGridEdge> visitedEdges,
            int boundaryEdgeCount)
        {
            List<Vector2> points = new();
            if (nextVerticesByStart == null || visitedEdges == null || boundaryEdgeCount <= 0)
            {
                return points;
            }

            GridVertex loopStart = start;
            GridVertex previous = start;
            GridVertex current = end;
            int guard = 0;

            while (guard <= boundaryEdgeCount + 1)
            {
                DirectedGridEdge edge = new(previous, current);
                if (!visitedEdges.Add(edge))
                {
                    break;
                }

                points.Add(new Vector2(previous.X, previous.Y));
                if (current.Equals(loopStart))
                {
                    break;
                }

                if (!nextVerticesByStart.TryGetValue(current, out List<GridVertex> candidates) ||
                    !TryChooseNextBoundaryVertex(previous, current, candidates, visitedEdges, out GridVertex next))
                {
                    break;
                }

                previous = current;
                current = next;
                guard++;
            }

            return CollapseCollinearLoop(points);
        }

        private static bool TryChooseNextBoundaryVertex(
            GridVertex previous,
            GridVertex current,
            IReadOnlyList<GridVertex> candidates,
            HashSet<DirectedGridEdge> visitedEdges,
            out GridVertex next)
        {
            next = default;
            if (candidates == null || candidates.Count == 0 || visitedEdges == null)
            {
                return false;
            }

            Vector2 incoming = new(current.X - previous.X, current.Y - previous.Y);
            float bestTurn = float.MinValue;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                GridVertex candidate = candidates[i];
                if (candidate.Equals(current) ||
                    visitedEdges.Contains(new DirectedGridEdge(current, candidate)))
                {
                    continue;
                }

                Vector2 outgoing = new(candidate.X - current.X, candidate.Y - current.Y);
                float turn = MathF.Atan2(CrossProduct(incoming, outgoing), Vector2.Dot(incoming, outgoing));
                if (!found || turn > bestTurn)
                {
                    found = true;
                    bestTurn = turn;
                    next = candidate;
                }
            }

            return found;
        }

        private static List<Vector2> BuildOctogonalLoop(IReadOnlyList<Vector2> points, float cornerCutCells)
        {
            List<Vector2> source = CollapseCollinearLoop(points);
            if (source.Count < 3 || cornerCutCells <= 0f)
            {
                return source;
            }

            List<Vector2> octogonal = new(source.Count * 2);
            float maxRequestedCut = MathF.Max(0.01f, cornerCutCells);

            for (int i = 0; i < source.Count; i++)
            {
                Vector2 previous = source[(i - 1 + source.Count) % source.Count];
                Vector2 current = source[i];
                Vector2 next = source[(i + 1) % source.Count];

                Vector2 incoming = current - previous;
                Vector2 outgoing = next - current;
                float incomingLength = incoming.Length();
                float outgoingLength = outgoing.Length();

                if (incomingLength <= 0.0001f || outgoingLength <= 0.0001f)
                {
                    continue;
                }

                Vector2 incomingDirection = incoming / incomingLength;
                Vector2 outgoingDirection = outgoing / outgoingLength;
                bool cardinalCorner =
                    IsAxisAlignedDirection(incomingDirection) &&
                    IsAxisAlignedDirection(outgoingDirection) &&
                    MathF.Abs(Vector2.Dot(incomingDirection, outgoingDirection)) <= 0.001f;

                if (!cardinalCorner)
                {
                    AddLoopPoint(octogonal, current);
                    continue;
                }

                float cut = MathF.Min(maxRequestedCut, MathF.Min(incomingLength, outgoingLength) * 0.45f);
                if (cut <= 0.0001f)
                {
                    AddLoopPoint(octogonal, current);
                    continue;
                }

                AddLoopPoint(octogonal, current - (incomingDirection * cut));
                AddLoopPoint(octogonal, current + (outgoingDirection * cut));
            }

            return CollapseCollinearLoop(octogonal);
        }

        private static bool IsAxisAlignedDirection(Vector2 direction)
        {
            return (MathF.Abs(direction.X) <= 0.001f && MathF.Abs(MathF.Abs(direction.Y) - 1f) <= 0.001f) ||
                   (MathF.Abs(direction.Y) <= 0.001f && MathF.Abs(MathF.Abs(direction.X) - 1f) <= 0.001f);
        }

        private static void AddLoopPoint(List<Vector2> points, Vector2 point)
        {
            if (points == null)
            {
                return;
            }

            if (points.Count > 0 &&
                Vector2.DistanceSquared(points[points.Count - 1], point) <= 0.0001f)
            {
                return;
            }

            points.Add(point);
        }

        private static void SpawnTerrainLoopColliders(
            IReadOnlyList<TerrainLoop> loops,
            float worldLeft,
            float worldTop,
            float sampleStepWorldUnits,
            TerrainMaterializationResult materialization,
            TerrainWorldBounds colliderWorldBounds)
        {
            if (materialization == null ||
                loops == null ||
                loops.Count == 0 ||
                sampleStepWorldUnits <= 0f)
            {
                return;
            }

            float shellThickness = MathF.Max(2f, CentifootUnits.CentifootToWorld(TerrainCollisionShellThicknessCentifoot));
            float halfShellThickness = shellThickness * 0.5f;

            for (int loopIndex = 0; loopIndex < loops.Count; loopIndex++)
            {
                TerrainLoop loop = loops[loopIndex];
                if (loop?.Points == null || loop.Points.Count < 3)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < loop.Points.Count; pointIndex++)
                {
                    if (materialization.ColliderCount >= MaxTerrainColliderRecordsPerRefresh)
                    {
                        return;
                    }

                    Vector2 localStart = loop.Points[pointIndex];
                    Vector2 localEnd = loop.Points[(pointIndex + 1) % loop.Points.Count];
                    Vector2 start = new(
                        worldLeft + (localStart.X * sampleStepWorldUnits),
                        worldTop + (localStart.Y * sampleStepWorldUnits));
                    Vector2 end = new(
                        worldLeft + (localEnd.X * sampleStepWorldUnits),
                        worldTop + (localEnd.Y * sampleStepWorldUnits));
                    Vector2 segment = end - start;
                    float segmentLength = segment.Length();
                    if (segmentLength <= 0.001f)
                    {
                        continue;
                    }

                    Vector2 segmentUnit = segment / segmentLength;
                    Vector2 midpoint = (start + end) * 0.5f;
                    Vector2 inwardNormal = ResolveTerrainLoopSegmentInwardNormal(
                        loop.Points,
                        localStart,
                        localEnd,
                        midpoint,
                        segmentUnit,
                        sampleStepWorldUnits,
                        halfShellThickness);
                    Vector2 position = midpoint + (inwardNormal * halfShellThickness);
                    float rotation = MathF.Atan2(segment.Y, segment.X);
                    float colliderLength = segmentLength + shellThickness;
                    TerrainWorldBounds bounds = BuildTerrainSegmentColliderBounds(
                        position,
                        segmentUnit,
                        inwardNormal,
                        colliderLength,
                        halfShellThickness);
                    if (!colliderWorldBounds.Intersects(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY))
                    {
                        continue;
                    }

                    materialization.ColliderObjects.Add(new TerrainColliderObjectRecord(
                        bounds,
                        position,
                        rotation,
                        colliderLength,
                        shellThickness));
                    materialization.ColliderCount++;
                }
            }
        }

        private static Vector2 ResolveTerrainLoopSegmentInwardNormal(
            IReadOnlyList<Vector2> loopPoints,
            Vector2 localStart,
            Vector2 localEnd,
            Vector2 worldMidpoint,
            Vector2 segmentUnitWorld,
            float sampleStepWorldUnits,
            float halfShellThicknessWorldUnits)
        {
            Vector2 normalWorld = new(-segmentUnitWorld.Y, segmentUnitWorld.X);
            if (normalWorld.LengthSquared() <= 0.0001f)
            {
                return Vector2.Zero;
            }

            normalWorld.Normalize();

            if (loopPoints != null && loopPoints.Count >= 3 && sampleStepWorldUnits > 0f)
            {
                Vector2 localSegment = localEnd - localStart;
                if (localSegment.LengthSquared() > 0.0001f)
                {
                    localSegment.Normalize();
                    Vector2 normalLocal = new(-localSegment.Y, localSegment.X);
                    Vector2 localMidpoint = (localStart + localEnd) * 0.5f;
                    float localProbeDistance = MathF.Max(
                        0.1f,
                        (halfShellThicknessWorldUnits + 1f) / sampleStepWorldUnits);

                    bool positiveInside = PointInsidePolygon(
                        localMidpoint + (normalLocal * localProbeDistance),
                        loopPoints);
                    bool negativeInside = PointInsidePolygon(
                        localMidpoint - (normalLocal * localProbeDistance),
                        loopPoints);
                    if (positiveInside != negativeInside)
                    {
                        return positiveInside ? normalWorld : -normalWorld;
                    }
                }
            }

            float worldProbeDistance = MathF.Max(2f, halfShellThicknessWorldUnits + 1f);
            bool positiveLand = IsTerrainLandAtWorldPosition(worldMidpoint + (normalWorld * worldProbeDistance));
            bool negativeLand = IsTerrainLandAtWorldPosition(worldMidpoint - (normalWorld * worldProbeDistance));
            if (positiveLand != negativeLand)
            {
                return positiveLand ? normalWorld : -normalWorld;
            }

            return normalWorld;
        }

        private static TerrainWorldBounds BuildTerrainSegmentColliderBounds(
            Vector2 position,
            Vector2 segmentUnit,
            Vector2 normal,
            float length,
            float halfThickness)
        {
            if (segmentUnit.LengthSquared() <= 0.0001f)
            {
                segmentUnit = Vector2.UnitX;
            }
            else
            {
                segmentUnit.Normalize();
            }

            if (normal.LengthSquared() <= 0.0001f)
            {
                normal = new Vector2(-segmentUnit.Y, segmentUnit.X);
            }
            else
            {
                normal.Normalize();
            }

            Vector2 halfAlong = segmentUnit * (MathF.Max(0f, length) * 0.5f);
            Vector2 halfAcross = normal * MathF.Max(0f, halfThickness);
            Vector2 cornerA = position - halfAlong - halfAcross;
            Vector2 cornerB = position - halfAlong + halfAcross;
            Vector2 cornerC = position + halfAlong - halfAcross;
            Vector2 cornerD = position + halfAlong + halfAcross;

            float minX = MathF.Min(MathF.Min(cornerA.X, cornerB.X), MathF.Min(cornerC.X, cornerD.X));
            float maxX = MathF.Max(MathF.Max(cornerA.X, cornerB.X), MathF.Max(cornerC.X, cornerD.X));
            float minY = MathF.Min(MathF.Min(cornerA.Y, cornerB.Y), MathF.Min(cornerC.Y, cornerD.Y));
            float maxY = MathF.Max(MathF.Max(cornerA.Y, cornerB.Y), MathF.Max(cornerC.Y, cornerD.Y));
            return new TerrainWorldBounds(minX, minY, maxX - minX, maxY - minY);
        }

        private static void AddTerrainWorldBoundaryColliders(
            TerrainMaterializationResult materialization,
            TerrainWorldBounds colliderWorldBounds)
        {
            if (materialization == null)
            {
                return;
            }

            EnsureTerrainWorldBoundsInitialized();
            float thickness = MathF.Max(8f, TerrainWorldBoundaryThicknessWorldUnits);
            float horizontalLength = TerrainWorldSizeWorldUnits + (thickness * 2f);
            float verticalLength = TerrainWorldSizeWorldUnits + (thickness * 2f);
            float centerX = (_terrainWorldBounds.MinX + _terrainWorldBounds.MaxX) * 0.5f;
            float centerY = (_terrainWorldBounds.MinY + _terrainWorldBounds.MaxY) * 0.5f;

            AddBoundaryCollider(new Vector2(centerX, _terrainWorldBounds.MinY - (thickness * 0.5f)), 0f, horizontalLength, thickness);
            AddBoundaryCollider(new Vector2(centerX, _terrainWorldBounds.MaxY + (thickness * 0.5f)), 0f, horizontalLength, thickness);
            AddBoundaryCollider(new Vector2(_terrainWorldBounds.MinX - (thickness * 0.5f), centerY), MathF.PI * 0.5f, verticalLength, thickness);
            AddBoundaryCollider(new Vector2(_terrainWorldBounds.MaxX + (thickness * 0.5f), centerY), MathF.PI * 0.5f, verticalLength, thickness);

            void AddBoundaryCollider(Vector2 position, float rotation, float length, float colliderThickness)
            {
                if (materialization.ColliderCount >= MaxTerrainColliderRecordsPerRefresh)
                {
                    return;
                }

                float halfLength = length * 0.5f;
                float halfThickness = colliderThickness * 0.5f;
                TerrainWorldBounds bounds = MathF.Abs(rotation) < 0.001f
                    ? new TerrainWorldBounds(position.X - halfLength, position.Y - halfThickness, length, colliderThickness)
                    : new TerrainWorldBounds(position.X - halfThickness, position.Y - halfLength, colliderThickness, length);

                if (!bounds.Intersects(
                    colliderWorldBounds.MinX,
                    colliderWorldBounds.MaxX,
                    colliderWorldBounds.MinY,
                    colliderWorldBounds.MaxY))
                {
                    return;
                }

                materialization.ColliderObjects.Add(new TerrainColliderObjectRecord(
                    bounds,
                    position,
                    rotation,
                    length,
                    colliderThickness));
                materialization.ColliderCount++;
            }
        }

        private static List<Vector2> SimplifyLoop(IReadOnlyList<Vector2> points, float tolerance)
        {
            List<Vector2> simplified = CollapseCollinearLoop(points);
            if (simplified.Count <= 3 || tolerance <= 0f)
            {
                return simplified;
            }

            float toleranceSq = tolerance * tolerance;
            bool changed;
            int guard = 0;

            do
            {
                changed = false;
                for (int i = 0; i < simplified.Count && simplified.Count > 3; )
                {
                    int count = simplified.Count;
                    Vector2 previous = simplified[(i - 1 + count) % count];
                    Vector2 current = simplified[i];
                    Vector2 next = simplified[(i + 1) % count];

                    if (DistancePointToSegmentSquared(current, previous, next) <= toleranceSq)
                    {
                        simplified.RemoveAt(i);
                        changed = true;
                        continue;
                    }

                    i++;
                }

                if (changed)
                {
                    simplified = CollapseCollinearLoop(simplified);
                }

                guard++;
            }
            while (changed && simplified.Count > 3 && guard < 32);

            return simplified;
        }

        private static List<Vector2> SmoothLoop(IReadOnlyList<Vector2> points, int iterations)
        {
            if (points == null || points.Count < 3 || iterations <= 0)
            {
                return points == null ? new List<Vector2>() : new List<Vector2>(points);
            }

            List<Vector2> current = new(points);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (current.Count < 3)
                {
                    break;
                }

                List<Vector2> next = new(current.Count * 2);
                for (int i = 0; i < current.Count; i++)
                {
                    Vector2 start = current[i];
                    Vector2 end = current[(i + 1) % current.Count];
                    next.Add(Vector2.Lerp(start, end, 0.25f));
                    next.Add(Vector2.Lerp(start, end, 0.75f));
                }

                current = next;
            }

            return current;
        }

        private static List<Vector2> CollapseCollinearLoop(IReadOnlyList<Vector2> points)
        {
            if (points == null)
            {
                return new List<Vector2>();
            }

            List<Vector2> current = new(points);
            if (current.Count < 3)
            {
                return current;
            }

            bool changed;
            int guard = 0;
            do
            {
                changed = false;
                List<Vector2> next = new(current.Count);
                for (int i = 0; i < current.Count; i++)
                {
                    int count = current.Count;
                    Vector2 previous = current[(i - 1 + count) % count];
                    Vector2 currentPoint = current[i];
                    Vector2 nextPoint = current[(i + 1) % count];

                    if (Vector2.DistanceSquared(previous, currentPoint) <= 0.0001f)
                    {
                        changed = true;
                        continue;
                    }

                    Vector2 a = currentPoint - previous;
                    Vector2 b = nextPoint - currentPoint;
                    float cross = (a.X * b.Y) - (a.Y * b.X);
                    if (MathF.Abs(cross) <= 0.0001f && Vector2.Dot(a, b) >= 0f)
                    {
                        changed = true;
                        continue;
                    }

                    next.Add(currentPoint);
                }

                current = next.Count >= 3 ? next : current;
                guard++;
            }
            while (changed && current.Count >= 3 && guard < 16);

            return current;
        }

        private static float DistancePointToSegmentSquared(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float lengthSq = segment.LengthSquared();
            if (lengthSq <= 0.0001f)
            {
                return Vector2.DistanceSquared(point, segmentStart);
            }

            float t = Vector2.Dot(point - segmentStart, segment) / lengthSq;
            t = MathHelper.Clamp(t, 0f, 1f);
            Vector2 projection = segmentStart + (segment * t);
            return Vector2.DistanceSquared(point, projection);
        }

        private static Vector2 NearestPointOnSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float lengthSq = segment.LengthSquared();
            if (lengthSq <= 0.0001f)
            {
                return segmentStart;
            }

            float t = Vector2.Dot(point - segmentStart, segment) / lengthSq;
            t = MathHelper.Clamp(t, 0f, 1f);
            return segmentStart + (segment * t);
        }

        private static bool PointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 current = polygon[i];
                Vector2 previous = polygon[j];
                bool crosses = (current.Y > point.Y) != (previous.Y > point.Y);
                if (!crosses)
                {
                    continue;
                }

                float denominator = previous.Y - current.Y;
                if (MathF.Abs(denominator) <= 0.0001f)
                {
                    continue;
                }

                float intersectX = ((previous.X - current.X) * (point.Y - current.Y) / denominator) + current.X;
                if (point.X < intersectX)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static GameObject CreateTerrainColliderObject(
            Vector2 position,
            float rotation,
            float length,
            float thickness)
        {
            Shape colliderShape = new(
                "Rectangle",
                Math.Max(1, (int)MathF.Round(length)),
                Math.Max(1, (int)MathF.Round(thickness)),
                0,
                Color.Transparent,
                Color.Transparent,
                0);
            colliderShape.SkipHover = true;

            GameObject colliderObject = new(
                GameObjectManager.GetNextID(),
                "TerrainCollider",
                position,
                rotation,
                TerrainStaticMass,
                false,
                true,
                false,
                colliderShape,
                Color.Transparent,
                Color.Transparent,
                0,
                registerWithShapeManager: false);
            colliderObject.DrawLayer = TerrainDrawLayer;
            return colliderObject;
        }

        private static void AddTerrainColliderRecordToSpatialIndex(TerrainColliderObjectRecord record)
        {
            if (record == null ||
                !TryGetTerrainColliderRecordCellRange(record.Bounds, out int minCellX, out int maxCellX, out int minCellY, out int maxCellY))
            {
                return;
            }

            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long cellKey = ComposeTerrainColliderCellKey(cellX, cellY);
                    if (!TerrainColliderRecordCells.TryGetValue(cellKey, out List<TerrainColliderObjectRecord> records))
                    {
                        records = AvailableTerrainColliderRecordCellLists.Count > 0
                            ? AvailableTerrainColliderRecordCellLists.Pop()
                            : new List<TerrainColliderObjectRecord>();
                        TerrainColliderRecordCells[cellKey] = records;
                        TerrainColliderRecordCellKeys.Add(cellKey);
                    }

                    records.Add(record);
                }
            }
        }

        private static bool TryGetTerrainColliderRecordCellRange(
            TerrainWorldBounds bounds,
            out int minCellX,
            out int maxCellX,
            out int minCellY,
            out int maxCellY)
        {
            minCellX = 0;
            maxCellX = 0;
            minCellY = 0;
            maxCellY = 0;

            if (!float.IsFinite(bounds.MinX) ||
                !float.IsFinite(bounds.MaxX) ||
                !float.IsFinite(bounds.MinY) ||
                !float.IsFinite(bounds.MaxY))
            {
                return false;
            }

            minCellX = (int)MathF.Floor(bounds.MinX / TerrainColliderSpatialCellSizeWorldUnits);
            maxCellX = (int)MathF.Floor(bounds.MaxX / TerrainColliderSpatialCellSizeWorldUnits);
            minCellY = (int)MathF.Floor(bounds.MinY / TerrainColliderSpatialCellSizeWorldUnits);
            maxCellY = (int)MathF.Floor(bounds.MaxY / TerrainColliderSpatialCellSizeWorldUnits);
            return true;
        }

        private static long ComposeTerrainColliderCellKey(int cellX, int cellY)
        {
            return ((long)cellX << 32) | (uint)cellY;
        }

        private static void ClearTerrainColliderSpatialIndex()
        {
            for (int i = 0; i < TerrainColliderRecordCellKeys.Count; i++)
            {
                long cellKey = TerrainColliderRecordCellKeys[i];
                if (!TerrainColliderRecordCells.TryGetValue(cellKey, out List<TerrainColliderObjectRecord> records))
                {
                    continue;
                }

                records.Clear();
                TerrainColliderRecordCells.Remove(cellKey);
                AvailableTerrainColliderRecordCellLists.Push(records);
            }

            TerrainColliderRecordCellKeys.Clear();
        }

        private static bool IsFiniteVector(Vector2 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y);
        }

        private static void ClearResidentTerrainWorldObjects()
        {
            for (int i = ResidentTerrainColliderObjects.Count - 1; i >= 0; i--)
            {
                TerrainColliderObjectRecord colliderRecord = ResidentTerrainColliderObjects[i];
                if (colliderRecord.IsCollisionActive && colliderRecord.ColliderObject != null)
                {
                    Core.Instance?.GameObjects?.Remove(colliderRecord.ColliderObject);
                    Core.Instance?.StaticObjects?.Remove(colliderRecord.ColliderObject);
                }

                colliderRecord.ColliderObject?.Dispose();
                colliderRecord.ReleaseColliderObject();
            }

            for (int i = ResidentTerrainVisualObjects.Count - 1; i >= 0; i--)
            {
                ResidentTerrainVisualObjects[i]?.Dispose();
            }

            ResidentTerrainColliderObjects.Clear();
            ResidentTerrainVisualObjects.Clear();
            ResidentTerrainCollisionLoops.Clear();
            ClearTerrainColliderSpatialIndex();
            ActiveTerrainColliderRecords.Clear();
            DesiredTerrainColliderRecords.Clear();
            TerrainColliderDeactivateScratch.Clear();
            _residentTerrainComponentCount = 0;
            _residentTerrainColliderCount = 0;
            _residentTerrainVisualTriangleCount = 0;
            _activeTerrainColliderCount = 0;
            _terrainColliderActivationCandidateCount = 0;
            _residentTerrainVertexColorValid = false;
        }

        private static Vector2 ResolveSeedAnchorCentifoot(int seed)
        {
            if (TryResolveCoastalSeedAnchorCentifoot(seed, out Vector2 preferredAnchor))
            {
                return preferredAnchor;
            }

            if (TryResolveSeedAnchorCentifoot(seed, SeaLevel + PreferredSpawnFieldMargin, out Vector2 fallbackCoastAnchor))
            {
                return fallbackCoastAnchor;
            }

            if (TryResolveSeedAnchorCentifoot(seed, SeaLevel, out Vector2 fallbackAnchor))
            {
                return fallbackAnchor;
            }

            return Vector2.Zero;
        }

        private static bool TryResolveCoastalSeedAnchorCentifoot(int seed, out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            float bestScore = float.MaxValue;
            bool foundAnchor = false;
            float searchStep = ArchipelagoCellSize * 0.58f;

            for (int cellY = -SpawnAnchorSearchCellRadius; cellY <= SpawnAnchorSearchCellRadius; cellY++)
            {
                for (int cellX = -SpawnAnchorSearchCellRadius; cellX <= SpawnAnchorSearchCellRadius; cellX++)
                {
                    float jitterX = (Hash2(cellX, cellY, seed + 4020) - 0.5f) * searchStep * 0.58f;
                    float jitterY = (Hash2(cellX, cellY, seed + 4021) - 0.5f) * searchStep * 0.58f;
                    float candidateX = (cellX * searchStep) + jitterX;
                    float candidateY = (cellY * searchStep) + jitterY;
                    TryUpdateCoastalAnchor(candidateX, candidateY, ArchipelagoCellSize * 1.35f, seed, ref foundAnchor, ref bestScore, ref anchor);
                }
            }

            return foundAnchor;
        }

        private static bool TryResolveSeedAnchorCentifoot(int seed, float minimumField, out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            float bestDistanceSq = float.MaxValue;
            float bestField = float.MinValue;
            bool foundAnchor = false;
            float searchStep = ArchipelagoCellSize * 0.55f;

            for (int cellY = -SpawnAnchorSearchCellRadius; cellY <= SpawnAnchorSearchCellRadius; cellY++)
            {
                for (int cellX = -SpawnAnchorSearchCellRadius; cellX <= SpawnAnchorSearchCellRadius; cellX++)
                {
                    float centerX = cellX * searchStep;
                    float centerY = cellY * searchStep;
                    float anchorRadius = ArchipelagoCellSize * (0.72f + (Hash2(cellX, cellY, seed + 4051) * 0.58f));
                    float angleOffset = Hash2(cellX, cellY, seed + 4050) * MathF.Tau;
                    float[] distanceMultipliers = [0.42f, 0.72f, 0.96f, 1.18f];
                    for (int angleIndex = 0; angleIndex < 16; angleIndex++)
                    {
                        float angle = angleOffset + (MathF.Tau * angleIndex / 16f);
                        for (int distanceIndex = 0; distanceIndex < distanceMultipliers.Length; distanceIndex++)
                        {
                            float distance = anchorRadius * distanceMultipliers[distanceIndex];
                            float candidateX = centerX + (MathF.Cos(angle) * distance);
                            float candidateY = centerY + (MathF.Sin(angle) * distance);
                            TryUpdateAnchor(candidateX, candidateY, seed, minimumField, ref foundAnchor, ref bestDistanceSq, ref bestField, ref anchor);
                        }
                    }
                }
            }

            return foundAnchor;
        }

        private static void TryUpdateLandformCoastalAnchor(
            TerrainLandformDescriptor descriptor,
            int seed,
            ref bool foundAnchor,
            ref float bestScore,
            ref Vector2 bestAnchor)
        {
            float preferredField = SeaLevel - 0.08f;
            float anchorRadius = ResolveLandformAnchorRadius(descriptor);
            float[] distanceMultipliers = [0.78f, 1f, 1.22f];
            float angleOffset = descriptor.Angle + (Hash2(descriptor.CellX, descriptor.CellY, seed + 4040) * 0.34f);

            for (int angleIndex = 0; angleIndex < 18; angleIndex++)
            {
                float angle = angleOffset + (MathF.Tau * angleIndex / 18f);
                for (int distanceIndex = 0; distanceIndex < distanceMultipliers.Length; distanceIndex++)
                {
                    float distance = anchorRadius * distanceMultipliers[distanceIndex];
                    float candidateX = descriptor.CenterX + (MathF.Cos(angle) * distance);
                    float candidateY = descriptor.CenterY + (MathF.Sin(angle) * distance);
                    float field = WorldField(candidateX, candidateY, seed);

                    float shorePenalty = MathF.Abs(field - preferredField);
                    if (field > SeaLevel + 0.12f)
                    {
                        shorePenalty += 3f + ((field - SeaLevel) * 6f);
                    }
                    else if (field < SeaLevel - 0.48f)
                    {
                        shorePenalty += 1.5f + ((SeaLevel - 0.48f - field) * 4f);
                    }

                    int visibleLandSamples = CountVisibleLandSamples(candidateX, candidateY, seed);
                    if (visibleLandSamples <= 0)
                    {
                        shorePenalty += 6f;
                    }
                    else
                    {
                        shorePenalty += MathF.Abs(visibleLandSamples - 4f) * 0.22f;
                    }

                    float distanceSq = (candidateX * candidateX) + (candidateY * candidateY);
                    float score = (shorePenalty * 1000f) + distanceSq;
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    foundAnchor = true;
                    bestScore = score;
                    bestAnchor = new Vector2(candidateX, candidateY);
                }
            }
        }

        private static void TryUpdateCoastalAnchor(
            float centerX,
            float centerY,
            float radius,
            int seed,
            ref bool foundAnchor,
            ref float bestScore,
            ref Vector2 bestAnchor)
        {
            float centerField = WorldField(centerX, centerY, seed);
            if (centerField <= SeaLevel + PreferredSpawnFieldMargin)
            {
                return;
            }

            float preferredField = SeaLevel - 0.08f;
            float directionX = centerX;
            float directionY = centerY;
            float directionLength = MathF.Sqrt((directionX * directionX) + (directionY * directionY));
            if (directionLength <= 0.0001f)
            {
                float randomAngle = Hash2((int)MathF.Round(centerX * 17f), (int)MathF.Round(centerY * 17f), seed + 2048) * MathF.Tau;
                directionX = MathF.Cos(randomAngle);
                directionY = MathF.Sin(randomAngle);
            }
            else
            {
                directionX /= directionLength;
                directionY /= directionLength;
            }

            float[] distanceMultipliers = [0.95f, 1.15f, 1.35f];
            float[] angleOffsets = [0f, 0.38f, -0.38f, 0.76f, -0.76f];
            for (int angleIndex = 0; angleIndex < angleOffsets.Length; angleIndex++)
            {
                Rotate(directionX, directionY, angleOffsets[angleIndex], out float rotatedX, out float rotatedY);
                for (int distanceIndex = 0; distanceIndex < distanceMultipliers.Length; distanceIndex++)
                {
                    float offset = radius * distanceMultipliers[distanceIndex];
                    float candidateX = centerX - (rotatedX * offset);
                    float candidateY = centerY - (rotatedY * offset);
                    float field = WorldField(candidateX, candidateY, seed);

                    float shorePenalty = MathF.Abs(field - preferredField);
                    if (field > SeaLevel + 0.12f)
                    {
                        shorePenalty += 3f + ((field - SeaLevel) * 6f);
                    }
                    else if (field < SeaLevel - 0.48f)
                    {
                        shorePenalty += 1.5f + ((SeaLevel - 0.48f - field) * 4f);
                    }

                    int visibleLandSamples = CountVisibleLandSamples(candidateX, candidateY, seed);
                    if (visibleLandSamples <= 0)
                    {
                        shorePenalty += 6f;
                    }
                    else
                    {
                        shorePenalty += MathF.Abs(visibleLandSamples - 4f) * 0.22f;
                    }

                    float distanceSq = (candidateX * candidateX) + (candidateY * candidateY);
                    float score = (shorePenalty * 1000f) + distanceSq;
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    foundAnchor = true;
                    bestScore = score;
                    bestAnchor = new Vector2(candidateX, candidateY);
                }
            }
        }

        private static int CountVisibleLandSamples(float anchorX, float anchorY, int seed)
        {
            float visibleRange = ResolveSpawnVisibleRangeTerrainUnits();
            float[] distanceMultipliers = [0.45f, 0.8f];
            float[] angles = [0f, MathF.PI * 0.25f, MathF.PI * 0.5f, MathF.PI * 0.75f, MathF.PI, MathF.PI * 1.25f, MathF.PI * 1.5f, MathF.PI * 1.75f];
            int landSamples = 0;

            for (int distanceIndex = 0; distanceIndex < distanceMultipliers.Length; distanceIndex++)
            {
                float distance = visibleRange * distanceMultipliers[distanceIndex];
                for (int angleIndex = 0; angleIndex < angles.Length; angleIndex++)
                {
                    float angle = angles[angleIndex];
                    float sampleX = anchorX + (MathF.Cos(angle) * distance);
                    float sampleY = anchorY + (MathF.Sin(angle) * distance);
                    if (WorldField(sampleX, sampleY, seed) > SeaLevel)
                    {
                        landSamples++;
                    }
                }
            }

            return landSamples;
        }

        private static float ResolveSpawnVisibleRangeTerrainUnits()
        {
            float viewportWidth = Core.Instance?.ViewportWidth ?? 1200f;
            float viewportHeight = Core.Instance?.ViewportHeight ?? 1200f;
            float visibleWorldRadius = MathF.Max(240f, MathF.Min(viewportWidth, viewportHeight) * 0.5f);
            return MathF.Max(1.2f, CentifootUnits.WorldToCentifoot(visibleWorldRadius) / TerrainFeatureScaleMultiplier);
        }

        private static void TryUpdateAnchor(
            float candidateX,
            float candidateY,
            int seed,
            float minimumField,
            ref bool foundAnchor,
            ref float bestDistanceSq,
            ref float bestField,
            ref Vector2 bestAnchor)
        {
            float field = WorldField(candidateX, candidateY, seed);
            if (field <= minimumField)
            {
                return;
            }

            float distanceSq = (candidateX * candidateX) + (candidateY * candidateY);
            if (foundAnchor &&
                (distanceSq > bestDistanceSq ||
                (distanceSq == bestDistanceSq && field <= bestField)))
            {
                return;
            }

            foundAnchor = true;
            bestDistanceSq = distanceSq;
            bestField = field;
            bestAnchor = new Vector2(candidateX, candidateY);
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

        private static ChunkBounds BuildChunkBounds(float minX, float maxX, float minY, float maxY)
        {
            return new ChunkBounds(
                ResolveChunkIndex(minX),
                ResolveChunkIndex(MathF.BitDecrement(maxX)),
                ResolveChunkIndex(minY),
                ResolveChunkIndex(MathF.BitDecrement(maxY)));
        }

        private static ChunkBounds ClampChunkBoundsToTerrainWorld(ChunkBounds chunkBounds)
        {
            EnsureTerrainWorldBoundsInitialized();
            ChunkBounds worldChunkBounds = BuildChunkBounds(
                _terrainWorldBounds.MinX,
                _terrainWorldBounds.MaxX,
                _terrainWorldBounds.MinY,
                _terrainWorldBounds.MaxY);

            int minChunkX = Math.Max(chunkBounds.MinChunkX, worldChunkBounds.MinChunkX);
            int maxChunkX = Math.Min(chunkBounds.MaxChunkX, worldChunkBounds.MaxChunkX);
            int minChunkY = Math.Max(chunkBounds.MinChunkY, worldChunkBounds.MinChunkY);
            int maxChunkY = Math.Min(chunkBounds.MaxChunkY, worldChunkBounds.MaxChunkY);

            if (maxChunkX < minChunkX)
            {
                maxChunkX = minChunkX;
            }
            if (maxChunkY < minChunkY)
            {
                maxChunkY = minChunkY;
            }

            return new ChunkBounds(minChunkX, maxChunkX, minChunkY, maxChunkY);
        }

        private static bool TerrainWorldContainsChunk(ChunkKey key)
        {
            EnsureTerrainWorldBoundsInitialized();
            float chunkMinX = key.X * ChunkWorldSize;
            float chunkMinY = key.Y * ChunkWorldSize;
            float chunkMaxX = chunkMinX + ChunkWorldSize;
            float chunkMaxY = chunkMinY + ChunkWorldSize;
            return chunkMaxX >= _terrainWorldBounds.MinX &&
                chunkMinX <= _terrainWorldBounds.MaxX &&
                chunkMaxY >= _terrainWorldBounds.MinY &&
                chunkMinY <= _terrainWorldBounds.MaxY;
        }

        private static bool TerrainWorldContainsPoint(float worldX, float worldY)
        {
            EnsureTerrainWorldBoundsInitialized();
            return worldX >= _terrainWorldBounds.MinX &&
                worldX <= _terrainWorldBounds.MaxX &&
                worldY >= _terrainWorldBounds.MinY &&
                worldY <= _terrainWorldBounds.MaxY;
        }

        private static TerrainWorldBounds BuildChunkWorldBounds(ChunkBounds chunkBounds)
        {
            float minX = chunkBounds.MinChunkX * ChunkWorldSize;
            float minY = chunkBounds.MinChunkY * ChunkWorldSize;
            float width = ((chunkBounds.MaxChunkX - chunkBounds.MinChunkX) + 1) * ChunkWorldSize;
            float height = ((chunkBounds.MaxChunkY - chunkBounds.MinChunkY) + 1) * ChunkWorldSize;
            return new TerrainWorldBounds(minX, minY, width, height);
        }

        private static bool ChunkBoundsEqual(ChunkBounds left, ChunkBounds right)
        {
            return left.MinChunkX == right.MinChunkX &&
                left.MaxChunkX == right.MaxChunkX &&
                left.MinChunkY == right.MinChunkY &&
                left.MaxChunkY == right.MaxChunkY;
        }

        private static string FormatChunkBounds(ChunkBounds bounds)
        {
            return $"{bounds.MinChunkX}..{bounds.MaxChunkX}, {bounds.MinChunkY}..{bounds.MaxChunkY}";
        }

        private static ChunkKey BuildChunkKey(float worldX, float worldY)
        {
            return new ChunkKey(ResolveChunkIndex(worldX), ResolveChunkIndex(worldY));
        }

        private static int ResolveChunkIndex(float worldCoordinate)
        {
            return (int)MathF.Floor(worldCoordinate / ChunkWorldSize);
        }

        private static int Index(int x, int y, int width)
        {
            return (y * width) + x;
        }

        private static byte[] CloneMask(byte[] mask)
        {
            return (byte[])mask.Clone();
        }

        private static int CountNeighbors(byte[] mask, int width, int height, int x, int y, byte target)
        {
            int count = 0;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    int nx = x + ox;
                    int ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    if (mask[Index(nx, ny, width)] == target)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static byte[] MajoritySmooth(byte[] mask, int width, int height, int iterations)
        {
            byte[] current = CloneMask(mask);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                byte[] next = CloneMask(current);
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int index = Index(x, y, width);
                        int landNeighbors = CountNeighbors(current, width, height, x, y, Land);
                        if (current[index] == Land && landNeighbors <= 2)
                        {
                            next[index] = Water;
                        }

                        if (current[index] == Water && landNeighbors >= 6)
                        {
                            next[index] = Land;
                        }
                    }
                }

                current = next;
            }

            return current;
        }

        private static byte[] RemoveSmallComponents(
            byte[] mask,
            int width,
            int height,
            byte targetValue,
            int minArea,
            byte replacementValue)
        {
            byte[] output = CloneMask(mask);
            byte[] visited = new byte[width * height];
            List<int> queue = new();
            List<int> component = new();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int start = Index(x, y, width);
                    if (visited[start] != 0 || output[start] != targetValue)
                    {
                        continue;
                    }

                    queue.Clear();
                    component.Clear();
                    queue.Add(start);
                    visited[start] = 1;

                    for (int queueIndex = 0; queueIndex < queue.Count; queueIndex++)
                    {
                        int current = queue[queueIndex];
                        component.Add(current);
                        int cx = current % width;
                        int cy = current / width;

                        TryEnqueue(cx + 1, cy);
                        TryEnqueue(cx - 1, cy);
                        TryEnqueue(cx, cy + 1);
                        TryEnqueue(cx, cy - 1);
                    }

                    if (component.Count < minArea)
                    {
                        for (int i = 0; i < component.Count; i++)
                        {
                            output[component[i]] = replacementValue;
                        }
                    }
                }
            }

            return output;

            void TryEnqueue(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    return;
                }

                int index = Index(nx, ny, width);
                if (visited[index] != 0 || output[index] != targetValue)
                {
                    return;
                }

                visited[index] = 1;
                queue.Add(index);
            }
        }

        private static float Hash2(int x, int y, int seed = 0)
        {
            unchecked
            {
                uint n = (uint)((x * 374761393) + (y * 668265263) + (seed * 1442695041));
                n = (uint)((int)(n ^ (n >> 13)) * 1274126177);
                return (n ^ (n >> 16)) / 4294967295f;
            }
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * t);
        }

        private static float Fade(float t)
        {
            return t * t * t * ((t * ((t * 6f) - 15f)) + 10f);
        }

        private static float ValueNoise(float x, float y, int seed = 0)
        {
            int xi = (int)MathF.Floor(x);
            int yi = (int)MathF.Floor(y);
            float xf = x - xi;
            float yf = y - yi;
            float u = Fade(xf);
            float v = Fade(yf);

            float a = (Hash2(xi, yi, seed) * 2f) - 1f;
            float b = (Hash2(xi + 1, yi, seed) * 2f) - 1f;
            float c = (Hash2(xi, yi + 1, seed) * 2f) - 1f;
            float d = (Hash2(xi + 1, yi + 1, seed) * 2f) - 1f;

            return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
        }

        private static float Fbm(
            float x,
            float y,
            int seed = 0,
            int octaves = 5,
            float lacunarity = 2f,
            float gain = 0.5f)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float sum = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < octaves; octave++)
            {
                sum += amplitude * ValueNoise(x * frequency, y * frequency, seed + (octave * 1013));
                normalization += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return normalization <= 0f ? 0f : sum / normalization;
        }

        private static float RidgedFbm(float x, float y, int seed = 0, int octaves = 5)
        {
            return 1f - MathF.Abs(Fbm(x, y, seed, octaves));
        }

        private static void Rotate(float x, float y, float angle, out float rotatedX, out float rotatedY)
        {
            float cosine = MathF.Cos(angle);
            float sine = MathF.Sin(angle);
            rotatedX = (x * cosine) - (y * sine);
            rotatedY = (x * sine) + (y * cosine);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            if (edge0 == edge1)
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - (2f * t));
        }

        private static void DomainWarp(float x, float y, int seed, float strength, out float warpedX, out float warpedY)
        {
            float warpX = Fbm((x * 0.09f) + 17.2f, (y * 0.09f) - 31.7f, seed + 910, 4) * strength;
            float warpY = Fbm((x * 0.09f) - 44.6f, (y * 0.09f) + 12.9f, seed + 911, 4) * strength;
            warpedX = x + warpX;
            warpedY = y + warpY;
        }

        private static float IslandContribution(
            float x,
            float y,
            float centerX,
            float centerY,
            float radius,
            int seed,
            float elongation = 1f,
            float angle = 0f)
        {
            DomainWarp(x, y, seed, radius * 0.12f, out float warpedX, out float warpedY);

            float deltaX = warpedX - centerX;
            float deltaY = warpedY - centerY;
            Rotate(deltaX, deltaY, angle, out deltaX, out deltaY);

            deltaX /= elongation;
            deltaY *= elongation;

            float distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) / radius;
            float baseValue = 1.08f - (distance * 1.46f);
            float organic = Fbm(warpedX * 0.48f, warpedY * 0.48f, seed, 5) * 0.56f;
            float continentalRipple = Fbm(warpedX * 0.16f, warpedY * 0.16f, seed + 51, 4) * 0.22f;
            return baseValue + organic + continentalRipple;
        }

        private static bool TryResolveIslandCluster(
            int islandCellX,
            int islandCellY,
            int seed,
            out float mainX,
            out float mainY,
            out float radius,
            out float elongation,
            out float angle,
            out int mainSeed)
        {
            mainX = 0f;
            mainY = 0f;
            radius = 0f;
            elongation = 1f;
            angle = 0f;
            mainSeed = seed;

            float presence = Hash2(islandCellX, islandCellY, seed + 1000);
            if (presence < IslandCellPresenceThreshold)
            {
                return false;
            }

            float jitterX = Hash2(islandCellX, islandCellY, seed + 1001);
            float jitterY = Hash2(islandCellX, islandCellY, seed + 1002);
            mainX = (islandCellX + IslandMainJitterInset + (jitterX * IslandMainJitterRange)) * ArchipelagoCellSize;
            mainY = (islandCellY + IslandMainJitterInset + (jitterY * IslandMainJitterRange)) * ArchipelagoCellSize;
            radius = IslandMainRadiusBase + (Hash2(islandCellX, islandCellY, seed + 1003) * IslandMainRadiusRange);
            elongation = 0.82f + (Hash2(islandCellX, islandCellY, seed + 1004) * 0.62f);
            angle = Hash2(islandCellX, islandCellY, seed + 1005) * MathF.PI;
            mainSeed = seed + (islandCellX * 83) + (islandCellY * 193);
            return true;
        }

        private static int ResolveIslandSatelliteCount(int islandCellX, int islandCellY, int seed)
        {
            return Math.Min(
                IslandSatelliteMaxCount,
                (int)(Hash2(islandCellX, islandCellY, seed + 1006) * (IslandSatelliteMaxCount + 1)));
        }

        private static void ResolveIslandSatellite(
            int islandCellX,
            int islandCellY,
            int seed,
            int satelliteIndex,
            float mainX,
            float mainY,
            float mainRadius,
            out float satelliteX,
            out float satelliteY,
            out float satelliteRadius,
            out float satelliteElongation,
            out float satelliteRotation,
            out int satelliteSeed)
        {
            float satelliteAngle = Hash2(islandCellX + (satelliteIndex * 7), islandCellY - (satelliteIndex * 5), seed + 1010) * MathF.Tau;
            float satelliteDistance = mainRadius * (1.25f + (Hash2(islandCellX - (satelliteIndex * 3), islandCellY + (satelliteIndex * 11), seed + 1011) * 0.72f));
            satelliteX = mainX + (MathF.Cos(satelliteAngle) * satelliteDistance);
            satelliteY = mainY + (MathF.Sin(satelliteAngle) * satelliteDistance);
            satelliteRadius = mainRadius * (0.26f + (Hash2(islandCellX + (satelliteIndex * 13), islandCellY + (satelliteIndex * 17), seed + 1012) * 0.18f));
            satelliteElongation = 0.9f + (Hash2(islandCellX + (satelliteIndex * 19), islandCellY - (satelliteIndex * 23), seed + 1013) * 0.38f);
            satelliteRotation = Hash2(islandCellX - (satelliteIndex * 29), islandCellY + (satelliteIndex * 31), seed + 1014) * MathF.PI;
            satelliteSeed = seed + (islandCellX * 307) + (islandCellY * 701) + (satelliteIndex * 997);
        }

        private static TerrainSubstrateWeights ResolveSubstrateWeights(float x, float y, int seed)
        {
            return ResolveDominantSubstrateWeights(x, y, seed);
        }

        private static TerrainSubstrateWeights ResolveDominantSubstrateWeights(float x, float y, int seed)
        {
            float limestone = 0.18f +
                (Fbm(x * 0.052f, y * 0.052f, seed + 9202, 4) * 0.28f) +
                (RidgedFbm((x + 9.1f) * 0.074f, (y - 4.2f) * 0.074f, seed + 9203, 4) * 0.34f);
            float volcanic = 0.18f +
                (RidgedFbm((x - 12.4f) * 0.044f, (y + 6.7f) * 0.044f, seed + 9204, 4) * 0.46f);
            float reef = 0.12f +
                (Fbm((x + 4.6f) * 0.058f, (y + 11.8f) * 0.058f, seed + 9205, 4) * 0.36f) +
                (SmoothStep(0.34f, 0.82f, ArchipelagoClusterSupport(x, y, seed)) * 0.12f);
            float sedimentary = 0.12f +
                (Fbm((x - 8.8f) * 0.050f, (y - 13.6f) * 0.050f, seed + 9208, 4) * 0.30f);
            float sand = 0.14f +
                (Fbm((x + y) * 0.046f, (y - x) * 0.046f, seed + 9206, 4) * 0.34f) +
                ((1f - RidgedFbm(x * 0.06f, y * 0.06f, seed + 9207, 3)) * 0.18f);

            limestone = MathF.Max(0.02f, limestone);
            volcanic = MathF.Max(0.02f, volcanic);
            reef = MathF.Max(0.02f, reef);
            sedimentary = MathF.Max(0.02f, sedimentary);
            sand = MathF.Max(0.02f, sand);
            float total = limestone + volcanic + reef + sedimentary + sand;
            return new TerrainSubstrateWeights(limestone / total, volcanic / total, reef / total, sand / total, sedimentary / total);
        }

        private static float EstimateBaseField(float x, float y, int seed)
        {
            return EstimateBaseField(x, y, seed, ResolveSubstrateWeights(x, y, seed));
        }

        private static float EstimateBaseField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            int cellX = (int)MathF.Floor(x / ArchipelagoCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoCellSize);
            float field = -1.72f + (sampleSubstrate.ReefLimestone * 0.04f) + (sampleSubstrate.SandSediment * 0.03f);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int islandCellX = cellX + offsetX;
                    int islandCellY = cellY + offsetY;
                    if (!TryResolveIslandCluster(
                        islandCellX,
                        islandCellY,
                        seed,
                        out float mainX,
                        out float mainY,
                        out float radius,
                        out float elongation,
                        out float angle,
                        out int mainSeed))
                    {
                        continue;
                    }

                    TerrainSubstrateWeights islandSubstrate = ResolveDominantSubstrateWeights(mainX, mainY, seed);
                    float islandRadius = radius * (0.86f + (islandSubstrate.VolcanicRock * 0.36f) + (islandSubstrate.LimestoneKarst * 0.2f) - (islandSubstrate.SandSediment * 0.08f));
                    if (Distance(x, y, mainX, mainY) > islandRadius * 2.15f)
                    {
                        continue;
                    }

                    float islandField = IslandContribution(x, y, mainX, mainY, islandRadius, mainSeed, elongation, angle);
                    islandField += CliffWallField(x, y, mainX, mainY, islandRadius, mainSeed, elongation, angle, islandSubstrate);
                    field = MathF.Max(field, islandField);

                    int satellites = ResolveIslandSatelliteCount(islandCellX, islandCellY, seed);
                    for (int satelliteIndex = 0; satelliteIndex < satellites; satelliteIndex++)
                    {
                        ResolveIslandSatellite(
                            islandCellX,
                            islandCellY,
                            seed,
                            satelliteIndex,
                            mainX,
                            mainY,
                            radius,
                            out float satelliteX,
                            out float satelliteY,
                            out float satelliteRadius,
                            out float satelliteElongation,
                            out float satelliteRotation,
                            out int satelliteSeed);

                        TerrainSubstrateWeights satelliteSubstrate = ResolveDominantSubstrateWeights(satelliteX, satelliteY, seed);
                        float satelliteRadiusScale = 0.82f + (satelliteSubstrate.VolcanicRock * 0.22f) + (satelliteSubstrate.LimestoneKarst * 0.16f);
                        float resolvedSatelliteRadius = satelliteRadius * satelliteRadiusScale;
                        if (Distance(x, y, satelliteX, satelliteY) > resolvedSatelliteRadius * 2.15f)
                        {
                            continue;
                        }

                        field = MathF.Max(
                            field,
                            IslandContribution(
                                x,
                                y,
                                satelliteX,
                                satelliteY,
                                resolvedSatelliteRadius,
                                satelliteSeed,
                                satelliteElongation,
                                satelliteRotation));
                    }
                }
            }

            field += Fbm(x * 0.035f, y * 0.035f, seed + 1300, 5) * (0.18f + (sampleSubstrate.VolcanicRock * 0.1f));
            field += MathF.Pow(RidgedFbm(x * 0.18f, y * 0.18f, seed + 1310, 4), 2.1f) * sampleSubstrate.VolcanicRock * 0.12f;
            float clusterSupport = ArchipelagoClusterSupport(x, y, seed);
            field += (clusterSupport * 0.26f) - ((1f - clusterSupport) * 1.08f);
            return field;
        }

        private static float ArchipelagoClusterSupport(float x, float y, int seed)
        {
            int cellX = (int)MathF.Floor(x / ArchipelagoMacroCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoMacroCellSize);
            float best = 0f;

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int macroCellX = cellX + offsetX;
                    int macroCellY = cellY + offsetY;
                    float presence = Hash2(macroCellX, macroCellY, seed + 1500);
                    if (presence < 0.36f)
                    {
                        continue;
                    }

                    float centerX = (macroCellX + 0.18f + (Hash2(macroCellX, macroCellY, seed + 1501) * 0.64f)) * ArchipelagoMacroCellSize;
                    float centerY = (macroCellY + 0.18f + (Hash2(macroCellX, macroCellY, seed + 1502) * 0.64f)) * ArchipelagoMacroCellSize;
                    float radius = ArchipelagoMacroCellSize * (0.42f + (Hash2(macroCellX, macroCellY, seed + 1503) * 0.58f));
                    float cluster = SmoothStep(radius, radius * 0.18f, Distance(x, y, centerX, centerY));
                    best = MathF.Max(best, cluster);
                }
            }

            float ridge = SmoothStep(0.48f, 0.84f, RidgedFbm(x * 0.024f, y * 0.024f, seed + 1510, 4));
            float shelf = SmoothStep(-0.12f, 0.56f, Fbm(x * 0.012f, y * 0.012f, seed + 1511, 5));
            return Math.Clamp(MathF.Max(best, ridge * 0.54f) + (shelf * 0.22f), 0f, 1f);
        }

        private static float CliffWallField(
            float x,
            float y,
            float centerX,
            float centerY,
            float radius,
            int seed,
            float elongation,
            float angle,
            TerrainSubstrateWeights substrate)
        {
            float cliffSupport = Math.Clamp((substrate.VolcanicRock * 0.85f) + (substrate.LimestoneKarst * 0.65f), 0f, 1f);
            if (cliffSupport <= 0.001f)
            {
                return 0f;
            }

            float ring = RingRidgeValue(
                x,
                y,
                centerX,
                centerY,
                radius * (0.86f + (cliffSupport * 0.1f)),
                0.18f + (cliffSupport * 0.08f),
                angle,
                elongation,
                seed + 1320,
                out _,
                out _,
                out _);
            float cliffTexture = SmoothStep(0.45f, 0.88f, RidgedFbm(x * 0.92f, y * 0.92f, seed + 1321, 4));
            return MathF.Max(0f, ring + (cliffTexture * 0.18f)) * cliffSupport * 0.26f;
        }

        private static float CoralReefEnclosureField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            if (sampleSubstrate.ReefLimestone + (sampleSubstrate.SandSediment * 0.35f) < 0.08f)
            {
                return -999f;
            }

            float best = -999f;
            int islandCellX = (int)MathF.Floor(x / ArchipelagoCellSize);
            int islandCellY = (int)MathF.Floor(y / ArchipelagoCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = islandCellX + offsetX;
                    int cellY = islandCellY + offsetY;
                    if (!TryResolveIslandCluster(
                        cellX,
                        cellY,
                        seed,
                        out float mainX,
                        out float mainY,
                        out float radius,
                        out float elongation,
                        out float angle,
                        out _))
                    {
                        continue;
                    }

                    TerrainSubstrateWeights reefSubstrate = ResolveDominantSubstrateWeights(mainX, mainY, seed);
                    float reefSupport = Math.Clamp(
                        reefSubstrate.ReefLimestone + (reefSubstrate.SandSediment * 0.35f) + (sampleSubstrate.ReefLimestone * 0.25f),
                        0f,
                        1f);
                    if (reefSupport < 0.18f)
                    {
                        continue;
                    }

                    float reefRadius = radius * (1.72f + (Hash2(cellX, cellY, seed + 2300) * 0.5f));
                    float reefWidth = 0.22f + (Hash2(cellX, cellY, seed + 2301) * 0.16f);
                    float reefRawDistance = Distance(x, y, mainX, mainY);
                    if (MathF.Abs(reefRawDistance - reefRadius) > reefWidth * 5.5f)
                    {
                        continue;
                    }

                    float reef = ReefRingField(
                        x,
                        y,
                        mainX,
                        mainY,
                        reefRadius,
                        reefWidth,
                        angle + (Hash2(cellX, cellY, seed + 2302) * 0.32f),
                        elongation * (0.86f + (Hash2(cellX, cellY, seed + 2303) * 0.24f)),
                        cellX,
                        cellY,
                        seed + 2304);
                    best = MathF.Max(best, reef * (0.74f + (reefSupport * 0.34f)));
                }
            }

            int ringCellX = (int)MathF.Floor((x + (ArchipelagoEnclosureCellSize * 0.5f)) / ArchipelagoEnclosureCellSize);
            int ringCellY = (int)MathF.Floor((y + (ArchipelagoEnclosureCellSize * 0.5f)) / ArchipelagoEnclosureCellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = ringCellX + offsetX;
                    int cellY = ringCellY + offsetY;
                    float centerX = (cellX + 0.25f + (Hash2(cellX, cellY, seed + 2400) * 0.5f)) * ArchipelagoEnclosureCellSize;
                    float centerY = (cellY + 0.25f + (Hash2(cellX, cellY, seed + 2401) * 0.5f)) * ArchipelagoEnclosureCellSize;
                    TerrainSubstrateWeights reefSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float reefSupport = reefSubstrate.ReefLimestone;
                    if (Hash2(cellX, cellY, seed + 2402) < 0.58f - (reefSupport * 0.28f))
                    {
                        continue;
                    }

                    float radius = 1.46f + (Hash2(cellX, cellY, seed + 2403) * 0.96f);
                    float width = 0.28f + (Hash2(cellX, cellY, seed + 2404) * 0.2f);
                    float rawDistance = Distance(x, y, centerX, centerY);
                    if (MathF.Abs(rawDistance - radius) > width * 5.5f)
                    {
                        continue;
                    }

                    float angle = Hash2(cellX, cellY, seed + 2405) * MathF.PI;
                    float elongation = 0.74f + (Hash2(cellX, cellY, seed + 2406) * 0.72f);
                    float ring = ReefRingField(x, y, centerX, centerY, radius, width, angle, elongation, cellX, cellY, seed + 2407);
                    best = MathF.Max(best, ring * (0.78f + (reefSupport * 0.32f)));
                }
            }

            return best;
        }

        private static float SandBarrierEnclosureField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            if (sampleSubstrate.SandSediment + (sampleSubstrate.ReefLimestone * 0.2f) < 0.08f)
            {
                return -999f;
            }

            float best = -999f;
            int cellX = (int)MathF.Floor(x / ArchipelagoBarrierCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoBarrierCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int barrierCellX = cellX + offsetX;
                    int barrierCellY = cellY + offsetY;
                    float centerX = (barrierCellX + 0.2f + (Hash2(barrierCellX, barrierCellY, seed + 2500) * 0.6f)) * ArchipelagoBarrierCellSize;
                    float centerY = (barrierCellY + 0.2f + (Hash2(barrierCellX, barrierCellY, seed + 2501) * 0.6f)) * ArchipelagoBarrierCellSize;
                    TerrainSubstrateWeights barrierSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float barrierSupport = Math.Clamp(
                        barrierSubstrate.SandSediment + (barrierSubstrate.ReefLimestone * 0.2f) + (sampleSubstrate.SandSediment * 0.2f),
                        0f,
                        1f);
                    if (Hash2(barrierCellX, barrierCellY, seed + 2502) < 0.5f - (barrierSupport * 0.26f))
                    {
                        continue;
                    }

                    float angle = (Hash2(barrierCellX, barrierCellY, seed + 2503) * MathF.PI) +
                        (Fbm(centerX * 0.08f, centerY * 0.08f, seed + 2504, 3) * 0.55f);
                    float halfLength = 1.08f + (Hash2(barrierCellX, barrierCellY, seed + 2505) * 0.96f);
                    float width = 0.24f + (Hash2(barrierCellX, barrierCellY, seed + 2506) * 0.16f);
                    Rotate(x - centerX, y - centerY, -angle, out float preLocalX, out float preLocalY);
                    if (MathF.Abs(preLocalX) > halfLength + (width * 6f) ||
                        MathF.Abs(preLocalY) > width * 8f)
                    {
                        continue;
                    }

                    float barrier = CurvedBarrierField(
                        x,
                        y,
                        centerX,
                        centerY,
                        angle,
                        halfLength,
                        width,
                        barrierCellX,
                        barrierCellY,
                        seed + 2507,
                        out float localX,
                        out float localY);
                    barrier -= TidalInletCut(localX, localY, halfLength, width, barrierCellX, barrierCellY, seed + 2508);
                    barrier = MathF.Max(
                        barrier,
                        SpitHookField(x, y, centerX, centerY, angle, halfLength, width, barrierCellX, barrierCellY, seed + 2509));
                    best = MathF.Max(best, barrier * (0.74f + (barrierSupport * 0.36f)));
                }
            }

            return best;
        }

        private static float IslandRingEnclosureField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            if (sampleSubstrate.VolcanicRock + sampleSubstrate.LimestoneKarst + sampleSubstrate.ReefLimestone < 0.08f)
            {
                return -999f;
            }

            float best = -999f;
            int cellX = (int)MathF.Floor(x / ArchipelagoEnclosureCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoEnclosureCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int ringCellX = cellX + offsetX;
                    int ringCellY = cellY + offsetY;
                    if (Hash2(ringCellX, ringCellY, seed + 2600) < 0.56f)
                    {
                        continue;
                    }

                    float centerX = (ringCellX + 0.2f + (Hash2(ringCellX, ringCellY, seed + 2601) * 0.6f)) * ArchipelagoEnclosureCellSize;
                    float centerY = (ringCellY + 0.2f + (Hash2(ringCellX, ringCellY, seed + 2602) * 0.6f)) * ArchipelagoEnclosureCellSize;
                    TerrainSubstrateWeights ringSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float landSupport = Math.Clamp(
                        0.35f + (ringSubstrate.VolcanicRock * 0.24f) + (ringSubstrate.LimestoneKarst * 0.22f) + (sampleSubstrate.ReefLimestone * 0.18f),
                        0f,
                        1f);
                    float radius = 1.34f + (Hash2(ringCellX, ringCellY, seed + 2603) * 1.02f);
                    float width = 0.32f + (Hash2(ringCellX, ringCellY, seed + 2604) * 0.22f);
                    float rawDistance = Distance(x, y, centerX, centerY);
                    if (MathF.Abs(rawDistance - radius) > width * 5.5f)
                    {
                        continue;
                    }

                    float angle = Hash2(ringCellX, ringCellY, seed + 2605) * MathF.PI;
                    float elongation = 0.76f + (Hash2(ringCellX, ringCellY, seed + 2606) * 0.68f);
                    float ring = RingRidgeValue(
                        x,
                        y,
                        centerX,
                        centerY,
                        radius,
                        width,
                        angle,
                        elongation,
                        seed + 2607,
                        out float theta,
                        out float distance,
                        out float warpedRadius);
                    float isletNoise = Fbm(
                        (MathF.Cos(theta) * 3.4f) + ringCellX,
                        (MathF.Sin(theta) * 3.4f) + ringCellY,
                        seed + 2608,
                        3);
                    float isletMask = SmoothStep(-0.18f, 0.34f, isletNoise + (landSupport * 0.22f));
                    int openingCount = ResolveOpeningCount(ringCellX, ringCellY, seed + 2609);
                    float channelCut = AngularOpeningMask(
                        theta,
                        distance,
                        warpedRadius,
                        width,
                        openingCount,
                        ringCellX,
                        ringCellY,
                        seed + 2610,
                        0.32f);
                    best = MathF.Max(best, ((ring * isletMask) - (channelCut * 0.92f)) * (0.78f + (landSupport * 0.24f)));
                }
            }

            return best;
        }

        private static float KarstCollapseDeltaField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            if (sampleSubstrate.LimestoneKarst < 0.1f)
            {
                return 0f;
            }

            float delta = 0f;
            int cellX = (int)MathF.Floor(x / ArchipelagoKarstCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoKarstCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int karstCellX = cellX + offsetX;
                    int karstCellY = cellY + offsetY;
                    float centerX = (karstCellX + 0.22f + (Hash2(karstCellX, karstCellY, seed + 2700) * 0.56f)) * ArchipelagoKarstCellSize;
                    float centerY = (karstCellY + 0.22f + (Hash2(karstCellX, karstCellY, seed + 2701) * 0.56f)) * ArchipelagoKarstCellSize;
                    TerrainSubstrateWeights karstSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float karstSupport = Math.Clamp(karstSubstrate.LimestoneKarst + (sampleSubstrate.LimestoneKarst * 0.25f), 0f, 1f);
                    if (Hash2(karstCellX, karstCellY, seed + 2702) < 0.68f - (karstSupport * 0.38f))
                    {
                        continue;
                    }

                    float radius = 0.58f + (Hash2(karstCellX, karstCellY, seed + 2703) * 0.62f);
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = MathF.Sqrt((dx * dx) + (dy * dy));
                    if (distance > radius * 5.2f)
                    {
                        continue;
                    }

                    float basin = SmoothStep(radius, radius * 0.32f, distance);
                    float rim = MathF.Max(0f, 1f - (MathF.Abs(distance - (radius * 1.04f)) / (0.12f + (radius * 0.12f))));
                    rim *= SmoothStep(radius * 1.55f, radius * 0.84f, distance);

                    int openingCount = ResolveOpeningCount(karstCellX, karstCellY, seed + 2704);
                    float caveCut = 0f;
                    for (int openingIndex = 0; openingIndex < openingCount; openingIndex++)
                    {
                        float angle = Hash2(karstCellX + (openingIndex * 11), karstCellY - (openingIndex * 13), seed + 2705) * MathF.Tau;
                        float length = radius * (2.6f + (Hash2(karstCellX - openingIndex, karstCellY + openingIndex, seed + 2706) * 1.2f));
                        float tunnelCenterX = centerX + (MathF.Cos(angle) * (radius + (length * 0.36f)));
                        float tunnelCenterY = centerY + (MathF.Sin(angle) * (radius + (length * 0.36f)));
                        caveCut = MathF.Max(
                            caveCut,
                            OrientedChannelField(x, y, tunnelCenterX, tunnelCenterY, angle, length * 0.58f, 0.105f + (radius * 0.09f)));
                    }

                    delta += (rim * karstSupport * 0.42f) - (basin * karstSupport * 1.08f) - (caveCut * karstSupport * 1.02f);
                }
            }

            return Math.Clamp(delta, -1.25f, 0.55f);
        }

        private static float DrownedRavineCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate, float baseField, float coastBand)
        {
            float cut = 0f;
            float support = Math.Clamp(sampleSubstrate.VolcanicRock + (sampleSubstrate.LimestoneKarst * 0.85f), 0f, 1f);
            float activation = MathF.Max(coastBand, SmoothStep(SeaLevel + 0.05f, SeaLevel + 0.55f, baseField) * 0.38f);
            if (support < 0.08f || activation < 0.04f)
            {
                return 0f;
            }

            float globalAngle = Fbm(x * 0.025f, y * 0.025f, seed + 2800, 3) * MathF.PI;
            Rotate(x, y, globalAngle + 0.72f, out float rotatedX1, out float rotatedY1);
            float calanque = SmoothStep(0.56f, 0.9f, RidgedFbm(rotatedX1 * 0.24f, rotatedY1 * 1.55f, seed + 2801, 5));
            cut = MathF.Max(cut, calanque * activation * (0.42f + (support * 0.5f)));

            int cellX = (int)MathF.Floor(x / ArchipelagoEnclosureCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoEnclosureCellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int ravineCellX = cellX + offsetX;
                    int ravineCellY = cellY + offsetY;
                    if (Hash2(ravineCellX, ravineCellY, seed + 2810) < 0.54f - (support * 0.24f))
                    {
                        continue;
                    }

                    float centerX = (ravineCellX + 0.25f + (Hash2(ravineCellX, ravineCellY, seed + 2811) * 0.5f)) * ArchipelagoEnclosureCellSize;
                    float centerY = (ravineCellY + 0.25f + (Hash2(ravineCellX, ravineCellY, seed + 2812) * 0.5f)) * ArchipelagoEnclosureCellSize;
                    float angle = (Hash2(ravineCellX, ravineCellY, seed + 2813) * MathF.Tau) +
                        (Fbm(centerX * 0.04f, centerY * 0.04f, seed + 2814, 3) * 0.55f);
                    float halfLength = 1.58f + (Hash2(ravineCellX, ravineCellY, seed + 2815) * 1.38f);
                    float width = 0.17f + (Hash2(ravineCellX, ravineCellY, seed + 2816) * 0.18f);
                    Rotate(x - centerX, y - centerY, -angle, out float preLocalX, out float preLocalY);
                    if (MathF.Abs(preLocalX) > halfLength + (width * 4f) ||
                        MathF.Abs(preLocalY) > width * 5f)
                    {
                        continue;
                    }

                    float channel = OrientedChannelField(x, y, centerX, centerY, angle, halfLength, width);
                    Rotate(x - centerX, y - centerY, -angle, out float localX, out float localY);
                    float interior = EllipseField(localX - (halfLength * 0.42f), localY, halfLength * 0.38f, width * 2.6f);
                    cut = MathF.Max(cut, (channel * 0.68f) + (interior * 0.38f));
                }
            }

            return cut * (0.5f + (support * 0.6f));
        }

        private static float ErosiveOpeningCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate, float coastBand)
        {
            if (coastBand < 0.04f)
            {
                return 0f;
            }

            float globalAngle = Fbm(x * 0.025f, y * 0.025f, seed + 2900, 3) * MathF.PI;
            Rotate(x, y, globalAngle - 0.48f, out float fractureX, out float fractureY);
            float fracture = SmoothStep(0.7f, 0.94f, RidgedFbm(fractureX * 0.18f, fractureY * 2.45f, seed + 2901, 5));

            Rotate(x + 18.4f, y - 7.2f, globalAngle + 1.15f, out float caveX, out float caveY);
            float caveAxis = 1f - MathF.Abs(ValueNoise(caveX * 0.44f, caveY * 2.25f, seed + 2902));
            float caveTunnel = SmoothStep(0.82f, 0.98f, caveAxis) *
                MathF.Pow(RidgedFbm(x * 0.7f, y * 0.7f, seed + 2903, 5), 2.25f);

            float stormCuts = SmoothStep(0.56f, 0.88f, RidgedFbm((x * 0.88f) + 21f, (y * 0.88f) - 13f, seed + 2904, 4));
            float caveSupport = Math.Clamp(sampleSubstrate.LimestoneKarst + (sampleSubstrate.VolcanicRock * 0.35f), 0f, 1f);
            float fractureSupport = Math.Clamp(sampleSubstrate.VolcanicRock + (sampleSubstrate.LimestoneKarst * 0.45f), 0f, 1f);

            return coastBand * ((fracture * (0.3f + (fractureSupport * 0.34f))) +
                (caveTunnel * caveSupport * 0.58f) +
                (stormCuts * (sampleSubstrate.SandSediment + sampleSubstrate.ReefLimestone) * 0.3f));
        }

        private static float EnclosedLagoonBasinCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate, float baseField)
        {
            float cut = 0f;
            cut = MathF.Max(cut, ReefLagoonBasinCutField(x, y, seed, sampleSubstrate, baseField));
            cut = MathF.Max(cut, BarrierLagoonBasinCutField(x, y, seed, sampleSubstrate));
            cut = MathF.Max(cut, IslandRingLagoonBasinCutField(x, y, seed, sampleSubstrate));
            return Math.Clamp(cut * LagoonBasinCutStrength, 0f, 1.35f);
        }

        private static float ReefLagoonBasinCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate, float baseField)
        {
            if (sampleSubstrate.ReefLimestone + (sampleSubstrate.SandSediment * 0.35f) < 0.08f)
            {
                return 0f;
            }

            float cut = 0f;
            int islandCellX = (int)MathF.Floor(x / ArchipelagoCellSize);
            int islandCellY = (int)MathF.Floor(y / ArchipelagoCellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = islandCellX + offsetX;
                    int cellY = islandCellY + offsetY;
                    if (!TryResolveIslandCluster(
                        cellX,
                        cellY,
                        seed,
                        out float mainX,
                        out float mainY,
                        out float radius,
                        out float elongation,
                        out float angle,
                        out _))
                    {
                        continue;
                    }

                    TerrainSubstrateWeights reefSubstrate = ResolveDominantSubstrateWeights(mainX, mainY, seed);
                    float reefSupport = Math.Clamp(
                        reefSubstrate.ReefLimestone + (reefSubstrate.SandSediment * 0.35f) + (sampleSubstrate.ReefLimestone * 0.25f),
                        0f,
                        1f);
                    if (reefSupport < 0.18f)
                    {
                        continue;
                    }

                    float reefRadius = radius * (1.72f + (Hash2(cellX, cellY, seed + 2300) * 0.5f));
                    float reefWidth = 0.22f + (Hash2(cellX, cellY, seed + 2301) * 0.16f);
                    if (Distance(x, y, mainX, mainY) > reefRadius + (reefWidth * 3.6f))
                    {
                        continue;
                    }

                    RingRidgeValue(
                        x,
                        y,
                        mainX,
                        mainY,
                        reefRadius,
                        reefWidth,
                        angle + (Hash2(cellX, cellY, seed + 2302) * 0.32f),
                        elongation * (0.86f + (Hash2(cellX, cellY, seed + 2303) * 0.24f)),
                        seed + 2304,
                        out _,
                        out float distance,
                        out float warpedRadius);
                    float innerWater = SmoothStep(warpedRadius * 0.92f, warpedRadius * 0.32f, distance);
                    float islandKeepout = SmoothStep(radius * 0.72f, radius * 1.08f, Distance(x, y, mainX, mainY));
                    cut = MathF.Max(cut, innerWater * islandKeepout * (0.48f + (reefSupport * 0.5f)));
                }
            }

            int ringCellX = (int)MathF.Floor((x + (ArchipelagoEnclosureCellSize * 0.5f)) / ArchipelagoEnclosureCellSize);
            int ringCellY = (int)MathF.Floor((y + (ArchipelagoEnclosureCellSize * 0.5f)) / ArchipelagoEnclosureCellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = ringCellX + offsetX;
                    int cellY = ringCellY + offsetY;
                    float centerX = (cellX + 0.25f + (Hash2(cellX, cellY, seed + 2400) * 0.5f)) * ArchipelagoEnclosureCellSize;
                    float centerY = (cellY + 0.25f + (Hash2(cellX, cellY, seed + 2401) * 0.5f)) * ArchipelagoEnclosureCellSize;
                    TerrainSubstrateWeights reefSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float reefSupport = reefSubstrate.ReefLimestone;
                    if (Hash2(cellX, cellY, seed + 2402) < 0.58f - (reefSupport * 0.28f))
                    {
                        continue;
                    }

                    float radius = 1.46f + (Hash2(cellX, cellY, seed + 2403) * 0.96f);
                    float width = 0.28f + (Hash2(cellX, cellY, seed + 2404) * 0.2f);
                    if (Distance(x, y, centerX, centerY) > radius + (width * 3.8f))
                    {
                        continue;
                    }

                    float angle = Hash2(cellX, cellY, seed + 2405) * MathF.PI;
                    float elongation = 0.74f + (Hash2(cellX, cellY, seed + 2406) * 0.72f);
                    RingRidgeValue(x, y, centerX, centerY, radius, width, angle, elongation, seed + 2407, out _, out float distance, out float warpedRadius);
                    float basin = SmoothStep(warpedRadius * 0.86f, warpedRadius * 0.24f, distance);
                    float shallowGate = SmoothStep(SeaLevel - 0.18f, SeaLevel + 0.26f, baseField);
                    cut = MathF.Max(cut, basin * (0.48f + (reefSupport * 0.52f)) * (0.72f + (shallowGate * 0.28f)));
                }
            }

            return cut;
        }

        private static float BarrierLagoonBasinCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            if (sampleSubstrate.SandSediment + (sampleSubstrate.ReefLimestone * 0.2f) < 0.08f)
            {
                return 0f;
            }

            float cut = 0f;
            int cellX = (int)MathF.Floor(x / ArchipelagoBarrierCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoBarrierCellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int barrierCellX = cellX + offsetX;
                    int barrierCellY = cellY + offsetY;
                    float centerX = (barrierCellX + 0.2f + (Hash2(barrierCellX, barrierCellY, seed + 2500) * 0.6f)) * ArchipelagoBarrierCellSize;
                    float centerY = (barrierCellY + 0.2f + (Hash2(barrierCellX, barrierCellY, seed + 2501) * 0.6f)) * ArchipelagoBarrierCellSize;
                    TerrainSubstrateWeights barrierSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float barrierSupport = Math.Clamp(
                        barrierSubstrate.SandSediment + (barrierSubstrate.ReefLimestone * 0.2f) + (sampleSubstrate.SandSediment * 0.2f),
                        0f,
                        1f);
                    if (Hash2(barrierCellX, barrierCellY, seed + 2502) < 0.5f - (barrierSupport * 0.26f))
                    {
                        continue;
                    }

                    float angle = (Hash2(barrierCellX, barrierCellY, seed + 2503) * MathF.PI) +
                        (Fbm(centerX * 0.08f, centerY * 0.08f, seed + 2504, 3) * 0.55f);
                    float halfLength = 1.08f + (Hash2(barrierCellX, barrierCellY, seed + 2505) * 0.96f);
                    float width = 0.24f + (Hash2(barrierCellX, barrierCellY, seed + 2506) * 0.16f);
                    CurvedBarrierField(
                        x,
                        y,
                        centerX,
                        centerY,
                        angle,
                        halfLength,
                        width,
                        barrierCellX,
                        barrierCellY,
                        seed + 2507,
                        out float localX,
                        out float localY);

                    float backSide = Hash2(barrierCellX, barrierCellY, seed + 2511) < 0.5f ? -1f : 1f;
                    float axialMask = SmoothStep(1.2f, 0.2f, MathF.Abs(localX) / MathF.Max(0.001f, halfLength));
                    float waterBehindBarrier = SmoothStep(width * 6.8f, width * 0.9f, MathF.Abs(localY - (backSide * width * 3.2f)));
                    float seaSideKeepout = SmoothStep(width * 0.8f, width * 2.8f, MathF.Abs(localY));
                    cut = MathF.Max(cut, axialMask * waterBehindBarrier * seaSideKeepout * (0.48f + (barrierSupport * 0.46f)));
                }
            }

            return cut;
        }

        private static float IslandRingLagoonBasinCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate)
        {
            if (sampleSubstrate.VolcanicRock + sampleSubstrate.LimestoneKarst + sampleSubstrate.ReefLimestone < 0.08f)
            {
                return 0f;
            }

            float cut = 0f;
            int cellX = (int)MathF.Floor(x / ArchipelagoEnclosureCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoEnclosureCellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int ringCellX = cellX + offsetX;
                    int ringCellY = cellY + offsetY;
                    if (Hash2(ringCellX, ringCellY, seed + 2600) < 0.56f)
                    {
                        continue;
                    }

                    float centerX = (ringCellX + 0.2f + (Hash2(ringCellX, ringCellY, seed + 2601) * 0.6f)) * ArchipelagoEnclosureCellSize;
                    float centerY = (ringCellY + 0.2f + (Hash2(ringCellX, ringCellY, seed + 2602) * 0.6f)) * ArchipelagoEnclosureCellSize;
                    TerrainSubstrateWeights ringSubstrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
                    float landSupport = Math.Clamp(
                        0.35f + (ringSubstrate.VolcanicRock * 0.24f) + (ringSubstrate.LimestoneKarst * 0.22f) + (sampleSubstrate.ReefLimestone * 0.18f),
                        0f,
                        1f);
                    float radius = 1.34f + (Hash2(ringCellX, ringCellY, seed + 2603) * 1.02f);
                    float width = 0.32f + (Hash2(ringCellX, ringCellY, seed + 2604) * 0.22f);
                    if (Distance(x, y, centerX, centerY) > radius + (width * 3.6f))
                    {
                        continue;
                    }

                    float angle = Hash2(ringCellX, ringCellY, seed + 2605) * MathF.PI;
                    float elongation = 0.76f + (Hash2(ringCellX, ringCellY, seed + 2606) * 0.68f);
                    RingRidgeValue(
                        x,
                        y,
                        centerX,
                        centerY,
                        radius,
                        width,
                        angle,
                        elongation,
                        seed + 2607,
                        out _,
                        out float distance,
                        out float warpedRadius);
                    float basin = SmoothStep(warpedRadius * 0.78f, warpedRadius * 0.24f, distance);
                    cut = MathF.Max(cut, basin * (0.42f + (landSupport * 0.44f)));
                }
            }

            return cut;
        }

        private static float RegionalTidalChannelCutField(float x, float y, int seed, TerrainSubstrateWeights sampleSubstrate, float field, float coastBand)
        {
            float landActivation = SmoothStep(SeaLevel - 0.34f, SeaLevel + 0.72f, field);
            if (landActivation <= 0.02f && coastBand <= 0.05f)
            {
                return 0f;
            }

            float support = Math.Clamp(
                (sampleSubstrate.SandSediment * 0.55f) +
                (sampleSubstrate.ReefLimestone * 0.5f) +
                (sampleSubstrate.LimestoneKarst * 0.34f) +
                (sampleSubstrate.VolcanicRock * 0.26f),
                0f,
                1f);
            float cut = 0f;
            int cellX = (int)MathF.Floor(x / ArchipelagoEnclosureCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoEnclosureCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int channelCellX = cellX + offsetX;
                    int channelCellY = cellY + offsetY;
                    if (Hash2(channelCellX, channelCellY, seed + 3000) < 0.42f - (support * 0.18f))
                    {
                        continue;
                    }

                    float centerX = (channelCellX + 0.18f + (Hash2(channelCellX, channelCellY, seed + 3001) * 0.64f)) * ArchipelagoEnclosureCellSize;
                    float centerY = (channelCellY + 0.18f + (Hash2(channelCellX, channelCellY, seed + 3002) * 0.64f)) * ArchipelagoEnclosureCellSize;
                    float angle = (Hash2(channelCellX, channelCellY, seed + 3003) * MathF.Tau) +
                        (Fbm(centerX * 0.035f, centerY * 0.035f, seed + 3004, 3) * 0.75f);
                    float halfLength = 1.42f + (Hash2(channelCellX, channelCellY, seed + 3005) * 1.45f);
                    float width = 0.14f + (Hash2(channelCellX, channelCellY, seed + 3006) * 0.2f);
                    Rotate(x - centerX, y - centerY, -angle, out float localX, out float localY);
                    if (MathF.Abs(localX) > halfLength + (width * 5.5f) ||
                        MathF.Abs(localY) > width * 6.5f)
                    {
                        continue;
                    }

                    float channel = OrientedChannelField(x, y, centerX, centerY, angle, halfLength, width);
                    float seaGate = MathF.Max(
                        EllipseField(localX - (halfLength * 0.58f), localY, halfLength * 0.3f, width * 2.7f),
                        EllipseField(localX + (halfLength * 0.58f), localY, halfLength * 0.3f, width * 2.7f));
                    float channelStrength = (channel * 0.86f) + (seaGate * 0.28f);
                    cut = MathF.Max(cut, channelStrength * (0.48f + (support * 0.52f)));
                }
            }

            float activation = Math.Clamp((landActivation * 0.62f) + (coastBand * 0.78f), 0f, 1f);
            return Math.Clamp(cut * activation * RegionalTidalChannelCutStrength, 0f, 1.15f);
        }

        private static float SampleSelectedLandformField(float x, float y, int seed)
        {
            float field = -1.18f + (Fbm(x * 0.04f, y * 0.04f, seed + 3300, 3) * 0.035f);
            int cellX = (int)MathF.Floor(x / ArchipelagoLandformCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoLandformCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    TerrainLandformDescriptor descriptor = ResolveLandformDescriptor(cellX + offsetX, cellY + offsetY, seed);
                    if (descriptor.Type == TerrainLandformType.OpenWater)
                    {
                        continue;
                    }

                    if (Distance(x, y, descriptor.CenterX, descriptor.CenterY) > ResolveLandformInfluenceRadius(descriptor))
                    {
                        continue;
                    }

                    field = MathF.Max(field, EvaluateSelectedLandform(descriptor, x, y));
                }
            }

            return field;
        }

        private static bool TryResolveDominantLandformDescriptor(
            float x,
            float y,
            int seed,
            out TerrainLandformDescriptor descriptor,
            out float field)
        {
            descriptor = default;
            field = float.MinValue;
            bool found = false;
            int cellX = (int)MathF.Floor(x / ArchipelagoLandformCellSize);
            int cellY = (int)MathF.Floor(y / ArchipelagoLandformCellSize);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    TerrainLandformDescriptor candidate = ResolveLandformDescriptor(cellX + offsetX, cellY + offsetY, seed);
                    if (candidate.Type == TerrainLandformType.OpenWater)
                    {
                        continue;
                    }

                    if (Distance(x, y, candidate.CenterX, candidate.CenterY) > ResolveLandformInfluenceRadius(candidate))
                    {
                        continue;
                    }

                    float candidateField = EvaluateSelectedLandform(candidate, x, y);
                    if (found && candidateField <= field)
                    {
                        continue;
                    }

                    found = true;
                    field = candidateField;
                    descriptor = candidate;
                }
            }

            return found;
        }

        private static TerrainLandformDescriptor ResolveLandformDescriptor(int cellX, int cellY, int seed)
        {
            float jitterX = (Hash2(cellX, cellY, seed + 3400) - 0.5f) * ArchipelagoLandformCenterJitter;
            float jitterY = (Hash2(cellX, cellY, seed + 3401) - 0.5f) * ArchipelagoLandformCenterJitter;
            float centerX = (cellX + 0.5f + jitterX) * ArchipelagoLandformCellSize;
            float centerY = (cellY + 0.5f + jitterY) * ArchipelagoLandformCellSize;
            TerrainSubstrateWeights substrate = ResolveDominantSubstrateWeights(centerX, centerY, seed);
            TerrainLandformType type = ResolveSelectedLandformType(cellX, cellY, seed, substrate);
            float sizeRoll = Hash2(cellX, cellY, seed + 3402);
            float widthRoll = Hash2(cellX, cellY, seed + 3403);
            float angle = Hash2(cellX, cellY, seed + 3404) * MathF.Tau;
            float elongation = 0.76f + (Hash2(cellX, cellY, seed + 3405) * 0.58f);
            int openingCount = ResolveOpeningCount(cellX, cellY, seed + 3406);
            float strength = 0.9f + (Hash2(cellX, cellY, seed + 3407) * 0.24f);

            float radius = type switch
            {
                TerrainLandformType.AtollLagoon => 2.05f + (sizeRoll * 1.05f),
                TerrainLandformType.ReefBarrierLagoon => 1.72f + (sizeRoll * 0.86f),
                TerrainLandformType.BarrierIslandLagoon => 1.22f + (sizeRoll * 0.72f),
                TerrainLandformType.KarstHongLagoon => 1.12f + (sizeRoll * 0.72f),
                TerrainLandformType.IslandRingLagoon => 1.62f + (sizeRoll * 1.02f),
                TerrainLandformType.CalanqueCoveLagoon => 1.18f + (sizeRoll * 0.74f),
                _ => 0f
            };

            float length = type switch
            {
                TerrainLandformType.BarrierIslandLagoon => 2.55f + (Hash2(cellX, cellY, seed + 3408) * 1.78f),
                TerrainLandformType.CalanqueCoveLagoon => 2.22f + (Hash2(cellX, cellY, seed + 3408) * 1.36f),
                TerrainLandformType.ReefBarrierLagoon => 2.1f + (Hash2(cellX, cellY, seed + 3408) * 1.22f),
                _ => radius
            };

            float width = type switch
            {
                TerrainLandformType.AtollLagoon => 0.24f + (widthRoll * 0.17f),
                TerrainLandformType.ReefBarrierLagoon => 0.22f + (widthRoll * 0.16f),
                TerrainLandformType.BarrierIslandLagoon => 0.2f + (widthRoll * 0.15f),
                TerrainLandformType.KarstHongLagoon => 0.2f + (widthRoll * 0.16f),
                TerrainLandformType.IslandRingLagoon => 0.26f + (widthRoll * 0.2f),
                TerrainLandformType.CalanqueCoveLagoon => 0.2f + (widthRoll * 0.16f),
                _ => 0f
            };

            int descriptorSeed = seed + (cellX * 92821) + (cellY * 68917);
            return new TerrainLandformDescriptor(
                type,
                cellX,
                cellY,
                centerX,
                centerY,
                radius,
                length,
                width,
                angle,
                elongation,
                openingCount,
                descriptorSeed,
                strength,
                substrate);
        }

        private static TerrainLandformType ResolveSelectedLandformType(
            int cellX,
            int cellY,
            int seed,
            TerrainSubstrateWeights substrate)
        {
            float openWater = 0.34f;
            float atoll = (substrate.ReefLimestone * 0.82f) + (substrate.SandSediment * 0.1f);
            float reefBarrier = (substrate.ReefLimestone * 0.72f) + (substrate.SandSediment * 0.18f);
            float barrierIsland = (substrate.SandSediment * 0.92f) + (substrate.ReefLimestone * 0.18f);
            float karstHong = substrate.LimestoneKarst * 0.92f;
            float islandRing = (substrate.VolcanicRock * 0.6f) + (substrate.ReefLimestone * 0.34f) + (substrate.LimestoneKarst * 0.22f);
            float calanque = (substrate.VolcanicRock * 0.48f) + (substrate.LimestoneKarst * 0.5f);
            float total = openWater + atoll + reefBarrier + barrierIsland + karstHong + islandRing + calanque;
            float roll = Hash2(cellX, cellY, seed + 3410) * total;

            if ((roll -= openWater) < 0f)
            {
                return TerrainLandformType.OpenWater;
            }
            if ((roll -= atoll) < 0f)
            {
                return TerrainLandformType.AtollLagoon;
            }
            if ((roll -= reefBarrier) < 0f)
            {
                return TerrainLandformType.ReefBarrierLagoon;
            }
            if ((roll -= barrierIsland) < 0f)
            {
                return TerrainLandformType.BarrierIslandLagoon;
            }
            if ((roll -= karstHong) < 0f)
            {
                return TerrainLandformType.KarstHongLagoon;
            }
            if ((roll -= islandRing) < 0f)
            {
                return TerrainLandformType.IslandRingLagoon;
            }

            return TerrainLandformType.CalanqueCoveLagoon;
        }

        private static float EvaluateSelectedLandform(TerrainLandformDescriptor descriptor, float x, float y)
        {
            return descriptor.Type switch
            {
                TerrainLandformType.AtollLagoon => EvaluateAtollLagoon(descriptor, x, y),
                TerrainLandformType.ReefBarrierLagoon => EvaluateReefBarrierLagoon(descriptor, x, y),
                TerrainLandformType.BarrierIslandLagoon => EvaluateBarrierIslandLagoon(descriptor, x, y),
                TerrainLandformType.KarstHongLagoon => EvaluateKarstHongLagoon(descriptor, x, y),
                TerrainLandformType.IslandRingLagoon => EvaluateIslandRingLagoon(descriptor, x, y),
                TerrainLandformType.CalanqueCoveLagoon => EvaluateCalanqueCoveLagoon(descriptor, x, y),
                _ => -1.18f
            };
        }

        private static float EvaluateAtollLagoon(TerrainLandformDescriptor descriptor, float x, float y)
        {
            float ring = ReefRingField(
                x,
                y,
                descriptor.CenterX,
                descriptor.CenterY,
                descriptor.Radius,
                descriptor.Width,
                descriptor.Angle,
                descriptor.Elongation,
                descriptor.CellX,
                descriptor.CellY,
                descriptor.Seed + 10);
            float motu = RingBeadField(descriptor, x, y, descriptor.Seed + 18, 5.8f, 0.05f, 0.5f);
            return -1.05f + (MathF.Max(0f, ring) * descriptor.Strength * 1.64f) + (motu * 0.34f);
        }

        private static float EvaluateReefBarrierLagoon(TerrainLandformDescriptor descriptor, float x, float y)
        {
            float reefRadius = descriptor.Radius * 1.18f;
            float ring = RingRidgeValue(
                x,
                y,
                descriptor.CenterX,
                descriptor.CenterY,
                reefRadius,
                descriptor.Width,
                descriptor.Angle,
                descriptor.Elongation,
                descriptor.Seed + 30,
                out float theta,
                out float distance,
                out float warpedRadius);
            float arcCenter = Hash2(descriptor.CellX, descriptor.CellY, descriptor.Seed + 31) * MathF.Tau;
            float arcSpan = 1.45f + (Hash2(descriptor.CellX, descriptor.CellY, descriptor.Seed + 32) * 1.25f);
            float arcMask = SmoothStep(arcSpan, arcSpan * 0.46f, AngularDistance(theta, arcCenter));
            float openingCut = AngularOpeningMask(
                theta,
                distance,
                warpedRadius,
                descriptor.Width,
                descriptor.OpeningCount,
                descriptor.CellX,
                descriptor.CellY,
                descriptor.Seed + 33,
                0.26f);
            float reefTexture = Fbm(x * 1.1f, y * 1.1f, descriptor.Seed + 34, 4) * 0.12f;
            float reef = MathF.Max(0f, ring + reefTexture - (openingCut * 1.05f));
            float field = -1.05f + (reef * arcMask * descriptor.Strength * 1.72f);

            float islandAngle = arcCenter + MathF.PI;
            float islandX = descriptor.CenterX + (MathF.Cos(islandAngle) * descriptor.Radius * 0.42f);
            float islandY = descriptor.CenterY + (MathF.Sin(islandAngle) * descriptor.Radius * 0.42f);
            float island = IslandContribution(
                x,
                y,
                islandX,
                islandY,
                descriptor.Radius * 0.32f,
                descriptor.Seed + 35,
                descriptor.Elongation * 0.92f,
                descriptor.Angle + 0.4f) - 0.18f;

            return MathF.Max(field, island * 0.72f);
        }

        private static float EvaluateBarrierIslandLagoon(TerrainLandformDescriptor descriptor, float x, float y)
        {
            float barrier = CurvedBarrierField(
                x,
                y,
                descriptor.CenterX,
                descriptor.CenterY,
                descriptor.Angle,
                descriptor.Length,
                descriptor.Width,
                descriptor.CellX,
                descriptor.CellY,
                descriptor.Seed + 50,
                out float localX,
                out float localY);
            barrier -= TidalInletCut(localX, localY, descriptor.Length, descriptor.Width, descriptor.CellX, descriptor.CellY, descriptor.Seed + 51);
            barrier = MathF.Max(
                barrier,
                SpitHookField(
                    x,
                    y,
                    descriptor.CenterX,
                    descriptor.CenterY,
                    descriptor.Angle,
                    descriptor.Length,
                    descriptor.Width,
                    descriptor.CellX,
                    descriptor.CellY,
                    descriptor.Seed + 52));
            return -1.02f + (MathF.Max(0f, barrier) * descriptor.Strength * 1.82f);
        }

        private static float EvaluateKarstHongLagoon(TerrainLandformDescriptor descriptor, float x, float y)
        {
            float ring = RingRidgeValue(
                x,
                y,
                descriptor.CenterX,
                descriptor.CenterY,
                descriptor.Radius,
                descriptor.Width,
                descriptor.Angle,
                descriptor.Elongation,
                descriptor.Seed + 70,
                out float theta,
                out float distance,
                out float warpedRadius);
            float openingCut = AngularOpeningMask(
                theta,
                distance,
                warpedRadius,
                descriptor.Width,
                descriptor.OpeningCount,
                descriptor.CellX,
                descriptor.CellY,
                descriptor.Seed + 71,
                0.24f);
            float cliffTexture = SmoothStep(0.42f, 0.86f, RidgedFbm(x * 0.9f, y * 0.9f, descriptor.Seed + 72, 4)) * 0.18f;
            float wall = MathF.Max(0f, ring + cliffTexture - (openingCut * 1.18f));
            return -1.06f + (wall * descriptor.Strength * 1.86f);
        }

        private static float EvaluateIslandRingLagoon(TerrainLandformDescriptor descriptor, float x, float y)
        {
            float ring = RingRidgeValue(
                x,
                y,
                descriptor.CenterX,
                descriptor.CenterY,
                descriptor.Radius,
                descriptor.Width,
                descriptor.Angle,
                descriptor.Elongation,
                descriptor.Seed + 90,
                out float theta,
                out float distance,
                out float warpedRadius);
            float isletNoise = Fbm(
                (MathF.Cos(theta) * 4.4f) + descriptor.CellX,
                (MathF.Sin(theta) * 4.4f) + descriptor.CellY,
                descriptor.Seed + 91,
                3);
            float isletMask = SmoothStep(-0.22f, 0.36f, isletNoise);
            float channelCut = AngularOpeningMask(
                theta,
                distance,
                warpedRadius,
                descriptor.Width,
                descriptor.OpeningCount,
                descriptor.CellX,
                descriptor.CellY,
                descriptor.Seed + 92,
                0.34f);
            float islets = MathF.Max(0f, (ring * isletMask) - (channelCut * 1.05f));
            return -1.04f + (islets * descriptor.Strength * 1.94f);
        }

        private static float EvaluateCalanqueCoveLagoon(TerrainLandformDescriptor descriptor, float x, float y)
        {
            Rotate(x - descriptor.CenterX, y - descriptor.CenterY, -descriptor.Angle, out float localX, out float localY);
            float mass = EllipseField(localX, localY, descriptor.Length, descriptor.Radius * 0.72f);
            float cliffTexture = 0.86f + (RidgedFbm(x * 0.58f, y * 0.58f, descriptor.Seed + 110, 4) * 0.2f);
            float channel = OrientedChannelField(
                x,
                y,
                descriptor.CenterX + (MathF.Cos(descriptor.Angle) * descriptor.Length * 0.22f),
                descriptor.CenterY + (MathF.Sin(descriptor.Angle) * descriptor.Length * 0.22f),
                descriptor.Angle,
                descriptor.Length * 0.74f,
                descriptor.Width);
            float protectedInterior = EllipseField(localX - (descriptor.Length * 0.18f), localY, descriptor.Length * 0.32f, descriptor.Radius * 0.42f);
            float mouth = OrientedChannelField(
                x,
                y,
                descriptor.CenterX + (MathF.Cos(descriptor.Angle) * descriptor.Length * 0.58f),
                descriptor.CenterY + (MathF.Sin(descriptor.Angle) * descriptor.Length * 0.58f),
                descriptor.Angle,
                descriptor.Length * 0.26f,
                descriptor.Width * 1.24f);
            float cut = MathF.Max(channel, MathF.Max(protectedInterior, mouth));
            return -0.98f + (mass * cliffTexture * descriptor.Strength * 1.82f) - (cut * 1.55f);
        }

        private static float RingBeadField(
            TerrainLandformDescriptor descriptor,
            float x,
            float y,
            int seed,
            float beadFrequency,
            float threshold,
            float strength)
        {
            RingRidgeValue(
                x,
                y,
                descriptor.CenterX,
                descriptor.CenterY,
                descriptor.Radius,
                descriptor.Width * 0.85f,
                descriptor.Angle,
                descriptor.Elongation,
                seed,
                out float theta,
                out float distance,
                out float warpedRadius);
            float ringEnvelope = SmoothStep(descriptor.Width * 2.1f, descriptor.Width * 0.35f, MathF.Abs(distance - warpedRadius));
            float beadNoise = Fbm(
                (MathF.Cos(theta) * beadFrequency) + descriptor.CellX,
                (MathF.Sin(theta) * beadFrequency) + descriptor.CellY,
                seed + 1,
                3);
            float beadMask = SmoothStep(threshold, threshold + 0.48f, beadNoise);
            return ringEnvelope * beadMask * strength;
        }

        private static float ResolveLandformInfluenceRadius(TerrainLandformDescriptor descriptor)
        {
            return descriptor.Type switch
            {
                TerrainLandformType.BarrierIslandLagoon => descriptor.Length + (descriptor.Width * 8f),
                TerrainLandformType.CalanqueCoveLagoon => descriptor.Length + descriptor.Radius + (descriptor.Width * 5f),
                TerrainLandformType.ReefBarrierLagoon => (descriptor.Radius * 1.4f) + (descriptor.Width * 6f),
                TerrainLandformType.KarstHongLagoon => descriptor.Radius + (descriptor.Width * 5.2f),
                TerrainLandformType.IslandRingLagoon => descriptor.Radius + (descriptor.Width * 5f),
                TerrainLandformType.AtollLagoon => descriptor.Radius + (descriptor.Width * 5.4f),
                _ => 0f
            };
        }

        private static float ResolveLandformAnchorRadius(TerrainLandformDescriptor descriptor)
        {
            return descriptor.Type switch
            {
                TerrainLandformType.BarrierIslandLagoon => descriptor.Length * 0.82f,
                TerrainLandformType.CalanqueCoveLagoon => descriptor.Length * 0.68f,
                TerrainLandformType.ReefBarrierLagoon => descriptor.Radius * 1.08f,
                _ => descriptor.Radius
            };
        }

        private static float WorldField(float x, float y, int seed)
        {
            return SampleLayeredTerrainCell(x, y, seed).Elevation;
        }

        private static TerrainCell SampleLayeredTerrainCell(float x, float y, int seed)
        {
            TerrainSubstrateWeights substrate = ResolveSubstrateWeights(x, y, seed);
            float macroSupport = ArchipelagoClusterSupport(x, y, seed);
            float preFloodElevation = EstimateBaseField(x, y, seed, substrate);
            float fractureStrength = ComputeFractureStrength(x, y, seed, substrate, macroSupport);
            float dissolution = Math.Clamp(
                substrate.LimestoneKarst * fractureStrength * (0.72f + (macroSupport * 0.28f)),
                0f,
                1f);

            float field = preFloodElevation;
            field += KarstCollapseDeltaField(x, y, seed, substrate) * (0.74f + (fractureStrength * 0.32f));

            float coastBand = ResolveCoastBand(field, 0.58f);
            float waveExposure = ComputeWaveExposure(x, y, seed, macroSupport, coastBand, substrate);
            float drownedRavineCut = DrownedRavineCutField(x, y, seed, substrate, preFloodElevation, coastBand);
            float erosiveCut = ErosiveOpeningCutField(x, y, seed, substrate, coastBand);
            field -= drownedRavineCut * (0.50f + (waveExposure * 0.34f));
            field -= erosiveCut * (0.42f + (waveExposure * 0.44f));

            float reefEnclosure = CoralReefEnclosureField(x, y, seed, substrate);
            float sandBarrier = SandBarrierEnclosureField(x, y, seed, substrate);
            float islandRing = IslandRingEnclosureField(x, y, seed, substrate);
            if (reefEnclosure > -900f)
            {
                field = MathF.Max(field, SeaLevel - 0.08f + (reefEnclosure * 0.56f));
            }
            if (sandBarrier > -900f)
            {
                field = MathF.Max(field, SeaLevel - 0.06f + (sandBarrier * 0.58f));
            }
            if (islandRing > -900f)
            {
                field = MathF.Max(field, SeaLevel - 0.07f + (islandRing * 0.52f));
            }

            float lagoonBasinCut = EnclosedLagoonBasinCutField(x, y, seed, substrate, preFloodElevation);
            field -= lagoonBasinCut * (0.78f + (MathF.Max(reefEnclosure, MathF.Max(sandBarrier, islandRing)) * 0.12f));

            coastBand = ResolveCoastBand(field, 0.62f);
            float tidalChannelCut = RegionalTidalChannelCutField(x, y, seed, substrate, field, coastBand);
            field -= tidalChannelCut;

            float waterDepth = MathF.Max(0f, SeaLevel - field);
            float sediment = ComputeSediment(x, y, seed, substrate, waterDepth, waveExposure, coastBand);
            float reefSuitability = ComputeReefSuitability(x, y, seed, substrate, waterDepth, waveExposure, macroSupport);
            field += sediment * coastBand * 0.10f;
            field += reefSuitability * Math.Clamp(1f - (waterDepth / 0.62f), 0f, 1f) * 0.06f;

            float tidalFlow = Math.Clamp((tidalChannelCut * 1.2f) + (drownedRavineCut * 0.52f) + (erosiveCut * 0.72f), 0f, 1f);
            float enclosure = Math.Clamp(
                MathF.Max(0f, reefEnclosure) +
                MathF.Max(0f, sandBarrier) +
                MathF.Max(0f, islandRing) +
                (lagoonBasinCut * 0.32f),
                0f,
                1f);
            float slope = Math.Clamp(
                (MathF.Abs(field - SeaLevel) * 1.08f) +
                (fractureStrength * 0.30f) +
                (dissolution * 0.24f),
                0f,
                1f);
            TerrainLandformTag tags = ClassifyLayeredLandforms(
                field,
                slope,
                waveExposure,
                sediment,
                reefSuitability,
                dissolution,
                fractureStrength,
                tidalFlow,
                macroSupport,
                enclosure,
                substrate);

            return new TerrainCell(
                field,
                SeaLevel,
                slope,
                waveExposure,
                sediment,
                reefSuitability,
                dissolution,
                fractureStrength,
                tidalFlow,
                macroSupport,
                enclosure,
                substrate.Dominant,
                tags);
        }

        private static float ComputeFractureStrength(float x, float y, int seed, TerrainSubstrateWeights substrate, float macroSupport)
        {
            float structuralAngle = Fbm(x * 0.018f, y * 0.018f, seed + 3600, 3) * MathF.PI;
            Rotate(x, y, structuralAngle, out float jointX, out float jointY);
            float jointSetA = SmoothStep(0.66f, 0.95f, RidgedFbm(jointX * 0.07f, jointY * 0.46f, seed + 3601, 5));
            Rotate(x + 17.4f, y - 9.8f, structuralAngle + 1.12f, out jointX, out jointY);
            float jointSetB = SmoothStep(0.68f, 0.96f, RidgedFbm(jointX * 0.06f, jointY * 0.40f, seed + 3602, 5));
            float drainage = SmoothStep(0.58f, 0.92f, RidgedFbm(x * 0.09f, y * 0.09f, seed + 3603, 4));
            float rockSupport = Math.Clamp(substrate.LimestoneKarst + (substrate.VolcanicRock * 0.82f) + (macroSupport * 0.16f), 0f, 1f);
            return Math.Clamp(MathF.Max(jointSetA, jointSetB) + (drainage * 0.24f), 0f, 1f) * rockSupport;
        }

        private static float ComputeWaveExposure(float x, float y, int seed, float macroSupport, float coastBand, TerrainSubstrateWeights substrate)
        {
            float openFetch = 1f - macroSupport;
            float stormTrack = (Fbm((x - 11.2f) * 0.018f, (y + 7.4f) * 0.018f, seed + 3610, 4) + 1f) * 0.5f;
            float resistantCliffBoost = (substrate.VolcanicRock * 0.16f) + (substrate.LimestoneKarst * 0.10f);
            return Math.Clamp((openFetch * 0.56f) + (stormTrack * 0.28f) + (coastBand * 0.20f) + resistantCliffBoost, 0f, 1f);
        }

        private static float ComputeSediment(float x, float y, int seed, TerrainSubstrateWeights substrate, float waterDepth, float waveExposure, float coastBand)
        {
            float shallow = 1f - Math.Clamp(waterDepth / 0.72f, 0f, 1f);
            float lowEnergy = 1f - waveExposure;
            float transportNoise = (Fbm(x * 0.12f, y * 0.12f, seed + 3620, 4) + 1f) * 0.5f;
            float supply = Math.Clamp(substrate.SandSediment + (substrate.SedimentaryRock * 0.46f) + (substrate.ReefLimestone * 0.22f), 0f, 1f);
            return Math.Clamp((supply * 0.48f) + (lowEnergy * shallow * 0.44f) + (transportNoise * coastBand * 0.18f), 0f, 1f);
        }

        private static float ComputeReefSuitability(float x, float y, int seed, TerrainSubstrateWeights substrate, float waterDepth, float waveExposure, float macroSupport)
        {
            float shallow = 1f - Math.Clamp(MathF.Abs(waterDepth - 0.24f) / 0.62f, 0f, 1f);
            float clearWater = 1f - Math.Clamp(substrate.SandSediment * 0.72f, 0f, 1f);
            float moderateEnergy = Math.Clamp(1f - (MathF.Abs(waveExposure - 0.45f) / 0.58f), 0f, 1f);
            float tropicalBias = (Fbm((x + 19.1f) * 0.015f, (y - 13.2f) * 0.015f, seed + 3630, 4) + 1f) * 0.5f;
            return Math.Clamp((substrate.ReefLimestone * 0.52f) + (shallow * clearWater * moderateEnergy * 0.54f) + (macroSupport * tropicalBias * 0.20f), 0f, 1f);
        }

        private static string BuildLandformDebugSignature(Vector2 worldPosition)
        {
            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();
            if (!TerrainWorldContainsPoint(worldPosition.X, worldPosition.Y))
            {
                return "world boundary";
            }

            float x = ResolveTerrainCentifootX(worldPosition.X);
            float y = ResolveTerrainCentifootY(worldPosition.Y);
            TerrainCell cell = SampleLayeredTerrainCell(x, y, _terrainWorldSeed);
            string tags = FormatTerrainLandformTags(cell.LandformTags);
            string terrainState = cell.IsLand ? "land" : $"water depth {CentifootUnits.FormatNumber(cell.WaterDepth, "0.00")}";
            return $"{tags} | {FormatTerrainSubstrate(cell.Lithology)} | {terrainState} | elev {CentifootUnits.FormatNumber(cell.Elevation, "0.00")} | fracture {CentifootUnits.FormatNumber(cell.FractureStrength, "0.00")} | wave {CentifootUnits.FormatNumber(cell.WaveExposure, "0.00")} | sediment {CentifootUnits.FormatNumber(cell.Sediment, "0.00")} | reef {CentifootUnits.FormatNumber(cell.ReefSuitability, "0.00")}";
        }

        private static TerrainLandformTag ClassifyLayeredLandforms(
            float elevation,
            float slope,
            float waveExposure,
            float sediment,
            float reefSuitability,
            float dissolution,
            float fractureStrength,
            float tidalFlow,
            float macroSupport,
            float enclosure,
            TerrainSubstrateWeights substrate)
        {
            TerrainLandformTag tags = TerrainLandformTag.None;
            bool isLand = elevation > SeaLevel;
            bool isWater = !isLand;
            bool isCoast = MathF.Abs(elevation - SeaLevel) <= 0.10f;
            float waterDepth = MathF.Max(0f, SeaLevel - elevation);

            if (macroSupport > 0.28f)
            {
                tags |= TerrainLandformTag.ArchipelagoCluster;
            }

            if (isLand)
            {
                tags |= TerrainLandformTag.Island;
                if (macroSupport < 0.46f || slope > 0.74f)
                {
                    tags |= TerrainLandformTag.Islet;
                }
                if (isCoast && slope > 0.58f && waveExposure > 0.46f)
                {
                    tags |= TerrainLandformTag.Headland;
                }
                if (slope > 0.72f && waveExposure > 0.42f)
                {
                    tags |= TerrainLandformTag.Stack;
                }
            }

            if (isWater && waterDepth < 0.58f && reefSuitability > 0.56f)
            {
                tags |= TerrainLandformTag.Reef;
                if (enclosure > 0.42f)
                {
                    tags |= TerrainLandformTag.Atoll;
                }
            }

            if (isWater && waterDepth < 0.66f && enclosure > 0.34f && tidalFlow > 0.08f)
            {
                tags |= TerrainLandformTag.Lagoon;
                if (reefSuitability > 0.54f)
                {
                    tags |= TerrainLandformTag.ReefLagoon;
                }
                if (sediment > 0.50f)
                {
                    tags |= TerrainLandformTag.BarrierLagoon;
                }
                if (substrate.LimestoneKarst > 0.46f && dissolution > 0.30f)
                {
                    tags |= TerrainLandformTag.KarstHongLagoon;
                }
                if (enclosure > 0.56f && reefSuitability < 0.52f)
                {
                    tags |= TerrainLandformTag.IslandRingLagoon;
                }
                if (fractureStrength > 0.52f && slope > 0.48f)
                {
                    tags |= TerrainLandformTag.CoveLagoon;
                }
            }

            if (isCoast && sediment > 0.54f && waveExposure < 0.48f)
            {
                tags |= TerrainLandformTag.Beach;
                if (enclosure > 0.36f || fractureStrength > 0.50f)
                {
                    tags |= TerrainLandformTag.PocketBeach;
                }
            }

            if (isCoast && fractureStrength > 0.56f && slope > 0.48f)
            {
                tags |= TerrainLandformTag.Cove;
                if (waveExposure > 0.42f)
                {
                    tags |= TerrainLandformTag.SeaCave;
                }
                if (fractureStrength > 0.70f && waveExposure > 0.44f)
                {
                    tags |= TerrainLandformTag.Geo;
                }
                if (dissolution > 0.22f && waveExposure > 0.58f)
                {
                    tags |= TerrainLandformTag.Arch;
                }
                if (enclosure > 0.42f && sediment > 0.48f && slope > 0.56f)
                {
                    tags |= TerrainLandformTag.Calanque;
                    tags |= TerrainLandformTag.StinivaLikePocketCove;
                }
                if (tidalFlow > 0.16f && enclosure > 0.46f && waveExposure < 0.42f && sediment > 0.44f)
                {
                    tags |= TerrainLandformTag.PorcoRossoLikeHiddenBase;
                }
            }

            if (isWater && tidalFlow > 0.18f && fractureStrength > 0.40f)
            {
                tags |= TerrainLandformTag.Channel;
                if (tidalFlow > 0.48f)
                {
                    tags |= TerrainLandformTag.Strait;
                }
            }

            if (sediment > 0.62f && isCoast)
            {
                tags |= TerrainLandformTag.Spit;
                if (enclosure > 0.32f)
                {
                    tags |= TerrainLandformTag.BarrierIsland;
                }
            }

            if (substrate.LimestoneKarst > 0.58f && dissolution > 0.40f && macroSupport > 0.44f)
            {
                tags |= TerrainLandformTag.PhangNgaLikeKarstBay;
            }

            return tags;
        }

        private static string FormatTerrainLandformTags(TerrainLandformTag tags)
        {
            if (tags == TerrainLandformTag.None)
            {
                return "open water";
            }

            if ((tags & TerrainLandformTag.PorcoRossoLikeHiddenBase) != 0)
            {
                return "hidden base candidate";
            }
            if ((tags & TerrainLandformTag.StinivaLikePocketCove) != 0)
            {
                return "stiniva-like pocket cove";
            }
            if ((tags & TerrainLandformTag.PhangNgaLikeKarstBay) != 0)
            {
                return "phang nga-like karst bay";
            }
            if ((tags & TerrainLandformTag.KarstHongLagoon) != 0)
            {
                return "karst hong lagoon";
            }
            if ((tags & TerrainLandformTag.ReefLagoon) != 0)
            {
                return "reef lagoon";
            }
            if ((tags & TerrainLandformTag.BarrierLagoon) != 0)
            {
                return "barrier lagoon";
            }
            if ((tags & TerrainLandformTag.CoveLagoon) != 0)
            {
                return "cove lagoon";
            }
            if ((tags & TerrainLandformTag.Lagoon) != 0)
            {
                return "lagoon";
            }
            if ((tags & TerrainLandformTag.Geo) != 0)
            {
                return "geo";
            }
            if ((tags & TerrainLandformTag.SeaCave) != 0)
            {
                return "sea cave";
            }
            if ((tags & TerrainLandformTag.Stack) != 0)
            {
                return "stack";
            }
            if ((tags & TerrainLandformTag.PocketBeach) != 0)
            {
                return "pocket beach";
            }
            if ((tags & TerrainLandformTag.Reef) != 0)
            {
                return "reef";
            }
            if ((tags & TerrainLandformTag.Islet) != 0)
            {
                return "islet";
            }
            if ((tags & TerrainLandformTag.Island) != 0)
            {
                return "island";
            }

            return "process terrain";
        }

        private static string ResolveDominantEnclosureProducerName(
            float x,
            float y,
            int seed,
            TerrainSubstrateWeights substrate,
            float baseField)
        {
            float reef = CoralReefEnclosureField(x, y, seed, substrate);
            float barrier = SandBarrierEnclosureField(x, y, seed, substrate);
            float islandRing = IslandRingEnclosureField(x, y, seed, substrate);
            float basin = EnclosedLagoonBasinCutField(x, y, seed, substrate, baseField);
            float karst = MathF.Abs(MathF.Min(0f, KarstCollapseDeltaField(x, y, seed, substrate)));

            string bestName = "open coast";
            float best = 0.08f;
            TryPromote("reef/barrier reef", reef);
            TryPromote("sand barrier", barrier);
            TryPromote("island ring", islandRing);
            TryPromote("lagoon basin", basin);
            TryPromote("karst hong", karst);
            return bestName;

            void TryPromote(string name, float value)
            {
                if (value > best)
                {
                    best = value;
                    bestName = name;
                }
            }
        }

        private static string ResolveDominantOpeningProducerName(
            float x,
            float y,
            int seed,
            TerrainSubstrateWeights substrate,
            float baseField,
            float field,
            float coastBand)
        {
            float drownedRavine = DrownedRavineCutField(x, y, seed, substrate, baseField, coastBand);
            float erosive = ErosiveOpeningCutField(x, y, seed, substrate, coastBand);
            float tidalChannel = RegionalTidalChannelCutField(x, y, seed, substrate, field, coastBand);

            string bestName = "none";
            float best = 0.04f;
            TryPromote("tidal channel", tidalChannel);
            TryPromote("eroded sea gate", erosive);
            TryPromote("drowned ravine", drownedRavine);
            return bestName;

            void TryPromote(string name, float value)
            {
                if (value > best)
                {
                    best = value;
                    bestName = name;
                }
            }
        }

        private static string FormatTerrainSubstrate(TerrainSubstrate substrate)
        {
            return substrate switch
            {
                TerrainSubstrate.LimestoneKarst => "limestone karst",
                TerrainSubstrate.VolcanicRock => "volcanic rock",
                TerrainSubstrate.SedimentaryRock => "sedimentary rock",
                TerrainSubstrate.ReefLimestone => "reef limestone",
                TerrainSubstrate.SandSediment => "sand/sediment",
                _ => "unknown substrate"
            };
        }

        private static string FormatTerrainLandformType(TerrainLandformType landformType)
        {
            return landformType switch
            {
                TerrainLandformType.AtollLagoon => "atoll lagoon",
                TerrainLandformType.ReefBarrierLagoon => "reef-barrier lagoon",
                TerrainLandformType.BarrierIslandLagoon => "barrier-island lagoon",
                TerrainLandformType.KarstHongLagoon => "karst hong lagoon",
                TerrainLandformType.IslandRingLagoon => "island-ring lagoon",
                TerrainLandformType.CalanqueCoveLagoon => "calanque/cove lagoon",
                TerrainLandformType.OpenWater => "open water gap",
                _ => "unknown landform"
            };
        }

        private static float ResolveCoastBand(float field, float width)
        {
            return MathF.Max(0f, 1f - (MathF.Abs(field - SeaLevel) / MathF.Max(0.001f, width)));
        }

        private static float Distance(float x, float y, float centerX, float centerY)
        {
            float dx = x - centerX;
            float dy = y - centerY;
            return MathF.Sqrt((dx * dx) + (dy * dy));
        }

        private static int ResolveOpeningCount(int cellX, int cellY, int seed)
        {
            return Hash2(cellX, cellY, seed) < 0.58f
                ? LagoonOpeningMinCount
                : LagoonOpeningMaxCount;
        }

        private static float RingRidgeValue(
            float x,
            float y,
            float centerX,
            float centerY,
            float radius,
            float width,
            float angle,
            float elongation,
            int seed,
            out float theta,
            out float distance,
            out float warpedRadius)
        {
            DomainWarp(
                x - centerX,
                y - centerY,
                seed,
                radius * 0.28f,
                out float deltaX,
                out float deltaY);
            Rotate(deltaX, deltaY, angle, out deltaX, out deltaY);
            deltaX /= MathF.Max(0.12f, elongation);
            deltaY *= MathF.Max(0.12f, elongation);

            theta = MathF.Atan2(deltaY, deltaX);
            warpedRadius = radius * (1f +
                (MathF.Sin((theta * 3f) + seed) * 0.08f) +
                (MathF.Sin((theta * 5f) + (seed * 0.37f)) * 0.035f) +
                (Fbm(
                    (MathF.Cos(theta) * 3.7f) + (centerX * 0.08f),
                    (MathF.Sin(theta) * 3.7f) + (centerY * 0.08f),
                    seed + 17,
                    4) * 0.28f));
            distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            return 1f - (MathF.Abs(distance - warpedRadius) / MathF.Max(0.001f, width));
        }

        private static float ReefRingField(
            float x,
            float y,
            float centerX,
            float centerY,
            float radius,
            float width,
            float angle,
            float elongation,
            int cellX,
            int cellY,
            int seed)
        {
            float ring = RingRidgeValue(
                x,
                y,
                centerX,
                centerY,
                radius,
                width,
                angle,
                elongation,
                seed,
                out float theta,
                out float distance,
                out float warpedRadius);
            float ringEnvelope = SmoothStep(width * 2.4f, width * 0.45f, MathF.Abs(distance - warpedRadius));
            float coralTexture = Fbm(x * 1.15f, y * 1.15f, seed + 31, 4) * 0.18f;
            int openingCount = ResolveOpeningCount(cellX, cellY, seed + 37);
            float passCut = AngularOpeningMask(
                theta,
                distance,
                warpedRadius,
                width,
                openingCount,
                cellX,
                cellY,
                seed + 41,
                0.28f);
            float stormCut = SmoothStep(0.62f, 0.92f, RidgedFbm(x * 0.82f, y * 0.82f, seed + 43, 4)) * ringEnvelope * 0.2f;
            return (ring + coralTexture - 0.02f) - (passCut * 1.18f) - stormCut;
        }

        private static float AngularOpeningMask(
            float theta,
            float distance,
            float radius,
            float width,
            int openingCount,
            int cellX,
            int cellY,
            int seed,
            float baseAngularWidth)
        {
            float radialMask = SmoothStep(width * 2.8f, width * 0.28f, MathF.Abs(distance - radius));
            float cut = 0f;
            int resolvedOpeningCount = Math.Clamp(openingCount, LagoonOpeningMinCount, LagoonOpeningMaxCount);

            for (int openingIndex = 0; openingIndex < resolvedOpeningCount; openingIndex++)
            {
                float angle = Hash2(cellX + (openingIndex * 37), cellY - (openingIndex * 41), seed + 101) * MathF.Tau;
                float angularWidth = baseAngularWidth * (0.86f + (Hash2(cellX - (openingIndex * 13), cellY + (openingIndex * 17), seed + 102) * 0.74f));
                float angularMask = SmoothStep(angularWidth, angularWidth * 0.22f, AngularDistance(theta, angle));
                cut = MathF.Max(cut, angularMask * radialMask);
            }

            return cut;
        }

        private static float AngularDistance(float left, float right)
        {
            float delta = MathF.Abs(left - right) % MathF.Tau;
            return delta > MathF.PI ? MathF.Tau - delta : delta;
        }

        private static float CurvedBarrierField(
            float x,
            float y,
            float centerX,
            float centerY,
            float angle,
            float halfLength,
            float width,
            int cellX,
            int cellY,
            int seed,
            out float localX,
            out float localY)
        {
            Rotate(x - centerX, y - centerY, -angle, out localX, out localY);
            float phase = Hash2(cellX, cellY, seed + 1) * MathF.Tau;
            float curve = MathF.Sin(((localX / MathF.Max(0.001f, halfLength)) * MathF.PI * 0.82f) + phase) *
                width *
                (0.55f + (Hash2(cellX, cellY, seed + 2) * 0.45f));
            localY -= curve;

            float axialMask = SmoothStep(1.12f, 0.84f, MathF.Abs(localX) / MathF.Max(0.001f, halfLength));
            float lateralMask = SmoothStep(1.12f, 0.12f, MathF.Abs(localY) / MathF.Max(0.001f, width));
            float duneTexture = Fbm((x * 1.28f) + cellX, (y * 1.28f) + cellY, seed + 3, 3) * 0.12f;
            return (axialMask * lateralMask * 0.92f) + (duneTexture * axialMask * lateralMask) - 0.08f;
        }

        private static float TidalInletCut(float localX, float localY, float halfLength, float width, int cellX, int cellY, int seed)
        {
            int openingCount = ResolveOpeningCount(cellX, cellY, seed + 1);
            float cut = 0f;

            for (int openingIndex = 0; openingIndex < openingCount; openingIndex++)
            {
                float normalizedPosition = -0.58f + (Hash2(cellX + (openingIndex * 19), cellY - (openingIndex * 23), seed + 2) * 1.16f);
                float inletX = normalizedPosition * halfLength;
                float alongMask = SmoothStep(width * 3.5f, width * 0.44f, MathF.Abs(localX - inletX));
                float acrossMask = SmoothStep(width * 5.2f, width * 0.32f, MathF.Abs(localY));
                cut = MathF.Max(cut, alongMask * acrossMask);
            }

            return cut * 1.08f;
        }

        private static float SpitHookField(
            float x,
            float y,
            float centerX,
            float centerY,
            float angle,
            float halfLength,
            float width,
            int cellX,
            int cellY,
            int seed)
        {
            float side = Hash2(cellX, cellY, seed + 1) < 0.5f ? -1f : 1f;
            float hookTurn = side * (0.72f + (Hash2(cellX, cellY, seed + 2) * 0.55f));
            float hookAngle = angle + hookTurn;
            float endX = centerX + (MathF.Cos(angle) * halfLength * side);
            float endY = centerY + (MathF.Sin(angle) * halfLength * side);
            float hookCenterX = endX + (MathF.Cos(hookAngle) * halfLength * 0.28f);
            float hookCenterY = endY + (MathF.Sin(hookAngle) * halfLength * 0.28f);
            return OrientedRidgeField(
                x,
                y,
                hookCenterX,
                hookCenterY,
                hookAngle,
                halfLength * 0.42f,
                width * 1.15f) * 0.52f;
        }

        private static float OrientedRidgeField(
            float x,
            float y,
            float centerX,
            float centerY,
            float angle,
            float halfLength,
            float width)
        {
            Rotate(x - centerX, y - centerY, -angle, out float localX, out float localY);
            float axialMask = SmoothStep(1.1f, 0.78f, MathF.Abs(localX) / MathF.Max(0.001f, halfLength));
            float lateralMask = SmoothStep(1.05f, 0.08f, MathF.Abs(localY) / MathF.Max(0.001f, width));
            return (axialMask * lateralMask * 0.92f) - 0.06f;
        }

        private static float OrientedChannelField(
            float x,
            float y,
            float centerX,
            float centerY,
            float angle,
            float halfLength,
            float width)
        {
            Rotate(x - centerX, y - centerY, -angle, out float localX, out float localY);
            float axialMask = SmoothStep(1.08f, 0.76f, MathF.Abs(localX) / MathF.Max(0.001f, halfLength));
            float lateralMask = SmoothStep(1.24f, 0.16f, MathF.Abs(localY) / MathF.Max(0.001f, width));
            return axialMask * lateralMask;
        }

        private static float EllipseField(float x, float y, float radiusX, float radiusY)
        {
            float normalizedX = x / MathF.Max(0.001f, radiusX);
            float normalizedY = y / MathF.Max(0.001f, radiusY);
            float distance = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
            return SmoothStep(1.05f, 0.18f, distance);
        }

        private enum TerrainLandformType
        {
            OpenWater,
            AtollLagoon,
            ReefBarrierLagoon,
            BarrierIslandLagoon,
            KarstHongLagoon,
            IslandRingLagoon,
            CalanqueCoveLagoon
        }

        private readonly struct TerrainLandformDescriptor
        {
            public TerrainLandformDescriptor(
                TerrainLandformType type,
                int cellX,
                int cellY,
                float centerX,
                float centerY,
                float radius,
                float length,
                float width,
                float angle,
                float elongation,
                int openingCount,
                int seed,
                float strength,
                TerrainSubstrateWeights substrate)
            {
                Type = type;
                CellX = cellX;
                CellY = cellY;
                CenterX = centerX;
                CenterY = centerY;
                Radius = radius;
                Length = length;
                Width = width;
                Angle = angle;
                Elongation = elongation;
                OpeningCount = openingCount;
                Seed = seed;
                Strength = strength;
                Substrate = substrate;
            }

            public TerrainLandformType Type { get; }
            public int CellX { get; }
            public int CellY { get; }
            public float CenterX { get; }
            public float CenterY { get; }
            public float Radius { get; }
            public float Length { get; }
            public float Width { get; }
            public float Angle { get; }
            public float Elongation { get; }
            public int OpeningCount { get; }
            public int Seed { get; }
            public float Strength { get; }
            public TerrainSubstrateWeights Substrate { get; }
        }

        private enum TerrainSubstrate
        {
            LimestoneKarst,
            VolcanicRock,
            SedimentaryRock,
            ReefLimestone,
            SandSediment
        }

        private readonly struct TerrainSubstrateWeights
        {
            public TerrainSubstrateWeights(float limestoneKarst, float volcanicRock, float reefLimestone, float sandSediment, float sedimentaryRock = 0f)
            {
                LimestoneKarst = limestoneKarst;
                VolcanicRock = volcanicRock;
                SedimentaryRock = sedimentaryRock;
                ReefLimestone = reefLimestone;
                SandSediment = sandSediment;
            }

            public float LimestoneKarst { get; }
            public float VolcanicRock { get; }
            public float SedimentaryRock { get; }
            public float ReefLimestone { get; }
            public float SandSediment { get; }

            public TerrainSubstrate Dominant
            {
                get
                {
                    TerrainSubstrate dominant = TerrainSubstrate.LimestoneKarst;
                    float best = LimestoneKarst;
                    if (VolcanicRock > best)
                    {
                        dominant = TerrainSubstrate.VolcanicRock;
                        best = VolcanicRock;
                    }
                    if (SedimentaryRock > best)
                    {
                        dominant = TerrainSubstrate.SedimentaryRock;
                        best = SedimentaryRock;
                    }
                    if (ReefLimestone > best)
                    {
                        dominant = TerrainSubstrate.ReefLimestone;
                        best = ReefLimestone;
                    }
                    if (SandSediment > best)
                    {
                        dominant = TerrainSubstrate.SandSediment;
                    }

                    return dominant;
                }
            }
        }

        [Flags]
        private enum TerrainLandformTag
        {
            None = 0,
            Island = 1 << 0,
            Islet = 1 << 1,
            Headland = 1 << 2,
            Cove = 1 << 3,
            Calanque = 1 << 4,
            StinivaLikePocketCove = 1 << 5,
            PorcoRossoLikeHiddenBase = 1 << 6,
            SeaCave = 1 << 7,
            Geo = 1 << 8,
            Arch = 1 << 9,
            Stack = 1 << 10,
            Reef = 1 << 11,
            Atoll = 1 << 12,
            Lagoon = 1 << 13,
            ReefLagoon = 1 << 14,
            BarrierLagoon = 1 << 15,
            KarstHongLagoon = 1 << 16,
            IslandRingLagoon = 1 << 17,
            CoveLagoon = 1 << 18,
            Beach = 1 << 19,
            PocketBeach = 1 << 20,
            Channel = 1 << 21,
            Strait = 1 << 22,
            Tombolo = 1 << 23,
            Spit = 1 << 24,
            BarrierIsland = 1 << 25,
            ArchipelagoCluster = 1 << 26,
            PhangNgaLikeKarstBay = 1 << 27
        }

        private readonly struct TerrainCell
        {
            public TerrainCell(
                float elevation,
                float seaLevel,
                float slope,
                float waveExposure,
                float sediment,
                float reefSuitability,
                float dissolution,
                float fractureStrength,
                float tidalFlow,
                float islandCluster,
                float enclosure,
                TerrainSubstrate lithology,
                TerrainLandformTag landformTags)
            {
                Elevation = elevation;
                SeaLevel = seaLevel;
                WaterDepth = MathF.Max(0f, seaLevel - elevation);
                Slope = slope;
                WaveExposure = waveExposure;
                Sediment = sediment;
                ReefSuitability = reefSuitability;
                Dissolution = dissolution;
                FractureStrength = fractureStrength;
                TidalFlow = tidalFlow;
                IslandCluster = islandCluster;
                Enclosure = enclosure;
                Lithology = lithology;
                LandformTags = landformTags;
            }

            public float Elevation { get; }
            public float WaterDepth { get; }
            public float SeaLevel { get; }
            public float Slope { get; }
            public float WaveExposure { get; }
            public float Sediment { get; }
            public float ReefSuitability { get; }
            public float Dissolution { get; }
            public float FractureStrength { get; }
            public float TidalFlow { get; }
            public float IslandCluster { get; }
            public float Enclosure { get; }
            public bool IsWater => Elevation <= SeaLevel;
            public bool IsLand => Elevation > SeaLevel;
            public bool IsCoast => MathF.Abs(Elevation - SeaLevel) <= 0.09f;
            public TerrainSubstrate Lithology { get; }
            public TerrainLandformTag LandformTags { get; }
        }

        private readonly struct TerrainWorldBounds
        {
            public TerrainWorldBounds(float minX, float minY, float width, float height)
            {
                MinX = minX;
                MinY = minY;
                MaxX = minX + MathF.Max(0f, width);
                MaxY = minY + MathF.Max(0f, height);
            }

            public float MinX { get; }
            public float MinY { get; }
            public float MaxX { get; }
            public float MaxY { get; }

            public bool Intersects(float minX, float maxX, float minY, float maxY)
            {
                return MaxX >= minX &&
                    MinX <= maxX &&
                    MaxY >= minY &&
                    MinY <= maxY;
            }
        }

        private readonly struct ChunkKey : IEquatable<ChunkKey>
        {
            public ChunkKey(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public bool Equals(ChunkKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is ChunkKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        private readonly struct ChunkBounds
        {
            public ChunkBounds(int minChunkX, int maxChunkX, int minChunkY, int maxChunkY)
            {
                MinChunkX = minChunkX;
                MaxChunkX = Math.Max(minChunkX, maxChunkX);
                MinChunkY = minChunkY;
                MaxChunkY = Math.Max(minChunkY, maxChunkY);
            }

            public int MinChunkX { get; }
            public int MaxChunkX { get; }
            public int MinChunkY { get; }
            public int MaxChunkY { get; }

            public bool Contains(ChunkKey key)
            {
                return key.X >= MinChunkX &&
                    key.X <= MaxChunkX &&
                    key.Y >= MinChunkY &&
                    key.Y <= MaxChunkY;
            }
        }

        private readonly struct TerrainStreamingWindowSet
        {
            public TerrainStreamingWindowSet(
                float cameraMinX,
                float cameraMaxX,
                float cameraMinY,
                float cameraMaxY,
                Vector2 streamingFocusWorldPosition,
                float preloadMarginWorldUnits,
                ChunkBounds visibleChunkWindow,
                ChunkBounds preloadChunkWindow,
                ChunkBounds terrainObjectChunkWindow,
                ChunkBounds terrainColliderChunkWindow,
                ChunkBounds retainChunkWindow)
            {
                CameraMinX = cameraMinX;
                CameraMaxX = cameraMaxX;
                CameraMinY = cameraMinY;
                CameraMaxY = cameraMaxY;
                StreamingFocusWorldPosition = streamingFocusWorldPosition;
                PreloadMarginWorldUnits = preloadMarginWorldUnits;
                VisibleChunkWindow = visibleChunkWindow;
                PreloadChunkWindow = preloadChunkWindow;
                TerrainObjectChunkWindow = terrainObjectChunkWindow;
                TerrainColliderChunkWindow = terrainColliderChunkWindow;
                RetainChunkWindow = retainChunkWindow;
            }

            public float CameraMinX { get; }
            public float CameraMaxX { get; }
            public float CameraMinY { get; }
            public float CameraMaxY { get; }
            public Vector2 StreamingFocusWorldPosition { get; }
            public float PreloadMarginWorldUnits { get; }
            public ChunkBounds VisibleChunkWindow { get; }
            public ChunkBounds PreloadChunkWindow { get; }
            public ChunkBounds TerrainObjectChunkWindow { get; }
            public ChunkBounds TerrainColliderChunkWindow { get; }
            public ChunkBounds RetainChunkWindow { get; }
        }

        private readonly struct ChunkBuildCandidate
        {
            public ChunkBuildCandidate(ChunkKey key, bool isVisible, int distanceSq)
            {
                Key = key;
                IsVisible = isVisible;
                DistanceSq = distanceSq;
            }

            public ChunkKey Key { get; }
            public bool IsVisible { get; }
            public int DistanceSq { get; }
        }

        private readonly struct CombinedResidentMask
        {
            public CombinedResidentMask(byte[] mask, int width, int height, int minChunkX, int minChunkY)
            {
                Mask = mask ?? Array.Empty<byte>();
                Width = width;
                Height = height;
                MinChunkX = minChunkX;
                MinChunkY = minChunkY;
            }

            public byte[] Mask { get; }
            public int Width { get; }
            public int Height { get; }
            public int MinChunkX { get; }
            public int MinChunkY { get; }
        }

        private readonly struct TerrainComponentBounds
        {
            public TerrainComponentBounds(int minX, int maxX, int minY, int maxY)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
            }

            public int MinX { get; }
            public int MaxX { get; }
            public int MinY { get; }
            public int MaxY { get; }
        }

        private readonly struct GridVertex : IEquatable<GridVertex>
        {
            public GridVertex(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public bool Equals(GridVertex other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is GridVertex other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        private readonly struct DirectedGridEdge : IEquatable<DirectedGridEdge>
        {
            public DirectedGridEdge(GridVertex start, GridVertex end)
            {
                Start = start;
                End = end;
            }

            public GridVertex Start { get; }
            public GridVertex End { get; }

            public bool Equals(DirectedGridEdge other) => Start.Equals(other.Start) && End.Equals(other.End);
            public override bool Equals(object obj) => obj is DirectedGridEdge other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Start, End);
        }

        private sealed class TerrainLoop
        {
            public TerrainLoop(List<Vector2> points)
            {
                Points = points ?? new List<Vector2>();
            }

            public List<Vector2> Points { get; }
        }

        private sealed class RefinedTerrainComponent
        {
            public RefinedTerrainComponent(
                byte[] mask,
                int width,
                int height,
                float worldLeft,
                float worldTop,
                float sampleStepWorldUnits)
            {
                Mask = mask ?? Array.Empty<byte>();
                Width = width;
                Height = height;
                WorldLeft = worldLeft;
                WorldTop = worldTop;
                SampleStepWorldUnits = sampleStepWorldUnits;
            }

            public byte[] Mask { get; }
            public int Width { get; }
            public int Height { get; }
            public float WorldLeft { get; }
            public float WorldTop { get; }
            public float SampleStepWorldUnits { get; }
        }

        private sealed class TerrainMaterializationResult
        {
            public TerrainMaterializationResult(
                int requestId,
                ChunkBounds materializedChunkWindow,
                ChunkBounds colliderChunkWindow)
            {
                RequestId = requestId;
                MaterializedChunkWindow = materializedChunkWindow;
                ColliderChunkWindow = colliderChunkWindow;
            }

            public int RequestId { get; }
            public ChunkBounds MaterializedChunkWindow { get; }
            public ChunkBounds ColliderChunkWindow { get; }
            public List<TerrainVisualObjectRecord> VisualObjects { get; } = new();
            public List<TerrainColliderObjectRecord> ColliderObjects { get; } = new();
            public List<TerrainCollisionLoopRecord> CollisionLoops { get; } = new();
            public int ComponentCount { get; set; }
            public int ColliderCount { get; set; }
            public int VisualTriangleCount { get; set; }
            public double BuildMilliseconds { get; set; }
        }

        private sealed class TerrainCollisionLoopRecord
        {
            public TerrainCollisionLoopRecord(
                TerrainWorldBounds bounds,
                List<Vector2> points)
            {
                Bounds = bounds;
                Points = points ?? new List<Vector2>();
            }

            public TerrainWorldBounds Bounds { get; }
            public List<Vector2> Points { get; }
        }

        private sealed class TerrainVisualObjectRecord
        {
            public TerrainVisualObjectRecord(
                TerrainWorldBounds bounds,
                VertexPositionColor[] fillVertices)
            {
                Bounds = bounds;
                FillVertices = fillVertices;
            }

            public TerrainWorldBounds Bounds { get; }
            public VertexPositionColor[] FillVertices { get; }
            public int FillPrimitiveCount => FillVertices?.Length / 3 ?? 0;

            public void Dispose()
            {
            }
        }

        private sealed class TerrainColliderObjectRecord
        {
            public TerrainColliderObjectRecord(
                TerrainWorldBounds bounds,
                Vector2 position,
                float rotation,
                float length,
                float thickness)
            {
                Bounds = bounds;
                Position = position;
                Rotation = rotation;
                Length = length;
                Thickness = thickness;
            }

            public GameObject ColliderObject { get; private set; }
            public TerrainWorldBounds Bounds { get; }
            public Vector2 Position { get; }
            public float Rotation { get; }
            public float Length { get; }
            public float Thickness { get; }
            public bool IsCollisionActive { get; set; }

            public GameObject EnsureColliderObjectCreated(Func<Vector2, float, float, float, GameObject> factory)
            {
                if (ColliderObject != null)
                {
                    return ColliderObject;
                }

                ColliderObject = factory?.Invoke(Position, Rotation, Length, Thickness);
                return ColliderObject;
            }

            public void ReleaseColliderObject()
            {
                ColliderObject = null;
            }
        }

        private sealed class TerrainChunkRecord
        {
            public TerrainChunkRecord(byte[] landMask, bool hasLand)
            {
                LandMask = landMask ?? Array.Empty<byte>();
                HasLand = hasLand;
            }

            public byte[] LandMask { get; }
            public bool HasLand { get; }
        }

        private sealed class GeneratedChunkData
        {
            public GeneratedChunkData(ChunkKey key, byte[] landMask, bool hasLand)
            {
                Key = key;
                LandMask = landMask ?? Array.Empty<byte>();
                HasLand = hasLand;
            }

            public ChunkKey Key { get; }
            public byte[] LandMask { get; }
            public bool HasLand { get; }
        }
    }
}
