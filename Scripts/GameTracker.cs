using System;
using System.Collections.Generic;
using System.Reflection;

namespace op.io
{
    public static class GameTracker
    {
        public static bool FreezeGameInputs { get; internal set; }

        /// <summary>
        /// Explains why FreezeGameInputs is currently true. Set by InputManager
        /// whenever it decides to suppress non-meta controls. Shown as a detail
        /// message in the Backend block so the cause is always visible.
        /// </summary>
        public static string FreezeGameInputsReason { get; internal set; } = string.Empty;

        // UI state — read live from BlockManager
        public static bool DockingMode          => BlockManager.DockingModeEnabled;
        public static bool DisableToolTips      => ControlStateManager.ContainsSwitchState(ControlKeyMigrations.DisableToolTipsKey) &&
                                                   ControlStateManager.GetSwitchState(ControlKeyMigrations.DisableToolTipsKey);
        public static bool GridRequested       => GameRenderer.WorldGridRequested;
        public static bool Grid                => GameRenderer.WorldGridVisible;
        public static bool OceanZoneDebugRequested => GameBlockTerrainBackground.TerrainOceanDebugOverlayRequested;
        public static bool OceanZoneDebugVisible => GameBlockTerrainBackground.TerrainOceanDebugOverlayVisible;
        public static bool BlockMenuOpen        => BlockManager.IsBlockMenuOpen();
        public static bool InputBlocked         => BlockManager.IsInputBlocked();
        public static bool DraggingLayout       => BlockManager.IsDraggingLayout;
        public static bool SuperimposeLocked    => BlockManager.IsSuperimposeLocked;
        public static bool CursorOnGameBlock    => BlockManager.IsCursorWithinGameBlock();
        public static bool NativeWindowResizeEdges => ScreenManager.NativeWindowResizeEdgesEnabled;
        public static bool CustomDockingResizeEdges => ScreenManager.CustomDockingResizeEdgesEnabled;
        public static bool DraggingWindowResize => BlockManager.IsDraggingWindowResize;
        public static string HoveredBlock       => BlockManager.GetHoveredBlockKind();
        public static string HoveredDragBar     => BlockManager.GetHoveredDragBarKind();
        public static string SuperimposeLockTarget => BlockManager.GetSuperimposeLockTargetKind();
        public static string FocusedBlock       => BlockManager.GetFocusedBlockKind()?.ToString() ?? "None";
        public static bool   AnyGUIInteracting  => BlockManager.IsAnyGuiInteracting;
        public static string GUIInteractingWith => BlockManager.GetInteractingBlockKind();
        public static bool WaitingForFocusClick => InputManager.WaitingForFocusClick;
        public static bool WindowGameplayFocus  => InputManager.HasWindowGameplayFocus;
        public static bool WindowChromeLeftClickSuppressed => InputTypeManager.WindowChromeLeftClickSuppressed;
        public static float DoubleTapSuppressionSeconds => InputTypeManager.DoubleTapSuppressionSeconds;
        public static int ShapeRuntimeContentLoadCount => Shape.RuntimeContentLoadCount;
        public static int ShapeDrawSkippedMissingTextureCount => Shape.DrawSkippedMissingTextureCount;
        public static bool LogSessionActive => LogFileHandler.IsSessionActive;
        public static string LogDirectory => LogFileHandler.LogsDirectoryPath;
        public static string LogFile => LogFileHandler.CurrentLogFileName;
        public static string RedFlagsFile => LogFileHandler.CurrentRedFlagsFileName;
        public static int MaxLogFiles => LogFileHandler.MaxLogFiles;
        public static string AmbienceFogOfWarColor => AmbienceSettings.FogOfWarHex;
        public static string AmbienceOceanWaterColor => AmbienceSettings.OceanWaterHex;
        public static string AmbienceOceanWaterLiveColor => AmbienceSettings.CurrentOceanWaterHex;
        public static string AmbienceBackgroundWavesColor => AmbienceSettings.BackgroundWavesHex;
        public static string AmbienceBackgroundWavesLiveColor => AmbienceSettings.CurrentBackgroundWavesHex;
        public static string AmbienceTerrainColor => AmbienceSettings.TerrainHex;
        public static string AmbienceWorldTintColor => AmbienceSettings.WorldTintHex;
        public static string GameLevelActiveName => GameLevelManager.ActiveLevelName;
        public static string GameLevelActiveKey => GameLevelManager.ActiveLevelKey;
        public static int GameLevelCount => GameLevelManager.LevelCount;
        public static int GameLevelReloadCount => GameLevelManager.ReloadCount;
        public static bool GameLevelLoadInProgress => GameLevelManager.LoadInProgress;
        public static bool GameLevelSpawnsPlayer => GameLevelManager.ActiveLevel.SpawnPlayer;
        public static bool GameLevelPlayerSpawnRelocated => GameObjectInitializer.PlayerSpawnRelocated;
        public static float GameLevelPlayerSpawnRelocationDistance => GameObjectInitializer.PlayerSpawnRelocationDistance;
        public static int GameLevelPlayerSpawnSearchAttempts => GameObjectInitializer.PlayerSpawnSearchAttempts;
        public static string GameLevelLoadoutSummary => GameLevelManager.ActiveLevelLoadoutSummary;
        public static string GameLevelTerrainConfiguration => GameLevelManager.ActiveLevelTerrainConfiguration;
        public static string GameLevelOceanZoneConfiguration => GameLevelManager.ActiveLevelOceanZoneConfiguration;
        public static bool GameBlockOceanShaderReady => GameBlockOceanBackground.ShaderReady;
        public static bool GameBlockOceanUsingShaderPath => GameBlockOceanBackground.UsingShaderPath;
        public static string GameBlockOceanShaderStatus => GameBlockOceanBackground.ShaderStatus;
        public static string GameBlockOceanBaseColor => GameBlockOceanBackground.BaseColorRgb;
        public static string GameBlockOceanWaveColor => GameBlockOceanBackground.WaveColorRgb;
        public static string GameBlockOceanDetectedZone => GameBlockOceanBackground.DetectedOceanZone;
        public static string GameBlockOceanPlayerZone => GameBlockOceanBackground.PlayerOceanZone;
        public static string GameBlockOceanPlayerZoneStatus => GameBlockOceanBackground.PlayerOceanZoneStatus;
        public static string GameBlockOceanTargetZone => GameBlockOceanBackground.TargetOceanZone;
        public static string GameBlockOceanCursorZone => GameBlockOceanBackground.CursorOceanZone;
        public static string GameBlockOceanCursorZoneStatus => GameBlockOceanBackground.CursorOceanZoneStatus;
        public static bool GameBlockOceanCursorZoneValid => GameBlockOceanBackground.CursorOceanZoneValid;
        public static float GameBlockOceanCursorZoneDepth => GameBlockOceanBackground.CursorOceanZoneDepth;
        public static float GameBlockOceanCursorZoneOffshoreDistance => GameBlockOceanBackground.CursorOceanZoneOffshoreDistance;
        public static string GameBlockOceanZoneProbeStatus => GameBlockOceanBackground.OceanZoneProbeStatus;
        public static float GameBlockOceanZoneDepth => GameBlockOceanBackground.OceanZoneDepth;
        public static float GameBlockOceanZoneOffshoreDistance => GameBlockOceanBackground.OceanZoneOffshoreDistance;
        public static float GameBlockOceanZoneDarkness => GameBlockOceanBackground.OceanZoneDarkness;
        public static string GameBlockOceanZoneTransitionBanner => GameBlockOceanBackground.OceanZoneTransitionBanner;
        public static bool GameBlockOceanZoneTransitioning => GameBlockOceanBackground.OceanZoneTransitioning;
        public static float GameBlockOceanZoneTransitionProgress => GameBlockOceanBackground.OceanZoneTransitionProgress;
        public static float GameBlockOceanTimeScale => GameBlockOceanBackground.TimeScale;
        public static float GameBlockOceanDownwardSpeed => GameBlockOceanBackground.DownwardSpeed;
        public static float GameBlockOceanBackgroundVariationStrength => GameBlockOceanBackground.BackgroundVariationStrength;
        public static float GameBlockOceanWarpStrengthX => GameBlockOceanBackground.WarpStrengthX;
        public static float GameBlockOceanWarpStrengthY => GameBlockOceanBackground.WarpStrengthY;
        public static float GameBlockOceanCrestBrightness => GameBlockOceanBackground.CrestBrightness;
        public static float GameBlockOceanCrestThickness => GameBlockOceanBackground.CrestThickness;
        public static float GameBlockOceanCrestSegmentation => GameBlockOceanBackground.CrestSegmentation;
        public static float GameBlockOceanCrestDensity => GameBlockOceanBackground.CrestDensity;
        public static float GameBlockOceanWaveSet1Strength => GameBlockOceanBackground.WaveSet1Strength;
        public static float GameBlockOceanWaveSet2Strength => GameBlockOceanBackground.WaveSet2Strength;
        public static float GameBlockOceanWaveSet3Strength => GameBlockOceanBackground.WaveSet3Strength;
        public static float GameBlockOceanPanelScale => GameBlockOceanBackground.PanelScale;
        public static float GameBlockOceanRenderScale => GameBlockOceanBackground.RenderScale;
        public static int GameBlockOceanMaxRenderPixels => GameBlockOceanBackground.MaxRenderPixels;
        public static float GameBlockOceanZoomInfluence => GameBlockOceanBackground.ZoomInfluence;
        public static float GameBlockOceanDetailScale => GameBlockOceanBackground.DetailScale;
        public static float GameBlockOceanLastResolvedRenderScale => GameBlockOceanBackground.LastResolvedRenderScale;
        public static float GameBlockOceanLastCameraZoom => GameBlockOceanBackground.LastCameraZoom;
        public static string GameBlockOceanRenderTextureResolution => GameBlockOceanBackground.RenderTextureResolution;
        public static int TerrainWorldSeed => GameBlockTerrainBackground.TerrainWorldSeed;
        public static int TerrainResidentChunkCount => GameBlockTerrainBackground.TerrainResidentChunkCount;
        public static int TerrainResidentChunkMemoryCap => GameBlockTerrainBackground.TerrainResidentChunkMemoryCap;
        public static int TerrainResidentComponentCount => GameBlockTerrainBackground.TerrainResidentComponentCount;
        public static int TerrainResidentEdgeLoopCount => GameBlockTerrainBackground.TerrainResidentEdgeLoopCount;
        public static int TerrainResidentColliderCount => GameBlockTerrainBackground.TerrainResidentColliderCount;
        public static int TerrainResidentVisualTriangleCount => GameBlockTerrainBackground.TerrainResidentVisualTriangleCount;
        public static int TerrainActiveColliderCount => GameBlockTerrainBackground.TerrainActiveColliderCount;
        public static int TerrainColliderActivationCandidateCount => GameBlockTerrainBackground.TerrainColliderActivationCandidateCount;
        public static int TerrainDynamicCollisionProbeCount => GameBlockTerrainBackground.TerrainDynamicCollisionProbeCount;
        public static int TerrainDynamicCollisionObjectProbeCount => GameBlockTerrainBackground.TerrainDynamicCollisionObjectProbeCount;
        public static int TerrainDynamicCollisionBulletProbeCount => GameBlockTerrainBackground.TerrainDynamicCollisionBulletProbeCount;
        public static int TerrainSpawnRelocationCount => GameBlockTerrainBackground.TerrainSpawnRelocationCount;
        public static int TerrainCollisionIntrusionCorrectionCount => GameBlockTerrainBackground.TerrainCollisionIntrusionCorrectionCount;
        public static int TerrainBulletCollisionCorrectionCount => GameBlockTerrainBackground.TerrainBulletCollisionCorrectionCount;
        public static int TerrainPendingChunkCount => GameBlockTerrainBackground.TerrainPendingChunkCount;
        public static int TerrainPendingCriticalChunkCount => GameBlockTerrainBackground.TerrainPendingCriticalChunkCount;
        public static string TerrainFullMapChunkWindow => GameBlockTerrainBackground.TerrainFullMapChunkWindow;
        public static int TerrainFullMapChunkCount => GameBlockTerrainBackground.TerrainFullMapChunkCount;
        public static int TerrainFullMapGeneratedChunkCount => GameBlockTerrainBackground.TerrainFullMapGeneratedChunkCount;
        public static int TerrainFullMapPendingChunkCount => GameBlockTerrainBackground.TerrainFullMapPendingChunkCount;
        public static bool TerrainFullMapGenerationComplete => GameBlockTerrainBackground.TerrainFullMapGenerationComplete;
        public static bool TerrainFullMapSnapshotReady => GameBlockTerrainBackground.TerrainFullMapSnapshotReady;
        public static int TerrainDiscardedStaleMaterializationCount => GameBlockTerrainBackground.TerrainDiscardedStaleMaterializationCount;
        public static bool TerrainChunkBuildsInFlight => GameBlockTerrainBackground.TerrainChunkBuildsInFlight;
        public static string TerrainBackgroundWorkerStatus => GameBlockTerrainBackground.TerrainBackgroundWorkerStatus;
        public static int TerrainBackgroundQueuedChunkCount => GameBlockTerrainBackground.TerrainBackgroundQueuedChunkCount;
        public static int TerrainBackgroundCompletedChunkQueueCount => GameBlockTerrainBackground.TerrainBackgroundCompletedChunkQueueCount;
        public static int TerrainBackgroundActiveChunkBuildCount => GameBlockTerrainBackground.TerrainBackgroundActiveChunkBuildCount;
        public static bool TerrainMaterializationInFlight => GameBlockTerrainBackground.TerrainMaterializationInFlight;
        public static bool TerrainMaterializationRestartPending => GameBlockTerrainBackground.TerrainMaterializationRestartPending;
        public static double TerrainLastMaterializationMilliseconds => GameBlockTerrainBackground.TerrainLastMaterializationMilliseconds;
        public static bool TerrainStartupVisibleTerrainReady => GameBlockTerrainBackground.TerrainStartupVisibleTerrainReady;
        public static string TerrainStartupReadinessSummary => GameBlockTerrainBackground.TerrainStartupReadinessSummary;
        public static string TerrainStartupPhase => GameBlockTerrainBackground.TerrainStartupPhase;
        public static int TerrainStartupSynchronousChunkBuildCount => GameBlockTerrainBackground.TerrainStartupSynchronousChunkBuildCount;
        public static int TerrainBackgroundQueuedChunkBuildCount => GameBlockTerrainBackground.TerrainBackgroundQueuedChunkBuildCount;
        public static bool TerrainStartupFirstSightTerrainReady => GameBlockTerrainBackground.TerrainStartupFirstSightTerrainReady;
        public static int TerrainStartupWarmupChunkCount => GameBlockTerrainBackground.TerrainStartupWarmupChunkCount;
        public static int TerrainRuntimeFieldCollisionFallbackSuppressedCount => GameBlockTerrainBackground.TerrainRuntimeFieldCollisionFallbackSuppressedCount;
        public static bool TerrainVisibleObjectsDirty => GameBlockTerrainBackground.TerrainVisibleObjectsDirty;
        public static int TerrainDeferredVisibleMaterializationCount => GameBlockTerrainBackground.TerrainDeferredVisibleMaterializationCount;
        public static int TerrainAcceptedDirtyMaterializationCount => GameBlockTerrainBackground.TerrainAcceptedDirtyMaterializationCount;
        public static string TerrainVisibleCoverageStatus => GameBlockTerrainBackground.TerrainVisibleCoverageStatus;
        public static int TerrainFlickerDiagnosticCount => GameBlockTerrainBackground.TerrainFlickerDiagnosticCount;
        public static string TerrainLastFlickerDiagnosticReason => GameBlockTerrainBackground.TerrainLastFlickerDiagnosticReason;
        public static string TerrainLastVisibleDrawSummary => GameBlockTerrainBackground.TerrainLastVisibleDrawSummary;
        public static bool TerrainAccessRequestActive => GameBlockTerrainBackground.TerrainAccessRequestActive;
        public static string TerrainAccessRequestStatus => GameBlockTerrainBackground.TerrainAccessRequestStatus;
        public static int TerrainMovementBlockedUntilReadyCount => GameBlockTerrainBackground.TerrainMovementBlockedUntilReadyCount;
        public static bool TerrainWorldBoundaryActive => GameBlockTerrainBackground.TerrainWorldBoundaryActive;
        public static int TerrainWorldDefaultIslandCount => GameBlockTerrainBackground.TerrainWorldDefaultIslandCount;
        public static float TerrainWorldDefaultMinimumSpacing => GameBlockTerrainBackground.TerrainWorldDefaultMinimumSpacing;
        public static float TerrainWorldDefaultInteractionSpacing => GameBlockTerrainBackground.TerrainWorldDefaultInteractionSpacing;
        public static string TerrainWorldDefaultClusterCountRange => GameBlockTerrainBackground.TerrainWorldDefaultClusterCountRange;
        public static float TerrainChunkWorldSize => GameBlockTerrainBackground.TerrainChunkWorldSize;
        public static float TerrainFeatureWorldScaleMultiplier => GameBlockTerrainBackground.TerrainFeatureWorldScaleMultiplier;
        public static float TerrainArchipelagoMacroCellSize => GameBlockTerrainBackground.TerrainArchipelagoMacroCellSize;
        public static float TerrainArchipelagoSubstrateCellSize => GameBlockTerrainBackground.TerrainArchipelagoSubstrateCellSize;
        public static float TerrainArchipelagoEnclosureCellSize => GameBlockTerrainBackground.TerrainArchipelagoEnclosureCellSize;
        public static float TerrainArchipelagoLandformCellSize => GameBlockTerrainBackground.TerrainArchipelagoLandformCellSize;
        public static string TerrainGenerationPipeline => GameBlockTerrainBackground.TerrainGenerationPipeline;
        public static string TerrainLandformSelectionMode => GameBlockTerrainBackground.TerrainLandformSelectionMode;
        public static string TerrainOceanZoneDistanceMode => GameBlockTerrainBackground.TerrainOceanZoneDistanceMode;
        public static string TerrainOceanZoneOrigin => GameBlockTerrainBackground.TerrainOceanZoneOrigin;
        public static float TerrainOceanZoneOriginRadius => GameBlockTerrainBackground.TerrainOceanZoneOriginRadius;
        public static float TerrainWaterZoneDistanceScale => GameBlockTerrainBackground.TerrainWaterZoneDistanceScale;
        public static float TerrainWaterShallowDistance => GameBlockTerrainBackground.TerrainWaterShallowDistance;
        public static float TerrainWaterSunlitDistance => GameBlockTerrainBackground.TerrainWaterSunlitDistance;
        public static float TerrainWaterTwilightDistance => GameBlockTerrainBackground.TerrainWaterTwilightDistance;
        public static float TerrainWaterMidnightDistance => GameBlockTerrainBackground.TerrainWaterMidnightDistance;
        public static float TerrainOceanZoneMinimumTransitionVolumeDistance => GameBlockTerrainBackground.TerrainOceanZoneMinimumTransitionVolumeDistance;
        public static string TerrainLagoonOpeningTarget => GameBlockTerrainBackground.TerrainLagoonOpeningTarget;
        public static float TerrainLagoonBasinCutStrength => GameBlockTerrainBackground.TerrainLagoonBasinCutStrength;
        public static float TerrainRegionalTidalChannelCutStrength => GameBlockTerrainBackground.TerrainRegionalTidalChannelCutStrength;
        public static int TerrainContourResolutionMultiplier => GameBlockTerrainBackground.TerrainContourResolutionMultiplierSetting;
        public static int TerrainTargetVisualTextureOversample => GameBlockTerrainBackground.TerrainTargetVisualTextureOversample;
        public static float TerrainOctogonalCornerCutCellRatio => GameBlockTerrainBackground.TerrainOctogonalCornerCutCellRatio;
        public static float TerrainPreloadMarginWorldUnits => GameBlockTerrainBackground.TerrainPreloadMarginWorldUnits;
        public static string TerrainWorldBounds => GameBlockTerrainBackground.TerrainWorldBoundsSummary;
        public static string TerrainSeedAnchor => GameBlockTerrainBackground.TerrainSeedAnchor;
        public static string TerrainStreamingFocus => GameBlockTerrainBackground.TerrainStreamingFocus;
        public static string TerrainStreamingLandformSignature => GameBlockTerrainBackground.TerrainStreamingLandformSignature;
        public static string TerrainCenterChunk => GameBlockTerrainBackground.TerrainCenterChunk;
        public static string TerrainVisibleChunkWindow => GameBlockTerrainBackground.TerrainVisibleChunkWindow;
        public static string TerrainTargetVisualChunkWindow => GameBlockTerrainBackground.TerrainTargetVisualChunkWindow;
        public static string TerrainTargetMaterializedChunkWindow => GameBlockTerrainBackground.TerrainTargetMaterializedChunkWindow;
        public static string TerrainAppliedVisualChunkWindow => GameBlockTerrainBackground.TerrainAppliedVisualChunkWindow;
        public static string TerrainAppliedColliderChunkWindow => GameBlockTerrainBackground.TerrainAppliedColliderChunkWindow;
        public static string TerrainColliderChunkWindow => GameBlockTerrainBackground.TerrainColliderChunkWindow;
        public static float TerrainWorldScaleMultiplier => GameBlockTerrainBackground.TerrainWorldScaleMultiplier;
        public static int TerrainOceanDebugBorderSegmentCount => GameBlockTerrainBackground.TerrainOceanDebugBorderSegmentCount;
        public static int TerrainOceanDebugBorderLabelCount => GameBlockTerrainBackground.TerrainOceanDebugBorderLabelCount;
        public static double TerrainOceanDebugBuildMilliseconds => GameBlockTerrainBackground.TerrainOceanDebugBuildMilliseconds;
        public static bool TerrainOceanDebugFullMapReady => GameBlockTerrainBackground.TerrainOceanDebugFullMapReady;
        public static int TerrainOceanDebugFullMapSegmentCount => GameBlockTerrainBackground.TerrainOceanDebugFullMapSegmentCount;
        public static double TerrainOceanDebugFullMapBuildMilliseconds => GameBlockTerrainBackground.TerrainOceanDebugFullMapBuildMilliseconds;
        public static string TerrainOceanDebugFullMapStatus => GameBlockTerrainBackground.TerrainOceanDebugFullMapStatus;
        public static int TerrainOceanDebugSuppressedTinyZoneCount => GameBlockTerrainBackground.TerrainOceanDebugSuppressedTinyZoneCount;
        public static float TerrainOceanDebugMinimumStableZoneRadius => GameBlockTerrainBackground.TerrainOceanDebugMinimumStableZoneRadius;
        public static string TerrainOceanDebugTinyZoneViolationSummary => GameBlockTerrainBackground.TerrainOceanDebugTinyZoneViolationSummary;
        public static int TerrainOceanDebugTileCacheCount => GameBlockTerrainBackground.TerrainOceanDebugTileCacheCount;
        public static int TerrainOceanDebugQueuedTileCount => GameBlockTerrainBackground.TerrainOceanDebugQueuedTileCount;
        public static int TerrainOceanDebugActiveTileBuildCount => GameBlockTerrainBackground.TerrainOceanDebugActiveTileBuildCount;
        public static int TerrainOceanDebugCompletedTileQueueCount => GameBlockTerrainBackground.TerrainOceanDebugCompletedTileQueueCount;
        public static int TerrainOceanDebugQueuedTileBuildCount => GameBlockTerrainBackground.TerrainOceanDebugQueuedTileBuildCount;
        public static string TerrainOceanDebugWorkerStatus => GameBlockTerrainBackground.TerrainOceanDebugWorkerStatus;

