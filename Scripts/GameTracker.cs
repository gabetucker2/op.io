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
        public static bool LogSessionActive => LogFileHandler.IsSessionActive;
        public static string LogDirectory => LogFileHandler.LogsDirectoryPath;
        public static string LogFile => LogFileHandler.CurrentLogFileName;
        public static string RedFlagsFile => LogFileHandler.CurrentRedFlagsFileName;
        public static int MaxLogFiles => LogFileHandler.MaxLogFiles;
        public static string AmbienceFogOfWarColor => AmbienceSettings.FogOfWarHex;
        public static string AmbienceOceanWaterColor => AmbienceSettings.OceanWaterHex;
        public static string AmbienceBackgroundWavesColor => AmbienceSettings.BackgroundWavesHex;
        public static string AmbienceTerrainColor => AmbienceSettings.TerrainHex;
        public static string AmbienceWorldTintColor => AmbienceSettings.WorldTintHex;
        public static bool GameBlockOceanShaderReady => GameBlockOceanBackground.ShaderReady;
        public static bool GameBlockOceanUsingShaderPath => GameBlockOceanBackground.UsingShaderPath;
        public static string GameBlockOceanShaderStatus => GameBlockOceanBackground.ShaderStatus;
        public static string GameBlockOceanBaseColor => GameBlockOceanBackground.BaseColorRgb;
        public static string GameBlockOceanWaveColor => GameBlockOceanBackground.WaveColorRgb;
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
        public static int TerrainResidentComponentCount => GameBlockTerrainBackground.TerrainResidentComponentCount;
        public static int TerrainResidentEdgeLoopCount => GameBlockTerrainBackground.TerrainResidentEdgeLoopCount;
        public static int TerrainResidentColliderCount => GameBlockTerrainBackground.TerrainResidentColliderCount;
        public static int TerrainResidentVisualTriangleCount => GameBlockTerrainBackground.TerrainResidentVisualTriangleCount;
        public static int TerrainActiveColliderCount => GameBlockTerrainBackground.TerrainActiveColliderCount;
        public static int TerrainColliderActivationCandidateCount => GameBlockTerrainBackground.TerrainColliderActivationCandidateCount;
        public static int TerrainSpawnRelocationCount => GameBlockTerrainBackground.TerrainSpawnRelocationCount;
        public static int TerrainCollisionIntrusionCorrectionCount => GameBlockTerrainBackground.TerrainCollisionIntrusionCorrectionCount;
        public static int TerrainPendingChunkCount => GameBlockTerrainBackground.TerrainPendingChunkCount;
        public static int TerrainPendingCriticalChunkCount => GameBlockTerrainBackground.TerrainPendingCriticalChunkCount;
        public static int TerrainDiscardedStaleMaterializationCount => GameBlockTerrainBackground.TerrainDiscardedStaleMaterializationCount;
        public static bool TerrainChunkBuildsInFlight => GameBlockTerrainBackground.TerrainChunkBuildsInFlight;
        public static bool TerrainMaterializationInFlight => GameBlockTerrainBackground.TerrainMaterializationInFlight;
        public static bool TerrainMaterializationRestartPending => GameBlockTerrainBackground.TerrainMaterializationRestartPending;
        public static double TerrainLastMaterializationMilliseconds => GameBlockTerrainBackground.TerrainLastMaterializationMilliseconds;
        public static bool TerrainStartupVisibleTerrainReady => GameBlockTerrainBackground.TerrainStartupVisibleTerrainReady;
        public static string TerrainStartupReadinessSummary => GameBlockTerrainBackground.TerrainStartupReadinessSummary;
        public static float TerrainChunkWorldSize => GameBlockTerrainBackground.TerrainChunkWorldSize;
        public static float TerrainFeatureWorldScaleMultiplier => GameBlockTerrainBackground.TerrainFeatureWorldScaleMultiplier;
        public static float TerrainArchipelagoMacroCellSize => GameBlockTerrainBackground.TerrainArchipelagoMacroCellSize;
        public static float TerrainArchipelagoSubstrateCellSize => GameBlockTerrainBackground.TerrainArchipelagoSubstrateCellSize;
        public static float TerrainArchipelagoEnclosureCellSize => GameBlockTerrainBackground.TerrainArchipelagoEnclosureCellSize;
        public static float TerrainArchipelagoLandformCellSize => GameBlockTerrainBackground.TerrainArchipelagoLandformCellSize;
        public static string TerrainGenerationPipeline => GameBlockTerrainBackground.TerrainGenerationPipeline;
        public static string TerrainLandformSelectionMode => GameBlockTerrainBackground.TerrainLandformSelectionMode;
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
        public static string TerrainColliderChunkWindow => GameBlockTerrainBackground.TerrainColliderChunkWindow;

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
        public static float OwnerImmunityDuration       => BulletManager.OwnerImmunityDuration;
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
        public static int    ActiveBodyIndex       => Core.Instance?.Player?.ActiveBodyIndex ?? 0;
        public static string ActiveBodyName        => Core.Instance?.Player is Agent p && p.BodyCount > 0
            ? (p.Bodies[p.ActiveBodyIndex].Name ?? $"Body {p.ActiveBodyIndex + 1}")
            : "None";
        public static bool   PlayerDeadOrDying     => Core.Instance?.Player?.IsDeadOrDying ?? false;
        public static bool   PlayerGameplayInputSuppressed => InputManager.IsPlayerGameplayInputSuppressed;
        public static bool   BodyTransitionAnimating => Core.Instance?.Player?.BodyTransitionAnimating ?? false;
        public static float  BodyTransitionProgress => Core.Instance?.Player?.BodyTransitionProgress ?? 0f;
        public static float  BodyTransitionCooldownRemaining => Core.Instance?.Player?.BodyTransitionCooldownRemaining ?? 0f;
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

            if (variableName.StartsWith("Terrain", StringComparison.OrdinalIgnoreCase))
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
                nameof(TerrainChunkWorldSize) => CentifootUnits.FormatDistance(numericValue),
                nameof(TerrainPreloadMarginWorldUnits) => CentifootUnits.FormatDistance(numericValue),
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
