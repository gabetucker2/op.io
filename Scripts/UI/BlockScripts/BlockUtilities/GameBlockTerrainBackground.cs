using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
        private const int DefaultTerrainWorldSeed = TerrainWorldDefaults.DefaultSeed;
        private const float ChunkWorldSize = 1024f;
        private const int ChunkTextureResolution = 96;
        private const int ChunkGuardResolution = 16;
        private const int MaxNewChunkBuildsPerFrame = 2;
        private const int StartupWarmupChunkRadius = 0;
        private const int StartupWarmupMaxSynchronousChunks = 1;
        private const float PreloadChunkMarginMultiplier = 1.25f;
        private const float RetainExtraChunkMultiplier = 1.5f;
        private const float TerrainFeatureScaleMultiplier = 3f;
        private const float TerrainDefaultWorldUnitsPerTerrainCoordinate = CentifootUnits.WorldUnitsPerCentifoot * TerrainFeatureScaleMultiplier;
        private const int TerrainContourResolutionMultiplier = 1;
        private const int TerrainVisualTextureOversample = 0;
        private const int MaxTerrainVisualTextureAxis = 4096;
        private const float TerrainCollisionActivationMarginWorldUnits = 256f;
        private const float TerrainCollisionDynamicProbeMarginWorldUnits = 128f;
        private const float TerrainAccessImmediatePaddingWorldUnits = 96f;
        private const float TerrainCollisionVelocityLeadSeconds = 0.35f;
        private const float TerrainCollisionHullSampleSpacingWorldUnits = 8f;
        private const int MaxTerrainFlickerObjectLogs = 64;
        private const float TerrainFlickerDiagnosticCooldownSeconds = 0.75f;
        private const int TerrainVisionRevealAheadChunkMargin = 3;
        private const int TerrainVisionRevealRefreshGuardChunkMargin = 1;
        private const float TerrainOceanZoneFieldSampleStepWorldUnits = 36f;
        private const int TerrainOceanZoneMaxFieldSamplesPerRing = 256;
        private const int TerrainOceanZoneCoastRefineIterations = 10;
        private const float OceanZoneDebugSampleStepWorldUnits = 64f;
        private const float OceanZoneDebugFullMapSampleStepWorldUnits = OceanZoneDebugSampleStepWorldUnits * 2f;
        private const float OceanZoneDebugTileWorldUnits = OceanZoneDebugSampleStepWorldUnits * 4f;
        private const int OceanZoneDebugMaxSampleAxis = 128;
        private const int OceanZoneDebugMaxSegments = 8192;
        private const int OceanZoneDebugMaxLabels = 512;
        private const float OceanZoneDebugLineThicknessScreenPixels = 3f;
        private const float OceanZoneDebugVisionBuildPaddingWorldUnits = OceanZoneDebugSampleStepWorldUnits * 2f;
        private const float OceanZoneDebugVisionClipSampleStepWorldUnits = 16f;
        private const float OceanZoneDebugLabelScreenScale = 0.58f;
        private const float OceanZoneDebugLabelOffsetScreenPixels = 78f;
        private const float OceanZoneDebugLabelShadowOffsetScreenPixels = 2f;
        private const float OceanZoneDebugLabelViewportMarginPixels = 220f;
        private const float OceanZoneDebugLabelCollisionPaddingPixels = 18f;
        private const float OceanZoneDebugMinimumCellWorldUnits = OceanZoneDebugSampleStepWorldUnits * 0.25f;
        private const int OceanZoneDebugMaxCellSubdivisionDepth = 3;
        private const int OceanZoneDebugTileBuildQueueLimit = 128;
        private const int MaxNewOceanZoneDebugTileBuildsPerFrame = 4;
        private const int MaxCompletedOceanZoneDebugTilePromotionsPerFrame = 8;
        private const int OceanZoneDebugTileSegmentCacheLimit = 256;
        private const double OceanZoneDebugSlowTileBuildLogThresholdMilliseconds = 120.0;
        private const int OceanZoneDebugMaxSlowTileBuildLogs = 16;
        private const string TerrainWaterZoneDistanceScaleSettingKey = "TerrainWaterZoneDistanceScale";
        private const string TerrainOceanZoneMinimumTransitionVolumeDistanceSettingKey = "TerrainOceanZoneMinimumTransitionVolumeDistance";
        private const float DefaultTerrainWaterZoneDistanceScale = 1.0f;
        private const float DefaultOceanZoneMinimumTransitionVolumeDistanceWorldUnits = 240f;
        private const float OceanZoneDebugMinimumStableZoneRadiusWorldUnits = OceanZoneDebugFullMapSampleStepWorldUnits;
        private const int OceanZoneDebugMinimumStableZoneFilterPasses = 16;
        private const int MaxFullTerrainMapChunkBuildEnqueuesPerFrame = 12;
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
        private static readonly bool TerrainWorldBoundaryEnabled = false;
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
        private const float ArchipelagoCellSize = TerrainWorldDefaults.WorldMinimumSpacing / TerrainDefaultWorldUnitsPerTerrainCoordinate;
        private const float ArchipelagoMacroCellSize = TerrainWorldDefaults.WorldInteractionSpacing / TerrainDefaultWorldUnitsPerTerrainCoordinate;
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
        private const float DevWaterDepthRampMax = TerrainWorldDefaults.WaterDepthRampMax;
        private const float DevWaterShallowBaseDistance = TerrainWorldDefaults.WaterShallowDistance / TerrainWorldDefaults.WaterZoneDistanceScale;
        private const float DevWaterSunlitBaseDistance = TerrainWorldDefaults.WaterSunlitDistance / TerrainWorldDefaults.WaterZoneDistanceScale;
        private const float DevWaterTwilightBaseDistance = TerrainWorldDefaults.WaterTwilightDistance / TerrainWorldDefaults.WaterZoneDistanceScale;
        private const float DevWaterMidnightBaseDistance = TerrainWorldDefaults.WaterMidnightDistance / TerrainWorldDefaults.WaterZoneDistanceScale;
        private static float DevWaterShallowDistance => DevWaterShallowBaseDistance * _terrainWaterZoneDistanceScale;
        private static float DevWaterSunlitDistance => DevWaterSunlitBaseDistance * _terrainWaterZoneDistanceScale;
        private static float DevWaterTwilightDistance => DevWaterTwilightBaseDistance * _terrainWaterZoneDistanceScale;
        private static float DevWaterMidnightDistance => DevWaterMidnightBaseDistance * _terrainWaterZoneDistanceScale;

        private static readonly Dictionary<ChunkKey, TerrainChunkRecord> ResidentChunks = new();
        private const int TerrainResidentChunkMemoryCapValue = 128;
        private const int TerrainChunkBuildQueueLimit = 256;
        private const int MaxCompletedChunkPromotionsPerFrame = 2;
        private static readonly object TerrainChunkWorkerLock = new();
        private static readonly List<ChunkBuildCandidate> TerrainChunkBuildQueue = new();
        private static readonly HashSet<ChunkKey> QueuedChunkKeys = new();
        private static readonly HashSet<ChunkKey> BuildingChunkKeys = new();
        private static readonly Queue<GeneratedChunkData> CompletedChunkBuildQueue = new();
        private static Thread _terrainChunkWorkerThread;
        private static bool _terrainChunkWorkerStopRequested;
        private static int _terrainChunkWorkerGeneration;
        private static int _terrainChunkWorkerCompletedQueueCount;
        private static int _terrainChunkWorkerActiveBuildCount;
        private static int _terrainChunkWorkerQueuedBuildCount;
        private static readonly List<TerrainVisualObjectRecord> ResidentTerrainVisualObjects = new();
        private static readonly List<TerrainCollisionLoopRecord> ResidentTerrainCollisionLoops = new();
        private static readonly VertexPositionColor[] TerrainBoundaryFillVertices = new VertexPositionColor[24];
        private static readonly RasterizerState TerrainVectorRasterizerState = new()
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };
        private static readonly RasterizerState OceanZoneDebugOverlayRasterizerState = new()
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };

        private static bool _settingsLoaded;
        private static float _terrainWaterZoneDistanceScale = DefaultTerrainWaterZoneDistanceScale;
        private static float _oceanZoneMinimumTransitionVolumeDistanceWorldUnits = DefaultOceanZoneMinimumTransitionVolumeDistanceWorldUnits;
        private static bool _terrainWorldObjectsDirty = true;
        private static int _terrainWorldSeed = DefaultTerrainWorldSeed;
        private static int _residentTerrainComponentCount;
        private static int _residentTerrainColliderCount;
        private static int _residentTerrainVisualTriangleCount;
        private static int _activeTerrainColliderCount;
        private static int _terrainColliderActivationCandidateCount;
        private static int _terrainDynamicCollisionProbeCount;
        private static int _terrainDynamicCollisionObjectProbeCount;
        private static int _terrainDynamicCollisionBulletProbeCount;
        private static int _terrainSpawnRelocationCount;
        private static int _terrainCollisionIntrusionCorrectionCount;
        private static int _terrainBulletCollisionCorrectionCount;
        private static Vector2 _terrainSeedAnchorCentifoot = Vector2.Zero;
        private static bool _terrainWorldBoundsInitialized;
        private static TerrainWorldBounds _terrainWorldBounds;
        private static float _lastPreloadMarginWorldUnits = ChunkWorldSize * PreloadChunkMarginMultiplier;
        private static ChunkBounds _lastVisibleChunkWindow;
        private static ChunkBounds _lastTerrainVisualChunkWindow;
        private static ChunkBounds _lastMaterializedChunkWindow;
        private static ChunkBounds _lastTerrainColliderChunkWindow;
        private static ChunkBounds _lastAppliedVisualChunkWindow;
        private static ChunkBounds _lastAppliedColliderChunkWindow;
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
        private static int _cachedDefaultWorldPlacementSeed = int.MinValue;
        private static TerrainWorldPlacement[] _cachedDefaultWorldPlacements = Array.Empty<TerrainWorldPlacement>();
        private static readonly object DefaultWorldPlacementCacheLock = new();
        private static int _cachedOceanZoneDistanceAnchorSeed = int.MinValue;
        private static OceanZoneDistanceAnchor[] _cachedOceanZoneDistanceAnchors = Array.Empty<OceanZoneDistanceAnchor>();
        private static readonly object OceanZoneDistanceAnchorCacheLock = new();
        private static int _cachedOceanZoneAnchorSeed = int.MinValue;
        private static Vector2 _cachedOceanZoneAnchorCenterWorld;
        private static float _cachedOceanZoneAnchorRadiusWorldUnits;
        private static float _cachedOceanZoneAnchorElongation = 1f;
        private static float _cachedOceanZoneAnchorRotationRadians;
        private static int _startupSynchronousChunkBuildCount;
        private static int _terrainBackgroundQueuedChunkBuildCount;
        private static string _terrainStartupPhase = "terrain startup pending";
        private static bool _startupFirstSightTerrainReady;
        private static int _startupWarmupChunkCount;
        private static int _terrainRuntimeFieldCollisionFallbackSuppressedCount;
        private static bool _hasAppliedTerrainVisualChunkWindow;
        private static bool _terrainVisibleObjectsDirty = true;
        private static int _terrainDeferredVisibleMaterializationCount;
        private static int _terrainAcceptedDirtyMaterializationCount;
        private static string _terrainVisibleCoverageStatus = "visible terrain not materialized";
        private static int _terrainNextVisualObjectDiagnosticId;
        private static int _terrainFlickerDiagnosticCount;
        private static float _lastTerrainFlickerDiagnosticTime = float.NegativeInfinity;
        private static string _lastTerrainFlickerDiagnosticReason = "none";
        private static int _lastTerrainVisibleDrawObjectCount = -1;
        private static int _lastTerrainVisibleDrawTriangleCount = -1;
        private static string _lastTerrainVisibleDrawSummary = "none";
        private static bool _hasTerrainDrawDiagnosticBaseline;
        private static ChunkBounds _lastTerrainDrawVisibleChunkWindow;
        private static ChunkBounds _lastTerrainDrawAppliedVisualChunkWindow;
        private static readonly List<string> _lastResidentTerrainVisualObjectSnapshot = new();
        private static bool _terrainAccessRequestActive;
        private static Vector2 _terrainAccessRequestWorldPosition;
        private static float _terrainAccessRequestRadiusWorldUnits;
        private static Texture2D _oceanZoneDebugPixelTexture;
        private static readonly List<OceanZoneDebugSegment> OceanZoneDebugSegments = new();
        private static readonly List<FogOfWarManager.VisionRegion> OceanZoneDebugVisionRegions = new();
        private static readonly Dictionary<OceanZoneDebugTileKey, List<OceanZoneDebugSegment>> OceanZoneDebugTileSegmentCache = new();
        private static readonly Dictionary<OceanZoneDebugTileKey, int> OceanZoneDebugTileSegmentCacheTouchTicks = new();
        private static readonly object OceanZoneDebugTileWorkerLock = new();
        private static readonly object OceanZoneAnchorCacheLock = new();
        private static readonly List<OceanZoneDebugTileBuildCandidate> OceanZoneDebugTileBuildQueue = new();
        private static readonly HashSet<OceanZoneDebugTileKey> QueuedOceanZoneDebugTileKeys = new();
        private static readonly HashSet<OceanZoneDebugTileKey> BuildingOceanZoneDebugTileKeys = new();
        private static readonly Queue<OceanZoneDebugTileBuildResult> CompletedOceanZoneDebugTileBuildQueue = new();
        private static readonly HashSet<int> OceanZoneDebugLabelCellKeys = new();
        private static readonly List<OceanZoneDebugLabelBounds> OceanZoneDebugPlacedLabelBounds = new();
        private static Thread _oceanZoneDebugTileWorkerThread;
        private static bool _oceanZoneDebugTileWorkerStopRequested;
        private static int _oceanZoneDebugTileWorkerGeneration;
        private static int _oceanZoneDebugTileWorkerQueuedBuildCount;
        private static int _oceanZoneDebugTileWorkerActiveBuildCount;
        private static int _oceanZoneDebugTileWorkerCompletedQueueCount;
        private static int _oceanZoneDebugTileCacheTouchTick;
        private static int _oceanZoneDebugQueuedTileBuildCount;
        private static int _oceanZoneDebugSlowTileBuildLogCount;
        private static int _lastOceanZoneDebugBuildSeed = int.MinValue;
        private static int _oceanZoneDebugBorderSegmentCount;
        private static int _oceanZoneDebugBorderLabelCount;
        private static double _oceanZoneDebugBuildMilliseconds;
        private static ChunkBounds _fullTerrainMapChunkWindow;
        private static bool _fullTerrainMapChunkWindowInitialized;
        private static int _fullTerrainMapChunkCount;
        private static int _fullTerrainMapGeneratedChunkCount;
        private static int _fullTerrainMapPendingChunkCount;
        private static bool _fullTerrainMapGenerationComplete;
        private static TerrainMapSnapshot _fullTerrainMapSnapshot;
        [ThreadStatic]
        private static TerrainMapSnapshot _terrainMapSnapshotOverride;
        private static readonly List<OceanZoneDebugSegment> FullOceanZoneDebugSegments = new();
        private static Task<OceanZoneDebugFullMapBuildResult> _fullOceanZoneDebugBuildTask;
        private static bool _fullOceanZoneDebugReady;
        private static int _fullOceanZoneDebugBuildSeed = int.MinValue;
        private static int _fullOceanZoneDebugSegmentCount;
        private static double _fullOceanZoneDebugBuildMilliseconds;
        private static string _fullOceanZoneDebugStatus = "waiting full-map ocean border queue";
        private static int _oceanZoneDebugSuppressedTinyZoneCount;
        private static string _oceanZoneDebugTinyZoneViolationSummary = "none";
        [ThreadStatic]
        private static int _oceanZoneDebugTinyZoneSuppressionScratchCount;
        [ThreadStatic]
        private static bool[] _oceanZoneDebugTinyZoneSuppressedCellsScratch;
        [ThreadStatic]
        private static bool _oceanZoneDebugDistanceSeedOverrideActive;
        [ThreadStatic]
        private static int _oceanZoneDebugDistanceSeedOverride;

        public static bool IsActive => true;
        public static int TerrainWorldSeed => _terrainWorldSeed;
        public static int TerrainResidentChunkCount => ResidentChunks.Count;
        public static int TerrainResidentChunkMemoryCap => TerrainResidentChunkMemoryCapValue;
        public static int TerrainResidentComponentCount => _residentTerrainComponentCount;
        public static int TerrainResidentEdgeLoopCount => ResidentTerrainCollisionLoops.Count;
        public static int TerrainResidentColliderCount => _residentTerrainColliderCount;
        public static int TerrainResidentVisualTriangleCount => _residentTerrainVisualTriangleCount;
        public static int TerrainActiveColliderCount => _activeTerrainColliderCount;
        public static int TerrainColliderActivationCandidateCount => _terrainColliderActivationCandidateCount;
        public static int TerrainDynamicCollisionProbeCount => _terrainDynamicCollisionProbeCount;
        public static int TerrainDynamicCollisionObjectProbeCount => _terrainDynamicCollisionObjectProbeCount;
        public static int TerrainDynamicCollisionBulletProbeCount => _terrainDynamicCollisionBulletProbeCount;
        public static int TerrainSpawnRelocationCount => _terrainSpawnRelocationCount;
        public static int TerrainCollisionIntrusionCorrectionCount => _terrainCollisionIntrusionCorrectionCount;
        public static int TerrainBulletCollisionCorrectionCount => _terrainBulletCollisionCorrectionCount;
        public static int TerrainPendingChunkCount => _terrainChunkWorkerQueuedBuildCount + _terrainChunkWorkerActiveBuildCount + _terrainChunkWorkerCompletedQueueCount;
        public static int TerrainPendingCriticalChunkCount => _terrainPendingCriticalChunkCount;
        public static string TerrainFullMapChunkWindow => _fullTerrainMapChunkWindowInitialized ? FormatChunkBounds(_fullTerrainMapChunkWindow) : "not initialized";
        public static int TerrainFullMapChunkCount => _fullTerrainMapChunkCount;
        public static int TerrainFullMapGeneratedChunkCount => _fullTerrainMapGeneratedChunkCount;
        public static int TerrainFullMapPendingChunkCount => _fullTerrainMapPendingChunkCount;
        public static bool TerrainFullMapGenerationComplete => _fullTerrainMapGenerationComplete;
        public static bool TerrainFullMapSnapshotReady => _fullTerrainMapSnapshot != null;
        public static bool TerrainChunkBuildsInFlight => _terrainChunkWorkerActiveBuildCount > 0;
        public static int TerrainBackgroundQueuedChunkCount => _terrainChunkWorkerQueuedBuildCount;
        public static int TerrainBackgroundCompletedChunkQueueCount => _terrainChunkWorkerCompletedQueueCount;
        public static int TerrainBackgroundActiveChunkBuildCount => _terrainChunkWorkerActiveBuildCount;
        public static string TerrainBackgroundWorkerStatus => ResolveTerrainBackgroundWorkerStatus();
        public static bool TerrainMaterializationInFlight => _terrainMaterializationTask != null;
        public static bool TerrainMaterializationRestartPending => _terrainMaterializationTask != null && _terrainWorldObjectsDirty;
        public static int TerrainDiscardedStaleMaterializationCount => _terrainDiscardedStaleMaterializationCount;
        public static double TerrainLastMaterializationMilliseconds => _lastTerrainMaterializationMilliseconds;
        public static bool TerrainStartupVisibleTerrainReady => _startupVisibleTerrainReady;
        public static string TerrainStartupReadinessSummary => _terrainStartupReadinessSummary;
        public static int TerrainStartupSynchronousChunkBuildCount => _startupSynchronousChunkBuildCount;
        public static int TerrainBackgroundQueuedChunkBuildCount => _terrainBackgroundQueuedChunkBuildCount;
        public static string TerrainStartupPhase => _terrainStartupPhase;
        public static bool TerrainStartupFirstSightTerrainReady => _startupFirstSightTerrainReady;
        public static int TerrainStartupWarmupChunkCount => _startupWarmupChunkCount;
        public static int TerrainRuntimeFieldCollisionFallbackSuppressedCount => _terrainRuntimeFieldCollisionFallbackSuppressedCount;
        public static bool TerrainVisibleObjectsDirty => _terrainVisibleObjectsDirty;
        public static int TerrainDeferredVisibleMaterializationCount => _terrainDeferredVisibleMaterializationCount;
        public static int TerrainAcceptedDirtyMaterializationCount => _terrainAcceptedDirtyMaterializationCount;
        public static string TerrainVisibleCoverageStatus => _terrainVisibleCoverageStatus;
        public static int TerrainFlickerDiagnosticCount => _terrainFlickerDiagnosticCount;
        public static string TerrainLastFlickerDiagnosticReason => _lastTerrainFlickerDiagnosticReason;
        public static string TerrainLastVisibleDrawSummary => _lastTerrainVisibleDrawSummary;
        public static bool TerrainAccessRequestActive => _terrainAccessRequestActive;
        public static string TerrainAccessRequestStatus => ResolveTerrainAccessRequestStatus();
        public static int TerrainMovementBlockedUntilReadyCount => 0;
        public static bool TerrainWorldBoundaryActive => TerrainWorldBoundaryEnabled;
        public static int TerrainWorldDefaultIslandCount => Math.Clamp((int)MathF.Round(TerrainWorldDefaults.WorldIslandCount), 1, TerrainWorldDefaults.WorldIslandGenerationLimit);
        public static float TerrainWorldDefaultMinimumSpacing => TerrainWorldDefaults.WorldMinimumSpacing;
        public static float TerrainWorldDefaultInteractionSpacing => TerrainWorldDefaults.WorldInteractionSpacing;
        public static string TerrainWorldDefaultClusterCountRange =>
            $"{TerrainWorldDefaults.WorldClusterCountMinimum:0}-{TerrainWorldDefaults.WorldClusterCountMaximum:0}";
        public static float TerrainChunkWorldSize => ChunkWorldSize;
        public static float TerrainFeatureWorldScaleMultiplier => TerrainFeatureScaleMultiplier;
        public static float TerrainArchipelagoMacroCellSize => ArchipelagoMacroCellSize;
        public static float TerrainArchipelagoSubstrateCellSize => ArchipelagoSubstrateCellSize;
        public static float TerrainArchipelagoEnclosureCellSize => ArchipelagoEnclosureCellSize;
        public static float TerrainArchipelagoLandformCellSize => ArchipelagoLandformCellSize;
        public static string TerrainGenerationPipeline => "macro mask > lithology > fractures > pre-flood terrain > karst dissolution > flooding > erosion > sediment > reef growth > classification";
        public static string TerrainLandformSelectionMode => "layered geological processes";
        public static string TerrainOceanZoneDistanceMode => "canonical archipelago polygon distance";
        public static string TerrainOceanZoneOrigin => FormatTerrainOceanZoneOrigin();
        public static float TerrainOceanZoneOriginRadius => ResolveTerrainOceanZoneOriginRadius();
        public static float TerrainWaterZoneDistanceScale => _terrainWaterZoneDistanceScale;
        public static float TerrainWaterShallowDistance => DevWaterShallowDistance;
        public static float TerrainWaterSunlitDistance => DevWaterSunlitDistance;
        public static float TerrainWaterTwilightDistance => DevWaterTwilightDistance;
        public static float TerrainWaterMidnightDistance => DevWaterMidnightDistance;
        public static float TerrainOceanZoneMinimumTransitionVolumeDistance => _oceanZoneMinimumTransitionVolumeDistanceWorldUnits;
        public static string TerrainLagoonOpeningTarget => $"{LagoonOpeningMinCount}-{LagoonOpeningMaxCount}";
        public static float TerrainLagoonBasinCutStrength => LagoonBasinCutStrength;
        public static float TerrainRegionalTidalChannelCutStrength => RegionalTidalChannelCutStrength;
        public static int TerrainContourResolutionMultiplierSetting => TerrainContourResolutionMultiplier;
        public static int TerrainTargetVisualTextureOversample => TerrainVisualTextureOversample;
        public static float TerrainOctogonalCornerCutCellRatio => TerrainOctogonalCornerCutCells;
        public static int TerrainDrawLayerSetting => TerrainDrawLayer;
        public static float TerrainPreloadMarginWorldUnits => _lastPreloadMarginWorldUnits;
        public static string TerrainWorldBoundsSummary =>
            TerrainWorldBoundaryEnabled
                ? $"{CentifootUnits.FormatDistance(_terrainWorldBounds.MinX)}, {CentifootUnits.FormatDistance(_terrainWorldBounds.MinY)} -> {CentifootUnits.FormatDistance(_terrainWorldBounds.MaxX)}, {CentifootUnits.FormatDistance(_terrainWorldBounds.MaxY)}"
                : "unbounded";
        public static string TerrainSeedAnchor =>
            $"{CentifootUnits.FormatNumber(_terrainSeedAnchorCentifoot.X)}, {CentifootUnits.FormatNumber(_terrainSeedAnchorCentifoot.Y)} {CentifootUnits.UnitAbbreviation}";
        public static string TerrainStreamingFocus => CentifootUnits.FormatVector2(_lastTerrainStreamingFocusWorldPosition);
        public static string TerrainStreamingLandformSignature => BuildLandformDebugSignature(_lastTerrainStreamingFocusWorldPosition);
        public static string TerrainCenterChunk => $"{_lastCenterChunk.X}, {_lastCenterChunk.Y}";
        public static string TerrainVisibleChunkWindow =>
            $"{_lastVisibleChunkWindow.MinChunkX}..{_lastVisibleChunkWindow.MaxChunkX}, {_lastVisibleChunkWindow.MinChunkY}..{_lastVisibleChunkWindow.MaxChunkY}";
        public static string TerrainAppliedVisualChunkWindow =>
            _hasAppliedTerrainVisualChunkWindow
                ? FormatChunkBounds(_lastAppliedVisualChunkWindow)
                : "none";
        public static string TerrainAppliedColliderChunkWindow =>
            _hasAppliedTerrainVisualChunkWindow
                ? FormatChunkBounds(_lastAppliedColliderChunkWindow)
                : "none";
        public static string TerrainTargetVisualChunkWindow => FormatChunkBounds(_lastTerrainVisualChunkWindow);
        public static string TerrainTargetMaterializedChunkWindow => FormatChunkBounds(_lastMaterializedChunkWindow);
        public static string TerrainColliderChunkWindow =>
            $"{_lastTerrainColliderChunkWindow.MinChunkX}..{_lastTerrainColliderChunkWindow.MaxChunkX}, {_lastTerrainColliderChunkWindow.MinChunkY}..{_lastTerrainColliderChunkWindow.MaxChunkY}";
        public static float TerrainWorldScaleMultiplier => TerrainWorldDefaults.GlobalScale;
        public static bool TerrainOceanDebugOverlayRequested => IsOceanZoneDebugOverlayRequested();
        public static bool TerrainOceanDebugOverlayVisible => IsOceanZoneDebugOverlayRequested();
        public static int TerrainOceanDebugBorderSegmentCount => _oceanZoneDebugBorderSegmentCount;
        public static int TerrainOceanDebugBorderLabelCount => _oceanZoneDebugBorderLabelCount;
        public static double TerrainOceanDebugBuildMilliseconds => _oceanZoneDebugBuildMilliseconds;
        public static bool TerrainOceanDebugFullMapReady => _fullOceanZoneDebugReady;
        public static int TerrainOceanDebugFullMapSegmentCount => _fullOceanZoneDebugSegmentCount;
        public static double TerrainOceanDebugFullMapBuildMilliseconds => _fullOceanZoneDebugBuildMilliseconds;
        public static string TerrainOceanDebugFullMapStatus => _fullOceanZoneDebugStatus;
        public static int TerrainOceanDebugSuppressedTinyZoneCount => _oceanZoneDebugSuppressedTinyZoneCount;
        public static float TerrainOceanDebugMinimumStableZoneRadius => OceanZoneDebugMinimumStableZoneRadiusWorldUnits;
        public static string TerrainOceanDebugTinyZoneViolationSummary => _oceanZoneDebugTinyZoneViolationSummary;
        public static int TerrainOceanDebugTileCacheCount => OceanZoneDebugTileSegmentCache.Count;
        public static int TerrainOceanDebugQueuedTileCount => _oceanZoneDebugTileWorkerQueuedBuildCount;
        public static int TerrainOceanDebugActiveTileBuildCount => _oceanZoneDebugTileWorkerActiveBuildCount;
        public static int TerrainOceanDebugCompletedTileQueueCount => _oceanZoneDebugTileWorkerCompletedQueueCount;
        public static int TerrainOceanDebugQueuedTileBuildCount => _oceanZoneDebugQueuedTileBuildCount;
        public static string TerrainOceanDebugWorkerStatus => ResolveOceanZoneDebugWorkerStatus();

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            LoadSettingsIfNeeded();
            EnsureTerrainVectorEffect(graphicsDevice);
            _terrainWorldObjectsDirty = true;
            _startupVisibleTerrainReady = _startupFirstSightTerrainReady && ResidentTerrainVisualObjects.Count > 0;
            if (_startupVisibleTerrainReady)
            {
                _terrainStartupReadinessSummary = "first-sight terrain ready; background preload pending";
                _terrainStartupPhase = "first-sight terrain ready; background preload streaming";
            }
            else if (ResidentChunks.Count > 0)
            {
                _terrainStartupReadinessSummary = "startup terrain chunks ready; visible materialization pending";
                _terrainStartupPhase = "player loaded; visible terrain materialization pending";
            }
            else
            {
                _terrainStartupReadinessSummary = "startup terrain pending";
                _terrainStartupPhase = "terrain startup pending";
            }
        }

        internal static void ResetRuntimeTerrainObjectsForLevelLoad()
        {
            _terrainMaterializationRequestId++;
            _terrainMaterializationTask = null;
            ClearResidentTerrainWorldObjects();
            _activeTerrainColliderCount = 0;
            _terrainColliderActivationCandidateCount = 0;
            _terrainWorldObjectsDirty = true;
            _startupVisibleTerrainReady = false;
            _startupFirstSightTerrainReady = false;
            _hasAppliedTerrainVisualChunkWindow = false;
            _lastTerrainVisualChunkWindow = default;
            _lastMaterializedChunkWindow = default;
            _lastTerrainColliderChunkWindow = default;
            _lastAppliedVisualChunkWindow = default;
            _lastAppliedColliderChunkWindow = default;
            _terrainVisibleObjectsDirty = true;
            _terrainAccessRequestActive = false;
            _terrainAccessRequestWorldPosition = Vector2.Zero;
            _terrainAccessRequestRadiusWorldUnits = 0f;
            _terrainPendingCriticalChunkCount = 0;
            _terrainStartupPhase = "terrain reset for level load";
            _terrainStartupReadinessSummary = "terrain reset for level load";
            ResetFullTerrainMapState(clearResidentChunks: false);
            ResetOceanZoneDebugFullMapState(clearSegments: true);
        }

        internal static bool PrepareStartupTerrainAroundWorldPosition(Vector2 focusWorldPosition)
        {
            if (!IsFiniteVector(focusWorldPosition))
            {
                focusWorldPosition = Vector2.Zero;
            }

            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized(focusWorldPosition);

            ChunkKey focusChunk = BuildChunkKey(focusWorldPosition.X, focusWorldPosition.Y);
            ChunkBounds warmupWindow = ClampChunkBoundsToTerrainWorld(new ChunkBounds(
                focusChunk.X - StartupWarmupChunkRadius,
                focusChunk.X + StartupWarmupChunkRadius,
                focusChunk.Y - StartupWarmupChunkRadius,
                focusChunk.Y + StartupWarmupChunkRadius));

            _terrainStartupPhase = $"warming nearby terrain chunks around {focusChunk.X}, {focusChunk.Y}";
            _terrainStartupReadinessSummary = $"startup terrain warming: {FormatChunkBounds(warmupWindow)} near player";
            ApplyTerrainStreamingWindowState(new TerrainStreamingWindowSet(
                focusWorldPosition.X,
                focusWorldPosition.X,
                focusWorldPosition.Y,
                focusWorldPosition.Y,
                focusWorldPosition,
                ChunkWorldSize * PreloadChunkMarginMultiplier,
                warmupWindow,
                warmupWindow,
                warmupWindow,
                warmupWindow,
                warmupWindow));

            TryPromoteCompletedChunks(warmupWindow);
            List<ChunkBuildCandidate> warmupCandidates = BuildStartupWarmupChunkCandidates(focusChunk, warmupWindow);
            List<ChunkKey> builtKeys = new(warmupCandidates.Count);
            bool foundLand = HasResidentLandChunkInBounds(new ChunkBounds(focusChunk.X, focusChunk.X, focusChunk.Y, focusChunk.Y));
            int synchronousBuilds = 0;
            int maxSynchronousBuilds = Math.Min(StartupWarmupMaxSynchronousChunks, warmupCandidates.Count);

            for (int i = 0; i < warmupCandidates.Count; i++)
            {
                ChunkKey key = warmupCandidates[i].Key;
                bool alreadyResident = ResidentChunks.ContainsKey(key);
                if (!alreadyResident && synchronousBuilds >= maxSynchronousBuilds)
                {
                    continue;
                }

                if (!TryBuildResidentChunkSynchronously(key, waitForPending: false))
                {
                    _terrainStartupPhase = "nearby terrain warmup failed";
                    return false;
                }

                if (!alreadyResident)
                {
                    synchronousBuilds++;
                }

                builtKeys.Add(key);
                if (ResidentChunks.TryGetValue(key, out TerrainChunkRecord chunk) && chunk.HasLand)
                {
                    foundLand = true;
                }
            }

            if (builtKeys.Count == 0)
            {
                _terrainStartupPhase = "nearby terrain warmup failed";
                return false;
            }

            _startupWarmupChunkCount = builtKeys.Count;
            ChunkBounds startupMaterializedWindow = BuildChunkBoundsForKeys(builtKeys);
            _terrainPendingCriticalChunkCount = CountPendingChunksInBounds(warmupWindow);
            if (foundLand &&
                TryBuildCombinedResidentMask(startupMaterializedWindow, out CombinedResidentMask residentMask))
            {
                TerrainMaterializationResult result = BuildTerrainMaterializationResult(
                    residentMask,
                    ++_terrainMaterializationRequestId,
                    BuildChunkWorldBounds(startupMaterializedWindow),
                    startupMaterializedWindow,
                    startupMaterializedWindow,
                    startupMaterializedWindow);
                _terrainMaterializationTask = null;
                _lastTerrainVisualChunkWindow = startupMaterializedWindow;
                _lastMaterializedChunkWindow = startupMaterializedWindow;
                _lastTerrainColliderChunkWindow = startupMaterializedWindow;
                _terrainWorldObjectsDirty = false;
                ApplyTerrainMaterializationResult(result);
                _startupFirstSightTerrainReady = result.ComponentCount > 0;
                if (_startupFirstSightTerrainReady)
                {
                    DebugLogger.Print($"GameBlockTerrainBackground: first-sight terrain materialized {result.ComponentCount} landforms from {builtKeys.Count} nearby chunks in {result.BuildMilliseconds:0.0} ms.");
                    _terrainStartupPhase = "nearby terrain ready; visible window pending";
                    _terrainStartupReadinessSummary = $"startup terrain warm: {result.ComponentCount} nearby landforms in {FormatChunkBounds(startupMaterializedWindow)}; visible window pending";
                    return true;
                }
            }

            _terrainWorldObjectsDirty = true;
            _terrainStartupPhase = "nearby terrain chunks ready; visible land still streaming";
            _terrainStartupReadinessSummary = $"startup terrain chunks ready: {FormatChunkBounds(startupMaterializedWindow)} near player";
            return true;
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
            ChunkBounds startupVisualChunkWindow = windows.TerrainObjectChunkWindow;
            ChunkBounds startupColliderChunkWindow = windows.TerrainColliderChunkWindow;
            ChunkBounds startupMaterializedWindow = UnionChunkBounds(startupVisualChunkWindow, startupColliderChunkWindow);
            if (!BuildResidentChunksSynchronously(startupMaterializedWindow, waitForPending: false))
            {
                _terrainStartupReadinessSummary = "startup terrain pending: visible preload chunk build failed";
                return false;
            }

            PruneResidentChunks(windows.RetainChunkWindow);
            QueueFullTerrainMapBuilds(startupMaterializedWindow);
            UpdateFullTerrainMapGenerationState();
            _terrainPendingCriticalChunkCount = CountPendingChunksInBounds(startupMaterializedWindow);
            if (_terrainPendingCriticalChunkCount > 0)
            {
                _terrainStartupReadinessSummary = $"startup terrain pending: {_terrainPendingCriticalChunkCount} visible preload chunks still queued";
                return false;
            }

            TerrainMaterializationResult result;
            if (TryBuildCombinedResidentMask(startupMaterializedWindow, out CombinedResidentMask residentMask))
            {
                result = BuildTerrainMaterializationResult(
                    residentMask,
                    ++_terrainMaterializationRequestId,
                    BuildChunkWorldBounds(startupColliderChunkWindow),
                    startupMaterializedWindow,
                    startupVisualChunkWindow,
                    startupColliderChunkWindow);
            }
            else
            {
                result = BuildTerrainMaterializationResult(
                    new CombinedResidentMask(Array.Empty<byte>(), 0, 0, startupMaterializedWindow.MinChunkX, startupMaterializedWindow.MinChunkY),
                    ++_terrainMaterializationRequestId,
                    BuildChunkWorldBounds(startupColliderChunkWindow),
                    startupMaterializedWindow,
                    startupVisualChunkWindow,
                    startupColliderChunkWindow);
            }

            _terrainMaterializationTask = null;
            _lastTerrainVisualChunkWindow = startupVisualChunkWindow;
            _lastMaterializedChunkWindow = startupMaterializedWindow;
            _lastTerrainColliderChunkWindow = startupColliderChunkWindow;
            _terrainWorldObjectsDirty = false;
            ApplyTerrainMaterializationResult(result);
            _startupVisibleTerrainReady = true;
            _terrainStartupPhase = "visible terrain and access buffer ready";
            _terrainStartupReadinessSummary = $"startup terrain ready: {FormatChunkBounds(startupMaterializedWindow)} visible buffer";
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
            ChunkBounds targetVisualMaterializedChunkWindow = _startupVisibleTerrainReady
                ? ResolveStableTerrainObjectChunkWindow(windows.TerrainObjectChunkWindow, windows.VisibleChunkWindow)
                : windows.VisibleChunkWindow;
            ChunkBounds targetColliderChunkWindow = _startupVisibleTerrainReady
                ? windows.TerrainColliderChunkWindow
                : windows.VisibleChunkWindow;
            ChunkBounds targetMaterializedChunkWindow = _startupVisibleTerrainReady
                ? UnionChunkBounds(targetVisualMaterializedChunkWindow, targetColliderChunkWindow)
                : targetVisualMaterializedChunkWindow;
            if (!ChunkBoundsEqual(_lastTerrainVisualChunkWindow, targetVisualMaterializedChunkWindow) ||
                !ChunkBoundsEqual(_lastMaterializedChunkWindow, targetMaterializedChunkWindow) ||
                !ChunkBoundsEqual(_lastTerrainColliderChunkWindow, targetColliderChunkWindow))
            {
                _lastTerrainVisualChunkWindow = targetVisualMaterializedChunkWindow;
                _lastMaterializedChunkWindow = targetMaterializedChunkWindow;
                _lastTerrainColliderChunkWindow = targetColliderChunkWindow;
                _terrainWorldObjectsDirty = true;
            }

            TryPromoteCompletedChunks(windows.RetainChunkWindow);
            ChunkBounds priorityChunkWindow = UnionChunkBounds(targetMaterializedChunkWindow, windows.TerrainObjectChunkWindow);
            QueueChunkBuilds(windows.PreloadChunkWindow, priorityChunkWindow);
            QueueFullTerrainMapBuilds(priorityChunkWindow);
            PruneResidentChunks(windows.RetainChunkWindow);
            UpdateFullTerrainMapGenerationState();
            TryApplyCompletedFullOceanZoneDebugBuild();
            _terrainPendingCriticalChunkCount = CountPendingChunksInBounds(targetMaterializedChunkWindow);
            RefreshResidentTerrainWorldObjects(
                graphicsDevice,
                targetMaterializedChunkWindow,
                targetVisualMaterializedChunkWindow,
                targetColliderChunkWindow,
                targetMaterializedChunkWindow);
            UpdateTerrainVisibleCoverageStatus(windows.VisibleChunkWindow);
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

        private static bool IsOceanZoneDebugOverlayRequested()
        {
            if (ControlStateManager.ContainsSwitchState(ControlKeyMigrations.OceanZoneDebugKey))
            {
                return ControlStateManager.GetSwitchState(ControlKeyMigrations.OceanZoneDebugKey);
            }

            ControlKeyData.ControlKeyRecord persistedControl = ControlKeyData.GetControl(ControlKeyMigrations.OceanZoneDebugKey);
            if (persistedControl?.SwitchStartState != null)
            {
                return TypeConversionFunctions.IntToBool(persistedControl.SwitchStartState.Value);
            }

            return !ControlStateManager.ContainsSwitchState(ControlKeyMigrations.OceanZoneDebugKey);
        }

        public static void DrawOceanZoneDebugOverlay(SpriteBatch spriteBatch, Matrix cameraTransform)
        {
            if (spriteBatch == null || !TerrainOceanDebugOverlayVisible)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            if (!GameRenderer.TryGetVisibleWorldBounds(
                cameraTransform,
                out float visibleMinX,
                out float visibleMaxX,
                out float visibleMinY,
                out float visibleMaxY))
            {
                return;
            }

            EnsureOceanZoneDebugPixelTexture(graphicsDevice);
            if (_oceanZoneDebugPixelTexture == null || _oceanZoneDebugPixelTexture.IsDisposed)
            {
                return;
            }

            Rectangle viewportBounds = graphicsDevice.Viewport.Bounds;
            DrawOceanZoneDebugOverlayCore(
                spriteBatch,
                cameraTransform,
                viewportBounds,
                viewportBounds,
                viewportBounds,
                visibleMinX,
                visibleMaxX,
                visibleMinY,
                visibleMaxY);
        }

        public static void DrawOceanZoneDebugFinalOverlay(SpriteBatch spriteBatch, Matrix cameraTransform)
        {
            if (spriteBatch == null || !TerrainOceanDebugOverlayVisible)
            {
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            if (!BlockManager.TryGetGameRenderBounds(out Rectangle renderBounds) ||
                !BlockManager.TryGetGameContentWindowBounds(out Rectangle windowBounds))
            {
                return;
            }

            Rectangle viewportBounds = graphicsDevice.Viewport.Bounds;
            Rectangle clippedWindowBounds = Rectangle.Intersect(windowBounds, viewportBounds);
            if (renderBounds.Width <= 0 ||
                renderBounds.Height <= 0 ||
                clippedWindowBounds.Width <= 0 ||
                clippedWindowBounds.Height <= 0)
            {
                return;
            }

            if (!TryResolveOceanZoneDebugVisibleWorldBounds(
                cameraTransform,
                renderBounds,
                out float visibleMinX,
                out float visibleMaxX,
                out float visibleMinY,
                out float visibleMaxY))
            {
                return;
            }

            EnsureOceanZoneDebugPixelTexture(graphicsDevice);
            if (_oceanZoneDebugPixelTexture == null || _oceanZoneDebugPixelTexture.IsDisposed)
            {
                return;
            }

            Rectangle previousScissor = graphicsDevice.ScissorRectangle;
            graphicsDevice.ScissorRectangle = clippedWindowBounds;

            try
            {
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    OceanZoneDebugOverlayRasterizerState,
                    null,
                    Matrix.Identity);

                DrawOceanZoneDebugOverlayCore(
                    spriteBatch,
                    cameraTransform,
                    renderBounds,
                    windowBounds,
                    clippedWindowBounds,
                    visibleMinX,
                    visibleMaxX,
                    visibleMinY,
                    visibleMaxY);

                spriteBatch.End();
            }
            finally
            {
                graphicsDevice.ScissorRectangle = previousScissor;
            }
        }

        internal static int CountOceanZoneDebugBorderSegmentsForWorldBounds(
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            List<OceanZoneDebugSegment> segments = new();
            return BuildOceanZoneDebugSegments(minX, maxX, minY, maxY, segments);
        }

        internal static int CountOceanZoneDebugVisionClipPartsForProbe(Vector2 from, Vector2 to)
        {
            OceanZoneDebugVisionRegions.Clear();
            if (FogOfWarManager.IsFogEnabled &&
                (!FogOfWarManager.TryGetVisionRegions(OceanZoneDebugVisionRegions) ||
                    OceanZoneDebugVisionRegions.Count <= 0))
            {
                return 0;
            }

            OceanZoneDebugSegment segment = new(from, to, Vector2.UnitY, TerrainWaterType.Shallow, TerrainWaterType.Sunlit, DevWaterShallowDistance);
            return CountOceanZoneDebugVisionClipParts(segment);
        }

        internal static string ResolveOceanZoneDebugBuildGridSignatureForProbe(
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            return TryResolveOceanZoneDebugBuildGrid(
                minX,
                maxX,
                minY,
                maxY,
                out float buildMinX,
                out float buildMaxX,
                out float buildMinY,
                out float buildMaxY,
                out float sampleStep)
                    ? $"{buildMinX:0.###}|{buildMaxX:0.###}|{buildMinY:0.###}|{buildMaxY:0.###}|{sampleStep:0.###}"
                    : "invalid";
        }

        internal static string ResolveOceanZoneDebugTileRangeSignatureForProbe(
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            return TryResolveOceanZoneDebugTileRange(
                minX,
                maxX,
                minY,
                maxY,
                out int minTileX,
                out int maxTileX,
                out int minTileY,
                out int maxTileY)
                    ? $"{minTileX}..{maxTileX}|{minTileY}..{maxTileY}"
                    : "invalid";
        }

        internal static bool ValidateOceanZoneDebugBorderConsistencyForProbe(
            float minX,
            float maxX,
            float minY,
            float maxY,
            out int checkedSegments,
            out int mismatchedSegments)
        {
            checkedSegments = 0;
            mismatchedSegments = 0;

            List<OceanZoneDebugSegment> segments = new();
            BuildOceanZoneDebugSegments(minX, maxX, minY, maxY, segments);
            if (segments.Count == 0)
            {
                return false;
            }

            if (!TryResolveOceanZoneDebugBuildGrid(
                minX,
                maxX,
                minY,
                maxY,
                out _,
                out _,
                out _,
                out _,
                out float sampleStep))
            {
                return false;
            }

            int stride = Math.Max(1, segments.Count / 96);
            float probeOffset = Math.Max(18f, sampleStep * 0.25f);
            float maxSearchDistance = ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance)) +
                probeOffset +
                OceanZoneDebugSampleStepWorldUnits;
            float borderTolerance = Math.Max(24f, sampleStep * 0.75f);
            for (int i = 0; i < segments.Count; i += stride)
            {
                OceanZoneDebugSegment segment = segments[i];
                Vector2 midpoint = (segment.From + segment.To) * 0.5f;
                OceanZoneDebugSample firstSideSample = ResolveOceanZoneRuntimeWaterSample(midpoint - (segment.Normal * probeOffset), maxSearchDistance);
                OceanZoneDebugSample secondSideSample = ResolveOceanZoneRuntimeWaterSample(midpoint + (segment.Normal * probeOffset), maxSearchDistance);
                if (!firstSideSample.IsWater || !secondSideSample.IsWater)
                {
                    continue;
                }

                checkedSegments++;
                bool distanceOrdered = firstSideSample.OffshoreDistance <= secondSideSample.OffshoreDistance + borderTolerance;
                bool thresholdStraddled =
                    firstSideSample.OffshoreDistance <= segment.Threshold + borderTolerance &&
                    secondSideSample.OffshoreDistance >= segment.Threshold - borderTolerance;
                bool zoneOrderValid =
                    ResolveOceanZoneDebugWaterTypeOrder(firstSideSample.WaterType) <= ResolveOceanZoneDebugWaterTypeOrder(segment.FirstSide) + 1 &&
                    ResolveOceanZoneDebugWaterTypeOrder(secondSideSample.WaterType) >= ResolveOceanZoneDebugWaterTypeOrder(segment.SecondSide) - 1;
                if (!distanceOrdered || !thresholdStraddled || !zoneOrderValid)
                {
                    mismatchedSegments++;
                    continue;
                }
            }

            return checkedSegments > 0 && mismatchedSegments <= Math.Max(1, checkedSegments / 32);
        }

        private static OceanZoneDebugSample ResolveOceanZoneRuntimeWaterSample(
            Vector2 worldPosition,
            float maxSearchDistanceWorldUnits)
        {
            return TryResolveOceanZoneAtWorldPositionCore(
                worldPosition,
                maxSearchDistanceWorldUnits,
                out TerrainWaterType waterType,
                out _,
                out float offshoreDistance)
                    ? new OceanZoneDebugSample(true, waterType, offshoreDistance)
                    : new OceanZoneDebugSample(false, TerrainWaterType.Shallow, 0f);
        }

        internal static bool ValidateOceanZoneDebugMinimumStableZoneRadiusForProbe(
            float minX,
            float maxX,
            float minY,
            float maxY,
            float sampleStep,
            out int suppressedComponents,
            out int remainingTinyComponents)
        {
            suppressedComponents = 0;
            remainingTinyComponents = 0;
            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                !float.IsFinite(sampleStep) ||
                maxX <= minX ||
                maxY <= minY ||
                sampleStep <= 0f)
            {
                return false;
            }

            float maxDebugThreshold = ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance));
            int sampleColumns = Math.Max(2, (int)MathF.Ceiling((maxX - minX) / sampleStep) + 1);
            int sampleRows = Math.Max(2, (int)MathF.Ceiling((maxY - minY) / sampleStep) + 1);
            OceanZoneDebugSample[] samples = new OceanZoneDebugSample[sampleColumns * sampleRows];
            float maxSearchDistance = maxDebugThreshold + sampleStep;

            for (int y = 0; y < sampleRows; y++)
            {
                float sampleY = minY + (y * sampleStep);
                for (int x = 0; x < sampleColumns; x++)
                {
                    float sampleX = minX + (x * sampleStep);
                    samples[Index(x, y, sampleColumns)] = ResolveOceanZoneDebugWaterSample(new Vector2(sampleX, sampleY), maxSearchDistance);
                }
            }

            suppressedComponents = ApplyOceanZoneDebugMinimumStableZoneFilter(samples, sampleColumns, sampleRows, sampleStep, sampleStep);
            remainingTinyComponents = CountOceanZoneDebugMinimumStableZoneViolations(
                samples,
                sampleColumns,
                sampleRows,
                sampleStep,
                sampleStep,
                out string violationSummary);
            _oceanZoneDebugTinyZoneViolationSummary = violationSummary;
            return remainingTinyComponents == 0;
        }

        private static void EnsureOceanZoneDebugPixelTexture(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_oceanZoneDebugPixelTexture != null && !_oceanZoneDebugPixelTexture.IsDisposed)
            {
                return;
            }

            _oceanZoneDebugPixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _oceanZoneDebugPixelTexture.SetData([Color.White]);
        }

        private static void UpdateOceanZoneDebugSegments(float minX, float maxX, float minY, float maxY)
        {
            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();
            Stopwatch stopwatch = Stopwatch.StartNew();
            OceanZoneDebugSegments.Clear();
            if (_lastOceanZoneDebugBuildSeed != _terrainWorldSeed)
            {
                ResetOceanZoneDebugTileWorkerState(clearCache: true);
                ResetOceanZoneDebugFullMapState(clearSegments: true);
                _lastOceanZoneDebugBuildSeed = _terrainWorldSeed;
            }

            ChunkBounds fullMapWindow = ResolveFullTerrainMapChunkWindow();
            QueueFullTerrainMapBuilds(_lastVisibleChunkWindow);
            TryPromoteCompletedChunks(fullMapWindow);
            UpdateFullTerrainMapGenerationState();
            EnsureFullOceanZoneDebugBuildQueued();
            TryApplyCompletedFullOceanZoneDebugBuild();
            TryPromoteCompletedOceanZoneDebugTiles();
            if (!_fullOceanZoneDebugReady)
            {
                _lastOceanZoneDebugBuildSeed = _terrainWorldSeed;
                _oceanZoneDebugBorderSegmentCount = OceanZoneDebugSegments.Count;
                _oceanZoneDebugBorderLabelCount = 0;
                _oceanZoneDebugBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                return;
            }

            PopulateVisibleOceanZoneDebugSegmentsFromFullMap(minX, maxX, minY, maxY);

            stopwatch.Stop();
            _lastOceanZoneDebugBuildSeed = _terrainWorldSeed;
            _oceanZoneDebugBorderSegmentCount = OceanZoneDebugSegments.Count;
            _oceanZoneDebugBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }

        private static bool OceanZoneDebugViewExtendsBeyondFullMap(float minX, float maxX, float minY, float maxY)
        {
            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                maxX <= minX ||
                maxY <= minY)
            {
                return false;
            }

            TerrainWorldBounds fullMapBounds = BuildChunkWorldBounds(ResolveFullTerrainMapChunkWindow());
            float margin = OceanZoneDebugVisionBuildPaddingWorldUnits;
            return minX - margin < fullMapBounds.MinX ||
                maxX + margin > fullMapBounds.MaxX ||
                minY - margin < fullMapBounds.MinY ||
                maxY + margin > fullMapBounds.MaxY;
        }

        private static bool TryResolveOceanZoneDebugTileRange(
            float minX,
            float maxX,
            float minY,
            float maxY,
            out int minTileX,
            out int maxTileX,
            out int minTileY,
            out int maxTileY)
        {
            minTileX = maxTileX = minTileY = maxTileY = 0;
            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                maxX <= minX ||
                maxY <= minY)
            {
                return false;
            }

            float margin = OceanZoneDebugTileWorldUnits;
            minTileX = ResolveOceanZoneDebugTileCoordinate(minX - margin);
            maxTileX = ResolveOceanZoneDebugTileCoordinate(maxX + margin);
            minTileY = ResolveOceanZoneDebugTileCoordinate(minY - margin);
            maxTileY = ResolveOceanZoneDebugTileCoordinate(maxY + margin);
            return true;
        }

        private static int ResolveOceanZoneDebugTileCoordinate(float worldCoordinate)
        {
            if (!float.IsFinite(worldCoordinate))
            {
                return 0;
            }

            return (int)MathF.Floor(worldCoordinate / OceanZoneDebugTileWorldUnits);
        }

        private static List<OceanZoneDebugSegment> GetOceanZoneDebugTileSegments(OceanZoneDebugTileKey tileKey)
        {
            if (OceanZoneDebugTileSegmentCache.TryGetValue(tileKey, out List<OceanZoneDebugSegment> cachedSegments))
            {
                return cachedSegments;
            }

            float tileMinX = tileKey.X * OceanZoneDebugTileWorldUnits;
            float tileMinY = tileKey.Y * OceanZoneDebugTileWorldUnits;
            List<OceanZoneDebugSegment> segments = new();
            BuildOceanZoneDebugSegmentsForFixedGrid(
                tileMinX,
                tileMinX + OceanZoneDebugTileWorldUnits,
                tileMinY,
                tileMinY + OceanZoneDebugTileWorldUnits,
                OceanZoneDebugSampleStepWorldUnits,
                segments);
            OceanZoneDebugTileSegmentCache[tileKey] = segments;
            TouchOceanZoneDebugTileCache(tileKey);

            return segments;
        }

        private static void QueueOceanZoneDebugTileBuilds(List<OceanZoneDebugTileBuildCandidate> missingTileCandidates)
        {
            if (missingTileCandidates == null || missingTileCandidates.Count == 0)
            {
                return;
            }

            missingTileCandidates.Sort(static (left, right) =>
            {
                int distanceCompare = left.DistanceSq.CompareTo(right.DistanceSq);
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }

                int yCompare = left.Key.Y.CompareTo(right.Key.Y);
                return yCompare != 0 ? yCompare : left.Key.X.CompareTo(right.Key.X);
            });

            EnsureOceanZoneDebugTileWorkerRunning();
            int enqueuedCount = 0;
            lock (OceanZoneDebugTileWorkerLock)
            {
                for (int i = 0; i < missingTileCandidates.Count && enqueuedCount < MaxNewOceanZoneDebugTileBuildsPerFrame; i++)
                {
                    if (OceanZoneDebugTileBuildQueue.Count >= OceanZoneDebugTileBuildQueueLimit)
                    {
                        break;
                    }

                    OceanZoneDebugTileBuildCandidate candidate = missingTileCandidates[i];
                    if (OceanZoneDebugTileSegmentCache.ContainsKey(candidate.Key) ||
                        QueuedOceanZoneDebugTileKeys.Contains(candidate.Key) ||
                        BuildingOceanZoneDebugTileKeys.Contains(candidate.Key) ||
                        ContainsCompletedOceanZoneDebugTileBuildLocked(candidate.Key))
                    {
                        continue;
                    }

                    OceanZoneDebugTileBuildQueue.Add(candidate);
                    QueuedOceanZoneDebugTileKeys.Add(candidate.Key);
                    _oceanZoneDebugQueuedTileBuildCount++;
                    enqueuedCount++;
                }

                if (enqueuedCount > 0)
                {
                    SortOceanZoneDebugTileBuildQueueLocked();
                    UpdateOceanZoneDebugTileWorkerTelemetryLocked();
                    Monitor.Pulse(OceanZoneDebugTileWorkerLock);
                }
            }
        }

        private static bool IsOceanZoneDebugTileQueuedOrBuilding(OceanZoneDebugTileKey tileKey)
        {
            lock (OceanZoneDebugTileWorkerLock)
            {
                return QueuedOceanZoneDebugTileKeys.Contains(tileKey) ||
                    BuildingOceanZoneDebugTileKeys.Contains(tileKey) ||
                    ContainsCompletedOceanZoneDebugTileBuildLocked(tileKey);
            }
        }

        private static bool ContainsCompletedOceanZoneDebugTileBuildLocked(OceanZoneDebugTileKey tileKey)
        {
            foreach (OceanZoneDebugTileBuildResult result in CompletedOceanZoneDebugTileBuildQueue)
            {
                if (result.Key.Equals(tileKey))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureOceanZoneDebugTileWorkerRunning()
        {
            if (_oceanZoneDebugTileWorkerThread != null && _oceanZoneDebugTileWorkerThread.IsAlive)
            {
                return;
            }

            lock (OceanZoneDebugTileWorkerLock)
            {
                if (_oceanZoneDebugTileWorkerThread != null && _oceanZoneDebugTileWorkerThread.IsAlive)
                {
                    return;
                }

                _oceanZoneDebugTileWorkerStopRequested = false;
                _oceanZoneDebugTileWorkerThread = new Thread(OceanZoneDebugTileWorkerLoop)
                {
                    IsBackground = true,
                    Name = "OceanZoneDebugTileWorker",
                    Priority = ThreadPriority.BelowNormal
                };
                _oceanZoneDebugTileWorkerThread.Start();
            }
        }

        private static void OceanZoneDebugTileWorkerLoop()
        {
            while (true)
            {
                OceanZoneDebugTileBuildCandidate candidate;
                int generation;
                lock (OceanZoneDebugTileWorkerLock)
                {
                    while (!_oceanZoneDebugTileWorkerStopRequested && OceanZoneDebugTileBuildQueue.Count == 0)
                    {
                        UpdateOceanZoneDebugTileWorkerTelemetryLocked();
                        Monitor.Wait(OceanZoneDebugTileWorkerLock);
                    }

                    if (_oceanZoneDebugTileWorkerStopRequested)
                    {
                        return;
                    }

                    candidate = OceanZoneDebugTileBuildQueue[0];
                    OceanZoneDebugTileBuildQueue.RemoveAt(0);
                    QueuedOceanZoneDebugTileKeys.Remove(candidate.Key);
                    BuildingOceanZoneDebugTileKeys.Add(candidate.Key);
                    generation = _oceanZoneDebugTileWorkerGeneration;
                    UpdateOceanZoneDebugTileWorkerTelemetryLocked();
                }

                OceanZoneDebugTileBuildResult result = default;
                bool hasResult = false;
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    float tileMinX = candidate.Key.X * OceanZoneDebugTileWorldUnits;
                    float tileMinY = candidate.Key.Y * OceanZoneDebugTileWorldUnits;
                    List<OceanZoneDebugSegment> segments = new();
                    BuildOceanZoneDebugSegmentsForFixedGrid(
                        tileMinX,
                        tileMinX + OceanZoneDebugTileWorldUnits,
                        tileMinY,
                        tileMinY + OceanZoneDebugTileWorldUnits,
                        OceanZoneDebugSampleStepWorldUnits,
                        segments);
                    stopwatch.Stop();

                    result = new OceanZoneDebugTileBuildResult(candidate.Key, segments, stopwatch.Elapsed.TotalMilliseconds);
                    hasResult = true;
                    if (result.BuildMilliseconds >= OceanZoneDebugSlowTileBuildLogThresholdMilliseconds &&
                        _oceanZoneDebugSlowTileBuildLogCount < OceanZoneDebugMaxSlowTileBuildLogs)
                    {
                        _oceanZoneDebugSlowTileBuildLogCount++;
                        DebugLogger.PrintDebug(
                            $"GameBlockTerrainBackground: ocean debug tile {candidate.Key.X},{candidate.Key.Y} built slowly in {result.BuildMilliseconds:0.0} ms with {segments.Count} segments.");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"GameBlockTerrainBackground: ocean debug tile build {candidate.Key.X},{candidate.Key.Y} failed. {ex.GetBaseException().Message}");
                }

                lock (OceanZoneDebugTileWorkerLock)
                {
                    BuildingOceanZoneDebugTileKeys.Remove(candidate.Key);
                    if (hasResult &&
                        generation == _oceanZoneDebugTileWorkerGeneration)
                    {
                        CompletedOceanZoneDebugTileBuildQueue.Enqueue(result);
                    }

                    UpdateOceanZoneDebugTileWorkerTelemetryLocked();
                    Monitor.Pulse(OceanZoneDebugTileWorkerLock);
                }
            }
        }

        private static void SortOceanZoneDebugTileBuildQueueLocked()
        {
            OceanZoneDebugTileBuildQueue.Sort(static (left, right) =>
            {
                int distanceCompare = left.DistanceSq.CompareTo(right.DistanceSq);
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }

                int yCompare = left.Key.Y.CompareTo(right.Key.Y);
                return yCompare != 0 ? yCompare : left.Key.X.CompareTo(right.Key.X);
            });
        }

        private static void TryPromoteCompletedOceanZoneDebugTiles()
        {
            int promotedCount = 0;
            while (promotedCount < MaxCompletedOceanZoneDebugTilePromotionsPerFrame)
            {
                OceanZoneDebugTileBuildResult result;
                bool hasResult;
                lock (OceanZoneDebugTileWorkerLock)
                {
                    hasResult = CompletedOceanZoneDebugTileBuildQueue.Count > 0;
                    result = hasResult ? CompletedOceanZoneDebugTileBuildQueue.Dequeue() : default;
                    UpdateOceanZoneDebugTileWorkerTelemetryLocked();
                }

                if (!hasResult)
                {
                    break;
                }

                OceanZoneDebugTileSegmentCache[result.Key] = result.Segments ?? new List<OceanZoneDebugSegment>();
                TouchOceanZoneDebugTileCache(result.Key);
                promotedCount++;
            }
        }

        private static void TouchOceanZoneDebugTileCache(OceanZoneDebugTileKey tileKey)
        {
            _oceanZoneDebugTileCacheTouchTick++;
            OceanZoneDebugTileSegmentCacheTouchTicks[tileKey] = _oceanZoneDebugTileCacheTouchTick;
        }

        private static void PruneOceanZoneDebugTileCache(int retainMinTileX, int retainMaxTileX, int retainMinTileY, int retainMaxTileY)
        {
            if (OceanZoneDebugTileSegmentCache.Count <= OceanZoneDebugTileSegmentCacheLimit)
            {
                return;
            }

            List<OceanZoneDebugTileKey> removableKeys = new(OceanZoneDebugTileSegmentCache.Count);
            foreach (OceanZoneDebugTileKey key in OceanZoneDebugTileSegmentCache.Keys)
            {
                if (key.X >= retainMinTileX &&
                    key.X <= retainMaxTileX &&
                    key.Y >= retainMinTileY &&
                    key.Y <= retainMaxTileY)
                {
                    continue;
                }

                removableKeys.Add(key);
            }

            removableKeys.Sort((left, right) =>
            {
                int leftTick = OceanZoneDebugTileSegmentCacheTouchTicks.TryGetValue(left, out int resolvedLeftTick)
                    ? resolvedLeftTick
                    : 0;
                int rightTick = OceanZoneDebugTileSegmentCacheTouchTicks.TryGetValue(right, out int resolvedRightTick)
                    ? resolvedRightTick
                    : 0;
                return leftTick.CompareTo(rightTick);
            });

            for (int i = 0; i < removableKeys.Count && OceanZoneDebugTileSegmentCache.Count > OceanZoneDebugTileSegmentCacheLimit; i++)
            {
                OceanZoneDebugTileSegmentCache.Remove(removableKeys[i]);
                OceanZoneDebugTileSegmentCacheTouchTicks.Remove(removableKeys[i]);
            }
        }

        private static void ResetOceanZoneDebugTileWorkerState(bool clearCache)
        {
            lock (OceanZoneDebugTileWorkerLock)
            {
                _oceanZoneDebugTileWorkerGeneration++;
                OceanZoneDebugTileBuildQueue.Clear();
                QueuedOceanZoneDebugTileKeys.Clear();
                BuildingOceanZoneDebugTileKeys.Clear();
                CompletedOceanZoneDebugTileBuildQueue.Clear();
                UpdateOceanZoneDebugTileWorkerTelemetryLocked();
                Monitor.Pulse(OceanZoneDebugTileWorkerLock);
            }

            if (clearCache)
            {
                OceanZoneDebugSegments.Clear();
                OceanZoneDebugTileSegmentCache.Clear();
                OceanZoneDebugTileSegmentCacheTouchTicks.Clear();
            }
        }

        private static void UpdateOceanZoneDebugTileWorkerTelemetryLocked()
        {
            _oceanZoneDebugTileWorkerQueuedBuildCount = OceanZoneDebugTileBuildQueue.Count;
            _oceanZoneDebugTileWorkerActiveBuildCount = BuildingOceanZoneDebugTileKeys.Count;
            _oceanZoneDebugTileWorkerCompletedQueueCount = CompletedOceanZoneDebugTileBuildQueue.Count;
        }

        private static string ResolveOceanZoneDebugWorkerStatus()
        {
            if (_fullOceanZoneDebugBuildTask != null)
            {
                return _fullOceanZoneDebugBuildTask.IsCompleted
                    ? "full-map build awaiting apply"
                    : "building full-map ocean borders";
            }

            return _fullOceanZoneDebugReady
                ? $"full-map ready segments={_fullOceanZoneDebugSegmentCount}"
                : _fullOceanZoneDebugStatus;
        }

        private static void EnsureFullOceanZoneDebugBuildQueued()
        {
            if (_fullOceanZoneDebugReady ||
                _fullOceanZoneDebugBuildTask != null)
            {
                return;
            }

            ChunkBounds fullMapWindow = ResolveFullTerrainMapChunkWindow();
            TerrainMapSnapshot snapshot = _fullTerrainMapSnapshot;
            int seed = _terrainWorldSeed;
            _fullOceanZoneDebugStatus = "building full-map ocean borders";
            _fullOceanZoneDebugBuildTask = Task.Run(() => BuildFullOceanZoneDebugSegments(seed, fullMapWindow, snapshot));
        }

        private static OceanZoneDebugFullMapBuildResult BuildFullOceanZoneDebugSegments(
            int seed,
            ChunkBounds fullMapWindow,
            TerrainMapSnapshot snapshot)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<OceanZoneDebugSegment> segments = new();
            TerrainMapSnapshot previousSnapshot = _terrainMapSnapshotOverride;
            bool previousSeedOverrideActive = _oceanZoneDebugDistanceSeedOverrideActive;
            int previousSeedOverride = _oceanZoneDebugDistanceSeedOverride;
            _terrainMapSnapshotOverride = snapshot;
            _oceanZoneDebugDistanceSeedOverrideActive = true;
            _oceanZoneDebugDistanceSeedOverride = seed;
            try
            {
                TerrainWorldBounds worldBounds = BuildChunkWorldBounds(fullMapWindow);
                BuildOceanZoneDebugSegmentsForFixedGrid(
                    worldBounds.MinX,
                    worldBounds.MaxX,
                    worldBounds.MinY,
                    worldBounds.MaxY,
                    OceanZoneDebugFullMapSampleStepWorldUnits,
                    segments);
            }
            finally
            {
                _terrainMapSnapshotOverride = previousSnapshot;
                _oceanZoneDebugDistanceSeedOverrideActive = previousSeedOverrideActive;
                _oceanZoneDebugDistanceSeedOverride = previousSeedOverride;
            }

            stopwatch.Stop();
            return new OceanZoneDebugFullMapBuildResult(
                seed,
                fullMapWindow,
                segments,
                stopwatch.Elapsed.TotalMilliseconds,
                _oceanZoneDebugTinyZoneSuppressionScratchCount);
        }

        private static void TryApplyCompletedFullOceanZoneDebugBuild()
        {
            Task<OceanZoneDebugFullMapBuildResult> task = _fullOceanZoneDebugBuildTask;
            if (task == null || !task.IsCompleted)
            {
                return;
            }

            _fullOceanZoneDebugBuildTask = null;
            if (task.IsFaulted)
            {
                Exception ex = task.Exception?.GetBaseException();
                _fullOceanZoneDebugStatus = $"full-map ocean border build failed: {ex?.Message ?? "unknown error"}";
                DebugLogger.PrintWarning($"GameBlockTerrainBackground: full-map ocean border build failed. {ex?.Message ?? "unknown error"}");
                return;
            }

            OceanZoneDebugFullMapBuildResult result = task.Result;
            if (result.Seed != _terrainWorldSeed ||
                !ChunkBoundsEqual(result.FullMapWindow, ResolveFullTerrainMapChunkWindow()))
            {
                _fullOceanZoneDebugStatus = "full-map ocean border build discarded as stale";
                return;
            }

            FullOceanZoneDebugSegments.Clear();
            FullOceanZoneDebugSegments.AddRange(result.Segments);
            _fullOceanZoneDebugReady = true;
            _fullOceanZoneDebugBuildSeed = result.Seed;
            _fullOceanZoneDebugSegmentCount = FullOceanZoneDebugSegments.Count;
            _fullOceanZoneDebugBuildMilliseconds = result.BuildMilliseconds;
            _oceanZoneDebugSuppressedTinyZoneCount = result.SuppressedTinyZoneCount;
            _fullOceanZoneDebugStatus = $"full-map ready segments={_fullOceanZoneDebugSegmentCount} buildMs={_fullOceanZoneDebugBuildMilliseconds:0.###} tinyZonesSuppressed={_oceanZoneDebugSuppressedTinyZoneCount}";
            DebugLogger.PrintDebug(
                $"GameBlockTerrainBackground: full-map ocean borders built {FullOceanZoneDebugSegments.Count} segments in {result.BuildMilliseconds:0.0} ms; tinyZonesSuppressed={_oceanZoneDebugSuppressedTinyZoneCount}.");
        }

        private static void PopulateVisibleOceanZoneDebugSegmentsFromFullMap(float minX, float maxX, float minY, float maxY)
        {
            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                maxX <= minX ||
                maxY <= minY)
            {
                return;
            }

            float margin = OceanZoneDebugVisionBuildPaddingWorldUnits;
            float expandedMinX = minX - margin;
            float expandedMaxX = maxX + margin;
            float expandedMinY = minY - margin;
            float expandedMaxY = maxY + margin;
            for (int i = 0; i < FullOceanZoneDebugSegments.Count && OceanZoneDebugSegments.Count < OceanZoneDebugMaxSegments; i++)
            {
                OceanZoneDebugSegment segment = FullOceanZoneDebugSegments[i];
                if (OceanZoneDebugSegmentIntersectsWorldBounds(
                    segment,
                    expandedMinX,
                    expandedMaxX,
                    expandedMinY,
                    expandedMaxY))
                {
                    OceanZoneDebugSegments.Add(segment);
                }
            }
        }

        private static bool TryPopulateVisibleOceanZoneDebugSegmentsFromTiles(float minX, float maxX, float minY, float maxY)
        {
            return TryPopulateVisibleOceanZoneDebugSegmentsFromTiles(
                minX,
                maxX,
                minY,
                maxY,
                excludeWorldBounds: false,
                excludedWorldBounds: default);
        }

        private static bool TryPopulateVisibleOceanZoneDebugSegmentsFromTiles(
            float minX,
            float maxX,
            float minY,
            float maxY,
            bool excludeWorldBounds,
            TerrainWorldBounds excludedWorldBounds)
        {
            if (!TryResolveOceanZoneDebugTileRange(
                minX,
                maxX,
                minY,
                maxY,
                out int minTileX,
                out int maxTileX,
                out int minTileY,
                out int maxTileY))
            {
                return false;
            }

            PruneOceanZoneDebugTileCache(minTileX, maxTileX, minTileY, maxTileY);
            float margin = OceanZoneDebugVisionBuildPaddingWorldUnits;
            float expandedMinX = minX - margin;
            float expandedMaxX = maxX + margin;
            float expandedMinY = minY - margin;
            float expandedMaxY = maxY + margin;

            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    OceanZoneDebugTileKey tileKey = new(tileX, tileY);
                    List<OceanZoneDebugSegment> cachedSegments = GetOceanZoneDebugTileSegments(tileKey);
                    if (cachedSegments == null)
                    {
                        continue;
                    }

                    TouchOceanZoneDebugTileCache(tileKey);
                    for (int i = 0; i < cachedSegments.Count && OceanZoneDebugSegments.Count < OceanZoneDebugMaxSegments; i++)
                    {
                        OceanZoneDebugSegment segment = cachedSegments[i];
                        if (excludeWorldBounds &&
                            IsOceanZoneDebugSegmentMidpointInsideBounds(segment, excludedWorldBounds))
                        {
                            continue;
                        }

                        if (OceanZoneDebugSegmentIntersectsWorldBounds(
                            segment,
                            expandedMinX,
                            expandedMaxX,
                            expandedMinY,
                            expandedMaxY))
                        {
                            OceanZoneDebugSegments.Add(segment);
                        }
                    }
                }
            }

            return OceanZoneDebugSegments.Count > 0;
        }

        private static bool IsOceanZoneDebugSegmentMidpointInsideBounds(
            OceanZoneDebugSegment segment,
            TerrainWorldBounds bounds)
        {
            Vector2 midpoint = (segment.From + segment.To) * 0.5f;
            return midpoint.X >= bounds.MinX &&
                midpoint.X <= bounds.MaxX &&
                midpoint.Y >= bounds.MinY &&
                midpoint.Y <= bounds.MaxY;
        }

        private static bool OceanZoneDebugSegmentIntersectsWorldBounds(
            OceanZoneDebugSegment segment,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            float segmentMinX = MathF.Min(segment.From.X, segment.To.X);
            float segmentMaxX = MathF.Max(segment.From.X, segment.To.X);
            float segmentMinY = MathF.Min(segment.From.Y, segment.To.Y);
            float segmentMaxY = MathF.Max(segment.From.Y, segment.To.Y);
            return segmentMaxX >= minX &&
                segmentMinX <= maxX &&
                segmentMaxY >= minY &&
                segmentMinY <= maxY;
        }

        private static bool TryResolveOceanZoneDebugBuildGrid(
            float minX,
            float maxX,
            float minY,
            float maxY,
            out float buildMinX,
            out float buildMaxX,
            out float buildMinY,
            out float buildMaxY,
            out float sampleStep)
        {
            buildMinX = buildMaxX = buildMinY = buildMaxY = 0f;
            sampleStep = OceanZoneDebugSampleStepWorldUnits;
            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                maxX <= minX ||
                maxY <= minY)
            {
                return false;
            }

            float maxDebugThreshold = ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance));
            float margin = maxDebugThreshold + OceanZoneDebugSampleStepWorldUnits;
            float targetWidth = (maxX - minX) + (margin * 2f);
            float targetHeight = (maxY - minY) + (margin * 2f);
            float expandedSpan = MathF.Max(targetWidth, targetHeight);
            sampleStep = MathF.Max(
                OceanZoneDebugSampleStepWorldUnits,
                expandedSpan / Math.Max(1, OceanZoneDebugMaxSampleAxis - 4));

            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int columns = Math.Max(2, (int)MathF.Ceiling(targetWidth / sampleStep) + 1);
                int rows = Math.Max(2, (int)MathF.Ceiling(targetHeight / sampleStep) + 1);
                if (columns <= OceanZoneDebugMaxSampleAxis + 1 &&
                    rows <= OceanZoneDebugMaxSampleAxis + 1)
                {
                    float snappedCenterX = MathF.Round(centerX / sampleStep) * sampleStep;
                    float snappedCenterY = MathF.Round(centerY / sampleStep) * sampleStep;
                    float halfWidth = ((columns - 1) * sampleStep) * 0.5f;
                    float halfHeight = ((rows - 1) * sampleStep) * 0.5f;
                    buildMinX = snappedCenterX - halfWidth;
                    buildMaxX = snappedCenterX + halfWidth;
                    buildMinY = snappedCenterY - halfHeight;
                    buildMaxY = snappedCenterY + halfHeight;
                    return true;
                }

                sampleStep *= 1.18f;
            }

            float fallbackCenterX = MathF.Round(centerX / sampleStep) * sampleStep;
            float fallbackCenterY = MathF.Round(centerY / sampleStep) * sampleStep;
            buildMinX = fallbackCenterX - (targetWidth * 0.5f);
            buildMaxX = fallbackCenterX + (targetWidth * 0.5f);
            buildMinY = fallbackCenterY - (targetHeight * 0.5f);
            buildMaxY = fallbackCenterY + (targetHeight * 0.5f);

            return true;
        }

        private static int BuildOceanZoneDebugSegments(
            float minX,
            float maxX,
            float minY,
            float maxY,
            List<OceanZoneDebugSegment> segments)
        {
            segments?.Clear();
            if (segments == null ||
                !float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                maxX <= minX ||
                maxY <= minY)
            {
                return 0;
            }

            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();

            if (!TryResolveOceanZoneDebugBuildGrid(
                minX,
                maxX,
                minY,
                maxY,
                out minX,
                out maxX,
                out minY,
                out maxY,
                out float sampleStep))
            {
                return 0;
            }

            return BuildOceanZoneDebugSegmentsForFixedGrid(minX, maxX, minY, maxY, sampleStep, segments);
        }

        private static int BuildOceanZoneDebugSegmentsForFixedGrid(
            float minX,
            float maxX,
            float minY,
            float maxY,
            float sampleStep,
            List<OceanZoneDebugSegment> segments)
        {
            if (segments == null ||
                !float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY) ||
                !float.IsFinite(sampleStep) ||
                maxX <= minX ||
                maxY <= minY ||
                sampleStep <= 0f)
            {
                return 0;
            }

            float maxDebugThreshold = ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance));
            int sampleColumns = Math.Max(2, (int)MathF.Ceiling((maxX - minX) / sampleStep) + 1);
            int sampleRows = Math.Max(2, (int)MathF.Ceiling((maxY - minY) / sampleStep) + 1);
            float stepX = sampleStep;
            float stepY = sampleStep;
            OceanZoneDebugSample[] samples = new OceanZoneDebugSample[sampleColumns * sampleRows];
            float maxSearchDistance = maxDebugThreshold + MathF.Max(stepX, stepY);

            for (int y = 0; y < sampleRows; y++)
            {
                float sampleY = minY + (y * stepY);
                for (int x = 0; x < sampleColumns; x++)
                {
                    float sampleX = minX + (x * stepX);
                    int sampleIndex = Index(x, y, sampleColumns);
                    Vector2 samplePosition = new(sampleX, sampleY);
                    samples[sampleIndex] = ResolveOceanZoneDebugWaterSample(samplePosition, maxSearchDistance);
                }
            }

            _oceanZoneDebugTinyZoneSuppressionScratchCount = 0;
            _oceanZoneDebugTinyZoneSuppressedCellsScratch = null;
            _oceanZoneDebugTinyZoneSuppressionScratchCount = ApplyOceanZoneDebugMinimumStableZoneFilter(
                samples,
                sampleColumns,
                sampleRows,
                stepX,
                stepY);

            float[] thresholds = ResolveOceanZoneDebugThresholds();
            for (int y = 0; y < sampleRows - 1 && segments.Count < OceanZoneDebugMaxSegments; y++)
            {
                for (int x = 0; x < sampleColumns - 1 && segments.Count < OceanZoneDebugMaxSegments; x++)
                {
                    Vector2 topLeftPosition = new(minX + (x * stepX), minY + (y * stepY));
                    Vector2 topRightPosition = new(topLeftPosition.X + stepX, topLeftPosition.Y);
                    Vector2 bottomRightPosition = new(topLeftPosition.X + stepX, topLeftPosition.Y + stepY);
                    Vector2 bottomLeftPosition = new(topLeftPosition.X, topLeftPosition.Y + stepY);
                    OceanZoneDebugSample topLeft = samples[Index(x, y, sampleColumns)];
                    OceanZoneDebugSample topRight = samples[Index(x + 1, y, sampleColumns)];
                    OceanZoneDebugSample bottomRight = samples[Index(x + 1, y + 1, sampleColumns)];
                    OceanZoneDebugSample bottomLeft = samples[Index(x, y + 1, sampleColumns)];
                    if (!topLeft.IsWater ||
                        !topRight.IsWater ||
                        !bottomRight.IsWater ||
                        !bottomLeft.IsWater)
                    {
                        continue;
                    }

                    AddOceanZoneDebugCellSegments(
                        topLeft,
                        topRight,
                        bottomRight,
                        bottomLeft,
                        topLeftPosition,
                        topRightPosition,
                        bottomRightPosition,
                        bottomLeftPosition,
                        thresholds,
                        maxSearchDistance,
                        depth: 0,
                        segments);
                }
            }

            return segments.Count;
        }

        private static float ResolveOceanZoneDebugSampleStep(float minX, float maxX, float minY, float maxY)
        {
            float span = MathF.Max(MathF.Abs(maxX - minX), MathF.Abs(maxY - minY));
            return MathF.Max(OceanZoneDebugSampleStepWorldUnits, span / Math.Max(1, OceanZoneDebugMaxSampleAxis));
        }

        private static float[] ResolveOceanZoneDebugThresholds()
        {
            return
            [
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance),
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance),
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterTwilightDistance),
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance)
            ];
        }

        private static int ApplyOceanZoneDebugMinimumStableZoneFilter(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            float stepX,
            float stepY)
        {
            if (samples == null ||
                samples.Length < width * height ||
                width <= 0 ||
                height <= 0 ||
                !float.IsFinite(stepX) ||
                !float.IsFinite(stepY) ||
                stepX <= 0f ||
                stepY <= 0f)
            {
                return 0;
            }

            int totalSuppressed = 0;
            bool[] suppressedCells = new bool[samples.Length];
            _oceanZoneDebugTinyZoneSuppressedCellsScratch = suppressedCells;
            for (int pass = 0; pass < OceanZoneDebugMinimumStableZoneFilterPasses; pass++)
            {
                int passSuppressed = ApplyOceanZoneDebugMinimumStableZoneFilterPass(samples, width, height, stepX, stepY, suppressedCells);
                totalSuppressed += passSuppressed;
                if (passSuppressed == 0)
                {
                    break;
                }
            }

            return totalSuppressed;
        }

        private static int ApplyOceanZoneDebugMinimumStableZoneFilterPass(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            float stepX,
            float stepY,
            bool[] suppressedCells)
        {
            bool[] visited = new bool[width * height];
            List<int> component = new();
            Queue<int> queue = new();
            int suppressed = 0;

            for (int index = 0; index < samples.Length; index++)
            {
                if (visited[index] || !samples[index].IsWater)
                {
                    continue;
                }

                component.Clear();
                CollectOceanZoneDebugComponent(samples, width, height, index, visited, queue, component);
                if (component.Count == 0)
                {
                    continue;
                }

                if (OceanZoneDebugComponentContainsSuppressedCell(component, suppressedCells))
                {
                    continue;
                }

                if (OceanZoneDebugComponentTouchesSampleBoundary(component, width, height))
                {
                    continue;
                }

                TerrainWaterType waterType = samples[index].WaterType;
                if (OceanZoneDebugComponentHasStableCore(samples, width, height, stepX, stepY, component, waterType))
                {
                    continue;
                }

                TerrainWaterType fallback = ResolveOceanZoneDebugTinyZoneFallback(samples, width, height, component, waterType);
                if (fallback == waterType)
                {
                    continue;
                }

                float fallbackDistance = ResolveRepresentativeOffshoreDistanceForWaterType(fallback);
                for (int i = 0; i < component.Count; i++)
                {
                    int componentIndex = component[i];
                    samples[componentIndex] = new OceanZoneDebugSample(true, fallback, fallbackDistance);
                    if (suppressedCells != null && componentIndex >= 0 && componentIndex < suppressedCells.Length)
                    {
                        suppressedCells[componentIndex] = true;
                    }
                }

                suppressed++;
            }

            return suppressed;
        }

        private static int CountOceanZoneDebugMinimumStableZoneViolations(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            float stepX,
            float stepY,
            out string summary)
        {
            summary = "none";
            if (samples == null || samples.Length < width * height)
            {
                return 0;
            }

            bool[] visited = new bool[width * height];
            List<int> component = new();
            Queue<int> queue = new();
            int violations = 0;
            Dictionary<string, int> violationCounts = new();
            bool[] suppressedCells = _oceanZoneDebugTinyZoneSuppressedCellsScratch;

            for (int index = 0; index < samples.Length; index++)
            {
                if (visited[index] || !samples[index].IsWater)
                {
                    continue;
                }

                component.Clear();
                CollectOceanZoneDebugComponent(samples, width, height, index, visited, queue, component);
                if (component.Count == 0)
                {
                    continue;
                }

                if (OceanZoneDebugComponentTouchesSampleBoundary(component, width, height))
                {
                    continue;
                }

                if (OceanZoneDebugComponentContainsSuppressedCell(component, suppressedCells))
                {
                    continue;
                }

                TerrainWaterType waterType = samples[index].WaterType;
                TerrainWaterType fallback = ResolveOceanZoneDebugTinyZoneFallback(samples, width, height, component, waterType);
                if (!OceanZoneDebugComponentHasStableCore(samples, width, height, stepX, stepY, component, waterType) &&
                    fallback != waterType)
                {
                    violations++;
                    string key = $"{waterType}->{fallback}";
                    violationCounts[key] = violationCounts.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }

            if (violationCounts.Count > 0)
            {
                List<string> parts = new();
                foreach (KeyValuePair<string, int> entry in violationCounts)
                {
                    parts.Add($"{entry.Key}:{entry.Value}");
                }

                parts.Sort(StringComparer.Ordinal);
                summary = string.Join(", ", parts);
            }

            return violations;
        }

        private static bool OceanZoneDebugComponentContainsSuppressedCell(List<int> component, bool[] suppressedCells)
        {
            if (component == null || suppressedCells == null)
            {
                return false;
            }

            for (int i = 0; i < component.Count; i++)
            {
                int index = component[i];
                if (index >= 0 && index < suppressedCells.Length && suppressedCells[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectOceanZoneDebugComponent(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            int startIndex,
            bool[] visited,
            Queue<int> queue,
            List<int> component)
        {
            TerrainWaterType waterType = samples[startIndex].WaterType;
            visited[startIndex] = true;
            queue.Enqueue(startIndex);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                component.Add(index);
                int x = index % width;
                int y = index / width;
                TryQueueOceanZoneDebugComponentNeighbor(samples, width, height, x - 1, y, waterType, visited, queue);
                TryQueueOceanZoneDebugComponentNeighbor(samples, width, height, x + 1, y, waterType, visited, queue);
                TryQueueOceanZoneDebugComponentNeighbor(samples, width, height, x, y - 1, waterType, visited, queue);
                TryQueueOceanZoneDebugComponentNeighbor(samples, width, height, x, y + 1, waterType, visited, queue);
            }
        }

        private static void TryQueueOceanZoneDebugComponentNeighbor(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            int x,
            int y,
            TerrainWaterType waterType,
            bool[] visited,
            Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = Index(x, y, width);
            if (visited[index] ||
                !samples[index].IsWater ||
                samples[index].WaterType != waterType)
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        private static bool OceanZoneDebugComponentHasStableCore(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            float stepX,
            float stepY,
            List<int> component,
            TerrainWaterType waterType)
        {
            for (int i = 0; i < component.Count; i++)
            {
                int index = component[i];
                int x = index % width;
                int y = index / width;
                if (IsOceanZoneDebugStableCoreCell(samples, width, height, stepX, stepY, x, y, waterType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOceanZoneDebugStableCoreCell(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            float stepX,
            float stepY,
            int centerX,
            int centerY,
            TerrainWaterType waterType)
        {
            float radius = MathF.Max(
                OceanZoneDebugMinimumStableZoneRadiusWorldUnits,
                MathF.Min(stepX, stepY));
            int radiusCellsX = Math.Max(1, (int)MathF.Ceiling(radius / stepX));
            int radiusCellsY = Math.Max(1, (int)MathF.Ceiling(radius / stepY));
            float radiusSq = radius * radius;

            for (int y = centerY - radiusCellsY; y <= centerY + radiusCellsY; y++)
            {
                for (int x = centerX - radiusCellsX; x <= centerX + radiusCellsX; x++)
                {
                    float dx = (x - centerX) * stepX;
                    float dy = (y - centerY) * stepY;
                    if ((dx * dx) + (dy * dy) > radiusSq)
                    {
                        continue;
                    }

                    if (x < 0 || x >= width || y < 0 || y >= height)
                    {
                        return false;
                    }

                    OceanZoneDebugSample sample = samples[Index(x, y, width)];
                    if (!sample.IsWater || sample.WaterType != waterType)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static TerrainWaterType ResolveOceanZoneDebugTinyZoneFallback(
            OceanZoneDebugSample[] samples,
            int width,
            int height,
            List<int> component,
            TerrainWaterType sourceType)
        {
            if (sourceType != TerrainWaterType.Abyss)
            {
                return ResolveOceanZoneDebugZoneBehind(sourceType);
            }

            if (OceanZoneDebugComponentTouchesSampleBoundary(component, width, height))
            {
                return sourceType;
            }

            Span<int> neighborCounts = stackalloc int[5];
            for (int i = 0; i < component.Count; i++)
            {
                int index = component[i];
                int centerX = index % width;
                int centerY = index / width;
                for (int y = centerY - 1; y <= centerY + 1; y++)
                {
                    for (int x = centerX - 1; x <= centerX + 1; x++)
                    {
                        if ((x == centerX && y == centerY) ||
                            x < 0 || x >= width || y < 0 || y >= height)
                        {
                            continue;
                        }

                        OceanZoneDebugSample neighbor = samples[Index(x, y, width)];
                        if (!neighbor.IsWater || neighbor.WaterType == sourceType)
                        {
                            continue;
                        }

                        int order = ResolveOceanZoneDebugWaterTypeOrder(neighbor.WaterType);
                        if (order >= 0 && order < neighborCounts.Length)
                        {
                            neighborCounts[order]++;
                        }
                    }
                }
            }

            int sourceOrder = ResolveOceanZoneDebugWaterTypeOrder(sourceType);
            int bestOrder = -1;
            int bestCount = 0;
            int bestDistance = int.MaxValue;
            for (int order = 0; order < neighborCounts.Length; order++)
            {
                int count = neighborCounts[order];
                int distance = Math.Abs(order - sourceOrder);
                if (count <= 0 ||
                    count < bestCount ||
                    (count == bestCount && distance >= bestDistance))
                {
                    continue;
                }

                bestOrder = order;
                bestCount = count;
                bestDistance = distance;
            }

            return bestOrder >= 0
                ? ResolveOceanZoneDebugWaterTypeFromOrder(bestOrder)
                : ResolveOceanZoneDebugZoneBehind(sourceType);
        }

        private static bool OceanZoneDebugComponentTouchesSampleBoundary(List<int> component, int width, int height)
        {
            for (int i = 0; i < component.Count; i++)
            {
                int index = component[i];
                int x = index % width;
                int y = index / width;
                if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static TerrainWaterType ResolveOceanZoneDebugZoneBehind(TerrainWaterType sourceType)
        {
            return sourceType switch
            {
                TerrainWaterType.Shallow => TerrainWaterType.Sunlit,
                TerrainWaterType.Sunlit => TerrainWaterType.Twilight,
                TerrainWaterType.Twilight => TerrainWaterType.Midnight,
                _ => TerrainWaterType.Abyss
            };
        }

        private static TerrainWaterType ResolveOceanZoneDebugWaterTypeFromOrder(int order)
        {
            return order switch
            {
                0 => TerrainWaterType.Shallow,
                1 => TerrainWaterType.Sunlit,
                2 => TerrainWaterType.Twilight,
                3 => TerrainWaterType.Midnight,
                _ => TerrainWaterType.Abyss
            };
        }

        private static float ResolveRepresentativeOffshoreDistanceForWaterType(TerrainWaterType waterType)
        {
            float shallow = ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance);
            float sunlit = ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance);
            float twilight = ResolveEffectiveOceanZoneTransitionDistance(DevWaterTwilightDistance);
            float midnight = ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance);
            return waterType switch
            {
                TerrainWaterType.Shallow => shallow * 0.5f,
                TerrainWaterType.Sunlit => (shallow + sunlit) * 0.5f,
                TerrainWaterType.Twilight => (sunlit + twilight) * 0.5f,
                TerrainWaterType.Midnight => (twilight + midnight) * 0.5f,
                _ => midnight + OceanZoneDebugMinimumStableZoneRadiusWorldUnits
            };
        }

        private static OceanZoneDebugSample ResolveOceanZoneDebugWaterSample(
            Vector2 worldPosition,
            float maxSearchDistanceWorldUnits)
        {
            if (!IsFiniteVector(worldPosition))
            {
                return new OceanZoneDebugSample(false, TerrainWaterType.Shallow, 0f);
            }

            if (!TerrainWorldContainsPoint(worldPosition.X, worldPosition.Y))
            {
                return new OceanZoneDebugSample(
                    true,
                    TerrainWaterType.Abyss,
                    ResolveInfiniteAbyssOffshoreDistance());
            }

            // Land is not a clipping mask here; zone polygons must stay closed and match the runtime distance resolver.
            float offshoreDistance = ResolveOceanZoneDebugOffshoreDistance(worldPosition, maxSearchDistanceWorldUnits);
            TerrainWaterType waterType = ResolveWaterTypeFromOffshoreDistance(offshoreDistance);
            return new OceanZoneDebugSample(true, waterType, offshoreDistance);
        }

        private static float ResolveOceanZoneDebugOffshoreDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            int seed = _oceanZoneDebugDistanceSeedOverrideActive
                ? _oceanZoneDebugDistanceSeedOverride
                : _terrainWorldSeed;
            return ResolveCanonicalOceanZoneOffshoreDistance(worldPosition, seed);
        }

        private static bool TryResolveGeneratedOceanZoneDebugOffshoreDistance(
            Vector2 worldPosition,
            float maxSearchDistanceWorldUnits,
            out float offshoreDistance)
        {
            offshoreDistance = 0f;
            TerrainMapSnapshot overrideSnapshot = _terrainMapSnapshotOverride;
            if (overrideSnapshot != null &&
                overrideSnapshot.TryResolveNearestLandDistance(
                    worldPosition.X,
                    worldPosition.Y,
                    maxSearchDistanceWorldUnits,
                    out offshoreDistance))
            {
                return true;
            }

            TerrainMapSnapshot fullMapSnapshot = _fullTerrainMapSnapshot;
            if (fullMapSnapshot != null &&
                fullMapSnapshot.TryResolveNearestLandDistance(
                    worldPosition.X,
                    worldPosition.Y,
                    maxSearchDistanceWorldUnits,
                    out offshoreDistance))
            {
                return true;
            }

            if (TryResolveResidentTerrainVectorOffshoreDistance(
                worldPosition,
                maxSearchDistanceWorldUnits,
                assumeFarWaterWhenNoNearbyEdge: false,
                out bool isWater,
                out offshoreDistance))
            {
                return isWater;
            }

            return false;
        }

        private static void AddOceanZoneDebugCellSegments(
            OceanZoneDebugSample topLeft,
            OceanZoneDebugSample topRight,
            OceanZoneDebugSample bottomRight,
            OceanZoneDebugSample bottomLeft,
            Vector2 topLeftPosition,
            Vector2 topRightPosition,
            Vector2 bottomRightPosition,
            Vector2 bottomLeftPosition,
            float[] thresholds,
            float maxSearchDistance,
            int depth,
            List<OceanZoneDebugSegment> segments)
        {
            if (segments == null ||
                segments.Count >= OceanZoneDebugMaxSegments ||
                thresholds == null ||
                thresholds.Length == 0)
            {
                return;
            }

            if (ShouldSubdivideOceanZoneDebugCell(
                topLeft,
                topRight,
                bottomRight,
                bottomLeft,
                topLeftPosition,
                topRightPosition,
                bottomRightPosition,
                bottomLeftPosition,
                thresholds,
                depth))
            {
                Vector2 topMidPosition = (topLeftPosition + topRightPosition) * 0.5f;
                Vector2 rightMidPosition = (topRightPosition + bottomRightPosition) * 0.5f;
                Vector2 bottomMidPosition = (bottomLeftPosition + bottomRightPosition) * 0.5f;
                Vector2 leftMidPosition = (topLeftPosition + bottomLeftPosition) * 0.5f;
                Vector2 centerPosition = (topLeftPosition + bottomRightPosition) * 0.5f;

                OceanZoneDebugSample topMid = ResolveOceanZoneDebugWaterSample(topMidPosition, maxSearchDistance);
                OceanZoneDebugSample rightMid = ResolveOceanZoneDebugWaterSample(rightMidPosition, maxSearchDistance);
                OceanZoneDebugSample bottomMid = ResolveOceanZoneDebugWaterSample(bottomMidPosition, maxSearchDistance);
                OceanZoneDebugSample leftMid = ResolveOceanZoneDebugWaterSample(leftMidPosition, maxSearchDistance);
                OceanZoneDebugSample center = ResolveOceanZoneDebugWaterSample(centerPosition, maxSearchDistance);
                int nextDepth = depth + 1;

                AddOceanZoneDebugCellSegments(
                    topLeft,
                    topMid,
                    center,
                    leftMid,
                    topLeftPosition,
                    topMidPosition,
                    centerPosition,
                    leftMidPosition,
                    thresholds,
                    maxSearchDistance,
                    nextDepth,
                    segments);
                AddOceanZoneDebugCellSegments(
                    topMid,
                    topRight,
                    rightMid,
                    center,
                    topMidPosition,
                    topRightPosition,
                    rightMidPosition,
                    centerPosition,
                    thresholds,
                    maxSearchDistance,
                    nextDepth,
                    segments);
                AddOceanZoneDebugCellSegments(
                    center,
                    rightMid,
                    bottomRight,
                    bottomMid,
                    centerPosition,
                    rightMidPosition,
                    bottomRightPosition,
                    bottomMidPosition,
                    thresholds,
                    maxSearchDistance,
                    nextDepth,
                    segments);
                AddOceanZoneDebugCellSegments(
                    leftMid,
                    center,
                    bottomMid,
                    bottomLeft,
                    leftMidPosition,
                    centerPosition,
                    bottomMidPosition,
                    bottomLeftPosition,
                    thresholds,
                    maxSearchDistance,
                    nextDepth,
                    segments);
                return;
            }

            Vector2 deeperNormal = ResolveOceanZoneDebugDeeperNormal(topLeft, topRight, bottomRight, bottomLeft);
            for (int thresholdIndex = 0; thresholdIndex < thresholds.Length && segments.Count < OceanZoneDebugMaxSegments; thresholdIndex++)
            {
                AddOceanZoneDebugContourSegments(
                    threshold: thresholds[thresholdIndex],
                    topLeft,
                    topRight,
                    bottomRight,
                    bottomLeft,
                    topLeftPosition,
                    topRightPosition,
                    bottomRightPosition,
                    bottomLeftPosition,
                    deeperNormal,
                    maxSearchDistance,
                    segments);
            }
        }

        private static bool ShouldSubdivideOceanZoneDebugCell(
            OceanZoneDebugSample topLeft,
            OceanZoneDebugSample topRight,
            OceanZoneDebugSample bottomRight,
            OceanZoneDebugSample bottomLeft,
            Vector2 topLeftPosition,
            Vector2 topRightPosition,
            Vector2 bottomRightPosition,
            Vector2 bottomLeftPosition,
            float[] thresholds,
            int depth)
        {
            if (ShouldUseFastOceanZoneDebugContours() ||
                depth >= OceanZoneDebugMaxCellSubdivisionDepth ||
                !topLeft.IsWater ||
                !topRight.IsWater ||
                !bottomRight.IsWater ||
                !bottomLeft.IsWater)
            {
                return false;
            }

            float width = MathF.Max(
                Vector2.Distance(topLeftPosition, topRightPosition),
                Vector2.Distance(bottomLeftPosition, bottomRightPosition));
            float height = MathF.Max(
                Vector2.Distance(topLeftPosition, bottomLeftPosition),
                Vector2.Distance(topRightPosition, bottomRightPosition));
            if (MathF.Max(width, height) <= OceanZoneDebugMinimumCellWorldUnits)
            {
                return false;
            }

            int minOrder = Math.Min(
                Math.Min(ResolveOceanZoneDebugWaterTypeOrder(topLeft.WaterType), ResolveOceanZoneDebugWaterTypeOrder(topRight.WaterType)),
                Math.Min(ResolveOceanZoneDebugWaterTypeOrder(bottomRight.WaterType), ResolveOceanZoneDebugWaterTypeOrder(bottomLeft.WaterType)));
            int maxOrder = Math.Max(
                Math.Max(ResolveOceanZoneDebugWaterTypeOrder(topLeft.WaterType), ResolveOceanZoneDebugWaterTypeOrder(topRight.WaterType)),
                Math.Max(ResolveOceanZoneDebugWaterTypeOrder(bottomRight.WaterType), ResolveOceanZoneDebugWaterTypeOrder(bottomLeft.WaterType)));
            return maxOrder - minOrder > 1;
        }

        private static bool ShouldUseFastOceanZoneDebugContours()
        {
            return true;
        }

        private static Vector2 ResolveOceanZoneDebugDeeperNormal(
            OceanZoneDebugSample topLeft,
            OceanZoneDebugSample topRight,
            OceanZoneDebugSample bottomRight,
            OceanZoneDebugSample bottomLeft)
        {
            float gradientX = ((topRight.OffshoreDistance + bottomRight.OffshoreDistance) -
                (topLeft.OffshoreDistance + bottomLeft.OffshoreDistance)) * 0.5f;
            float gradientY = ((bottomLeft.OffshoreDistance + bottomRight.OffshoreDistance) -
                (topLeft.OffshoreDistance + topRight.OffshoreDistance)) * 0.5f;
            Vector2 normal = new(gradientX, gradientY);
            if (normal.LengthSquared() <= 0.0001f)
            {
                return Vector2.UnitY;
            }

            normal.Normalize();
            return normal;
        }

        private static void AddOceanZoneDebugContourSegments(
            float threshold,
            OceanZoneDebugSample topLeft,
            OceanZoneDebugSample topRight,
            OceanZoneDebugSample bottomRight,
            OceanZoneDebugSample bottomLeft,
            Vector2 topLeftPosition,
            Vector2 topRightPosition,
            Vector2 bottomRightPosition,
            Vector2 bottomLeftPosition,
            Vector2 deeperNormal,
            float maxSearchDistance,
            List<OceanZoneDebugSegment> segments)
        {
            Span<Vector2> intersections = stackalloc Vector2[4];
            int count = 0;
            TryAddOceanZoneDebugIntersection(topLeft, topRight, topLeftPosition, topRightPosition, threshold, intersections, ref count);
            TryAddOceanZoneDebugIntersection(topRight, bottomRight, topRightPosition, bottomRightPosition, threshold, intersections, ref count);
            TryAddOceanZoneDebugIntersection(bottomRight, bottomLeft, bottomRightPosition, bottomLeftPosition, threshold, intersections, ref count);
            TryAddOceanZoneDebugIntersection(bottomLeft, topLeft, bottomLeftPosition, topLeftPosition, threshold, intersections, ref count);
            if (count < 2)
            {
                return;
            }

            TerrainWaterType shallowSide = ResolveWaterTypeFromOffshoreDistance(MathF.Max(0f, threshold - 0.01f));
            TerrainWaterType deepSide = ResolveWaterTypeFromOffshoreDistance(threshold + 0.01f);
            bool topLeftDeep = topLeft.OffshoreDistance >= threshold;
            bool topRightDeep = topRight.OffshoreDistance >= threshold;
            bool bottomRightDeep = bottomRight.OffshoreDistance >= threshold;
            bool bottomLeftDeep = bottomLeft.OffshoreDistance >= threshold;
            if (count >= 4 &&
                topLeftDeep == bottomRightDeep &&
                topRightDeep == bottomLeftDeep &&
                topLeftDeep != topRightDeep)
            {
                Vector2 centerPosition = (topLeftPosition + bottomRightPosition) * 0.5f;
                OceanZoneDebugSample center = ResolveOceanZoneDebugWaterSample(centerPosition, maxSearchDistance);
                float centerValue = center.IsWater
                    ? center.OffshoreDistance
                    : (topLeft.OffshoreDistance + topRight.OffshoreDistance + bottomRight.OffshoreDistance + bottomLeft.OffshoreDistance) * 0.25f;
                bool centerOnTopLeftSide = (centerValue >= threshold) == topLeftDeep;
                if (centerOnTopLeftSide)
                {
                    AddOceanZoneDebugContourSegment(intersections[0], intersections[1], threshold, deeperNormal, shallowSide, deepSide, segments);
                    AddOceanZoneDebugContourSegment(intersections[2], intersections[3], threshold, deeperNormal, shallowSide, deepSide, segments);
                }
                else
                {
                    AddOceanZoneDebugContourSegment(intersections[0], intersections[3], threshold, deeperNormal, shallowSide, deepSide, segments);
                    AddOceanZoneDebugContourSegment(intersections[1], intersections[2], threshold, deeperNormal, shallowSide, deepSide, segments);
                }

                return;
            }

            AddOceanZoneDebugContourSegment(intersections[0], intersections[1], threshold, deeperNormal, shallowSide, deepSide, segments);
            if (count >= 4)
            {
                AddOceanZoneDebugContourSegment(intersections[2], intersections[3], threshold, deeperNormal, shallowSide, deepSide, segments);
            }
        }

        private static void TryAddOceanZoneDebugIntersection(
            OceanZoneDebugSample first,
            OceanZoneDebugSample second,
            Vector2 firstPosition,
            Vector2 secondPosition,
            float threshold,
            Span<Vector2> intersections,
            ref int count)
        {
            if (count >= intersections.Length)
            {
                return;
            }

            float firstValue = first.OffshoreDistance;
            float secondValue = second.OffshoreDistance;
            if ((firstValue < threshold && secondValue < threshold) ||
                (firstValue > threshold && secondValue > threshold) ||
                MathF.Abs(firstValue - secondValue) <= 0.001f)
            {
                return;
            }

            float t = Math.Clamp((threshold - firstValue) / (secondValue - firstValue), 0f, 1f);
            intersections[count++] = RefineOceanZoneDebugIntersection(
                firstPosition,
                secondPosition,
                firstValue - threshold,
                secondValue - threshold,
                threshold,
                t);
        }

        private static Vector2 RefineOceanZoneDebugIntersection(
            Vector2 firstPosition,
            Vector2 secondPosition,
            float firstDelta,
            float secondDelta,
            float threshold,
            float initialT)
        {
            Vector2 lowPosition = firstPosition;
            Vector2 highPosition = secondPosition;
            float lowDelta = firstDelta;
            float highDelta = secondDelta;
            if (MathF.Abs(lowDelta) <= 0.001f)
            {
                return firstPosition;
            }

            if (MathF.Abs(highDelta) <= 0.001f)
            {
                return secondPosition;
            }

            if (ShouldUseFastOceanZoneDebugContours())
            {
                return Vector2.Lerp(firstPosition, secondPosition, initialT);
            }

            if (MathF.Sign(lowDelta) == MathF.Sign(highDelta))
            {
                return Vector2.Lerp(firstPosition, secondPosition, initialT);
            }

            float maxSearchDistance = Math.Max(
                    ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance)),
                    threshold) +
                OceanZoneDebugSampleStepWorldUnits;
            for (int i = 0; i < 3; i++)
            {
                Vector2 midpoint = (lowPosition + highPosition) * 0.5f;
                OceanZoneDebugSample midpointSample = ResolveOceanZoneDebugWaterSample(midpoint, maxSearchDistance);
                if (!midpointSample.IsWater)
                {
                    lowPosition = midpoint;
                    lowDelta = -threshold;
                    continue;
                }

                float midpointDelta = midpointSample.OffshoreDistance - threshold;
                if (MathF.Abs(midpointDelta) <= 0.001f)
                {
                    return midpoint;
                }

                if (MathF.Sign(midpointDelta) == MathF.Sign(lowDelta))
                {
                    lowPosition = midpoint;
                    lowDelta = midpointDelta;
                }
                else
                {
                    highPosition = midpoint;
                    highDelta = midpointDelta;
                }
            }

            return (lowPosition + highPosition) * 0.5f;
        }

        private static void AddOceanZoneDebugContourSegment(
            Vector2 from,
            Vector2 to,
            float threshold,
            Vector2 deeperNormal,
            TerrainWaterType shallowSide,
            TerrainWaterType deepSide,
            List<OceanZoneDebugSegment> segments)
        {
            if (segments.Count >= OceanZoneDebugMaxSegments ||
                Vector2.DistanceSquared(from, to) <= 1f)
            {
                return;
            }

            Vector2 normal = ResolveOceanZoneDebugSegmentNormal(from, to, threshold, deeperNormal, shallowSide, deepSide);
            segments.Add(new OceanZoneDebugSegment(from, to, normal, shallowSide, deepSide, threshold));
        }

        private static Vector2 ResolveOceanZoneDebugSegmentNormal(
            Vector2 from,
            Vector2 to,
            float threshold,
            Vector2 fallbackNormal,
            TerrainWaterType shallowSide,
            TerrainWaterType deepSide)
        {
            Vector2 normal = fallbackNormal;
            if (normal.LengthSquared() <= 0.0001f)
            {
                Vector2 delta = to - from;
                normal = new Vector2(-delta.Y, delta.X);
            }

            if (normal.LengthSquared() <= 0.0001f)
            {
                return Vector2.UnitY;
            }

            if (normal.LengthSquared() <= 0.0001f)
            {
                return Vector2.UnitY;
            }

            normal.Normalize();
            Vector2 midpoint = (from + to) * 0.5f;
            float probeOffset = Math.Clamp(OceanZoneDebugSampleStepWorldUnits * 0.28f, 18f, 48f);
            float maxSearchDistance = Math.Max(
                    ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance)),
                    threshold) +
                probeOffset;
            OceanZoneDebugSample negativeSample = ResolveOceanZoneDebugWaterSample(midpoint - (normal * probeOffset), maxSearchDistance);
            OceanZoneDebugSample positiveSample = ResolveOceanZoneDebugWaterSample(midpoint + (normal * probeOffset), maxSearchDistance);
            if (negativeSample.IsWater &&
                positiveSample.IsWater &&
                negativeSample.WaterType == deepSide &&
                positiveSample.WaterType == shallowSide)
            {
                normal = -normal;
            }
            else if (negativeSample.IsWater &&
                positiveSample.IsWater &&
                negativeSample.WaterType != shallowSide &&
                positiveSample.WaterType != deepSide &&
                negativeSample.OffshoreDistance > positiveSample.OffshoreDistance)
            {
                normal = -normal;
            }

            return normal;
        }

        private static bool TryResolveOceanZoneDebugVisibleWorldBounds(
            Matrix cameraTransform,
            Rectangle renderBounds,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (renderBounds.Width <= 0 || renderBounds.Height <= 0)
            {
                return false;
            }

            Matrix inverse = Matrix.Invert(cameraTransform);
            float right = renderBounds.X + renderBounds.Width;
            float bottom = renderBounds.Y + renderBounds.Height;
            Vector2 topLeft = Vector2.Transform(new Vector2(renderBounds.X, renderBounds.Y), inverse);
            Vector2 topRight = Vector2.Transform(new Vector2(right, renderBounds.Y), inverse);
            Vector2 bottomLeft = Vector2.Transform(new Vector2(renderBounds.X, bottom), inverse);
            Vector2 bottomRight = Vector2.Transform(new Vector2(right, bottom), inverse);

            minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
            maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
            minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
            maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
            return float.IsFinite(minX) && float.IsFinite(maxX) && float.IsFinite(minY) && float.IsFinite(maxY);
        }

        private static void DrawOceanZoneDebugOverlayCore(
            SpriteBatch spriteBatch,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds,
            Rectangle visibleBounds,
            float visibleMinX,
            float visibleMaxX,
            float visibleMinY,
            float visibleMaxY)
        {
            OceanZoneDebugVisionRegions.Clear();
            const bool visionClipActive = false;
            UpdateOceanZoneDebugSegments(visibleMinX, visibleMaxX, visibleMinY, visibleMaxY);
            if (OceanZoneDebugSegments.Count == 0)
            {
                _oceanZoneDebugBorderSegmentCount = 0;
                _oceanZoneDebugBorderLabelCount = 0;
                return;
            }

            int visibleSegmentCount = 0;
            for (int i = 0; i < OceanZoneDebugSegments.Count; i++)
            {
                OceanZoneDebugSegment segment = OceanZoneDebugSegments[i];
                if (DrawOceanZoneDebugLine(spriteBatch, segment, cameraTransform, renderBounds, targetBounds, visibleBounds, visionClipActive))
                {
                    visibleSegmentCount++;
                }
            }

            _oceanZoneDebugBorderSegmentCount = visibleSegmentCount;
            DrawOceanZoneDebugLabels(spriteBatch, cameraTransform, renderBounds, targetBounds, visibleBounds, visionClipActive);
        }

        private static bool TryPrepareOceanZoneDebugVisionClip(
            ref float visibleMinX,
            ref float visibleMaxX,
            ref float visibleMinY,
            ref float visibleMaxY)
        {
            OceanZoneDebugVisionRegions.Clear();
            if (!FogOfWarManager.IsFogEnabled)
            {
                return false;
            }

            if (!FogOfWarManager.TryGetVisionRegions(OceanZoneDebugVisionRegions) ||
                OceanZoneDebugVisionRegions.Count <= 0)
            {
                return false;
            }

            float visionMinX = 0f;
            float visionMaxX = 0f;
            float visionMinY = 0f;
            float visionMaxY = 0f;
            bool foundRegion = false;
            for (int i = 0; i < OceanZoneDebugVisionRegions.Count; i++)
            {
                FogOfWarManager.VisionRegion region = OceanZoneDebugVisionRegions[i];
                float radius = region.Radius + OceanZoneDebugVisionBuildPaddingWorldUnits;
                float regionMinX = region.Position.X - radius;
                float regionMaxX = region.Position.X + radius;
                float regionMinY = region.Position.Y - radius;
                float regionMaxY = region.Position.Y + radius;

                if (!foundRegion)
                {
                    visionMinX = regionMinX;
                    visionMaxX = regionMaxX;
                    visionMinY = regionMinY;
                    visionMaxY = regionMaxY;
                    foundRegion = true;
                }
                else
                {
                    visionMinX = MathF.Min(visionMinX, regionMinX);
                    visionMaxX = MathF.Max(visionMaxX, regionMaxX);
                    visionMinY = MathF.Min(visionMinY, regionMinY);
                    visionMaxY = MathF.Max(visionMaxY, regionMaxY);
                }
            }

            if (!foundRegion)
            {
                return false;
            }

            visibleMinX = MathF.Max(visibleMinX, visionMinX);
            visibleMaxX = MathF.Min(visibleMaxX, visionMaxX);
            visibleMinY = MathF.Max(visibleMinY, visionMinY);
            visibleMaxY = MathF.Min(visibleMaxY, visionMaxY);

            return visibleMaxX > visibleMinX && visibleMaxY > visibleMinY;
        }

        private static Vector2 ProjectOceanZoneDebugPointToTarget(
            Vector2 worldPosition,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds)
        {
            Vector2 renderPosition = Vector2.Transform(worldPosition, cameraTransform);
            float targetX = targetBounds.X + ((renderPosition.X - renderBounds.X) / renderBounds.Width) * targetBounds.Width;
            float targetY = targetBounds.Y + ((renderPosition.Y - renderBounds.Y) / renderBounds.Height) * targetBounds.Height;
            return new Vector2(targetX, targetY);
        }

        private static bool DrawOceanZoneDebugLine(
            SpriteBatch spriteBatch,
            OceanZoneDebugSegment segment,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds,
            Rectangle visibleBounds,
            bool visionClipActive)
        {
            if (visionClipActive)
            {
                return DrawOceanZoneDebugVisionClippedLine(
                    spriteBatch,
                    segment,
                    cameraTransform,
                    renderBounds,
                    targetBounds,
                    visibleBounds);
            }

            return DrawOceanZoneDebugLinePart(
                spriteBatch,
                segment.From,
                segment.To,
                segment,
                cameraTransform,
                renderBounds,
                targetBounds,
                visibleBounds);
        }

        private static bool DrawOceanZoneDebugVisionClippedLine(
            SpriteBatch spriteBatch,
            OceanZoneDebugSegment segment,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds,
            Rectangle visibleBounds)
        {
            float worldLength = Vector2.Distance(segment.From, segment.To);
            if (!float.IsFinite(worldLength) || worldLength <= 0.001f)
            {
                return false;
            }

            int sampleCount = Math.Clamp(
                (int)MathF.Ceiling(worldLength / OceanZoneDebugVisionClipSampleStepWorldUnits),
                1,
                64);
            Vector2 previousPoint = segment.From;
            bool previousVisible = IsOceanZoneDebugWorldPointInVision(segment.From);
            bool drewAny = false;

            for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
            {
                float t = sampleIndex / (float)sampleCount;
                Vector2 currentPoint = Vector2.Lerp(segment.From, segment.To, t);
                bool currentVisible = IsOceanZoneDebugWorldPointInVision(currentPoint);

                if (previousVisible && currentVisible)
                {
                    drewAny |= DrawOceanZoneDebugLinePart(
                        spriteBatch,
                        previousPoint,
                        currentPoint,
                        segment,
                        cameraTransform,
                        renderBounds,
                        targetBounds,
                        visibleBounds);
                }
                else if (previousVisible != currentVisible)
                {
                    Vector2 boundaryPoint = previousVisible
                        ? RefineOceanZoneDebugVisionBoundary(previousPoint, currentPoint)
                        : RefineOceanZoneDebugVisionBoundary(currentPoint, previousPoint);

                    drewAny |= previousVisible
                        ? DrawOceanZoneDebugLinePart(
                            spriteBatch,
                            previousPoint,
                            boundaryPoint,
                            segment,
                            cameraTransform,
                            renderBounds,
                            targetBounds,
                            visibleBounds)
                        : DrawOceanZoneDebugLinePart(
                            spriteBatch,
                            boundaryPoint,
                            currentPoint,
                            segment,
                            cameraTransform,
                            renderBounds,
                            targetBounds,
                            visibleBounds);
                }

                previousVisible = currentVisible;
                previousPoint = currentPoint;
            }

            return drewAny;
        }

        private static Vector2 RefineOceanZoneDebugVisionBoundary(Vector2 visiblePoint, Vector2 hiddenPoint)
        {
            Vector2 inside = visiblePoint;
            Vector2 outside = hiddenPoint;
            for (int i = 0; i < 6; i++)
            {
                Vector2 midpoint = (inside + outside) * 0.5f;
                if (IsOceanZoneDebugWorldPointInVision(midpoint))
                {
                    inside = midpoint;
                }
                else
                {
                    outside = midpoint;
                }
            }

            return inside;
        }

        private static bool IsOceanZoneDebugWorldPointInVision(Vector2 worldPosition)
        {
            if (!IsFiniteVector(worldPosition))
            {
                return false;
            }

            if (!FogOfWarManager.IsFogEnabled)
            {
                return false;
            }

            for (int i = 0; i < OceanZoneDebugVisionRegions.Count; i++)
            {
                if (OceanZoneDebugVisionRegions[i].Contains(worldPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountOceanZoneDebugVisionClipParts(OceanZoneDebugSegment segment)
        {
            if (!FogOfWarManager.IsFogEnabled)
            {
                return 0;
            }

            float worldLength = Vector2.Distance(segment.From, segment.To);
            if (!float.IsFinite(worldLength) || worldLength <= 0.001f)
            {
                return IsOceanZoneDebugWorldPointInVision(segment.From) ? 1 : 0;
            }

            int sampleCount = Math.Clamp(
                (int)MathF.Ceiling(worldLength / OceanZoneDebugVisionClipSampleStepWorldUnits),
                1,
                64);
            bool previousVisible = IsOceanZoneDebugWorldPointInVision(segment.From);
            int clipPartCount = 0;
            for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
            {
                float t = sampleIndex / (float)sampleCount;
                Vector2 currentPoint = Vector2.Lerp(segment.From, segment.To, t);
                bool currentVisible = IsOceanZoneDebugWorldPointInVision(currentPoint);
                if (previousVisible && currentVisible)
                {
                    clipPartCount++;
                }
                else if (previousVisible != currentVisible)
                {
                    clipPartCount++;
                }

                previousVisible = currentVisible;
            }

            return clipPartCount;
        }

        private static bool DrawOceanZoneDebugLinePart(
            SpriteBatch spriteBatch,
            Vector2 worldFrom,
            Vector2 worldTo,
            OceanZoneDebugSegment segment,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds,
            Rectangle visibleBounds)
        {
            Vector2 screenFrom = ProjectOceanZoneDebugPointToTarget(worldFrom, cameraTransform, renderBounds, targetBounds);
            Vector2 screenTo = ProjectOceanZoneDebugPointToTarget(worldTo, cameraTransform, renderBounds, targetBounds);
            if (!IsOceanZoneDebugScreenSegmentVisible(screenFrom, screenTo, visibleBounds, OceanZoneDebugLabelViewportMarginPixels))
            {
                return false;
            }

            Vector2 delta = screenTo - screenFrom;
            float length = delta.Length();
            if (length <= 0.001f)
            {
                return false;
            }

            float angle = MathF.Atan2(delta.Y, delta.X);
            Color lineColor = ResolveOceanZoneDebugBorderColor(segment.FirstSide, segment.SecondSide);
            spriteBatch.Draw(
                _oceanZoneDebugPixelTexture,
                screenFrom,
                null,
                lineColor,
                angle,
                new Vector2(0f, 0.5f),
                new Vector2(length, OceanZoneDebugLineThicknessScreenPixels),
                SpriteEffects.None,
                0f);
            return true;
        }

        private static void DrawOceanZoneDebugLabels(
            SpriteBatch spriteBatch,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds,
            Rectangle visibleBounds,
            bool visionClipActive)
        {
            UIStyle.UIFont font = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            if (!font.IsAvailable || font.Font == null)
            {
                _oceanZoneDebugBorderLabelCount = 0;
                return;
            }

            int labelsDrawn = 0;
            OceanZoneDebugLabelCellKeys.Clear();
            OceanZoneDebugPlacedLabelBounds.Clear();
            for (int i = 0; i < OceanZoneDebugSegments.Count && labelsDrawn < OceanZoneDebugMaxLabels; i++)
            {
                OceanZoneDebugSegment segment = OceanZoneDebugSegments[i];
                if (visionClipActive &&
                    !IsOceanZoneDebugWorldPointInVision((segment.From + segment.To) * 0.5f))
                {
                    continue;
                }

                Vector2 screenFrom = ProjectOceanZoneDebugPointToTarget(segment.From, cameraTransform, renderBounds, targetBounds);
                Vector2 screenTo = ProjectOceanZoneDebugPointToTarget(segment.To, cameraTransform, renderBounds, targetBounds);
                if (!IsOceanZoneDebugScreenSegmentVisible(screenFrom, screenTo, visibleBounds, OceanZoneDebugLabelViewportMarginPixels))
                {
                    continue;
                }

                int labelCellKey = ResolveOceanZoneDebugLabelCellKey(segment);
                if (!OceanZoneDebugLabelCellKeys.Add(labelCellKey))
                {
                    continue;
                }

                labelsDrawn += DrawOceanZoneDebugLabelPair(
                    spriteBatch,
                    font.Font,
                    OceanZoneDebugLabelScreenScale,
                    OceanZoneDebugLabelOffsetScreenPixels,
                    segment,
                    screenFrom,
                    screenTo,
                    cameraTransform,
                    renderBounds,
                    targetBounds);
            }

            OceanZoneDebugLabelCellKeys.Clear();
            OceanZoneDebugPlacedLabelBounds.Clear();
            _oceanZoneDebugBorderLabelCount = labelsDrawn;
        }

        private static int ResolveOceanZoneDebugLabelCellKey(OceanZoneDebugSegment segment)
        {
            Vector2 midpoint = (segment.From + segment.To) * 0.5f;
            float cellSize = MathF.Max(OceanZoneDebugTileWorldUnits * 0.55f, OceanZoneDebugSampleStepWorldUnits * 6f);
            int cellX = (int)MathF.Floor(midpoint.X / cellSize);
            int cellY = (int)MathF.Floor(midpoint.Y / cellSize);
            return HashCode.Combine(
                cellX,
                cellY,
                ResolveOceanZoneDebugWaterTypeOrder(segment.FirstSide),
                ResolveOceanZoneDebugWaterTypeOrder(segment.SecondSide));
        }

        private static bool IsOceanZoneDebugScreenSegmentVisible(
            Vector2 from,
            Vector2 to,
            Rectangle visibleBounds,
            float margin)
        {
            if (!IsFiniteVector(from) || !IsFiniteVector(to))
            {
                return false;
            }

            float left = visibleBounds.X - margin;
            float right = visibleBounds.X + visibleBounds.Width + margin;
            float top = visibleBounds.Y - margin;
            float bottom = visibleBounds.Y + visibleBounds.Height + margin;
            float segmentMinX = MathF.Min(from.X, to.X);
            float segmentMaxX = MathF.Max(from.X, to.X);
            float segmentMinY = MathF.Min(from.Y, to.Y);
            float segmentMaxY = MathF.Max(from.Y, to.Y);
            return segmentMaxX >= left &&
                segmentMinX <= right &&
                segmentMaxY >= top &&
                segmentMinY <= bottom;
        }

        private static int DrawOceanZoneDebugLabelPair(
            SpriteBatch spriteBatch,
            SpriteFont font,
            float scale,
            float labelOffsetScreenPixels,
            OceanZoneDebugSegment segment,
            Vector2 screenFrom,
            Vector2 screenTo,
            Matrix cameraTransform,
            Rectangle renderBounds,
            Rectangle targetBounds)
        {
            Vector2 delta = screenTo - screenFrom;
            float length = delta.Length();
            if (length <= 0.001f)
            {
                return 0;
            }

            float rotation = MathF.Atan2(delta.Y, delta.X);
            rotation = NormalizeOceanZoneDebugLabelRotation(rotation);
            Vector2 midpoint = (screenFrom + screenTo) * 0.5f;
            Vector2 worldMidpoint = (segment.From + segment.To) * 0.5f;
            Vector2 normal = ProjectOceanZoneDebugPointToTarget(worldMidpoint + segment.Normal, cameraTransform, renderBounds, targetBounds) -
                ProjectOceanZoneDebugPointToTarget(worldMidpoint, cameraTransform, renderBounds, targetBounds);
            if (normal.LengthSquared() <= 0.0001f)
            {
                normal = new Vector2(-delta.Y, delta.X);
            }

            normal.Normalize();
            int drawnCount = 0;
            string firstText = FormatOceanZoneDebugLabel(segment.FirstSide);
            string secondText = FormatOceanZoneDebugLabel(segment.SecondSide);
            Color firstColor = ResolveOceanZoneDebugZoneColor(segment.FirstSide);
            Color secondColor = ResolveOceanZoneDebugZoneColor(segment.SecondSide);
            if (TryDrawOceanZoneDebugLabel(
                spriteBatch,
                font,
                firstText,
                midpoint - (normal * labelOffsetScreenPixels),
                rotation,
                scale,
                firstColor))
            {
                drawnCount++;
            }

            if (TryDrawOceanZoneDebugLabel(
                spriteBatch,
                font,
                secondText,
                midpoint + (normal * labelOffsetScreenPixels),
                rotation,
                scale,
                secondColor))
            {
                drawnCount++;
            }

            return drawnCount;
        }

        private static bool TryDrawOceanZoneDebugLabel(
            SpriteBatch spriteBatch,
            SpriteFont font,
            string text,
            Vector2 position,
            float rotation,
            float scale,
            Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            float resolvedScale = MathF.Max(0.08f, scale);
            Vector2 measuredSize = font.MeasureString(text) * resolvedScale;
            OceanZoneDebugLabelBounds bounds = BuildOceanZoneDebugLabelBounds(
                position,
                measuredSize,
                rotation,
                OceanZoneDebugLabelCollisionPaddingPixels);
            if (DoesOceanZoneDebugLabelOverlap(bounds))
            {
                return false;
            }

            OceanZoneDebugPlacedLabelBounds.Add(bounds);
            Vector2 origin = font.MeasureString(text) * 0.5f;
            spriteBatch.DrawString(
                font,
                text,
                position + new Vector2(
                    OceanZoneDebugLabelShadowOffsetScreenPixels,
                    OceanZoneDebugLabelShadowOffsetScreenPixels),
                new Color(0, 0, 0, 235),
                rotation,
                origin,
                resolvedScale,
                SpriteEffects.None,
                0f);
            spriteBatch.DrawString(
                font,
                text,
                position,
                color,
                rotation,
                origin,
                resolvedScale,
                SpriteEffects.None,
                0f);
            return true;
        }

        private static OceanZoneDebugLabelBounds BuildOceanZoneDebugLabelBounds(
            Vector2 center,
            Vector2 measuredSize,
            float rotation,
            float padding)
        {
            float halfWidth = MathF.Max(1f, measuredSize.X * 0.5f);
            float halfHeight = MathF.Max(1f, measuredSize.Y * 0.5f);
            float cos = MathF.Abs(MathF.Cos(rotation));
            float sin = MathF.Abs(MathF.Sin(rotation));
            float extentX = (cos * halfWidth) + (sin * halfHeight) + padding;
            float extentY = (sin * halfWidth) + (cos * halfHeight) + padding;
            return new OceanZoneDebugLabelBounds(
                center.X - extentX,
                center.Y - extentY,
                center.X + extentX,
                center.Y + extentY);
        }

        private static bool DoesOceanZoneDebugLabelOverlap(OceanZoneDebugLabelBounds bounds)
        {
            for (int i = 0; i < OceanZoneDebugPlacedLabelBounds.Count; i++)
            {
                if (bounds.Intersects(OceanZoneDebugPlacedLabelBounds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static Color ResolveOceanZoneDebugBorderColor(TerrainWaterType firstSide, TerrainWaterType secondSide)
        {
            TerrainWaterType deeperSide = ResolveOceanZoneDebugWaterTypeOrder(firstSide) >= ResolveOceanZoneDebugWaterTypeOrder(secondSide)
                ? firstSide
                : secondSide;

            return deeperSide switch
            {
                TerrainWaterType.Sunlit => new Color(80, 234, 255, 215),
                TerrainWaterType.Twilight => new Color(64, 156, 255, 220),
                TerrainWaterType.Midnight => new Color(126, 126, 255, 225),
                TerrainWaterType.Abyss => new Color(255, 186, 72, 230),
                _ => new Color(156, 255, 214, 210)
            };
        }

        private static Color ResolveOceanZoneDebugZoneColor(TerrainWaterType waterType)
        {
            return waterType switch
            {
                TerrainWaterType.Shallow => new Color(162, 255, 216, 255),
                TerrainWaterType.Sunlit => new Color(86, 232, 255, 255),
                TerrainWaterType.Twilight => new Color(88, 166, 255, 255),
                TerrainWaterType.Midnight => new Color(152, 140, 255, 255),
                TerrainWaterType.Abyss => new Color(255, 196, 88, 255),
                _ => Color.White
            };
        }

        private static int ResolveOceanZoneDebugWaterTypeOrder(TerrainWaterType waterType)
        {
            return waterType switch
            {
                TerrainWaterType.Shallow => 0,
                TerrainWaterType.Sunlit => 1,
                TerrainWaterType.Twilight => 2,
                TerrainWaterType.Midnight => 3,
                TerrainWaterType.Abyss => 4,
                _ => 0
            };
        }

        private static string FormatOceanZoneDebugLabel(TerrainWaterType waterType)
        {
            return waterType.ToString();
        }

        internal static string FormatOceanZoneDebugLabelForProbe(TerrainWaterType waterType)
        {
            return FormatOceanZoneDebugLabel(waterType);
        }

        internal static float ResolveOceanZoneDebugLabelScreenScaleForProbe()
        {
            return OceanZoneDebugLabelScreenScale;
        }

        private static float NormalizeOceanZoneDebugLabelRotation(float rotation)
        {
            float normalized = rotation;
            while (normalized > MathF.PI)
            {
                normalized -= MathF.Tau;
            }

            while (normalized < -MathF.PI)
            {
                normalized += MathF.Tau;
            }

            if (normalized > MathF.PI / 2f)
            {
                normalized -= MathF.PI;
            }
            else if (normalized < -MathF.PI / 2f)
            {
                normalized += MathF.PI;
            }

            return normalized;
        }

        private static void LoadSettingsIfNeeded()
        {
            if (_settingsLoaded)
            {
                return;
            }

            _terrainWorldSeed = DatabaseFetch.GetSetting("GeneralSettings", "Value", "SettingKey", "TerrainWorldSeed", DefaultTerrainWorldSeed);
            _terrainWaterZoneDistanceScale = LoadClampedGeneralFloatSetting(
                TerrainWaterZoneDistanceScaleSettingKey,
                DefaultTerrainWaterZoneDistanceScale,
                0.1f,
                12f);
            _oceanZoneMinimumTransitionVolumeDistanceWorldUnits = LoadClampedGeneralFloatSetting(
                TerrainOceanZoneMinimumTransitionVolumeDistanceSettingKey,
                DefaultOceanZoneMinimumTransitionVolumeDistanceWorldUnits,
                0f,
                10000f);
            _terrainSeedAnchorCentifoot = ResolveSeedAnchorCentifoot(_terrainWorldSeed);
            ResetTerrainChunkWorkerQueues();
            ResetOceanZoneDebugTileWorkerState(clearCache: true);
            ResetFullTerrainMapState(clearResidentChunks: false);
            _settingsLoaded = true;
        }

        private static float LoadClampedGeneralFloatSetting(
            string settingKey,
            float defaultValue,
            float minValue,
            float maxValue)
        {
            float configured = DatabaseFetch.GetSetting("GeneralSettings", "Value", "SettingKey", settingKey, defaultValue);
            if (!float.IsFinite(configured))
            {
                configured = defaultValue;
            }

            return Math.Clamp(configured, minValue, maxValue);
        }

        private static void EnsureTerrainWorldBoundsInitialized()
        {
            EnsureTerrainWorldBoundsInitialized(Core.Instance?.PlayerOrNull?.Position ?? Vector2.Zero);
        }

        private static void EnsureTerrainWorldBoundsInitialized(Vector2 center)
        {
            if (_terrainWorldBoundsInitialized)
            {
                return;
            }

            if (!IsFiniteVector(center))
            {
                center = Vector2.Zero;
            }

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
            Vector2 playerPosition = Core.Instance?.PlayerOrNull?.Position ?? Vector2.Zero;
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

        private static void IncludeTerrainWorldBounds(
            ref float targetMinX,
            ref float targetMaxX,
            ref float targetMinY,
            ref float targetMaxY,
            TerrainWorldBounds bounds)
        {
            IncludeWorldBounds(
                ref targetMinX,
                ref targetMaxX,
                ref targetMinY,
                ref targetMaxY,
                bounds.MinX,
                bounds.MaxX,
                bounds.MinY,
                bounds.MaxY);
        }

        private static void UpdateTerrainDynamicCollisionProbeTelemetry(
            int objectProbeCount,
            int bulletProbeCount)
        {
            _terrainDynamicCollisionObjectProbeCount = Math.Max(0, objectProbeCount);
            _terrainDynamicCollisionBulletProbeCount = Math.Max(0, bulletProbeCount);
            _terrainDynamicCollisionProbeCount =
                _terrainDynamicCollisionObjectProbeCount +
                _terrainDynamicCollisionBulletProbeCount;
        }

        internal static void RequestPlayerTerrainAccess(Agent player)
        {
            if (player == null || !IsActive)
            {
                return;
            }

            float accessRadius = ResolvePlayerTerrainAccessRadius(player);
            if (!IsTerrainAccessReady(player.Position, accessRadius))
            {
                RequestTerrainAccessAroundWorldPosition(player.Position, accessRadius);
            }
        }

        private static float ResolvePlayerTerrainAccessRadius(Agent player)
        {
            float bodyRadius = player?.BoundingRadius ?? 0f;
            if (!float.IsFinite(bodyRadius))
            {
                bodyRadius = 0f;
            }

            return MathF.Max(
                TerrainCollisionDynamicProbeMarginWorldUnits,
                bodyRadius + TerrainAccessImmediatePaddingWorldUnits);
        }

        private static bool IsTerrainAccessReady(Vector2 worldPosition, float radiusWorldUnits)
        {
            if (!_startupVisibleTerrainReady ||
                !_hasAppliedTerrainVisualChunkWindow ||
                !IsFiniteVector(worldPosition))
            {
                return false;
            }

            ChunkBounds accessWindow = BuildTerrainAccessChunkWindow(worldPosition, radiusWorldUnits);
            return _lastAppliedVisualChunkWindow.Contains(accessWindow) &&
                _lastAppliedColliderChunkWindow.Contains(accessWindow) &&
                !HasPendingChunkInBounds(accessWindow);
        }

        private static void RequestTerrainAccessAroundWorldPosition(Vector2 worldPosition, float radiusWorldUnits)
        {
            if (!IsFiniteVector(worldPosition))
            {
                return;
            }

            _terrainAccessRequestActive = true;
            _terrainAccessRequestWorldPosition = worldPosition;
            _terrainAccessRequestRadiusWorldUnits = MathF.Max(TerrainCollisionDynamicProbeMarginWorldUnits, radiusWorldUnits);
        }

        private static ChunkBounds BuildTerrainAccessChunkWindow(Vector2 worldPosition, float radiusWorldUnits)
        {
            float radius = MathF.Max(0f, radiusWorldUnits);
            return BuildChunkBounds(
                worldPosition.X - radius,
                worldPosition.X + radius,
                worldPosition.Y - radius,
                worldPosition.Y + radius);
        }

        private static void IncludeTerrainAccessRequestWorldBounds(
            ref float streamMinX,
            ref float streamMaxX,
            ref float streamMinY,
            ref float streamMaxY,
            ref float maxVisionRadiusWorldUnits)
        {
            if (!_terrainAccessRequestActive)
            {
                return;
            }

            if (IsTerrainAccessReady(_terrainAccessRequestWorldPosition, _terrainAccessRequestRadiusWorldUnits))
            {
                _terrainAccessRequestActive = false;
                return;
            }

            float radius = MathF.Max(ChunkWorldSize, _terrainAccessRequestRadiusWorldUnits);
            IncludeWorldBounds(
                ref streamMinX,
                ref streamMaxX,
                ref streamMinY,
                ref streamMaxY,
                _terrainAccessRequestWorldPosition.X - radius,
                _terrainAccessRequestWorldPosition.X + radius,
                _terrainAccessRequestWorldPosition.Y - radius,
                _terrainAccessRequestWorldPosition.Y + radius);
            maxVisionRadiusWorldUnits = MathF.Max(maxVisionRadiusWorldUnits, radius);
        }

        private static void ExpandTerrainCollisionWorldBoundsForDynamicProbes(
            ref float collisionMinX,
            ref float collisionMaxX,
            ref float collisionMinY,
            ref float collisionMaxY)
        {
            int objectProbeCount = 0;
            int bulletProbeCount = 0;

            List<GameObject> gameObjects = Core.Instance?.GameObjects;
            if (gameObjects != null)
            {
                for (int i = 0; i < gameObjects.Count; i++)
                {
                    if (!TryBuildTerrainActivationBoundsForDynamicGameObject(
                        gameObjects[i],
                        out TerrainWorldBounds activationBounds))
                    {
                        continue;
                    }

                    IncludeTerrainWorldBounds(
                        ref collisionMinX,
                        ref collisionMaxX,
                        ref collisionMinY,
                        ref collisionMaxY,
                        activationBounds);
                    objectProbeCount++;
                }
            }

            IReadOnlyList<Bullet> bullets = BulletManager.GetBullets();
            if (bullets != null)
            {
                for (int i = 0; i < bullets.Count; i++)
                {
                    if (!TryBuildTerrainActivationBoundsForBullet(
                        bullets[i],
                        out TerrainWorldBounds activationBounds))
                    {
                        continue;
                    }

                    IncludeTerrainWorldBounds(
                        ref collisionMinX,
                        ref collisionMaxX,
                        ref collisionMinY,
                        ref collisionMaxY,
                        activationBounds);
                    bulletProbeCount++;
                }
            }

            UpdateTerrainDynamicCollisionProbeTelemetry(objectProbeCount, bulletProbeCount);
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
            IncludeTerrainAccessRequestWorldBounds(
                ref streamMinX,
                ref streamMaxX,
                ref streamMinY,
                ref streamMaxY,
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
            ChunkBounds terrainObjectChunkWindow = ExpandChunkBounds(
                visibleChunkWindow,
                TerrainVisionRevealAheadChunkMargin);
            preloadChunkWindow = UnionChunkBounds(preloadChunkWindow, terrainObjectChunkWindow);
            float collisionMinX = streamMinX - TerrainCollisionActivationMarginWorldUnits;
            float collisionMaxX = streamMaxX + TerrainCollisionActivationMarginWorldUnits;
            float collisionMinY = streamMinY - TerrainCollisionActivationMarginWorldUnits;
            float collisionMaxY = streamMaxY + TerrainCollisionActivationMarginWorldUnits;
            ExpandTerrainCollisionWorldBoundsForDynamicProbes(
                ref collisionMinX,
                ref collisionMaxX,
                ref collisionMinY,
                ref collisionMaxY);
            ChunkBounds terrainColliderChunkWindow = BuildChunkBounds(
                collisionMinX,
                collisionMaxX,
                collisionMinY,
                collisionMaxY);
            preloadChunkWindow = UnionChunkBounds(preloadChunkWindow, terrainColliderChunkWindow);
            ChunkBounds retainChunkWindow = BuildChunkBounds(
                streamMinX - retainMarginWorldUnits,
                streamMaxX + retainMarginWorldUnits,
                streamMinY - retainMarginWorldUnits,
                streamMaxY + retainMarginWorldUnits);
            retainChunkWindow = UnionChunkBounds(retainChunkWindow, terrainColliderChunkWindow);

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

        private static ChunkBounds ResolveStableTerrainObjectChunkWindow(
            ChunkBounds desiredChunkWindow,
            ChunkBounds visibleChunkWindow)
        {
            if (_terrainMaterializationTask != null &&
                _lastTerrainVisualChunkWindow.Contains(visibleChunkWindow))
            {
                return _lastTerrainVisualChunkWindow;
            }

            if (_hasAppliedTerrainVisualChunkWindow &&
                ContainsChunkBoundsWithMargin(
                    _lastAppliedVisualChunkWindow,
                    visibleChunkWindow,
                    TerrainVisionRevealRefreshGuardChunkMargin))
            {
                return _lastAppliedVisualChunkWindow;
            }

            if (ContainsChunkBoundsWithMargin(
                _lastTerrainVisualChunkWindow,
                visibleChunkWindow,
                TerrainVisionRevealRefreshGuardChunkMargin))
            {
                return _lastTerrainVisualChunkWindow;
            }

            return desiredChunkWindow;
        }

        private static bool ContainsChunkBoundsWithMargin(
            ChunkBounds outer,
            ChunkBounds inner,
            int marginChunks)
        {
            int margin = Math.Max(0, marginChunks);
            return inner.MinChunkX >= outer.MinChunkX + margin &&
                inner.MaxChunkX <= outer.MaxChunkX - margin &&
                inner.MinChunkY >= outer.MinChunkY + margin &&
                inner.MaxChunkY <= outer.MaxChunkY - margin;
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

        private static bool HasResidentLandChunkInBounds(ChunkBounds bounds)
        {
            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (bounds.Contains(entry.Key) && entry.Value.HasLand)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPendingChunkInBounds(ChunkBounds bounds)
        {
            lock (TerrainChunkWorkerLock)
            {
                for (int i = 0; i < TerrainChunkBuildQueue.Count; i++)
                {
                    ChunkKey key = TerrainChunkBuildQueue[i].Key;
                    if (!ResidentChunks.ContainsKey(key) && bounds.Contains(key))
                    {
                        return true;
                    }
                }

                foreach (ChunkKey key in BuildingChunkKeys)
                {
                    if (!ResidentChunks.ContainsKey(key) && bounds.Contains(key))
                    {
                        return true;
                    }
                }

                foreach (GeneratedChunkData chunkData in CompletedChunkBuildQueue)
                {
                    if (chunkData != null && !ResidentChunks.ContainsKey(chunkData.Key) && bounds.Contains(chunkData.Key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountPendingChunksInBounds(ChunkBounds bounds)
        {
            int count = 0;
            lock (TerrainChunkWorkerLock)
            {
                for (int i = 0; i < TerrainChunkBuildQueue.Count; i++)
                {
                    ChunkKey key = TerrainChunkBuildQueue[i].Key;
                    if (!ResidentChunks.ContainsKey(key) && bounds.Contains(key))
                    {
                        count++;
                    }
                }

                foreach (ChunkKey key in BuildingChunkKeys)
                {
                    if (!ResidentChunks.ContainsKey(key) && bounds.Contains(key))
                    {
                        count++;
                    }
                }

                foreach (GeneratedChunkData chunkData in CompletedChunkBuildQueue)
                {
                    if (chunkData != null && !ResidentChunks.ContainsKey(chunkData.Key) && bounds.Contains(chunkData.Key))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static ChunkBounds ResolveFullTerrainMapChunkWindow()
        {
            EnsureTerrainWorldBoundsInitialized();
            if (_fullTerrainMapChunkWindowInitialized)
            {
                return _fullTerrainMapChunkWindow;
            }

            _fullTerrainMapChunkWindow = BuildChunkBounds(
                _terrainWorldBounds.MinX,
                _terrainWorldBounds.MaxX,
                _terrainWorldBounds.MinY,
                _terrainWorldBounds.MaxY);
            _fullTerrainMapChunkCount = CountChunksInBounds(_fullTerrainMapChunkWindow);
            _fullTerrainMapChunkWindowInitialized = true;
            return _fullTerrainMapChunkWindow;
        }

        private static int CountChunksInBounds(ChunkBounds bounds)
        {
            int width = Math.Max(0, bounds.MaxChunkX - bounds.MinChunkX + 1);
            int height = Math.Max(0, bounds.MaxChunkY - bounds.MinChunkY + 1);
            return width * height;
        }

        private static bool IsFullTerrainMapChunk(ChunkKey key)
        {
            return ResolveFullTerrainMapChunkWindow().Contains(key);
        }

        private static void QueueFullTerrainMapBuilds(ChunkBounds priorityChunkWindow)
        {
            ChunkBounds fullMapWindow = ResolveFullTerrainMapChunkWindow();
            if (_fullTerrainMapGenerationComplete)
            {
                return;
            }

            QueueChunkBuildsWithLimit(
                fullMapWindow,
                priorityChunkWindow,
                MaxFullTerrainMapChunkBuildEnqueuesPerFrame);
        }

        private static void UpdateFullTerrainMapGenerationState()
        {
            ChunkBounds fullMapWindow = ResolveFullTerrainMapChunkWindow();
            _fullTerrainMapGeneratedChunkCount = CountResidentChunksInBounds(fullMapWindow);
            _fullTerrainMapPendingChunkCount = CountPendingChunksInBounds(fullMapWindow);
            bool complete =
                _fullTerrainMapChunkCount > 0 &&
                _fullTerrainMapGeneratedChunkCount >= _fullTerrainMapChunkCount &&
                _fullTerrainMapPendingChunkCount == 0;

            if (!complete)
            {
                _fullTerrainMapGenerationComplete = false;
                if (_fullTerrainMapSnapshot == null &&
                    _fullOceanZoneDebugBuildTask == null &&
                    !_fullOceanZoneDebugReady)
                {
                    _fullOceanZoneDebugStatus = "waiting full-map ocean border queue";
                }
                return;
            }

            _fullTerrainMapGenerationComplete = true;
            if (_fullTerrainMapSnapshot == null)
            {
                _fullTerrainMapSnapshot = BuildFullTerrainMapSnapshot(fullMapWindow);
                if (_fullTerrainMapSnapshot != null)
                {
                    DebugLogger.PrintDebug(
                        $"GameBlockTerrainBackground: full terrain map generated {_fullTerrainMapGeneratedChunkCount}/{_fullTerrainMapChunkCount} chunks in {FormatChunkBounds(fullMapWindow)}.");
                }
            }

            EnsureFullOceanZoneDebugBuildQueued();
        }

        private static int CountResidentChunksInBounds(ChunkBounds bounds)
        {
            int count = 0;
            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (bounds.Contains(entry.Key))
                {
                    count++;
                }
            }

            return count;
        }

        private static List<ChunkBuildCandidate> BuildStartupWarmupChunkCandidates(ChunkKey focusChunk, ChunkBounds warmupWindow)
        {
            List<ChunkBuildCandidate> candidates = new();
            for (int chunkY = warmupWindow.MinChunkY; chunkY <= warmupWindow.MaxChunkY; chunkY++)
            {
                for (int chunkX = warmupWindow.MinChunkX; chunkX <= warmupWindow.MaxChunkX; chunkX++)
                {
                    int dx = chunkX - focusChunk.X;
                    int dy = chunkY - focusChunk.Y;
                    int distanceSq = (dx * dx) + (dy * dy);
                    candidates.Add(new ChunkBuildCandidate(new ChunkKey(chunkX, chunkY), isVisible: true, distanceSq));
                }
            }

            candidates.Sort(static (left, right) =>
            {
                int distanceCompare = left.DistanceSq.CompareTo(right.DistanceSq);
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }

                int yCompare = left.Key.Y.CompareTo(right.Key.Y);
                return yCompare != 0 ? yCompare : left.Key.X.CompareTo(right.Key.X);
            });

            return candidates;
        }

        private static ChunkBounds BuildChunkBoundsForKeys(IReadOnlyList<ChunkKey> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return new ChunkBounds(0, 0, 0, 0);
            }

            int minChunkX = int.MaxValue;
            int maxChunkX = int.MinValue;
            int minChunkY = int.MaxValue;
            int maxChunkY = int.MinValue;
            for (int i = 0; i < keys.Count; i++)
            {
                ChunkKey key = keys[i];
                minChunkX = Math.Min(minChunkX, key.X);
                maxChunkX = Math.Max(maxChunkX, key.X);
                minChunkY = Math.Min(minChunkY, key.Y);
                maxChunkY = Math.Max(maxChunkY, key.Y);
            }

            return ClampChunkBoundsToTerrainWorld(new ChunkBounds(minChunkX, maxChunkX, minChunkY, maxChunkY));
        }

        private static void QueueChunkBuilds(ChunkBounds preloadChunkWindow, ChunkBounds visibleChunkWindow)
        {
            QueueChunkBuildsWithLimit(preloadChunkWindow, visibleChunkWindow, MaxNewChunkBuildsPerFrame);
        }

        private static void QueueChunkBuildsWithLimit(
            ChunkBounds preloadChunkWindow,
            ChunkBounds visibleChunkWindow,
            int maxEnqueuesPerFrame)
        {
            EnsureTerrainChunkWorkerRunning();

            List<ChunkBuildCandidate> missingChunks = new();

            for (int chunkY = preloadChunkWindow.MinChunkY; chunkY <= preloadChunkWindow.MaxChunkY; chunkY++)
            {
                for (int chunkX = preloadChunkWindow.MinChunkX; chunkX <= preloadChunkWindow.MaxChunkX; chunkX++)
                {
                    ChunkKey key = new(chunkX, chunkY);
                    if (ResidentChunks.ContainsKey(key) || IsChunkQueuedOrBuilding(key))
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

            int maxEnqueues = Math.Max(1, maxEnqueuesPerFrame);
            int buildsQueued = 0;
            lock (TerrainChunkWorkerLock)
            {
                for (int i = 0; i < missingChunks.Count && buildsQueued < maxEnqueues; i++)
                {
                    if (TerrainChunkBuildQueue.Count >= TerrainChunkBuildQueueLimit)
                    {
                        break;
                    }

                    ChunkBuildCandidate candidate = missingChunks[i];
                    if (ResidentChunks.ContainsKey(candidate.Key) ||
                        QueuedChunkKeys.Contains(candidate.Key) ||
                        BuildingChunkKeys.Contains(candidate.Key))
                    {
                        continue;
                    }

                    TerrainChunkBuildQueue.Add(candidate);
                    QueuedChunkKeys.Add(candidate.Key);
                    _terrainBackgroundQueuedChunkBuildCount++;
                    buildsQueued++;
                }

                if (buildsQueued > 0)
                {
                    SortTerrainChunkBuildQueueLocked();
                    UpdateTerrainChunkWorkerTelemetryLocked();
                    Monitor.Pulse(TerrainChunkWorkerLock);
                }
            }
        }

        private static bool IsChunkQueuedOrBuilding(ChunkKey key)
        {
            lock (TerrainChunkWorkerLock)
            {
                if (QueuedChunkKeys.Contains(key) || BuildingChunkKeys.Contains(key))
                {
                    return true;
                }

                foreach (GeneratedChunkData completedChunk in CompletedChunkBuildQueue)
                {
                    if (completedChunk != null && completedChunk.Key.Equals(key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void EnsureTerrainChunkWorkerRunning()
        {
            if (_terrainChunkWorkerThread != null && _terrainChunkWorkerThread.IsAlive)
            {
                return;
            }

            lock (TerrainChunkWorkerLock)
            {
                if (_terrainChunkWorkerThread != null && _terrainChunkWorkerThread.IsAlive)
                {
                    return;
                }

                _terrainChunkWorkerStopRequested = false;
                _terrainChunkWorkerThread = new Thread(TerrainChunkWorkerLoop)
                {
                    IsBackground = true,
                    Name = "TerrainChunkWorker",
                    Priority = ThreadPriority.BelowNormal
                };
                _terrainChunkWorkerThread.Start();
            }
        }

        private static void TerrainChunkWorkerLoop()
        {
            while (true)
            {
                ChunkBuildCandidate candidate;
                int generation;
                lock (TerrainChunkWorkerLock)
                {
                    while (!_terrainChunkWorkerStopRequested && TerrainChunkBuildQueue.Count == 0)
                    {
                        UpdateTerrainChunkWorkerTelemetryLocked();
                        Monitor.Wait(TerrainChunkWorkerLock);
                    }

                    if (_terrainChunkWorkerStopRequested)
                    {
                        return;
                    }

                    candidate = TerrainChunkBuildQueue[0];
                    TerrainChunkBuildQueue.RemoveAt(0);
                    QueuedChunkKeys.Remove(candidate.Key);
                    BuildingChunkKeys.Add(candidate.Key);
                    generation = _terrainChunkWorkerGeneration;
                    UpdateTerrainChunkWorkerTelemetryLocked();
                }

                GeneratedChunkData chunkData = null;
                try
                {
                    chunkData = BuildChunkData(candidate.Key);
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"GameBlockTerrainBackground: background chunk build {candidate.Key.X},{candidate.Key.Y} failed. {ex.GetBaseException().Message}");
                }

                lock (TerrainChunkWorkerLock)
                {
                    BuildingChunkKeys.Remove(candidate.Key);
                    if (chunkData != null &&
                        generation == _terrainChunkWorkerGeneration)
                    {
                        CompletedChunkBuildQueue.Enqueue(chunkData);
                    }

                    UpdateTerrainChunkWorkerTelemetryLocked();
                    Monitor.Pulse(TerrainChunkWorkerLock);
                }
            }
        }

        private static void SortTerrainChunkBuildQueueLocked()
        {
            TerrainChunkBuildQueue.Sort(static (left, right) =>
            {
                int visibleCompare = right.IsVisible.CompareTo(left.IsVisible);
                if (visibleCompare != 0)
                {
                    return visibleCompare;
                }

                return left.DistanceSq.CompareTo(right.DistanceSq);
            });
        }

        private static void UpdateTerrainChunkWorkerTelemetryLocked()
        {
            _terrainChunkWorkerQueuedBuildCount = TerrainChunkBuildQueue.Count;
            _terrainChunkWorkerActiveBuildCount = BuildingChunkKeys.Count;
            _terrainChunkWorkerCompletedQueueCount = CompletedChunkBuildQueue.Count;
        }

        private static string ResolveTerrainBackgroundWorkerStatus()
        {
            int queued = _terrainChunkWorkerQueuedBuildCount;
            int active = _terrainChunkWorkerActiveBuildCount;
            int completed = _terrainChunkWorkerCompletedQueueCount;
            if (_terrainChunkWorkerThread == null || !_terrainChunkWorkerThread.IsAlive)
            {
                return "stopped";
            }

            return active > 0
                ? $"building queued={queued} completed={completed}"
                : queued > 0
                    ? $"queued={queued} completed={completed}"
                    : completed > 0
                        ? $"completed={completed}"
                        : "idle";
        }

        private static string ResolveTerrainAccessRequestStatus()
        {
            if (!_terrainAccessRequestActive)
            {
                return "none";
            }

            return $"{CentifootUnits.FormatVector2(_terrainAccessRequestWorldPosition)} radius={CentifootUnits.FormatDistance(_terrainAccessRequestRadiusWorldUnits)}";
        }

        private static GeneratedChunkData TryDequeueCompletedChunk()
        {
            lock (TerrainChunkWorkerLock)
            {
                if (CompletedChunkBuildQueue.Count == 0)
                {
                    UpdateTerrainChunkWorkerTelemetryLocked();
                    return null;
                }

                GeneratedChunkData chunkData = CompletedChunkBuildQueue.Dequeue();
                UpdateTerrainChunkWorkerTelemetryLocked();
                return chunkData;
            }
        }

        private static GeneratedChunkData TryTakeCompletedChunk(ChunkKey key)
        {
            lock (TerrainChunkWorkerLock)
            {
                if (CompletedChunkBuildQueue.Count == 0)
                {
                    UpdateTerrainChunkWorkerTelemetryLocked();
                    return null;
                }

                GeneratedChunkData match = null;
                int count = CompletedChunkBuildQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    GeneratedChunkData chunkData = CompletedChunkBuildQueue.Dequeue();
                    if (match == null && chunkData != null && chunkData.Key.Equals(key))
                    {
                        match = chunkData;
                        continue;
                    }

                    CompletedChunkBuildQueue.Enqueue(chunkData);
                }

                UpdateTerrainChunkWorkerTelemetryLocked();
                return match;
            }
        }

        private static void RemoveQueuedChunkBuild(ChunkKey key)
        {
            lock (TerrainChunkWorkerLock)
            {
                if (!QueuedChunkKeys.Remove(key))
                {
                    return;
                }

                for (int i = TerrainChunkBuildQueue.Count - 1; i >= 0; i--)
                {
                    if (TerrainChunkBuildQueue[i].Key.Equals(key))
                    {
                        TerrainChunkBuildQueue.RemoveAt(i);
                    }
                }

                UpdateTerrainChunkWorkerTelemetryLocked();
            }
        }

        private static void ResetTerrainChunkWorkerQueues()
        {
            lock (TerrainChunkWorkerLock)
            {
                _terrainChunkWorkerGeneration++;
                TerrainChunkBuildQueue.Clear();
                QueuedChunkKeys.Clear();
                BuildingChunkKeys.Clear();
                CompletedChunkBuildQueue.Clear();
                UpdateTerrainChunkWorkerTelemetryLocked();
                Monitor.Pulse(TerrainChunkWorkerLock);
            }
        }

        private static void ResetFullTerrainMapState(bool clearResidentChunks)
        {
            _fullTerrainMapChunkWindow = default;
            _fullTerrainMapChunkWindowInitialized = false;
            _fullTerrainMapChunkCount = 0;
            _fullTerrainMapGeneratedChunkCount = 0;
            _fullTerrainMapPendingChunkCount = 0;
            _fullTerrainMapGenerationComplete = false;
            _fullTerrainMapSnapshot = null;
            _terrainMapSnapshotOverride = null;
            ResetOceanZoneDebugFullMapState(clearSegments: true);

            if (!clearResidentChunks)
            {
                return;
            }

            List<ChunkKey> residentKeys = new(ResidentChunks.Keys);
            for (int i = 0; i < residentKeys.Count; i++)
            {
                DisposeResidentChunk(residentKeys[i]);
            }
        }

        private static void ResetOceanZoneDebugFullMapState(bool clearSegments)
        {
            _fullOceanZoneDebugBuildTask = null;
            _fullOceanZoneDebugReady = false;
            _fullOceanZoneDebugBuildSeed = int.MinValue;
            _fullOceanZoneDebugSegmentCount = 0;
            _fullOceanZoneDebugBuildMilliseconds = 0.0;
            _fullOceanZoneDebugStatus = "waiting full-map ocean border queue";
            _oceanZoneDebugSuppressedTinyZoneCount = 0;
            _oceanZoneDebugTinyZoneViolationSummary = "none";
            _oceanZoneDebugTinyZoneSuppressionScratchCount = 0;
            _oceanZoneDebugTinyZoneSuppressedCellsScratch = null;
            if (clearSegments)
            {
                FullOceanZoneDebugSegments.Clear();
                OceanZoneDebugSegments.Clear();
                _oceanZoneDebugBorderSegmentCount = 0;
                _oceanZoneDebugBorderLabelCount = 0;
            }
        }

        private static bool BuildResidentChunksSynchronously(ChunkBounds chunkWindow, bool waitForPending = true)
        {
            for (int chunkY = chunkWindow.MinChunkY; chunkY <= chunkWindow.MaxChunkY; chunkY++)
            {
                for (int chunkX = chunkWindow.MinChunkX; chunkX <= chunkWindow.MaxChunkX; chunkX++)
                {
                    if (!TryBuildResidentChunkSynchronously(new ChunkKey(chunkX, chunkY), waitForPending))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryBuildResidentChunkSynchronously(ChunkKey key, bool waitForPending = true)
        {
            if (ResidentChunks.ContainsKey(key))
            {
                return true;
            }

            GeneratedChunkData completedChunk = TryTakeCompletedChunk(key);
            if (completedChunk != null)
            {
                PromoteChunk(completedChunk);
                return true;
            }

            RemoveQueuedChunkBuild(key);

            try
            {
                PromoteChunk(BuildChunkData(key));
                _startupSynchronousChunkBuildCount++;
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"GameBlockTerrainBackground: synchronous chunk build {key.X},{key.Y} failed. {ex.GetBaseException().Message}");
                return false;
            }
        }

        private static void TryPromoteCompletedChunks(ChunkBounds retainChunkWindow)
        {
            int promotedCount = 0;

            while (promotedCount < MaxCompletedChunkPromotionsPerFrame)
            {
                GeneratedChunkData chunkData = TryDequeueCompletedChunk();
                if (chunkData == null)
                {
                    break;
                }

                if (!retainChunkWindow.Contains(chunkData.Key) &&
                    !IsFullTerrainMapChunk(chunkData.Key))
                {
                    continue;
                }

                PromoteChunk(chunkData);
                promotedCount++;
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
            if (_lastVisibleChunkWindow.Contains(chunkData.Key))
            {
                _terrainVisibleObjectsDirty = true;
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
            if (_lastVisibleChunkWindow.Contains(key))
            {
                _terrainVisibleObjectsDirty = true;
            }
        }

        private static void PruneResidentChunks(ChunkBounds retainChunkWindow)
        {
            List<ChunkKey> staleKeys = new();

            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (!retainChunkWindow.Contains(entry.Key) &&
                    !IsFullTerrainMapChunk(entry.Key))
                {
                    staleKeys.Add(entry.Key);
                }
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                DisposeResidentChunk(staleKeys[i]);
            }

            EnforceResidentChunkMemoryCap();
            PruneQueuedChunkBuilds(retainChunkWindow);
        }

        private static void EnforceResidentChunkMemoryCap()
        {
            if (ResidentChunks.Count <= TerrainResidentChunkMemoryCapValue)
            {
                return;
            }

            List<ChunkKey> removable = new(ResidentChunks.Count);
            foreach (KeyValuePair<ChunkKey, TerrainChunkRecord> entry in ResidentChunks)
            {
                if (_lastVisibleChunkWindow.Contains(entry.Key) ||
                    _lastMaterializedChunkWindow.Contains(entry.Key) ||
                    IsFullTerrainMapChunk(entry.Key))
                {
                    continue;
                }

                removable.Add(entry.Key);
            }

            removable.Sort((left, right) =>
            {
                int leftDistance = ChunkDistanceSqFromCurrentCenter(left);
                int rightDistance = ChunkDistanceSqFromCurrentCenter(right);
                return rightDistance.CompareTo(leftDistance);
            });

            for (int i = 0; i < removable.Count && ResidentChunks.Count > TerrainResidentChunkMemoryCapValue; i++)
            {
                DisposeResidentChunk(removable[i]);
            }
        }

        private static int ChunkDistanceSqFromCurrentCenter(ChunkKey key)
        {
            int dx = key.X - _lastCenterChunk.X;
            int dy = key.Y - _lastCenterChunk.Y;
            return (dx * dx) + (dy * dy);
        }

        private static void PruneQueuedChunkBuilds(ChunkBounds retainChunkWindow)
        {
            lock (TerrainChunkWorkerLock)
            {
                for (int i = TerrainChunkBuildQueue.Count - 1; i >= 0; i--)
                {
                    ChunkKey key = TerrainChunkBuildQueue[i].Key;
                    if (retainChunkWindow.Contains(key) ||
                        IsFullTerrainMapChunk(key))
                    {
                        continue;
                    }

                    TerrainChunkBuildQueue.RemoveAt(i);
                    QueuedChunkKeys.Remove(key);
                }

                UpdateTerrainChunkWorkerTelemetryLocked();
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

        internal static bool IsTerrainFreeWorldPosition(Vector2 worldPosition, float clearanceRadiusWorldUnits)
        {
            if (!IsFiniteVector(worldPosition))
            {
                return false;
            }

            LoadSettingsIfNeeded();
            return !OverlapsTerrainAtWorldPosition(worldPosition, MathF.Max(0f, clearanceRadiusWorldUnits));
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

            bool residentTerrainCollisionReady = ResidentTerrainCollisionLoops.Count > 0;
            if (!residentTerrainCollisionReady && !HasDynamicTerrainWorldBoundaryIntrusion(gameObjects))
            {
                _terrainRuntimeFieldCollisionFallbackSuppressedCount++;
                return;
            }

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

                if (!OverlapsStableTerrainCollisionSurface(gameObject, gameObject.Position, residentTerrainCollisionReady))
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

        internal static void ResolveBulletTerrainIntrusions(IReadOnlyList<Bullet> bullets)
        {
            _terrainBulletCollisionCorrectionCount = 0;

            if (bullets == null || bullets.Count == 0)
            {
                return;
            }

            LoadSettingsIfNeeded();

            bool residentTerrainCollisionReady = ResidentTerrainCollisionLoops.Count > 0;
            if (!residentTerrainCollisionReady)
            {
                return;
            }

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

                if (!OverlapsTerrainAtCollisionHull(bullet, bullet.Position))
                {
                    continue;
                }

                float escapeProbeRadius = ResolveTerrainEscapeProbeRadius(bullet);
                Vector2 originalPosition = bullet.Position;
                Vector2 resolvedPosition = ResolveTerrainIntrusionPosition(
                    bullet,
                    originalPosition,
                    bullet.PreviousPosition,
                    escapeProbeRadius);
                Vector2 terrainMtv = originalPosition - resolvedPosition;
                if (!IsFiniteVector(terrainMtv) || terrainMtv.LengthSquared() <= 0.25f)
                {
                    continue;
                }

                BulletCollisionResolver.ReflectBulletOffStaticSurface(
                    bullet,
                    terrainMtv,
                    TerrainStaticMass);
                bullet.TriggerHitFlash();
                _terrainBulletCollisionCorrectionCount++;
            }
        }

        private static bool HasDynamicTerrainWorldBoundaryIntrusion(IReadOnlyList<GameObject> gameObjects)
        {
            EnsureTerrainWorldBoundsInitialized();
            if (!TerrainWorldBoundaryEnabled || gameObjects == null)
            {
                return false;
            }

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

                if (OverlapsTerrainWorldBoundary(gameObject.Position, gameObject.BoundingRadius) ||
                    TerrainCollisionHullOverlapsWorldBoundary(gameObject, gameObject.Position))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsStableTerrainCollisionSurface(
            GameObject gameObject,
            Vector2 worldPosition,
            bool residentTerrainCollisionReady)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (OverlapsTerrainWorldBoundary(worldPosition, gameObject.BoundingRadius) ||
                TerrainCollisionHullOverlapsWorldBoundary(gameObject, worldPosition))
            {
                return true;
            }

            return residentTerrainCollisionReady &&
                OverlapsTerrainAtWorldPosition(worldPosition, gameObject.BoundingRadius) &&
                OverlapsTerrainAtCollisionHull(gameObject, worldPosition);
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

        private static bool TerrainCollisionHullOverlapsWorldBoundary(GameObject gameObject, Vector2 worldPosition)
        {
            EnsureTerrainWorldBoundsInitialized();
            if (!TerrainWorldBoundaryEnabled ||
                gameObject?.Shape == null ||
                !IsFiniteVector(worldPosition))
            {
                return false;
            }

            try
            {
                Vector2[] vertices = gameObject.Shape.GetTransformedVertices(worldPosition, gameObject.Rotation);
                if (vertices == null || vertices.Length == 0)
                {
                    return OverlapsTerrainWorldBoundary(worldPosition, gameObject.BoundingRadius);
                }

                for (int i = 0; i < vertices.Length; i++)
                {
                    if (OverlapsTerrainWorldBoundary(vertices[i], 0f))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning(
                    $"Terrain world boundary hull probe failed for ID={gameObject.ID}, Name={gameObject.Name}: {ex.Message}");
                return OverlapsTerrainWorldBoundary(worldPosition, gameObject.BoundingRadius);
            }
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
                return Water;
            }

            float terrainX = ResolveTerrainCentifootX(worldX);
            float terrainY = ResolveTerrainCentifootY(worldY);
            return WorldField(terrainX, terrainY, _terrainWorldSeed) > SeaLevel ? Land : Water;
        }

        private static bool IsTerrainLandAtWorldPosition(Vector2 worldPosition)
        {
            if (TrySampleGeneratedTerrainAtWorldPosition(worldPosition, out bool isLand))
            {
                return isLand;
            }

            return SampleTerrainMaskAtWorldPosition(worldPosition.X, worldPosition.Y) == Land;
        }

        private static bool TrySampleGeneratedTerrainAtWorldPosition(Vector2 worldPosition, out bool isLand)
        {
            isLand = false;
            TerrainMapSnapshot overrideSnapshot = _terrainMapSnapshotOverride;
            if (overrideSnapshot != null &&
                overrideSnapshot.TrySample(worldPosition.X, worldPosition.Y, out byte overrideMask))
            {
                isLand = overrideMask == Land;
                return true;
            }

            TerrainMapSnapshot fullMapSnapshot = _fullTerrainMapSnapshot;
            if (fullMapSnapshot != null &&
                fullMapSnapshot.TrySample(worldPosition.X, worldPosition.Y, out byte snapshotMask))
            {
                isLand = snapshotMask == Land;
                return true;
            }

            if (TrySampleResidentTerrainChunkAtWorldPosition(worldPosition, out bool residentIsLand))
            {
                isLand = residentIsLand;
                return true;
            }

            return false;
        }

        private static bool TrySampleResidentTerrainChunkAtWorldPosition(Vector2 worldPosition, out bool isLand)
        {
            isLand = false;
            if (!IsFiniteVector(worldPosition) || ResidentChunks.Count <= 0)
            {
                return false;
            }

            ChunkKey key = BuildChunkKey(worldPosition.X, worldPosition.Y);
            if (!ResidentChunks.TryGetValue(key, out TerrainChunkRecord chunk))
            {
                return false;
            }

            if (chunk == null || !chunk.HasLand || chunk.LandMask == null || chunk.LandMask.Length < ChunkTextureResolution * ChunkTextureResolution)
            {
                isLand = false;
                return true;
            }

            float chunkMinX = key.X * ChunkWorldSize;
            float chunkMinY = key.Y * ChunkWorldSize;
            float sampleStep = ChunkWorldSize / ChunkTextureResolution;
            int x = Math.Clamp((int)MathF.Floor((worldPosition.X - chunkMinX) / sampleStep), 0, ChunkTextureResolution - 1);
            int y = Math.Clamp((int)MathF.Floor((worldPosition.Y - chunkMinY) / sampleStep), 0, ChunkTextureResolution - 1);
            isLand = chunk.LandMask[Index(x, y, ChunkTextureResolution)] == Land;
            return true;
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
            if (!TerrainWorldBoundaryEnabled)
            {
                return false;
            }

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

        internal static bool TryResolveOceanZoneAtWorldPosition(
            Vector2 worldPosition,
            out TerrainWaterType waterType,
            out float waterDepth)
        {
            return TryResolveOceanZoneAtWorldPosition(
                worldPosition,
                out waterType,
                out waterDepth,
                out _);
        }

        internal static bool TryResolveOceanZoneAtWorldPosition(
            Vector2 worldPosition,
            out TerrainWaterType waterType,
            out float waterDepth,
            out float offshoreDistance)
        {
            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();

            waterType = TerrainWaterType.Sunlit;
            waterDepth = 0f;
            offshoreDistance = 0f;

            if (!IsFiniteVector(worldPosition))
            {
                return false;
            }

            if (!TerrainWorldContainsPoint(worldPosition.X, worldPosition.Y))
            {
                ResolveInfiniteAbyssOceanZone(out waterType, out waterDepth, out offshoreDistance);
                return true;
            }

            if (!TryResolveOceanZoneAtWorldPositionCore(
                worldPosition,
                ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance)) +
                    TerrainOceanZoneFieldSampleStepWorldUnits,
                out waterType,
                out waterDepth,
                out offshoreDistance))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveOceanZoneAtWorldPositionCore(
            Vector2 worldPosition,
            float maxSearchDistanceWorldUnits,
            out TerrainWaterType waterType,
            out float waterDepth,
            out float offshoreDistance)
        {
            LoadSettingsIfNeeded();
            EnsureTerrainWorldBoundsInitialized();

            return TryResolveLayeredOceanZoneAtWorldPosition(
                worldPosition,
                maxSearchDistanceWorldUnits,
                out waterType,
                out waterDepth,
                out offshoreDistance);
        }

        private static TerrainWaterType ResolveWaterTypeFromOffshoreDistance(float offshoreDistance)
        {
            float distance = MathF.Max(0f, offshoreDistance);
            if (distance <= ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance))
            {
                return TerrainWaterType.Shallow;
            }

            if (distance <= ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance))
            {
                return TerrainWaterType.Sunlit;
            }

            if (distance <= ResolveEffectiveOceanZoneTransitionDistance(DevWaterTwilightDistance))
            {
                return TerrainWaterType.Twilight;
            }

            return distance <= ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance) ? TerrainWaterType.Midnight : TerrainWaterType.Abyss;
        }

        private static void ResolveInfiniteAbyssOceanZone(
            out TerrainWaterType waterType,
            out float waterDepth,
            out float offshoreDistance)
        {
            waterType = TerrainWaterType.Abyss;
            waterDepth = DevWaterDepthRampMax;
            offshoreDistance = ResolveInfiniteAbyssOffshoreDistance();
        }

        private static float ResolveInfiniteAbyssOffshoreDistance()
        {
            return ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance) +
                TerrainOceanZoneFieldSampleStepWorldUnits;
        }

        private static float ResolveDefaultArchipelagoAbyssOverrideDistance()
        {
            float abyssThreshold = ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance);
            float safetyBand = Math.Max(
                Math.Max(ChunkWorldSize * 2f, ResolveEffectiveOceanZoneTransitionDistance(DevWaterTwilightDistance)),
                TerrainOceanZoneFieldSampleStepWorldUnits * 8f);
            return abyssThreshold + safetyBand;
        }

        private static float ResolveOceanZoneSmoothArchipelagoDistanceStart()
        {
            return ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance) +
                OceanZoneDebugSampleStepWorldUnits;
        }

        private static bool TryResolveStableArchipelagoOceanZoneOffshoreDistance(
            Vector2 worldPosition,
            int seed,
            out float offshoreDistance)
        {
            if (!TryResolveDefaultArchipelagoOceanZoneOffshoreDistance(worldPosition, seed, out offshoreDistance))
            {
                return false;
            }

            return offshoreDistance > ResolveStableArchipelagoOceanZonePrecisionDistance();
        }

        private static float ResolveStableArchipelagoOceanZonePrecisionDistance()
        {
            return ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance);
        }

        private static float ResolveStableArchipelagoBlendedOffshoreDistance(float preciseDistance, float stableArchipelagoDistance)
        {
            if (!float.IsFinite(stableArchipelagoDistance))
            {
                return preciseDistance;
            }

            if (!float.IsFinite(preciseDistance))
            {
                return stableArchipelagoDistance;
            }

            float precisionDistance = ResolveStableArchipelagoOceanZonePrecisionDistance();
            if (stableArchipelagoDistance <= precisionDistance)
            {
                return preciseDistance;
            }

            float fullStableDistance = MathF.Max(
                precisionDistance + TerrainOceanZoneFieldSampleStepWorldUnits,
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance));
            float blend = SmoothStep(precisionDistance, fullStableDistance, stableArchipelagoDistance);
            return MathHelper.Lerp(preciseDistance, stableArchipelagoDistance, blend);
        }

        private static float ResolveEffectiveOceanZoneTransitionDistance(float baseDistance)
        {
            float distance = MathF.Max(0f, baseDistance);
            return distance + (_oceanZoneMinimumTransitionVolumeDistanceWorldUnits * ResolveOceanZoneTransitionSpreadMultiplier(distance));
        }

        private static float ResolveOceanZoneTransitionSpreadMultiplier(float baseDistance)
        {
            float epsilon = MathF.Max(0.001f, TerrainOceanZoneFieldSampleStepWorldUnits * 0.01f);
            if (baseDistance <= DevWaterShallowDistance + epsilon)
            {
                return 1f;
            }

            if (baseDistance <= DevWaterSunlitDistance + epsilon)
            {
                return 2f;
            }

            if (baseDistance <= DevWaterTwilightDistance + epsilon)
            {
                return 3f;
            }

            return 4f;
        }

        private static TerrainWaterType ResolveWaterTypeAtTerrainPosition(float terrainX, float terrainY, int seed)
        {
            Vector2 worldPosition = TerrainCoordinateToWorldPosition(terrainX, terrainY);
            float offshoreDistance = ResolveCanonicalOceanZoneOffshoreDistance(worldPosition, seed);
            return ResolveWaterTypeFromOffshoreDistance(offshoreDistance);
        }

        private static float ResolveOceanZoneOffshoreDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            return ResolveCanonicalOceanZoneOffshoreDistance(worldPosition, _terrainWorldSeed);
        }

        private static float ResolveCanonicalOceanZoneOffshoreDistance(Vector2 worldPosition, int seed)
        {
            if (!IsFiniteVector(worldPosition))
            {
                return 0f;
            }

            if (TryResolveDefaultArchipelagoOceanZoneOffshoreDistance(
                worldPosition,
                seed,
                out float defaultArchipelagoDistance))
            {
                return defaultArchipelagoDistance;
            }

            return ResolveInfiniteAbyssOffshoreDistance();
        }

        private static float ResolveCentralIslandOceanZoneOffshoreDistance(Vector2 worldPosition, int seed)
        {
            if (!IsFiniteVector(worldPosition) ||
                !TryResolvePrimaryOceanZoneAnchor(
                    seed,
                    out Vector2 centerWorld,
                    out float radiusWorldUnits,
                    out float elongation,
                    out float rotationRadians))
            {
                return 0f;
            }

            Vector2 delta = worldPosition - centerWorld;
            Rotate(delta.X, delta.Y, -rotationRadians, out float localX, out float localY);
            float safeElongation = Math.Clamp(elongation, 0.25f, 4f);
            localX /= safeElongation;
            localY *= safeElongation;
            float radialDistance = MathF.Sqrt((localX * localX) + (localY * localY));
            return MathF.Max(0f, radialDistance - Math.Max(1f, radiusWorldUnits));
        }

        private static bool TryResolveDefaultArchipelagoOceanZoneOffshoreDistance(
            Vector2 worldPosition,
            int seed,
            out float offshoreDistance)
        {
            offshoreDistance = 0f;
            if (!IsFiniteVector(worldPosition))
            {
                return false;
            }

            IReadOnlyList<OceanZoneDistanceAnchor> anchors = ResolveDefaultOceanZoneDistanceAnchors(seed);
            if (anchors.Count <= 0)
            {
                return false;
            }

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < anchors.Count; i++)
            {
                OceanZoneDistanceAnchor anchor = anchors[i];
                Vector2 delta = worldPosition - anchor.CenterWorld;
                Rotate(delta.X, delta.Y, -anchor.RotationRadians, out float localX, out float localY);
                localX /= anchor.Elongation;
                localY *= anchor.Elongation;
                float radialDistance = MathF.Sqrt((localX * localX) + (localY * localY));
                float coastDistance = MathF.Max(0f, radialDistance - anchor.RadiusWorldUnits);
                if (coastDistance < bestDistance)
                {
                    bestDistance = coastDistance;
                }
            }

            if (!float.IsFinite(bestDistance))
            {
                return false;
            }

            offshoreDistance = bestDistance;
            return true;
        }

        private static bool TryResolveCentralIslandOceanZoneNormal(Vector2 worldPosition, out Vector2 normal)
        {
            normal = Vector2.Zero;
            if (!IsFiniteVector(worldPosition) ||
                !TryResolvePrimaryOceanZoneAnchor(
                    _terrainWorldSeed,
                    out Vector2 centerWorld,
                    out _,
                    out float elongation,
                    out float rotationRadians))
            {
                return false;
            }

            Vector2 delta = worldPosition - centerWorld;
            Rotate(delta.X, delta.Y, -rotationRadians, out float rotatedX, out float rotatedY);
            float safeElongation = Math.Clamp(elongation, 0.25f, 4f);
            float localX = rotatedX / safeElongation;
            float localY = rotatedY * safeElongation;
            float radialDistance = MathF.Sqrt((localX * localX) + (localY * localY));
            if (radialDistance <= 0.001f)
            {
                return false;
            }

            float gradientX = localX / (radialDistance * safeElongation);
            float gradientY = (localY * safeElongation) / radialDistance;
            Rotate(gradientX, gradientY, rotationRadians, out float normalX, out float normalY);
            normal = new Vector2(normalX, normalY);
            if (normal.LengthSquared() <= 0.0001f)
            {
                return false;
            }

            normal.Normalize();
            return true;
        }

        private static float ResolveLayeredTerrainOffshoreDistance(float terrainX, float terrainY, int seed)
        {
            Vector2 worldPosition = TerrainCoordinateToWorldPosition(terrainX, terrainY);
            bool stableArchipelagoDistanceAvailable =
                TryResolveStableArchipelagoOceanZoneOffshoreDistance(
                    worldPosition,
                    seed,
                    out float stableArchipelagoDistance);

            float sampleStep = MathF.Max(0.01f, DefaultWorldToTerrainCoordinate(TerrainOceanZoneFieldSampleStepWorldUnits));
            float maxDistance = stableArchipelagoDistanceAvailable
                ? MathF.Max(sampleStep, DefaultWorldToTerrainCoordinate(ResolveStableArchipelagoOceanZonePrecisionDistance()))
                : MathF.Max(
                sampleStep,
                DefaultWorldToTerrainCoordinate(
                    ResolveEffectiveOceanZoneTransitionDistance(Math.Max(DevWaterMidnightDistance, DevWaterTwilightDistance)) +
                    TerrainOceanZoneFieldSampleStepWorldUnits));
            float bestDistance = float.PositiveInfinity;

            for (float radius = sampleStep; radius <= maxDistance;)
            {
                float radiusWorldUnits = TerrainCoordinateToDefaultWorld(radius);
                float ringStepWorldUnits = ResolveOceanZoneDistanceSearchStep(radiusWorldUnits);
                float ringStep = MathF.Max(sampleStep, DefaultWorldToTerrainCoordinate(ringStepWorldUnits));
                int sampleCount = Math.Clamp(
                    (int)MathF.Ceiling(MathF.Tau * radiusWorldUnits / ringStepWorldUnits),
                    12,
                    TerrainOceanZoneMaxFieldSamplesPerRing);
                float angleOffset = ResolveTerrainDistanceRingAngleOffset(radiusWorldUnits, ringStepWorldUnits);
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = ((sampleIndex + angleOffset) / sampleCount) * MathF.Tau;
                    float directionX = MathF.Cos(angle);
                    float directionY = MathF.Sin(angle);
                    float sampleX = terrainX + (directionX * radius);
                    float sampleY = terrainY + (directionY * radius);
                    if (!IsLayeredTerrainLandAtTerrainPosition(sampleX, sampleY, seed))
                    {
                        continue;
                    }

                    float coastDistance = RefineLayeredTerrainCoastDistance(terrainX, terrainY, directionX, directionY, radius, seed);
                    if (coastDistance < bestDistance)
                    {
                        bestDistance = coastDistance;
                    }
                }

                if (float.IsFinite(bestDistance))
                {
                    float preciseDistance = TerrainCoordinateToDefaultWorld(bestDistance);
                    return stableArchipelagoDistanceAvailable
                        ? ResolveStableArchipelagoBlendedOffshoreDistance(preciseDistance, stableArchipelagoDistance)
                        : preciseDistance;
                }

                radius += sampleStep;
            }

            float fallbackDistance = TerrainCoordinateToDefaultWorld(maxDistance + sampleStep);
            return stableArchipelagoDistanceAvailable
                ? ResolveStableArchipelagoBlendedOffshoreDistance(fallbackDistance, stableArchipelagoDistance)
                : fallbackDistance;
        }

        private static float RefineLayeredTerrainCoastDistance(
            float waterX,
            float waterY,
            float directionX,
            float directionY,
            float landDistance,
            int seed)
        {
            float low = 0f;
            float high = Math.Max(0f, landDistance);
            for (int i = 0; i < TerrainOceanZoneCoastRefineIterations; i++)
            {
                float midpoint = (low + high) * 0.5f;
                float sampleX = waterX + (directionX * midpoint);
                float sampleY = waterY + (directionY * midpoint);
                if (IsLayeredTerrainLandAtTerrainPosition(sampleX, sampleY, seed))
                {
                    high = midpoint;
                }
                else
                {
                    low = midpoint;
                }
            }

            return high;
        }

        private static bool IsLayeredTerrainLandAtTerrainPosition(float terrainX, float terrainY, int seed)
        {
            return WorldField(terrainX, terrainY, seed) > SeaLevel;
        }

        private static bool TryResolveLayeredOceanZoneAtWorldPosition(
            Vector2 worldPosition,
            float maxSearchDistanceWorldUnits,
            out TerrainWaterType waterType,
            out float waterDepth,
            out float offshoreDistance)
        {
            waterType = TerrainWaterType.Sunlit;
            waterDepth = 0f;
            offshoreDistance = 0f;

            if (!IsFiniteVector(worldPosition))
            {
                return false;
            }

            if (!TerrainWorldContainsPoint(worldPosition.X, worldPosition.Y))
            {
                ResolveInfiniteAbyssOceanZone(out waterType, out waterDepth, out offshoreDistance);
                return true;
            }

            float terrainX = ResolveTerrainCentifootX(worldPosition.X);
            float terrainY = ResolveTerrainCentifootY(worldPosition.Y);
            bool appliedTerrainAuthoritative = IsAppliedTerrainWindowAuthoritativeForOceanZone(worldPosition, 0f);
            if (!appliedTerrainAuthoritative &&
                TryResolveDefaultArchipelagoOceanZoneOffshoreDistance(
                    worldPosition,
                    _terrainWorldSeed,
                    out float defaultArchipelagoDistance) &&
                defaultArchipelagoDistance > ResolveDefaultArchipelagoAbyssOverrideDistance())
            {
                waterDepth = DevWaterDepthRampMax;
                offshoreDistance = defaultArchipelagoDistance;
                waterType = ResolveWaterTypeFromOffshoreDistance(offshoreDistance);
                return true;
            }

            if (!appliedTerrainAuthoritative &&
                TrySampleGeneratedTerrainAtWorldPosition(worldPosition, out bool snapshotIsLand))
            {
                if (snapshotIsLand)
                {
                    return false;
                }

                offshoreDistance = ResolveOceanZoneOffshoreDistance(worldPosition, maxSearchDistanceWorldUnits);
                waterType = ResolveWaterTypeFromOffshoreDistance(offshoreDistance);
                waterDepth = MathHelper.Lerp(0f, DevWaterDepthRampMax, Math.Clamp(offshoreDistance / Math.Max(1f, ResolveEffectiveOceanZoneTransitionDistance(DevWaterMidnightDistance)), 0f, 1f));
                return true;
            }

            TerrainCell cell = SampleLayeredTerrainCell(terrainX, terrainY, _terrainWorldSeed, resolveWaterType: false);
            if (appliedTerrainAuthoritative)
            {
                if (OverlapsResidentTerrainGeometry(worldPosition, clearanceRadiusWorldUnits: 0f))
                {
                    return false;
                }
            }
            else if (!cell.IsWater)
            {
                return false;
            }

            waterDepth = cell.WaterDepth;
            offshoreDistance = ResolveOceanZoneOffshoreDistance(worldPosition, maxSearchDistanceWorldUnits);
            waterType = ResolveWaterTypeFromOffshoreDistance(offshoreDistance);
            return true;
        }

        private static float ResolveGeneratedTerrainOffshoreDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            if (!IsFiniteVector(worldPosition))
            {
                return Math.Max(0f, maxSearchDistanceWorldUnits);
            }

            float maxDistance = Math.Max(
                TerrainOceanZoneFieldSampleStepWorldUnits,
                maxSearchDistanceWorldUnits);
            bool appliedPointAuthoritative = IsAppliedTerrainWindowAuthoritativeForOceanZone(worldPosition, 0f);
            bool appliedSearchAuthoritative = IsAppliedTerrainWindowAuthoritativeForOceanZone(worldPosition, maxDistance);
            bool stableArchipelagoDistanceAvailable =
                TryResolveStableArchipelagoOceanZoneOffshoreDistance(
                    worldPosition,
                    _terrainWorldSeed,
                    out float stableArchipelagoDistance);
            float residentSearchDistance = stableArchipelagoDistanceAvailable
                ? MathF.Min(maxDistance, ResolveStableArchipelagoOceanZonePrecisionDistance())
                : maxDistance;
            if (TryResolveResidentTerrainVectorOffshoreDistance(
                worldPosition,
                residentSearchDistance,
                assumeFarWaterWhenNoNearbyEdge: appliedPointAuthoritative,
                out bool residentVectorWater,
                out float residentVectorDistance))
            {
                if (residentVectorWater && stableArchipelagoDistanceAvailable)
                {
                    return ResolveStableArchipelagoBlendedOffshoreDistance(
                        residentVectorDistance,
                        stableArchipelagoDistance);
                }

                return residentVectorWater ? residentVectorDistance : 0f;
            }

            if (appliedPointAuthoritative || appliedSearchAuthoritative)
            {
                return maxDistance + TerrainOceanZoneFieldSampleStepWorldUnits;
            }

            if (IsTerrainLandAtWorldPosition(worldPosition))
            {
                return 0f;
            }

            float fieldSearchDistance = stableArchipelagoDistanceAvailable
                ? MathF.Min(maxDistance, ResolveStableArchipelagoOceanZonePrecisionDistance())
                : maxDistance;
            float fieldDistance = ResolveFieldTerrainOffshoreDistance(worldPosition, fieldSearchDistance);
            return stableArchipelagoDistanceAvailable
                ? ResolveStableArchipelagoBlendedOffshoreDistance(fieldDistance, stableArchipelagoDistance)
                : fieldDistance;
        }

        private static bool IsAppliedTerrainWindowAuthoritativeForOceanZone(Vector2 worldPosition, float searchRadiusWorldUnits)
        {
            if (!_hasAppliedTerrainVisualChunkWindow || !IsFiniteVector(worldPosition))
            {
                return false;
            }

            float radius = Math.Max(0f, searchRadiusWorldUnits);
            ChunkBounds searchWindow = BuildChunkBounds(
                worldPosition.X - radius,
                worldPosition.X + radius,
                worldPosition.Y - radius,
                worldPosition.Y + radius);
            return _lastAppliedVisualChunkWindow.Contains(searchWindow) &&
                !HasPendingChunkInBounds(searchWindow);
        }

        private static float ResolveResidentTerrainOffshoreDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            return TryResolveResidentTerrainVectorOffshoreDistance(
                worldPosition,
                maxSearchDistanceWorldUnits,
                assumeFarWaterWhenNoNearbyEdge: false,
                out bool isWater,
                out float offshoreDistance) && isWater
                    ? offshoreDistance
                    : float.PositiveInfinity;
        }

        private static bool TryResolveResidentTerrainVectorOffshoreDistance(
            Vector2 worldPosition,
            float maxSearchDistanceWorldUnits,
            bool assumeFarWaterWhenNoNearbyEdge,
            out bool isWater,
            out float offshoreDistance)
        {
            isWater = true;
            offshoreDistance = 0f;
            if (ResidentTerrainCollisionLoops.Count == 0)
            {
                return false;
            }

            float maxDistance = Math.Max(0f, maxSearchDistanceWorldUnits);
            float maxDistanceSq = maxDistance * maxDistance;
            float bestDistanceSq = float.PositiveInfinity;
            for (int loopIndex = 0; loopIndex < ResidentTerrainCollisionLoops.Count; loopIndex++)
            {
                TerrainCollisionLoopRecord loop = ResidentTerrainCollisionLoops[loopIndex];
                if (loop?.Points == null || loop.Points.Count < 3)
                {
                    continue;
                }

                bool pointInLoopBounds = loop.Bounds.Intersects(
                    worldPosition.X,
                    worldPosition.X,
                    worldPosition.Y,
                    worldPosition.Y);
                bool expandedBoundsHit = loop.Bounds.Intersects(
                    worldPosition.X - maxDistance,
                    worldPosition.X + maxDistance,
                    worldPosition.Y - maxDistance,
                    worldPosition.Y + maxDistance);
                if (!pointInLoopBounds && !expandedBoundsHit)
                {
                    continue;
                }

                if (pointInLoopBounds && PointInsidePolygon(worldPosition, loop.Points))
                {
                    isWater = false;
                    offshoreDistance = 0f;
                    return true;
                }

                if (!expandedBoundsHit)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < loop.Points.Count; pointIndex++)
                {
                    Vector2 start = loop.Points[pointIndex];
                    Vector2 end = loop.Points[(pointIndex + 1) % loop.Points.Count];
                    float distanceSq = DistancePointToSegmentSquared(worldPosition, start, end);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                    }
                }
            }

            if (bestDistanceSq <= maxDistanceSq)
            {
                isWater = true;
                offshoreDistance = MathF.Sqrt(bestDistanceSq);
                return true;
            }

            if (assumeFarWaterWhenNoNearbyEdge)
            {
                isWater = true;
                offshoreDistance = maxDistance + TerrainOceanZoneFieldSampleStepWorldUnits;
                return true;
            }

            return false;
        }

        private static float ResolveFieldTerrainOffshoreDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            float sampleStep = MathF.Max(8f, TerrainOceanZoneFieldSampleStepWorldUnits);
            float maxDistance = Math.Max(sampleStep, maxSearchDistanceWorldUnits);
            float bestDistance = float.PositiveInfinity;
            float firstHitRadius = float.PositiveInfinity;

            for (float radius = sampleStep; radius <= maxDistance;)
            {
                float ringStep = ResolveOceanZoneDistanceSearchStep(radius);
                int sampleCount = Math.Clamp(
                    (int)MathF.Ceiling(MathF.Tau * radius / ringStep),
                    12,
                    TerrainOceanZoneMaxFieldSamplesPerRing);
                float angleOffset = ResolveTerrainDistanceRingAngleOffset(radius, ringStep);
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = ((sampleIndex + angleOffset) / sampleCount) * MathF.Tau;
                    Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 sample = worldPosition + (direction * radius);
                    if (!IsTerrainLandAtWorldPosition(sample))
                    {
                        continue;
                    }

                    float coastDistance = RefineTerrainCoastDistance(worldPosition, direction, radius);
                    if (coastDistance < bestDistance)
                    {
                        bestDistance = coastDistance;
                    }
                }

                if (float.IsFinite(bestDistance))
                {
                    if (!float.IsFinite(firstHitRadius))
                    {
                        firstHitRadius = radius;
                    }

                    if (radius >= MathF.Min(maxDistance, firstHitRadius + (sampleStep * 2f)))
                    {
                        return ResolveRefinedTerrainOffshoreDistance(worldPosition, maxDistance, bestDistance);
                    }
                }

                radius += sampleStep;
            }

            float nearCoastFallbackDistance = ResolveNearCoastFallbackDistance(worldPosition, maxDistance);
            if (float.IsFinite(nearCoastFallbackDistance))
            {
                return nearCoastFallbackDistance;
            }

            float shelfFallbackDistance = ResolveShelfFallbackDistance(worldPosition, maxDistance);
            return float.IsFinite(shelfFallbackDistance) ? shelfFallbackDistance : maxDistance + sampleStep;
        }

        private static float ResolveRefinedTerrainOffshoreDistance(
            Vector2 worldPosition,
            float maxDistance,
            float candidateDistance)
        {
            float refinedDistance = candidateDistance;
            if (refinedDistance > ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance))
            {
                refinedDistance = MathF.Min(refinedDistance, ResolveNearCoastFallbackDistance(worldPosition, maxDistance));
            }

            if (refinedDistance > ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance))
            {
                refinedDistance = MathF.Min(refinedDistance, ResolveShelfFallbackDistance(worldPosition, maxDistance));
                if (refinedDistance > ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance) &&
                    refinedDistance <= ResolveEffectiveOceanZoneTransitionDistance(DevWaterTwilightDistance) + (TerrainOceanZoneFieldSampleStepWorldUnits * 3f))
                {
                    refinedDistance = MathF.Min(refinedDistance, ResolveShelfGridFallbackDistance(worldPosition, maxDistance));
                }
            }

            return refinedDistance;
        }

        private static float ResolveNearCoastFallbackDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            float nearCoastStep = MathF.Max(6f, TerrainOceanZoneFieldSampleStepWorldUnits / 3f);
            float nearCoastMaxDistance = MathF.Min(
                maxSearchDistanceWorldUnits,
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance));
            float bestDistance = float.PositiveInfinity;
            for (float radius = nearCoastStep; radius <= nearCoastMaxDistance; radius += nearCoastStep)
            {
                int sampleCount = Math.Clamp(
                    (int)MathF.Ceiling(MathF.Tau * radius / nearCoastStep),
                    16,
                    TerrainOceanZoneMaxFieldSamplesPerRing);
                float angleOffset = ResolveTerrainDistanceRingAngleOffset(radius, nearCoastStep);
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = ((sampleIndex + angleOffset) / sampleCount) * MathF.Tau;
                    Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 sample = worldPosition + (direction * radius);
                    if (!IsTerrainLandAtWorldPosition(sample))
                    {
                        continue;
                    }

                    float coastDistance = RefineTerrainCoastDistance(worldPosition, direction, radius);
                    if (coastDistance < bestDistance)
                    {
                        bestDistance = coastDistance;
                    }
                }

                if (float.IsFinite(bestDistance))
                {
                    return bestDistance;
                }
            }

            return float.PositiveInfinity;
        }

        private static float ResolveShelfFallbackDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            float shelfStep = MathF.Max(18f, TerrainOceanZoneFieldSampleStepWorldUnits * 0.67f * TerrainWaterZoneDistanceScale);
            float shelfMaxDistance = MathF.Min(
                maxSearchDistanceWorldUnits,
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance));
            float bestDistance = float.PositiveInfinity;
            for (float radius = shelfStep; radius <= shelfMaxDistance; radius += shelfStep)
            {
                int sampleCount = Math.Clamp(
                    (int)MathF.Ceiling(MathF.Tau * radius / shelfStep),
                    24,
                    TerrainOceanZoneMaxFieldSamplesPerRing);
                float angleOffset = ResolveTerrainDistanceRingAngleOffset(radius, shelfStep);
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = ((sampleIndex + angleOffset) / sampleCount) * MathF.Tau;
                    Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 sample = worldPosition + (direction * radius);
                    if (!IsTerrainLandAtWorldPosition(sample))
                    {
                        continue;
                    }

                    float coastDistance = RefineTerrainCoastDistance(worldPosition, direction, radius);
                    if (coastDistance < bestDistance)
                    {
                        bestDistance = coastDistance;
                    }
                }

                if (float.IsFinite(bestDistance))
                {
                    return bestDistance;
                }
            }

            return float.PositiveInfinity;
        }

        private static float ResolveShelfGridFallbackDistance(Vector2 worldPosition, float maxSearchDistanceWorldUnits)
        {
            float gridStep = MathF.Max(12f, TerrainOceanZoneFieldSampleStepWorldUnits * 0.5f * TerrainWaterZoneDistanceScale);
            float maxDistance = MathF.Min(
                maxSearchDistanceWorldUnits,
                ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance));
            float maxDistanceSq = maxDistance * maxDistance;
            float bestDistance = float.PositiveInfinity;

            for (float y = -maxDistance; y <= maxDistance; y += gridStep)
            {
                for (float x = -maxDistance; x <= maxDistance; x += gridStep)
                {
                    float distanceSq = (x * x) + (y * y);
                    if (distanceSq > maxDistanceSq)
                    {
                        continue;
                    }

                    Vector2 sample = new(worldPosition.X + x, worldPosition.Y + y);
                    if (!IsTerrainLandAtWorldPosition(sample))
                    {
                        continue;
                    }

                    float distance = MathF.Sqrt(distanceSq);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                    }
                }
            }

            return bestDistance;
        }

        private static float ResolveOceanZoneDistanceSearchStep(float radiusWorldUnits)
        {
            float radius = MathF.Max(0f, radiusWorldUnits);
            float scale = Math.Clamp(TerrainWaterZoneDistanceScale, 0.5f, 6f);
            if (radius > ResolveEffectiveOceanZoneTransitionDistance(DevWaterTwilightDistance))
            {
                return TerrainOceanZoneFieldSampleStepWorldUnits * scale * 2f;
            }

            if (radius > ResolveEffectiveOceanZoneTransitionDistance(DevWaterSunlitDistance))
            {
                return TerrainOceanZoneFieldSampleStepWorldUnits * scale * 1.5f;
            }

            if (radius > ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance))
            {
                return TerrainOceanZoneFieldSampleStepWorldUnits * scale;
            }

            return TerrainOceanZoneFieldSampleStepWorldUnits;
        }

        private static float ResolveTerrainDistanceRingAngleOffset(float radius, float sampleStep)
        {
            float ring = radius / MathF.Max(0.001f, sampleStep);
            return (ring * 0.61803398875f) % 1f;
        }

        private static float RefineTerrainCoastDistance(Vector2 waterPosition, Vector2 direction, float landDistance)
        {
            float low = 0f;
            float high = Math.Max(0f, landDistance);
            for (int i = 0; i < TerrainOceanZoneCoastRefineIterations; i++)
            {
                float midpoint = (low + high) * 0.5f;
                Vector2 sample = waterPosition + (direction * midpoint);
                if (IsTerrainLandAtWorldPosition(sample))
                {
                    high = midpoint;
                }
                else
                {
                    low = midpoint;
                }
            }

            return high;
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
            if (spriteBatch == null)
            {
                return;
            }

            if (ResidentTerrainVisualObjects.Count == 0)
            {
                UpdateTerrainVisualDrawDiagnostics(
                    "no resident terrain visuals",
                    minX,
                    maxX,
                    minY,
                    maxY,
                    drawnObjectCount: 0,
                    drawnTriangleCount: 0);
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

            int drawnObjectCount = 0;
            int drawnTriangleCount = 0;
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

                    drawnObjectCount++;
                    drawnTriangleCount += record.FillPrimitiveCount;
                    graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        record.FillVertices,
                        0,
                        record.FillPrimitiveCount);
                }
            }

            graphicsDevice.ScissorRectangle = previousScissor;
            UpdateTerrainVisualDrawDiagnostics(
                "draw terrain visuals",
                minX,
                maxX,
                minY,
                maxY,
                drawnObjectCount,
                drawnTriangleCount);
        }

        private static void UpdateTerrainVisualDrawDiagnostics(
            string reason,
            float minX,
            float maxX,
            float minY,
            float maxY,
            int drawnObjectCount,
            int drawnTriangleCount)
        {
            bool stableVisibleCoverage =
                _hasAppliedTerrainVisualChunkWindow &&
                !_terrainVisibleObjectsDirty &&
                _terrainPendingCriticalChunkCount == 0 &&
                _lastAppliedVisualChunkWindow.Contains(_lastVisibleChunkWindow);
            bool sameDrawWindow =
                _hasTerrainDrawDiagnosticBaseline &&
                ChunkBoundsEqual(_lastTerrainDrawVisibleChunkWindow, _lastVisibleChunkWindow) &&
                ChunkBoundsEqual(_lastTerrainDrawAppliedVisualChunkWindow, _lastAppliedVisualChunkWindow);
            bool zeroDrawWithResidents =
                stableVisibleCoverage &&
                ResidentTerrainVisualObjects.Count > 0 &&
                drawnObjectCount == 0;
            bool zeroResidentWhileCovered =
                stableVisibleCoverage &&
                ResidentTerrainVisualObjects.Count == 0;
            bool zeroAfterVisibleDraw =
                stableVisibleCoverage &&
                sameDrawWindow &&
                _lastTerrainVisibleDrawObjectCount > 0 &&
                drawnObjectCount == 0;

            _lastTerrainVisibleDrawSummary =
                $"drawn={drawnObjectCount}/{ResidentTerrainVisualObjects.Count} objects, " +
                $"triangles={drawnTriangleCount}/{_residentTerrainVisualTriangleCount}, " +
                $"camera={FormatWorldRectangle(minX, maxX, minY, maxY)}, " +
                $"visible={FormatChunkBounds(_lastVisibleChunkWindow)}, " +
                $"applied={TerrainAppliedVisualChunkWindow}, " +
                $"dirty={_terrainVisibleObjectsDirty}, pendingCritical={_terrainPendingCriticalChunkCount}";

            if (zeroDrawWithResidents || zeroResidentWhileCovered || zeroAfterVisibleDraw)
            {
                string diagnosticReason = zeroResidentWhileCovered
                    ? "covered visible window has zero resident terrain objects"
                    : zeroAfterVisibleDraw
                        ? "covered visible window stopped drawing terrain objects"
                        : "resident terrain objects skipped by camera intersection";
                EmitTerrainFlickerDiagnostic(
                    $"{reason}: {diagnosticReason}",
                    minX,
                    maxX,
                    minY,
                    maxY,
                    drawnObjectCount,
                    drawnTriangleCount,
                    previousObjectSnapshot: null);
            }

            _lastTerrainVisibleDrawObjectCount = drawnObjectCount;
            _lastTerrainVisibleDrawTriangleCount = drawnTriangleCount;
            _lastTerrainDrawVisibleChunkWindow = _lastVisibleChunkWindow;
            _lastTerrainDrawAppliedVisualChunkWindow = _lastAppliedVisualChunkWindow;
            _hasTerrainDrawDiagnosticBaseline = true;
        }

        private static void EmitTerrainFlickerDiagnostic(
            string reason,
            float minX,
            float maxX,
            float minY,
            float maxY,
            int drawnObjectCount,
            int drawnTriangleCount,
            List<string> previousObjectSnapshot)
        {
            if (!ShouldEmitTerrainFlickerDiagnostic(reason))
            {
                return;
            }

            _terrainFlickerDiagnosticCount++;
            DebugLogger.PrintWarning(
                $"[TerrainFlicker] #{_terrainFlickerDiagnosticCount} reason='{reason}' " +
                $"time={Core.GAMETIME:0.000} residentObjects={ResidentTerrainVisualObjects.Count} " +
                $"drawnObjects={drawnObjectCount} drawnTriangles={drawnTriangleCount} " +
                $"residentTriangles={_residentTerrainVisualTriangleCount} coverage='{_terrainVisibleCoverageStatus}' " +
                $"visibleWindow={FormatChunkBounds(_lastVisibleChunkWindow)} " +
                $"targetMaterializedWindow={FormatChunkBounds(_lastMaterializedChunkWindow)} " +
                $"targetVisualWindow={FormatChunkBounds(_lastTerrainVisualChunkWindow)} " +
                $"appliedVisualWindow={TerrainAppliedVisualChunkWindow} " +
                $"appliedColliderWindow={TerrainAppliedColliderChunkWindow} " +
                $"colliderWindow={FormatChunkBounds(_lastTerrainColliderChunkWindow)} " +
                $"pendingCritical={_terrainPendingCriticalChunkCount} pendingTotal={TerrainPendingChunkCount} " +
                $"dirty={_terrainVisibleObjectsDirty} materializationInFlight={_terrainMaterializationTask != null} " +
                $"startupReady={_startupVisibleTerrainReady} camera={FormatWorldRectangle(minX, maxX, minY, maxY)}");

            LogTerrainVisualObjectDiagnostics(
                "[TerrainFlickerObject]",
                ResidentTerrainVisualObjects,
                minX,
                maxX,
                minY,
                maxY);

            List<string> snapshotToLog = previousObjectSnapshot ??
                (ResidentTerrainVisualObjects.Count == 0 ? _lastResidentTerrainVisualObjectSnapshot : null);
            if ((snapshotToLog?.Count ?? 0) > 0)
            {
                int limit = Math.Min(snapshotToLog.Count, MaxTerrainFlickerObjectLogs);
                for (int i = 0; i < limit; i++)
                {
                    DebugLogger.PrintDebug($"[TerrainFlickerObjectPrevious] #{_terrainFlickerDiagnosticCount} index={i} {snapshotToLog[i]}");
                }

                if (snapshotToLog.Count > limit)
                {
                    DebugLogger.PrintDebug($"[TerrainFlickerObjectPrevious] #{_terrainFlickerDiagnosticCount} suppressed={snapshotToLog.Count - limit}");
                }
            }
        }

        private static bool ShouldEmitTerrainFlickerDiagnostic(string reason)
        {
            float now = Core.GAMETIME;
            if (!float.IsFinite(now))
            {
                now = 0f;
            }

            if (_terrainFlickerDiagnosticCount == 0 ||
                !string.Equals(reason, _lastTerrainFlickerDiagnosticReason, StringComparison.Ordinal) ||
                now - _lastTerrainFlickerDiagnosticTime >= TerrainFlickerDiagnosticCooldownSeconds)
            {
                _lastTerrainFlickerDiagnosticTime = now;
                _lastTerrainFlickerDiagnosticReason = reason;
                return true;
            }

            return false;
        }

        private static void LogTerrainVisualObjectDiagnostics(
            string prefix,
            IReadOnlyList<TerrainVisualObjectRecord> records,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            int count = records?.Count ?? 0;
            int limit = Math.Min(count, MaxTerrainFlickerObjectLogs);
            for (int i = 0; i < limit; i++)
            {
                DebugLogger.PrintDebug($"{prefix} #{_terrainFlickerDiagnosticCount} index={i} {DescribeTerrainVisualObject(records[i], minX, maxX, minY, maxY)}");
            }

            if (count > limit)
            {
                DebugLogger.PrintDebug($"{prefix} #{_terrainFlickerDiagnosticCount} suppressed={count - limit}");
            }
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
            if (!TerrainWorldBoundaryEnabled)
            {
                return;
            }

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
            if (!TerrainWorldBoundaryEnabled)
            {
                return 0;
            }

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
                _activeTerrainColliderCount = 0;
                _terrainColliderActivationCandidateCount = 0;
                UpdateTerrainDynamicCollisionProbeTelemetry(0, 0);
                return;
            }

            int objectProbeCount = 0;
            int bulletProbeCount = 0;
            List<GameObject> gameObjects = Core.Instance.GameObjects;
            for (int i = 0; i < gameObjects.Count; i++)
            {
                if (!TryBuildTerrainActivationBoundsForDynamicGameObject(
                    gameObjects[i],
                    out _))
                {
                    continue;
                }

                objectProbeCount++;
            }

            IReadOnlyList<Bullet> bullets = BulletManager.GetBullets();
            for (int i = 0; i < bullets.Count; i++)
            {
                if (!TryBuildTerrainActivationBoundsForBullet(
                    bullets[i],
                    out _))
                {
                    continue;
                }

                bulletProbeCount++;
            }

            UpdateTerrainDynamicCollisionProbeTelemetry(objectProbeCount, bulletProbeCount);
            _terrainColliderActivationCandidateCount = 0;
            _activeTerrainColliderCount = 0;
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

        private static bool TryBuildTerrainActivationBoundsForDynamicGameObject(
            GameObject gameObject,
            out TerrainWorldBounds bounds)
        {
            bounds = default;
            if (gameObject == null ||
                !gameObject.DynamicPhysics ||
                !gameObject.IsCollidable ||
                gameObject.Shape == null)
            {
                return false;
            }

            return TryBuildTerrainActivationBounds(
                gameObject.Position,
                gameObject.BoundingRadius,
                ResolveTerrainActivationVelocity(gameObject),
                out bounds);
        }

        private static bool TryBuildTerrainActivationBoundsForBullet(
            Bullet bullet,
            out TerrainWorldBounds bounds)
        {
            bounds = default;
            if (bullet == null ||
                bullet.IsDying ||
                bullet.IsBarrelLocked ||
                bullet.Shape == null)
            {
                return false;
            }

            return TryBuildTerrainActivationBounds(
                bullet.Position,
                bullet.BoundingRadius,
                bullet.Velocity,
                out bounds);
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

        private static void RefreshResidentTerrainWorldObjects(
            GraphicsDevice graphicsDevice,
            ChunkBounds materializedChunkWindow,
            ChunkBounds visualChunkWindow,
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
                _terrainDeferredVisibleMaterializationCount++;
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
                    visualChunkWindow,
                    colliderChunkWindow);
                return;
            }

            StartTerrainMaterialization(
                residentMask,
                BuildChunkWorldBounds(colliderChunkWindow),
                materializedChunkWindow,
                visualChunkWindow,
                colliderChunkWindow);
        }

        private static void UpdateTerrainVisibleCoverageStatus(ChunkBounds visibleChunkWindow)
        {
            if (HasPendingChunkInBounds(visibleChunkWindow))
            {
                _terrainVisibleCoverageStatus = $"visible terrain waiting on {CountPendingChunksInBounds(visibleChunkWindow)} chunks";
                return;
            }

            if (!_hasAppliedTerrainVisualChunkWindow)
            {
                _terrainVisibleCoverageStatus = "visible terrain not materialized";
                return;
            }

            if (!_lastAppliedVisualChunkWindow.Contains(visibleChunkWindow))
            {
                _terrainVisibleCoverageStatus = "visible terrain outside applied visual window";
                return;
            }

            _terrainVisibleCoverageStatus = _terrainVisibleObjectsDirty
                ? "visible terrain dirty"
                : "visible terrain covered";
        }

        private static void UpdateStartupVisibleTerrainReadiness(ChunkBounds visibleChunkWindow)
        {
            if (_startupVisibleTerrainReady)
            {
                return;
            }

            bool visibleWindowCovered = _hasAppliedTerrainVisualChunkWindow &&
                _lastAppliedVisualChunkWindow.Contains(visibleChunkWindow) &&
                _terrainPendingCriticalChunkCount == 0 &&
                _terrainMaterializationTask == null &&
                !_terrainWorldObjectsDirty;
            if (visibleWindowCovered)
            {
                _startupVisibleTerrainReady = true;
                _terrainStartupPhase = "visible terrain ready; background preload streaming";
                _terrainStartupReadinessSummary = $"startup terrain ready: {_residentTerrainComponentCount} landforms in {FormatChunkBounds(visibleChunkWindow)}";
                return;
            }

            if (_residentTerrainComponentCount > 0)
            {
                _startupFirstSightTerrainReady = true;
                _terrainStartupPhase = _terrainPendingCriticalChunkCount > 0
                    ? "nearby terrain ready; visible chunks streaming"
                    : "nearby terrain ready; visible materialization pending";
                _terrainStartupReadinessSummary = $"startup terrain warm: {_residentTerrainComponentCount} nearby landforms; visible window {FormatChunkBounds(visibleChunkWindow)} pending";
                return;
            }

            if (_terrainPendingCriticalChunkCount > 0)
            {
                _terrainStartupPhase = "visible terrain chunks building";
                _terrainStartupReadinessSummary = $"startup terrain pending: {_terrainPendingCriticalChunkCount} visible chunks still queued";
                return;
            }

            if (_terrainMaterializationTask != null)
            {
                _terrainStartupPhase = "visible terrain materializing";
                _terrainStartupReadinessSummary = "startup terrain pending: visible terrain materialization in flight";
                return;
            }

            if (_terrainWorldObjectsDirty)
            {
                _terrainStartupPhase = "visible terrain materialization queued";
                _terrainStartupReadinessSummary = "startup terrain pending: visible terrain materialization queued";
                return;
            }

            _startupVisibleTerrainReady = true;
            _terrainStartupPhase = "visible terrain ready; background preload streaming";
            _terrainStartupReadinessSummary = $"startup terrain ready: {FormatChunkBounds(visibleChunkWindow)} visible";
        }

        private static void StartTerrainMaterialization(
            CombinedResidentMask residentMask,
            TerrainWorldBounds colliderWorldBounds,
            ChunkBounds materializedChunkWindow,
            ChunkBounds visualChunkWindow,
            ChunkBounds colliderChunkWindow)
        {
            int requestId = ++_terrainMaterializationRequestId;
            _terrainWorldObjectsDirty = false;
            _terrainStartupPhase = _startupVisibleTerrainReady
                ? "background terrain materializing"
                : "visible terrain materializing";
            _terrainMaterializationTask = Task.Run(() => BuildTerrainMaterializationResult(
                residentMask,
                requestId,
                colliderWorldBounds,
                materializedChunkWindow,
                visualChunkWindow,
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
            bool restartAfterApply = _terrainWorldObjectsDirty;
            if (!IsTerrainMaterializationResultCurrent(result))
            {
                _terrainDiscardedStaleMaterializationCount++;
                _terrainWorldObjectsDirty = true;
                return;
            }

            if (restartAfterApply &&
                (!result.VisualChunkWindow.Contains(_lastVisibleChunkWindow) ||
                HasPendingChunkInBounds(_lastVisibleChunkWindow)))
            {
                _terrainDiscardedStaleMaterializationCount++;
                _terrainWorldObjectsDirty = true;
                return;
            }

            ApplyTerrainMaterializationResult(result);
            if (restartAfterApply)
            {
                _terrainAcceptedDirtyMaterializationCount++;
                _terrainWorldObjectsDirty = true;
            }
        }

        private static bool IsTerrainMaterializationResultCurrent(TerrainMaterializationResult result)
        {
            return result != null &&
                result.RequestId == _terrainMaterializationRequestId &&
                ChunkBoundsEqual(result.VisualChunkWindow, _lastTerrainVisualChunkWindow) &&
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
            ChunkBounds visualChunkWindow,
            ChunkBounds colliderChunkWindow)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            TerrainMaterializationResult result = new(requestId, materializedChunkWindow, visualChunkWindow, colliderChunkWindow);
            float sampleStepWorldUnits = ChunkWorldSize / ChunkTextureResolution;
            float worldLeft = residentMask.MinChunkX * ChunkWorldSize;
            float worldTop = residentMask.MinChunkY * ChunkWorldSize;

            List<TerrainLoop> singleVisualLoop = new(1);
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
                List<TerrainLoop> renderedCollisionLoops = new(visualLoops.Count);

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

                    singleVisualLoop.Clear();
                    singleVisualLoop.Add(loop);
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
                        singleVisualLoop,
                        component.WorldLeft,
                        component.WorldTop,
                        component.SampleStepWorldUnits);
                    if (visualVertices.Length == 0)
                    {
                        continue;
                    }

                    result.VisualObjects.Add(new TerrainVisualObjectRecord(
                        AllocateTerrainVisualObjectDiagnosticId(),
                        requestId,
                        loopBounds,
                        visualVertices,
                        BuildTerrainVisualObjectSignature(loopBounds, visualVertices)));
                    result.ComponentCount++;
                    result.VisualTriangleCount += visualVertices.Length / 3;
                    renderedCollisionLoops.Add(loop);
                }

                for (int loopIndex = 0; loopIndex < renderedCollisionLoops.Count; loopIndex++)
                {
                    TerrainLoop collisionLoop = renderedCollisionLoops[loopIndex];
                    if (collisionLoop?.Points == null || collisionLoop.Points.Count < 3)
                    {
                        continue;
                    }

                    TerrainWorldBounds loopBounds = BuildTerrainLoopBounds(
                        collisionLoop,
                        component.WorldLeft,
                        component.WorldTop,
                        component.SampleStepWorldUnits);
                    if (!IsTerrainLoopWorldBoundsPlausible(loopBounds))
                    {
                        continue;
                    }

                    result.CollisionLoops.Add(new TerrainCollisionLoopRecord(
                        loopBounds,
                        BuildTerrainWorldLoop(collisionLoop, component.WorldLeft, component.WorldTop, component.SampleStepWorldUnits)));
                }
            }

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

            int previousVisualObjectCount = ResidentTerrainVisualObjects.Count;
            int previousVisualTriangleCount = _residentTerrainVisualTriangleCount;
            List<string> previousVisualObjectSnapshot = CaptureTerrainVisualObjectSnapshot(ResidentTerrainVisualObjects);

            ClearResidentTerrainWorldObjects();

            ResidentTerrainVisualObjects.AddRange(result.VisualObjects);
            ResidentTerrainCollisionLoops.AddRange(result.CollisionLoops);

            _residentTerrainComponentCount = result.ComponentCount;
            _residentTerrainColliderCount = 0;
            _residentTerrainVisualTriangleCount = result.VisualTriangleCount;
            _lastTerrainMaterializationMilliseconds = result.BuildMilliseconds;
            _residentTerrainVertexColorValid = false;
            _lastAppliedVisualChunkWindow = result.VisualChunkWindow;
            _lastAppliedColliderChunkWindow = result.ColliderChunkWindow;
            _hasAppliedTerrainVisualChunkWindow = true;
            _terrainVisibleObjectsDirty = !result.VisualChunkWindow.Contains(_lastVisibleChunkWindow);
            _lastResidentTerrainVisualObjectSnapshot.Clear();
            _lastResidentTerrainVisualObjectSnapshot.AddRange(CaptureTerrainVisualObjectSnapshot(ResidentTerrainVisualObjects));
            LogTerrainMaterializationApplyDiagnostics(
                result,
                previousVisualObjectCount,
                previousVisualTriangleCount,
                previousVisualObjectSnapshot);
        }

        private static void LogTerrainMaterializationApplyDiagnostics(
            TerrainMaterializationResult result,
            int previousVisualObjectCount,
            int previousVisualTriangleCount,
            List<string> previousVisualObjectSnapshot)
        {
            if (result == null)
            {
                return;
            }

            bool visibleWindowCovered = result.VisualChunkWindow.Contains(_lastVisibleChunkWindow);
            DebugLogger.PrintDebug(
                $"[TerrainMaterializationApply] request={result.RequestId} " +
                $"oldObjects={previousVisualObjectCount} newObjects={ResidentTerrainVisualObjects.Count} " +
                $"oldTriangles={previousVisualTriangleCount} newTriangles={_residentTerrainVisualTriangleCount} " +
                $"buildMs={result.BuildMilliseconds:0.###} visibleWindow={FormatChunkBounds(_lastVisibleChunkWindow)} " +
                $"resultMaterializedWindow={FormatChunkBounds(result.MaterializedChunkWindow)} " +
                $"resultVisualWindow={FormatChunkBounds(result.VisualChunkWindow)} " +
                $"resultColliderWindow={FormatChunkBounds(result.ColliderChunkWindow)} " +
                $"covered={visibleWindowCovered} dirty={_terrainVisibleObjectsDirty} " +
                $"pendingCritical={_terrainPendingCriticalChunkCount} pendingTotal={TerrainPendingChunkCount}");

            bool removedCoveredVisuals =
                previousVisualObjectCount > 0 &&
                ResidentTerrainVisualObjects.Count == 0 &&
                visibleWindowCovered &&
                _terrainPendingCriticalChunkCount == 0;
            bool removedMostCoveredVisuals =
                previousVisualObjectCount >= 4 &&
                ResidentTerrainVisualObjects.Count > 0 &&
                ResidentTerrainVisualObjects.Count <= previousVisualObjectCount / 4 &&
                visibleWindowCovered &&
                _terrainPendingCriticalChunkCount == 0;

            if (removedCoveredVisuals || removedMostCoveredVisuals)
            {
                string reason = removedCoveredVisuals
                    ? "materialization removed all terrain visuals while visible window was covered"
                    : "materialization removed most terrain visuals while visible window was covered";
                EmitTerrainFlickerDiagnostic(
                    reason,
                    float.NaN,
                    float.NaN,
                    float.NaN,
                    float.NaN,
                    _lastTerrainVisibleDrawObjectCount,
                    _lastTerrainVisibleDrawTriangleCount,
                    previousVisualObjectSnapshot);
            }
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

        private static TerrainMapSnapshot BuildFullTerrainMapSnapshot(ChunkBounds chunkBounds)
        {
            int chunkColumns = Math.Max(1, chunkBounds.MaxChunkX - chunkBounds.MinChunkX + 1);
            int chunkRows = Math.Max(1, chunkBounds.MaxChunkY - chunkBounds.MinChunkY + 1);
            int maskWidth = chunkColumns * ChunkTextureResolution;
            int maskHeight = chunkRows * ChunkTextureResolution;
            byte[] mask = new byte[maskWidth * maskHeight];

            for (int chunkY = chunkBounds.MinChunkY; chunkY <= chunkBounds.MaxChunkY; chunkY++)
            {
                for (int chunkX = chunkBounds.MinChunkX; chunkX <= chunkBounds.MaxChunkX; chunkX++)
                {
                    ChunkKey key = new(chunkX, chunkY);
                    if (!ResidentChunks.TryGetValue(key, out TerrainChunkRecord chunk))
                    {
                        return null;
                    }

                    if (!chunk.HasLand || chunk.LandMask.Length == 0)
                    {
                        continue;
                    }

                    int offsetX = (chunkX - chunkBounds.MinChunkX) * ChunkTextureResolution;
                    int offsetY = (chunkY - chunkBounds.MinChunkY) * ChunkTextureResolution;
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
            }

            TerrainWorldBounds worldBounds = BuildChunkWorldBounds(chunkBounds);
            return new TerrainMapSnapshot(
                mask,
                maskWidth,
                maskHeight,
                worldBounds.MinX,
                worldBounds.MinY,
                ChunkWorldSize / ChunkTextureResolution);
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
            if (!TerrainWorldBoundaryEnabled)
            {
                return false;
            }

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

        private static bool IsFiniteVector(Vector2 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y);
        }

        private static void ClearResidentTerrainWorldObjects()
        {
            for (int i = ResidentTerrainVisualObjects.Count - 1; i >= 0; i--)
            {
                ResidentTerrainVisualObjects[i]?.Dispose();
            }

            ResidentTerrainVisualObjects.Clear();
            ResidentTerrainCollisionLoops.Clear();
            _lastResidentTerrainVisualObjectSnapshot.Clear();
            _hasAppliedTerrainVisualChunkWindow = false;
            _terrainVisibleObjectsDirty = true;
            _residentTerrainComponentCount = 0;
            _residentTerrainColliderCount = 0;
            _residentTerrainVisualTriangleCount = 0;
            _activeTerrainColliderCount = 0;
            _terrainColliderActivationCandidateCount = 0;
            UpdateTerrainDynamicCollisionProbeTelemetry(0, 0);
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
            if (!TerrainWorldBoundaryEnabled)
            {
                return chunkBounds;
            }

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
            if (!TerrainWorldBoundaryEnabled)
            {
                return true;
            }

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
            if (!TerrainWorldBoundaryEnabled)
            {
                return true;
            }

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

        private static ChunkBounds ExpandChunkBounds(ChunkBounds bounds, int marginChunks)
        {
            int margin = Math.Max(0, marginChunks);
            return ClampChunkBoundsToTerrainWorld(new ChunkBounds(
                bounds.MinChunkX - margin,
                bounds.MaxChunkX + margin,
                bounds.MinChunkY - margin,
                bounds.MaxChunkY + margin));
        }

        private static ChunkBounds UnionChunkBounds(ChunkBounds left, ChunkBounds right)
        {
            return ClampChunkBoundsToTerrainWorld(new ChunkBounds(
                Math.Min(left.MinChunkX, right.MinChunkX),
                Math.Max(left.MaxChunkX, right.MaxChunkX),
                Math.Min(left.MinChunkY, right.MinChunkY),
                Math.Max(left.MaxChunkY, right.MaxChunkY)));
        }

        private static string FormatWorldRectangle(float minX, float maxX, float minY, float maxY)
        {
            if (!float.IsFinite(minX) ||
                !float.IsFinite(maxX) ||
                !float.IsFinite(minY) ||
                !float.IsFinite(maxY))
            {
                return "unknown";
            }

            return $"{minX:0.#}..{maxX:0.#}, {minY:0.#}..{maxY:0.#}";
        }

        private static string FormatTerrainWorldBounds(TerrainWorldBounds bounds)
        {
            return $"{bounds.MinX:0.#},{bounds.MinY:0.#}->{bounds.MaxX:0.#},{bounds.MaxY:0.#}";
        }

        private static string DescribeTerrainVisualObject(
            TerrainVisualObjectRecord record,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (record == null)
            {
                return "terrainObject=null";
            }

            bool hasCameraBounds =
                float.IsFinite(minX) &&
                float.IsFinite(maxX) &&
                float.IsFinite(minY) &&
                float.IsFinite(maxY);
            string intersectsCamera = hasCameraBounds
                ? record.Bounds.Intersects(minX, maxX, minY, maxY).ToString()
                : "unknown";
            return
                $"terrainObjectId={record.DiagnosticId} request={record.MaterializationRequestId} " +
                $"bounds={FormatTerrainWorldBounds(record.Bounds)} " +
                $"vertices={record.FillVertices?.Length ?? 0} primitives={record.FillPrimitiveCount} " +
                $"signature={record.BoundsSignature} intersectsCamera={intersectsCamera}";
        }

        private static List<string> CaptureTerrainVisualObjectSnapshot(IReadOnlyList<TerrainVisualObjectRecord> records)
        {
            List<string> snapshot = new(records?.Count ?? 0);
            if (records == null)
            {
                return snapshot;
            }

            for (int i = 0; i < records.Count; i++)
            {
                snapshot.Add(DescribeTerrainVisualObject(
                    records[i],
                    float.NaN,
                    float.NaN,
                    float.NaN,
                    float.NaN));
            }

            return snapshot;
        }

        private static int AllocateTerrainVisualObjectDiagnosticId()
        {
            unchecked
            {
                _terrainNextVisualObjectDiagnosticId++;
                if (_terrainNextVisualObjectDiagnosticId <= 0)
                {
                    _terrainNextVisualObjectDiagnosticId = 1;
                }

                return _terrainNextVisualObjectDiagnosticId;
            }
        }

        private static int BuildTerrainVisualObjectSignature(
            TerrainWorldBounds bounds,
            VertexPositionColor[] vertices)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + bounds.MinX.GetHashCode();
                hash = (hash * 31) + bounds.MinY.GetHashCode();
                hash = (hash * 31) + bounds.MaxX.GetHashCode();
                hash = (hash * 31) + bounds.MaxY.GetHashCode();
                hash = (hash * 31) + (vertices?.Length ?? 0);
                if (vertices != null && vertices.Length > 0)
                {
                    VertexPositionColor first = vertices[0];
                    VertexPositionColor last = vertices[vertices.Length - 1];
                    hash = (hash * 31) + first.Position.X.GetHashCode();
                    hash = (hash * 31) + first.Position.Y.GetHashCode();
                    hash = (hash * 31) + last.Position.X.GetHashCode();
                    hash = (hash * 31) + last.Position.Y.GetHashCode();
                }

                return hash;
            }
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

        private static float DegreesToRadians(float degrees)
        {
            return degrees * MathF.PI / 180f;
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

        private static IReadOnlyList<TerrainWorldPlacement> ResolveDefaultWorldPlacements(int seed)
        {
            lock (DefaultWorldPlacementCacheLock)
            {
                if (_cachedDefaultWorldPlacementSeed != seed)
                {
                    _cachedDefaultWorldPlacements = TerrainWorldPlacementGenerator.BuildDefaultArchipelagoPlacements(seed).ToArray();
                    _cachedDefaultWorldPlacementSeed = seed;
                }

                return _cachedDefaultWorldPlacements;
            }
        }

        private static IReadOnlyList<OceanZoneDistanceAnchor> ResolveDefaultOceanZoneDistanceAnchors(int seed)
        {
            lock (OceanZoneDistanceAnchorCacheLock)
            {
                if (_cachedOceanZoneDistanceAnchorSeed != seed)
                {
                    IReadOnlyList<TerrainWorldPlacement> placements = ResolveDefaultWorldPlacements(seed);
                    OceanZoneDistanceAnchor[] anchors = new OceanZoneDistanceAnchor[placements.Count];
                    for (int i = 0; i < placements.Count; i++)
                    {
                        TerrainWorldPlacement placement = placements[i];
                        float centerTerrainX = DefaultWorldToTerrainCoordinate(placement.X);
                        float centerTerrainY = DefaultWorldToTerrainCoordinate(placement.Y);
                        Vector2 centerWorld = TerrainCoordinateToWorldPosition(centerTerrainX, centerTerrainY);
                        TerrainSubstrateWeights substrate = ResolveDominantSubstrateWeights(centerTerrainX, centerTerrainY, seed);
                        float radiusScale = Math.Clamp(
                            0.86f +
                            (substrate.VolcanicRock * 0.36f) +
                            (substrate.LimestoneKarst * 0.20f) -
                            (substrate.SandSediment * 0.08f),
                            0.60f,
                            1.45f);
                        anchors[i] = new OceanZoneDistanceAnchor(
                            centerWorld,
                            Math.Max(1f, placement.Radius * radiusScale),
                            Math.Clamp(ResolveDefaultWorldPlacementElongation(placement), 0.25f, 4f),
                            DegreesToRadians(placement.RotationDegrees));
                    }

                    _cachedOceanZoneDistanceAnchors = anchors;
                    _cachedOceanZoneDistanceAnchorSeed = seed;
                }

                return _cachedOceanZoneDistanceAnchors;
            }
        }

        private static string FormatTerrainOceanZoneOrigin()
        {
            return "nearest default archipelago polygon border";
        }

        private static float ResolveTerrainOceanZoneOriginRadius()
        {
            return ResolveEffectiveOceanZoneTransitionDistance(DevWaterShallowDistance);
        }

        private static bool TryResolvePrimaryOceanZoneAnchor(
            int seed,
            out Vector2 centerWorld,
            out float radiusWorldUnits,
            out float elongation,
            out float rotationRadians)
        {
            centerWorld = Vector2.Zero;
            radiusWorldUnits = 0f;
            elongation = 1f;
            rotationRadians = 0f;

            lock (OceanZoneAnchorCacheLock)
            {
                if (_cachedOceanZoneAnchorSeed == seed &&
                    _cachedOceanZoneAnchorRadiusWorldUnits > 0f)
                {
                    centerWorld = _cachedOceanZoneAnchorCenterWorld;
                    radiusWorldUnits = _cachedOceanZoneAnchorRadiusWorldUnits;
                    elongation = _cachedOceanZoneAnchorElongation;
                    rotationRadians = _cachedOceanZoneAnchorRotationRadians;
                    return true;
                }

                IReadOnlyList<TerrainWorldPlacement> placements = ResolveDefaultWorldPlacements(seed);
                if (placements.Count <= 0)
                {
                    _cachedOceanZoneAnchorSeed = int.MinValue;
                    return false;
                }

                TerrainWorldPlacement primary = placements[0];
                float centerTerrainX = DefaultWorldToTerrainCoordinate(primary.X);
                float centerTerrainY = DefaultWorldToTerrainCoordinate(primary.Y);
                TerrainSubstrateWeights substrate = ResolveDominantSubstrateWeights(centerTerrainX, centerTerrainY, seed);
                float radiusScale = Math.Clamp(
                    0.86f +
                    (substrate.VolcanicRock * 0.36f) +
                    (substrate.LimestoneKarst * 0.20f) -
                    (substrate.SandSediment * 0.08f),
                    0.60f,
                    1.45f);

                _cachedOceanZoneAnchorCenterWorld = TerrainCoordinateToWorldPosition(centerTerrainX, centerTerrainY);
                _cachedOceanZoneAnchorRadiusWorldUnits = Math.Max(1f, primary.Radius * radiusScale);
                _cachedOceanZoneAnchorElongation = ResolveDefaultWorldPlacementElongation(primary);
                _cachedOceanZoneAnchorRotationRadians = DegreesToRadians(primary.RotationDegrees);
                _cachedOceanZoneAnchorSeed = seed;

                centerWorld = _cachedOceanZoneAnchorCenterWorld;
                radiusWorldUnits = _cachedOceanZoneAnchorRadiusWorldUnits;
                elongation = _cachedOceanZoneAnchorElongation;
                rotationRadians = _cachedOceanZoneAnchorRotationRadians;
                return true;
            }
        }

        private static float DefaultWorldToTerrainCoordinate(float defaultWorldUnits)
        {
            return defaultWorldUnits / TerrainDefaultWorldUnitsPerTerrainCoordinate;
        }

        private static float TerrainCoordinateToDefaultWorld(float terrainCoordinate)
        {
            return terrainCoordinate * TerrainDefaultWorldUnitsPerTerrainCoordinate;
        }

        private static Vector2 TerrainCoordinateToWorldPosition(float terrainX, float terrainY)
        {
            return new Vector2(
                TerrainCoordinateToDefaultWorld(terrainX - _terrainSeedAnchorCentifoot.X),
                TerrainCoordinateToDefaultWorld(terrainY - _terrainSeedAnchorCentifoot.Y));
        }

        private static float ResolveDefaultWorldPlacementElongation(TerrainWorldPlacement placement)
        {
            float lengthRatio = TerrainWorldDefaults.MainIslandBaseLength / Math.Max(1f, TerrainWorldDefaults.MainIslandBaseRadius);
            return Math.Clamp(0.76f + ((lengthRatio - 1f) * 0.36f) + ((placement.WidthScale - 1f) * 0.28f), 0.62f, 1.72f);
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
            IReadOnlyList<TerrainWorldPlacement> placements = ResolveDefaultWorldPlacements(seed);
            for (int i = 0; i < placements.Count; i++)
            {
                TerrainWorldPlacement placement = placements[i];
                float candidateX = DefaultWorldToTerrainCoordinate(placement.X);
                float candidateY = DefaultWorldToTerrainCoordinate(placement.Y);
                if ((int)MathF.Floor(candidateX / ArchipelagoCellSize) != islandCellX ||
                    (int)MathF.Floor(candidateY / ArchipelagoCellSize) != islandCellY)
                {
                    continue;
                }

                mainX = candidateX;
                mainY = candidateY;
                radius = Math.Max(0.2f, DefaultWorldToTerrainCoordinate(placement.Radius));
                elongation = ResolveDefaultWorldPlacementElongation(placement);
                angle = DegreesToRadians(placement.RotationDegrees);
                mainSeed = seed + (i * 9973) + (placement.ClusterIndex * 193);
                return true;
            }

            return false;
        }

        private static int ResolveIslandSatelliteCount(int islandCellX, int islandCellY, int seed)
        {
            return 0;
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
            float field = -1.72f + (sampleSubstrate.ReefLimestone * 0.04f) + (sampleSubstrate.SandSediment * 0.03f);
            IReadOnlyList<TerrainWorldPlacement> placements = ResolveDefaultWorldPlacements(seed);
            for (int i = 0; i < placements.Count; i++)
            {
                TerrainWorldPlacement placement = placements[i];
                float mainX = DefaultWorldToTerrainCoordinate(placement.X);
                float mainY = DefaultWorldToTerrainCoordinate(placement.Y);
                float radius = Math.Max(0.2f, DefaultWorldToTerrainCoordinate(placement.Radius));
                float influenceRadius = radius * 2.35f;
                if (Distance(x, y, mainX, mainY) > influenceRadius)
                {
                    continue;
                }

                TerrainSubstrateWeights islandSubstrate = ResolveDominantSubstrateWeights(mainX, mainY, seed);
                float islandRadius = radius * (0.86f + (islandSubstrate.VolcanicRock * 0.36f) + (islandSubstrate.LimestoneKarst * 0.2f) - (islandSubstrate.SandSediment * 0.08f));
                float elongation = ResolveDefaultWorldPlacementElongation(placement);
                float angle = DegreesToRadians(placement.RotationDegrees);
                int mainSeed = seed + (i * 9973) + (placement.ClusterIndex * 193);
                float islandField = IslandContribution(x, y, mainX, mainY, islandRadius, mainSeed, elongation, angle);
                islandField += CliffWallField(x, y, mainX, mainY, islandRadius, mainSeed, elongation, angle, islandSubstrate);
                field = MathF.Max(field, islandField);
            }

            field += Fbm(x * 0.035f, y * 0.035f, seed + 1300, 5) * (0.18f + (sampleSubstrate.VolcanicRock * 0.1f));
            field += MathF.Pow(RidgedFbm(x * 0.18f, y * 0.18f, seed + 1310, 4), 2.1f) * sampleSubstrate.VolcanicRock * 0.12f;
            float clusterSupport = ArchipelagoClusterSupport(x, y, seed);
            field += (clusterSupport * 0.26f) - ((1f - clusterSupport) * 1.08f);
            return field;
        }

        private static float ArchipelagoClusterSupport(float x, float y, int seed)
        {
            float best = 0f;
            IReadOnlyList<TerrainWorldPlacement> placements = ResolveDefaultWorldPlacements(seed);
            for (int i = 0; i < placements.Count; i++)
            {
                TerrainWorldPlacement placement = placements[i];
                float centerX = DefaultWorldToTerrainCoordinate(placement.X);
                float centerY = DefaultWorldToTerrainCoordinate(placement.Y);
                float islandRadius = DefaultWorldToTerrainCoordinate(placement.Radius);
                float supportRadius = Math.Max(
                    islandRadius * (2.6f + (TerrainWorldDefaults.WorldClusterSpread * 0.42f)),
                    ArchipelagoMacroCellSize * (0.72f + (TerrainWorldDefaults.WorldChainBias * 0.28f)));
                float cluster = SmoothStep(supportRadius, supportRadius * 0.18f, Distance(x, y, centerX, centerY));
                best = MathF.Max(best, cluster);
            }

            float shelfNoise = (Fbm(x * 0.012f, y * 0.012f, seed + 1511, 5) + 1f) * 0.5f;
            return Math.Clamp(best + (best * shelfNoise * 0.16f), 0f, 1f);
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
            return EstimateRuntimeTerrainField(x, y, seed);
        }

        private static float EstimateRuntimeTerrainField(float x, float y, int seed)
        {
            TerrainSubstrateWeights substrate = ResolveSubstrateWeights(x, y, seed);
            float field = EstimateBaseField(x, y, seed, substrate);
            float macroSupport = ArchipelagoClusterSupport(x, y, seed);
            float selectedLandform = macroSupport > 0.02f ? SampleSelectedLandformField(x, y, seed) : -1.18f;
            if (selectedLandform > -1f)
            {
                float landformGate = SmoothStep(0.04f, 0.22f, macroSupport);
                field = MathF.Max(field, MathHelper.Lerp(-1.18f, selectedLandform * 0.92f, landformGate));
            }

            float coastBand = ResolveCoastBand(field, 0.58f);
            field -= ErosiveOpeningCutField(x, y, seed, substrate, coastBand) * 0.42f;
            return field;
        }

        private static TerrainCell SampleLayeredTerrainCell(float x, float y, int seed, bool resolveWaterType = true)
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
            TerrainWaterType waterType = field <= SeaLevel && resolveWaterType
                ? ResolveWaterTypeAtTerrainPosition(x, y, seed)
                : TerrainWaterType.Shallow;

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
                waterType,
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
            string terrainState = cell.IsLand
                ? "land"
                : $"{FormatTerrainWaterType(cell.WaterType)} water depth {CentifootUnits.FormatNumber(cell.WaterDepth, "0.00")}";
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

        private static string FormatTerrainWaterType(TerrainWaterType waterType)
        {
            return waterType switch
            {
                TerrainWaterType.Shallow => "shallow",
                TerrainWaterType.Sunlit => "sunlit",
                TerrainWaterType.Twilight => "twilight",
                TerrainWaterType.Midnight => "midnight",
                TerrainWaterType.Abyss => "abyss",
                _ => "unknown"
            };
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
                TerrainWaterType waterType,
                TerrainSubstrate lithology,
                TerrainLandformTag landformTags)
            {
                Elevation = elevation;
                SeaLevel = seaLevel;
                WaterDepth = MathF.Max(0f, seaLevel - elevation);
                WaterType = IsWater ? waterType : TerrainWaterType.Shallow;
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
            public TerrainWaterType WaterType { get; }
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

            public bool Contains(ChunkBounds bounds)
            {
                return bounds.MinChunkX >= MinChunkX &&
                    bounds.MaxChunkX <= MaxChunkX &&
                    bounds.MinChunkY >= MinChunkY &&
                    bounds.MaxChunkY <= MaxChunkY;
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

        private readonly struct OceanZoneDistanceAnchor
        {
            public OceanZoneDistanceAnchor(
                Vector2 centerWorld,
                float radiusWorldUnits,
                float elongation,
                float rotationRadians)
            {
                CenterWorld = centerWorld;
                RadiusWorldUnits = MathF.Max(1f, radiusWorldUnits);
                Elongation = Math.Clamp(elongation, 0.25f, 4f);
                RotationRadians = rotationRadians;
            }

            public Vector2 CenterWorld { get; }
            public float RadiusWorldUnits { get; }
            public float Elongation { get; }
            public float RotationRadians { get; }
        }

        private readonly struct OceanZoneDebugSample
        {
            public OceanZoneDebugSample(bool isWater, TerrainWaterType waterType, float offshoreDistance)
            {
                IsInitialized = true;
                IsWater = isWater;
                WaterType = waterType;
                OffshoreDistance = MathF.Max(0f, offshoreDistance);
            }

            public bool IsInitialized { get; }
            public bool IsWater { get; }
            public TerrainWaterType WaterType { get; }
            public float OffshoreDistance { get; }
        }

        private readonly struct OceanZoneDebugTileKey : IEquatable<OceanZoneDebugTileKey>
        {
            public OceanZoneDebugTileKey(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public bool Equals(OceanZoneDebugTileKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is OceanZoneDebugTileKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        private readonly struct OceanZoneDebugTileBuildCandidate
        {
            public OceanZoneDebugTileBuildCandidate(OceanZoneDebugTileKey key, int distanceSq)
            {
                Key = key;
                DistanceSq = distanceSq;
            }

            public OceanZoneDebugTileKey Key { get; }
            public int DistanceSq { get; }
        }

        private readonly struct OceanZoneDebugTileBuildResult
        {
            public OceanZoneDebugTileBuildResult(
                OceanZoneDebugTileKey key,
                List<OceanZoneDebugSegment> segments,
                double buildMilliseconds)
            {
                Key = key;
                Segments = segments ?? new List<OceanZoneDebugSegment>();
                BuildMilliseconds = buildMilliseconds;
            }

            public OceanZoneDebugTileKey Key { get; }
            public List<OceanZoneDebugSegment> Segments { get; }
            public double BuildMilliseconds { get; }
        }

        private sealed class TerrainMapSnapshot
        {
            public TerrainMapSnapshot(
                byte[] mask,
                int width,
                int height,
                float worldMinX,
                float worldMinY,
                float sampleStepWorldUnits)
            {
                Mask = mask ?? Array.Empty<byte>();
                Width = Math.Max(0, width);
                Height = Math.Max(0, height);
                WorldMinX = worldMinX;
                WorldMinY = worldMinY;
                SampleStepWorldUnits = MathF.Max(0.0001f, sampleStepWorldUnits);
                WorldMaxX = worldMinX + (Width * SampleStepWorldUnits);
                WorldMaxY = worldMinY + (Height * SampleStepWorldUnits);
            }

            private byte[] Mask { get; }
            private int Width { get; }
            private int Height { get; }
            private float WorldMinX { get; }
            private float WorldMinY { get; }
            private float WorldMaxX { get; }
            private float WorldMaxY { get; }
            private float SampleStepWorldUnits { get; }

            public bool TrySample(float worldX, float worldY, out byte value)
            {
                value = Water;
                if (Width <= 0 ||
                    Height <= 0 ||
                    Mask.Length < Width * Height ||
                    !float.IsFinite(worldX) ||
                    !float.IsFinite(worldY) ||
                    worldX < WorldMinX ||
                    worldX >= WorldMaxX ||
                    worldY < WorldMinY ||
                    worldY >= WorldMaxY)
                {
                    return false;
                }

                int x = Math.Clamp((int)MathF.Floor((worldX - WorldMinX) / SampleStepWorldUnits), 0, Width - 1);
                int y = Math.Clamp((int)MathF.Floor((worldY - WorldMinY) / SampleStepWorldUnits), 0, Height - 1);
                value = Mask[Index(x, y, Width)];
                return true;
            }

            public bool TryResolveNearestLandDistance(
                float worldX,
                float worldY,
                float maxDistanceWorldUnits,
                out float distanceWorldUnits)
            {
                distanceWorldUnits = 0f;
                if (Width <= 0 ||
                    Height <= 0 ||
                    Mask.Length < Width * Height ||
                    !float.IsFinite(worldX) ||
                    !float.IsFinite(worldY) ||
                    !float.IsFinite(maxDistanceWorldUnits) ||
                    worldX < WorldMinX ||
                    worldX >= WorldMaxX ||
                    worldY < WorldMinY ||
                    worldY >= WorldMaxY)
                {
                    return false;
                }

                float maxDistance = MathF.Max(0f, maxDistanceWorldUnits);
                int centerX = Math.Clamp((int)MathF.Floor((worldX - WorldMinX) / SampleStepWorldUnits), 0, Width - 1);
                int centerY = Math.Clamp((int)MathF.Floor((worldY - WorldMinY) / SampleStepWorldUnits), 0, Height - 1);
                if (Mask[Index(centerX, centerY, Width)] == Land)
                {
                    distanceWorldUnits = 0f;
                    return true;
                }

                int cellRadius = Math.Max(1, (int)MathF.Ceiling(maxDistance / SampleStepWorldUnits));
                int minX = Math.Max(0, centerX - cellRadius);
                int maxX = Math.Min(Width - 1, centerX + cellRadius);
                int minY = Math.Max(0, centerY - cellRadius);
                int maxY = Math.Min(Height - 1, centerY + cellRadius);
                float bestDistanceSq = float.PositiveInfinity;
                float maxDistanceSq = maxDistance * maxDistance;

                for (int y = minY; y <= maxY; y++)
                {
                    float sampleY = WorldMinY + ((y + 0.5f) * SampleStepWorldUnits);
                    float dy = sampleY - worldY;
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (Mask[Index(x, y, Width)] != Land)
                        {
                            continue;
                        }

                        float sampleX = WorldMinX + ((x + 0.5f) * SampleStepWorldUnits);
                        float dx = sampleX - worldX;
                        float distanceSq = (dx * dx) + (dy * dy);
                        if (distanceSq < bestDistanceSq)
                        {
                            bestDistanceSq = distanceSq;
                        }
                    }
                }

                if (bestDistanceSq <= maxDistanceSq)
                {
                    float cellHalfDiagonal = SampleStepWorldUnits * 0.70710677f;
                    distanceWorldUnits = MathF.Max(0f, MathF.Sqrt(bestDistanceSq) - cellHalfDiagonal);
                }
                else
                {
                    distanceWorldUnits = maxDistance + TerrainOceanZoneFieldSampleStepWorldUnits;
                }

                return true;
            }
        }

        private sealed class OceanZoneDebugFullMapBuildResult
        {
            public OceanZoneDebugFullMapBuildResult(
                int seed,
                ChunkBounds fullMapWindow,
                List<OceanZoneDebugSegment> segments,
                double buildMilliseconds,
                int suppressedTinyZoneCount)
            {
                Seed = seed;
                FullMapWindow = fullMapWindow;
                Segments = segments ?? new List<OceanZoneDebugSegment>();
                BuildMilliseconds = buildMilliseconds;
                SuppressedTinyZoneCount = Math.Max(0, suppressedTinyZoneCount);
            }

            public int Seed { get; }
            public ChunkBounds FullMapWindow { get; }
            public List<OceanZoneDebugSegment> Segments { get; }
            public double BuildMilliseconds { get; }
            public int SuppressedTinyZoneCount { get; }
        }

        private readonly struct OceanZoneDebugLabelBounds
        {
            public OceanZoneDebugLabelBounds(float minX, float minY, float maxX, float maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public float MinX { get; }
            public float MinY { get; }
            public float MaxX { get; }
            public float MaxY { get; }

            public bool Intersects(OceanZoneDebugLabelBounds other)
            {
                return MaxX >= other.MinX &&
                    MinX <= other.MaxX &&
                    MaxY >= other.MinY &&
                    MinY <= other.MaxY;
            }
        }

        private readonly struct OceanZoneDebugSegment
        {
            public OceanZoneDebugSegment(
                Vector2 from,
                Vector2 to,
                Vector2 normal,
                TerrainWaterType firstSide,
                TerrainWaterType secondSide,
                float threshold)
            {
                From = from;
                To = to;
                Normal = normal;
                FirstSide = firstSide;
                SecondSide = secondSide;
                Threshold = threshold;
            }

            public Vector2 From { get; }
            public Vector2 To { get; }
            public Vector2 Normal { get; }
            public TerrainWaterType FirstSide { get; }
            public TerrainWaterType SecondSide { get; }
            public float Threshold { get; }
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
                ChunkBounds visualChunkWindow,
                ChunkBounds colliderChunkWindow)
            {
                RequestId = requestId;
                MaterializedChunkWindow = materializedChunkWindow;
                VisualChunkWindow = visualChunkWindow;
                ColliderChunkWindow = colliderChunkWindow;
            }

            public int RequestId { get; }
            public ChunkBounds MaterializedChunkWindow { get; }
            public ChunkBounds VisualChunkWindow { get; }
            public ChunkBounds ColliderChunkWindow { get; }
            public List<TerrainVisualObjectRecord> VisualObjects { get; } = new();
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
                int diagnosticId,
                int materializationRequestId,
                TerrainWorldBounds bounds,
                VertexPositionColor[] fillVertices,
                int boundsSignature)
            {
                DiagnosticId = diagnosticId;
                MaterializationRequestId = materializationRequestId;
                Bounds = bounds;
                FillVertices = fillVertices;
                BoundsSignature = boundsSignature;
            }

            public int DiagnosticId { get; }
            public int MaterializationRequestId { get; }
            public TerrainWorldBounds Bounds { get; }
            public VertexPositionColor[] FillVertices { get; }
            public int BoundsSignature { get; }
            public int FillPrimitiveCount => FillVertices?.Length / 3 ?? 0;

            public void Dispose()
            {
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