        /// <summary>
        /// Shows the DockBlockCategory (Standard / Overlay / Dynamic) of the
        /// currently hovered or focused block so developers can inspect block types
        /// at runtime.
        /// </summary>
        public static string BlockType => BlockManager.GetFocusedBlockCategory();
        public static string EnumDisabledOptions => ControlStateManager.GetAllEnumDisabledOptionsSummary();

        // ── Constants (from MathBlock) ────────────────────────────────────────
        // Bullet Physics (DB-driven)
        public static float AirResistanceScalar     => BulletManager.AirResistanceScalar;
        public static float BounceVelocityLoss      => BulletManager.BounceVelocityLoss;
        public static float HitVelocityLoss         => BulletManager.HitVelocityLoss;
        public static float PenetrationSpring       => BulletManager.PenetrationSpringCoeff;
        public static float PenetrationDamping      => BulletManager.PenetrationDamping;
        // Scalars (DB-driven)
        public static float BulletRadiusScalar          => BulletManager.BulletRadiusScalar;
        public static float BarrelHeightScalar          => BulletManager.BarrelHeightScalar;
        public static float BulletKnockbackScalar       => BulletManager.BulletKnockbackScalar;
        public static float BulletRecoilScalar          => BulletManager.BulletRecoilScalar;
        public static float BulletDynamicKnockbackScalar => BulletManager.BulletDynamicKnockbackScalar;
        public static float BulletFarmKnockbackScalar   => BulletManager.BulletFarmKnockbackScalar;
        public static int BulletActiveCount             => BulletManager.ActiveBulletCount;
        public static int BulletBarrelLockedCount       => BulletManager.BarrelLockedBulletCount;
        public static int BulletCollisionReadyCount     => BulletManager.CollisionReadyBulletCount;
        public static int WorldRenderRegisteredObjectCount => ShapeManager.RegisteredWorldObjectCount;
        public static int WorldRenderDrawnObjectCount => ShapeManager.DrawnWorldObjectCount;
        // XP clump runtime telemetry
        public static int   XPClumpCount                => XPClumpManager.ActiveClumpCount;
        public static int   XPUnstableClumpCount        => XPClumpManager.ActiveUnstableClumpCount;
        public static int   PendingFarmXPDrops          => XPClumpManager.PendingDropCount;
        public static int   XPClumpsAbsorbedThisSecond  => XPClumpManager.AbsorbedThisSecond;
        public static int   XPClumpPickupPerSecond      => XPClumpManager.PickupPerSecond;
        public static float XPClumpDeadZoneRadius       => XPClumpManager.DeadZoneRadius;
        public static float XPClumpPullZoneRadius       => XPClumpManager.PullZoneRadius;
        public static float XPClumpAbsorbZoneRadius     => XPClumpManager.AbsorbZoneRadius;
        public static float XPClumpClusterHomeostasisVariance => XPClumpManager.ClusterHomeostasisVariance;
        public static float XPClumpClusterInstabilityForce => XPClumpManager.ClusterInstabilityForce;
        public static float XPClumpClusterInstabilityPulseHz => XPClumpManager.ClusterInstabilityPulseHz;
        public static int   XPPlayerUnstablePreviewClumpCount => XPClumpManager.PlayerUnstablePreviewClumpCount;
        public static float XPPlayerUnstablePreviewHealthRatio => XPClumpManager.PlayerUnstablePreviewHealthRatio;
        public static float XPPlayerUnstablePreviewRewardXP => XPClumpManager.PlayerUnstablePreviewRewardXP;
        public static bool  XPPlayerUnstablePreviewEligible => XPClumpManager.PlayerUnstablePreviewEligible;
        // Bullet Defaults (DB-driven)
        public static float DefaultBulletSpeed      => BulletManager.DefaultBulletSpeed;
        public static float DefaultBulletLifespan   => BulletManager.DefaultBulletLifespan;
        public static float DefaultDragFactor       => BulletManager.DefaultBulletDragFactor;
        public static float DefaultBulletMass       => BulletManager.DefaultBulletMass;
        public static float DefaultBulletDamage     => BulletManager.DefaultBulletDamage;
        public static float DefaultBulletPenHP      => BulletManager.DefaultBulletHealth;
        // Physics (DB-driven)
        public static float PhysicsFrictionRate     => PhysicsManager.FrictionRate;
        public static float CollisionBounceMomentumTransfer => CollisionResolver.CollisionBounceMomentumTransfer;
        public static int PhysicsBroadPhaseActiveCollidableCount => CollisionResolver.BroadPhaseActiveCollidableCount;
        public static int PhysicsBroadPhaseCandidatePairCount => CollisionResolver.BroadPhaseCandidatePairCount;
        public static int PhysicsStartupOverlapResolvedPairCount => CollisionResolver.StartupOverlapResolvedPairCount;
        public static int PhysicsStartupOverlapIterationCount => CollisionResolver.StartupOverlapIterationCount;
        // Code-defined constants
        public static string AngularAccelFactor     => "4";
        public static string BarrelSwitchSpeed      => "15 /s";
        public static int    ActiveBodyIndex       => Core.Instance?.PlayerOrNull?.ActiveBodyIndex ?? 0;
        public static string ActiveBodyName        => Core.Instance?.PlayerOrNull is Agent p && p.BodyCount > 0
            ? (p.Bodies[p.ActiveBodyIndex].Name ?? $"Body {p.ActiveBodyIndex + 1}")
            : "None";
        public static bool   PlayerDeadOrDying     => Core.Instance?.PlayerOrNull?.IsDeadOrDying ?? false;
        public static bool   PlayerGameplayInputSuppressed => InputManager.IsPlayerGameplayInputSuppressed;
        public static bool   BodyTransitionAnimating => Core.Instance?.PlayerOrNull?.BodyTransitionAnimating ?? false;
        public static float  BodyTransitionProgress => Core.Instance?.PlayerOrNull?.BodyTransitionProgress ?? 0f;
        public static float  BodyTransitionCooldownRemaining => Core.Instance?.PlayerOrNull?.BodyTransitionCooldownRemaining ?? 0f;
        public static float  BodyTransitionDurationSeconds => Agent.BodyTransitionDurationSeconds;
        public static float  BodyTransitionBufferSeconds => Agent.BodyTransitionBufferSeconds;
        public static bool   YourBarRevealActive => HealthBarManager.YourBarRevealActive;
        public static bool   YourBarControlSwitchMode => HealthBarManager.YourBarControlSwitchMode;
        public static float  YourBarRevealRemainingSeconds => HealthBarManager.YourBarRevealRemainingSeconds;
        public static float  YourBarRevealSeconds => HealthBarManager.YourBarRevealSeconds;
        public static bool   YourBarVisible => HealthBarManager.YourBarVisible;
        public static float  YourBarVisibilityAlpha => HealthBarManager.YourBarVisibilityAlpha;
        public static float  PlayerSightRadius     => FogOfWarManager.PlayerSightRadius;
        public static bool   FogOfWarEnabled       => FogOfWarManager.IsFogEnabled;
        public static bool   FogOfWarActive        => FogOfWarManager.IsFogActive;
        public static int    FogVisionSourceCount  => FogOfWarManager.ActiveVisionSourceCount;
        public static float  FogColorIntensity     => FogOfWarManager.FogColorIntensity;
        public static float  FogBodyDetailMaskScale => FogOfWarManager.FogBodyDetailMaskScale;
        public static int    FogBodyDetailMaskResolution => FogOfWarManager.FogBodyDetailMaskResolution;
        public static bool   FogFrontierCacheFromDisk => FogOfWarManager.FrontierCacheLoadedFromDisk;
        public static int    FogFrontierDirectionBin  => FogOfWarManager.ActiveFrontierDirectionBin;
        public static int    FogFrontierPhaseIndex    => FogOfWarManager.ActiveFrontierPhaseIndex;
        public static int    FogFrontierTextureResolution => FogOfWarManager.FrontierTextureResolution;
        public static int    FogFrontierActiveTextureResolution => FogOfWarManager.FrontierActiveTextureResolution;
        public static int    FogFrontierPhaseTotal    => FogOfWarManager.FrontierPhaseTotal;
        public static float  FogFrontierAnimationPhase => FogOfWarManager.FrontierAnimationPhase;
        public static float  FogFrontierFramesPerCentifoot => FogOfWarManager.FrontierFramesPerCentifootRate;
        public static float  FogFrontierTargetUpdateIntervalSeconds => FogOfWarManager.FrontierTargetUpdateIntervalSeconds;
        public static float  FogFrontierBuildMsLast   => FogOfWarManager.FrontierBuildMsLast;
        public static float  FogFrontierBuildMsAvg    => FogOfWarManager.FrontierBuildMsAvg;
        public static float  FogFrontierBorderThickness => FogOfWarManager.FrontierBorderThickness;
        public static float  FogFrontierFieldSmoothingRadius => FogOfWarManager.FrontierFieldSmoothingRadius;
        public static bool   FogFrontierBuildInFlight => FogOfWarManager.FrontierBuildInFlight;
        public static float  CentifootWorldUnits   => CentifootUnits.WorldUnitsPerCentifoot;
        public static string DistanceUnit          => CentifootUnits.UnitName;

        public static IReadOnlyList<GameTrackerVariable> GetTrackedVariables()
        {
            List<GameTrackerVariable> variables = new();
            Type trackerType = typeof(GameTracker);

            foreach (PropertyInfo property in trackerType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (!property.CanRead) continue;

                // Skip FreezeGameInputsReason — it's surfaced as the Detail of FreezeGameInputs instead.
                if (string.Equals(property.Name, nameof(FreezeGameInputsReason), StringComparison.OrdinalIgnoreCase))
                    continue;

                // Default bullet settings are static DB config, not runtime state — hide from Backend block.
                if (property.Name.StartsWith("Default", StringComparison.Ordinal))
                    continue;

                object value;
                try { value = property.GetValue(null); }
                catch { continue; }

                string detail = string.Equals(property.Name, nameof(FreezeGameInputs), StringComparison.OrdinalIgnoreCase)
                    ? FreezeGameInputsReason
                    : string.Empty;

                object displayValue = FormatTrackedValue(property.Name, value);
                string category = GetVariableCategory(property.Name);
                variables.Add(new GameTrackerVariable(property.Name, displayValue, detail, category));
            }

            foreach (FieldInfo field in trackerType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object value = field.GetValue(null);
                string category = GetVariableCategory(field.Name);
                variables.Add(new GameTrackerVariable(field.Name, value, string.Empty, category));
            }

            variables.Sort((left, right) =>
            {
                int cmp = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }

                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
            return variables;
        }

        public readonly struct GameTrackerVariable
        {
            public GameTrackerVariable(string name, object value, string detail = null, string category = null)
            {
                Name   = name;
                Value  = value;
                Detail = detail ?? string.Empty;
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            }

            public string Name      { get; }
            public object Value     { get; }
            /// <summary>Optional detail/reason message shown in the Backend message column.</summary>
            public string Detail    { get; }
            public string Category  { get; }
            public bool   IsBoolean => Value is bool;
        }

        private static string GetVariableCategory(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return "General";
            }

            if (variableName.StartsWith("Fog", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, nameof(PlayerSightRadius), StringComparison.OrdinalIgnoreCase))
            {
                return "Fog";
            }

            if (variableName.StartsWith("Ambience", StringComparison.OrdinalIgnoreCase))
            {
                return "Ambience";
            }

            if (variableName.StartsWith("GameLevel", StringComparison.OrdinalIgnoreCase))
            {
                return "Level";
            }

            if (variableName.StartsWith("Terrain", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("OceanZoneDebug", StringComparison.OrdinalIgnoreCase))
            {
                return "Terrain";
            }

            if (variableName.StartsWith("XP", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("PendingFarm", StringComparison.OrdinalIgnoreCase))
            {
                return "XP";
            }

            if (variableName.StartsWith("Bullet", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("AirResistance", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Bounce", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("HitVelocity", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Penetration", StringComparison.OrdinalIgnoreCase))
            {
                return "Combat";
            }

            if (variableName.StartsWith("Physics", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Collision", StringComparison.OrdinalIgnoreCase))
            {
                return "Physics";
            }

            if (variableName.StartsWith("WorldRender", StringComparison.OrdinalIgnoreCase))
            {
                return "UI";
            }

            if (variableName.StartsWith("Log", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("RedFlags", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("MaxLog", StringComparison.OrdinalIgnoreCase))
            {
                return "Debug";
            }

            if (variableName.StartsWith("Window", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Block", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Dock", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Hovered", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("GUI", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Input", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Freeze", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("AnyGUI", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("GameBlockOcean", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Cursor", StringComparison.OrdinalIgnoreCase))
            {
                return "UI";
            }

            if (variableName.StartsWith("ActiveBody", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("BodyTransition", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("YourBar", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Centifoot", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("DistanceUnit", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("Angular", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("BarrelSwitch", StringComparison.OrdinalIgnoreCase))
            {
                return "Player";
            }

            return "General";
        }

        private static object FormatTrackedValue(string variableName, object value)
        {
            if (value == null || !TryGetFloat(value, out float numericValue))
            {
                return value;
            }

            return variableName switch
            {
                nameof(PlayerSightRadius) => CentifootUnits.FormatDistance(numericValue),
                nameof(GameBlockOceanZoneDepth) => CentifootUnits.FormatNumber(numericValue, "0.00"),
                nameof(GameBlockOceanZoneOffshoreDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(GameBlockOceanCursorZoneDepth) => CentifootUnits.FormatNumber(numericValue, "0.00"),
                nameof(GameBlockOceanCursorZoneOffshoreDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(GameLevelPlayerSpawnRelocationDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainOceanDebugBuildMilliseconds) => CentifootUnits.FormatNumber(numericValue, "0.00"),
                nameof(TerrainChunkWorldSize) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainPreloadMarginWorldUnits) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainWorldDefaultMinimumSpacing) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainWorldDefaultInteractionSpacing) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainWaterShallowDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainWaterSunlitDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainWaterTwilightDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainWaterMidnightDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainOceanZoneMinimumTransitionVolumeDistance) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainOceanZoneOriginRadius) => CentifootUnits.FormatDistance(numericValue),
                nameof(XPClumpDeadZoneRadius) => CentifootUnits.FormatDistance(numericValue),
                nameof(XPClumpPullZoneRadius) => CentifootUnits.FormatDistance(numericValue),
                nameof(XPClumpAbsorbZoneRadius) => CentifootUnits.FormatDistance(numericValue),
                _ => value
            };
        }

        private static bool TryGetFloat(object value, out float numericValue)
        {
            switch (value)
            {
                case float f:
                    numericValue = f;
                    return true;
                case double d:
                    numericValue = (float)d;
                    return true;
                case int i:
                    numericValue = i;
                    return true;
                case long l:
                    numericValue = l;
                    return true;
                case decimal m:
                    numericValue = (float)m;
                    return true;
                default:
                    numericValue = 0f;
                    return false;
            }
        }
    }
}
