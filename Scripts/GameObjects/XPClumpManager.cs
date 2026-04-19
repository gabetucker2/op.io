using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Handles XP drops and clumps:
    /// - unstable clumps (low-health warning on destructible drop sources)
    /// - free clumps (pickup clumps after death, including dead/pulled/locked states)
    /// </summary>
    public static class XPClumpManager
    {
        private readonly struct ClumpSettings
        {
            public ClumpSettings(
                int pickupPerSecond,
                float deadZoneRadius,
                float pullZoneRadius,
                float absorbZoneRadius,
                float deadZoneStartSeconds,
                float deadZoneDespawnSeconds,
                float pullSpeedMin,
                float pullSpeedMax,
                float pullVelocityLerpPerSecond,
                float clumpRadius,
                float spawnSpreadRadius,
                float spawnInitialSpeed,
                float maxSpeed,
                float velocityDampingPerSecond,
                float clusterRadius,
                float clusterAttractForce,
                float clusterHomeostasisDistance,
                float clusterRepelForce,
                float visualMergeRadius,
                float visualMergeGrowth,
                float visualMergeMaxScale,
                float absorbConsumeDistance,
                float absorbFadeSeconds,
                float absorbConsumeGrowMaxScale,
                float unstableHealthThresholdRatio,
                float unstableJitterAccel,
                float unstableCenterPullAccel,
                float unstableVelocityDampingPerSecond,
                float unstableMaxSpeed,
                float unstableAlphaMin,
                float unstableAlphaMax,
                float unstableAlphaPulseHz,
                float unstableRadiusScale,
                float unstableRenderScale,
                float unstableFadeOutSeconds,
                float absorbOrbitMaxAngularSpeedDeg,
                float absorbOrbitAngularBlendPerSecond,
                float absorbOrbitAngularDampingPerSecond,
                float absorbOrbitCollapseSpeed,
                float absorbConsumeCollapseSpeed,
                float absorbOrbitFollowGain,
                float absorbVelocityLerpPerSecond,
                float absorbOrbitTangentialVelocityWeight,
                float absorbOrbitInitialRadiusFactor,
                float absorbOrbitMinRadiusFactor,
                float absorbOrbitMinRadiusAbsolute,
                float absorbOrbitCollapseBoostLow,
                float absorbOrbitCollapseBoostHigh,
                float absorbOrbitInwardCollapseFactor,
                float absorbOrbitMaxInwardSpeedMin,
                float absorbOrbitMaxInwardSpeedPullScale,
                float absorbConsumeExtraInwardFactor,
                float coreHighlightScale,
                float coreHighlightAlphaScale,
                float glowScale,
                float glowAlphaScale,
                float shadowScale,
                float shadowAlphaScale,
                float deadPulseSpeedMin,
                float deadPulseSpeedMax,
                float deadPulseLowAlphaStart,
                float deadPulseLowAlphaEnd,
                float consumeGrowthExponent,
                float visualMergeScaleLerpPerSecond,
                float unstableShowLerpPerSecond,
                float unstableVisibilityEpsilon,
                float unstableBurstRatePerSecond,
                float unstableRandomKickFactorMin,
                float unstableRandomKickFactorMax,
                float unstableTangentialKickBaseFactor,
                float unstableTangentialKickPressureFactor,
                float unstableCenterPullBaseFactor,
                float unstableCenterPullPressureFactor,
                float unstableBurstFactorMin,
                float unstableBurstFactorMax,
                float unstableBoundaryBounceFactor,
                float unstableBoundaryTangentialFactor,
                float unstableAlphaNoiseAmplitude,
                float unstableAlphaPulseWeight,
                float unstableAlphaPressureWeight,
                float unstableAlphaLerpPerSecond)
            {
                PickupPerSecond = pickupPerSecond;
                DeadZoneRadius = deadZoneRadius;
                PullZoneRadius = pullZoneRadius;
                AbsorbZoneRadius = absorbZoneRadius;
                DeadZoneStartSeconds = deadZoneStartSeconds;
                DeadZoneDespawnSeconds = deadZoneDespawnSeconds;
                PullSpeedMin = pullSpeedMin;
                PullSpeedMax = pullSpeedMax;
                PullVelocityLerpPerSecond = pullVelocityLerpPerSecond;
                ClumpRadius = clumpRadius;
                SpawnSpreadRadius = spawnSpreadRadius;
                SpawnInitialSpeed = spawnInitialSpeed;
                MaxSpeed = maxSpeed;
                VelocityDampingPerSecond = velocityDampingPerSecond;
                ClusterRadius = clusterRadius;
                ClusterAttractForce = clusterAttractForce;
                ClusterHomeostasisDistance = clusterHomeostasisDistance;
                ClusterRepelForce = clusterRepelForce;
                VisualMergeRadius = visualMergeRadius;
                VisualMergeGrowth = visualMergeGrowth;
                VisualMergeMaxScale = visualMergeMaxScale;
                AbsorbConsumeDistance = absorbConsumeDistance;
                AbsorbFadeSeconds = absorbFadeSeconds;
                AbsorbConsumeGrowMaxScale = absorbConsumeGrowMaxScale;
                UnstableHealthThresholdRatio = unstableHealthThresholdRatio;
                UnstableJitterAccel = unstableJitterAccel;
                UnstableCenterPullAccel = unstableCenterPullAccel;
                UnstableVelocityDampingPerSecond = unstableVelocityDampingPerSecond;
                UnstableMaxSpeed = unstableMaxSpeed;
                UnstableAlphaMin = unstableAlphaMin;
                UnstableAlphaMax = unstableAlphaMax;
                UnstableAlphaPulseHz = unstableAlphaPulseHz;
                UnstableRadiusScale = unstableRadiusScale;
                UnstableRenderScale = unstableRenderScale;
                UnstableFadeOutSeconds = unstableFadeOutSeconds;
                AbsorbOrbitMaxAngularSpeedDeg = absorbOrbitMaxAngularSpeedDeg;
                AbsorbOrbitAngularBlendPerSecond = absorbOrbitAngularBlendPerSecond;
                AbsorbOrbitAngularDampingPerSecond = absorbOrbitAngularDampingPerSecond;
                AbsorbOrbitCollapseSpeed = absorbOrbitCollapseSpeed;
                AbsorbConsumeCollapseSpeed = absorbConsumeCollapseSpeed;
                AbsorbOrbitFollowGain = absorbOrbitFollowGain;
                AbsorbVelocityLerpPerSecond = absorbVelocityLerpPerSecond;
                AbsorbOrbitTangentialVelocityWeight = absorbOrbitTangentialVelocityWeight;
                AbsorbOrbitInitialRadiusFactor = absorbOrbitInitialRadiusFactor;
                AbsorbOrbitMinRadiusFactor = absorbOrbitMinRadiusFactor;
                AbsorbOrbitMinRadiusAbsolute = absorbOrbitMinRadiusAbsolute;
                AbsorbOrbitCollapseBoostLow = absorbOrbitCollapseBoostLow;
                AbsorbOrbitCollapseBoostHigh = absorbOrbitCollapseBoostHigh;
                AbsorbOrbitInwardCollapseFactor = absorbOrbitInwardCollapseFactor;
                AbsorbOrbitMaxInwardSpeedMin = absorbOrbitMaxInwardSpeedMin;
                AbsorbOrbitMaxInwardSpeedPullScale = absorbOrbitMaxInwardSpeedPullScale;
                AbsorbConsumeExtraInwardFactor = absorbConsumeExtraInwardFactor;
                CoreHighlightScale = coreHighlightScale;
                CoreHighlightAlphaScale = coreHighlightAlphaScale;
                GlowScale = glowScale;
                GlowAlphaScale = glowAlphaScale;
                ShadowScale = shadowScale;
                ShadowAlphaScale = shadowAlphaScale;
                DeadPulseSpeedMin = deadPulseSpeedMin;
                DeadPulseSpeedMax = deadPulseSpeedMax;
                DeadPulseLowAlphaStart = deadPulseLowAlphaStart;
                DeadPulseLowAlphaEnd = deadPulseLowAlphaEnd;
                ConsumeGrowthExponent = consumeGrowthExponent;
                VisualMergeScaleLerpPerSecond = visualMergeScaleLerpPerSecond;
                UnstableShowLerpPerSecond = unstableShowLerpPerSecond;
                UnstableVisibilityEpsilon = unstableVisibilityEpsilon;
                UnstableBurstRatePerSecond = unstableBurstRatePerSecond;
                UnstableRandomKickFactorMin = unstableRandomKickFactorMin;
                UnstableRandomKickFactorMax = unstableRandomKickFactorMax;
                UnstableTangentialKickBaseFactor = unstableTangentialKickBaseFactor;
                UnstableTangentialKickPressureFactor = unstableTangentialKickPressureFactor;
                UnstableCenterPullBaseFactor = unstableCenterPullBaseFactor;
                UnstableCenterPullPressureFactor = unstableCenterPullPressureFactor;
                UnstableBurstFactorMin = unstableBurstFactorMin;
                UnstableBurstFactorMax = unstableBurstFactorMax;
                UnstableBoundaryBounceFactor = unstableBoundaryBounceFactor;
                UnstableBoundaryTangentialFactor = unstableBoundaryTangentialFactor;
                UnstableAlphaNoiseAmplitude = unstableAlphaNoiseAmplitude;
                UnstableAlphaPulseWeight = unstableAlphaPulseWeight;
                UnstableAlphaPressureWeight = unstableAlphaPressureWeight;
                UnstableAlphaLerpPerSecond = unstableAlphaLerpPerSecond;
            }

            public int PickupPerSecond { get; }
            public float DeadZoneRadius { get; }
            public float PullZoneRadius { get; }
            public float AbsorbZoneRadius { get; }
            public float DeadZoneStartSeconds { get; }
            public float DeadZoneDespawnSeconds { get; }
            public float PullSpeedMin { get; }
            public float PullSpeedMax { get; }
            public float PullVelocityLerpPerSecond { get; }
            public float ClumpRadius { get; }
            public float SpawnSpreadRadius { get; }
            public float SpawnInitialSpeed { get; }
            public float MaxSpeed { get; }
            public float VelocityDampingPerSecond { get; }
            public float ClusterRadius { get; }
            public float ClusterAttractForce { get; }
            public float ClusterHomeostasisDistance { get; }
            public float ClusterRepelForce { get; }
            public float VisualMergeRadius { get; }
            public float VisualMergeGrowth { get; }
            public float VisualMergeMaxScale { get; }
            public float AbsorbConsumeDistance { get; }
            public float AbsorbFadeSeconds { get; }
            public float AbsorbConsumeGrowMaxScale { get; }
            public float UnstableHealthThresholdRatio { get; }
            public float UnstableJitterAccel { get; }
            public float UnstableCenterPullAccel { get; }
            public float UnstableVelocityDampingPerSecond { get; }
            public float UnstableMaxSpeed { get; }
            public float UnstableAlphaMin { get; }
            public float UnstableAlphaMax { get; }
            public float UnstableAlphaPulseHz { get; }
            public float UnstableRadiusScale { get; }
            public float UnstableRenderScale { get; }
            public float UnstableFadeOutSeconds { get; }
            public float AbsorbOrbitMaxAngularSpeedDeg { get; }
            public float AbsorbOrbitAngularBlendPerSecond { get; }
            public float AbsorbOrbitAngularDampingPerSecond { get; }
            public float AbsorbOrbitCollapseSpeed { get; }
            public float AbsorbConsumeCollapseSpeed { get; }
            public float AbsorbOrbitFollowGain { get; }
            public float AbsorbVelocityLerpPerSecond { get; }
            public float AbsorbOrbitTangentialVelocityWeight { get; }
            public float AbsorbOrbitInitialRadiusFactor { get; }
            public float AbsorbOrbitMinRadiusFactor { get; }
            public float AbsorbOrbitMinRadiusAbsolute { get; }
            public float AbsorbOrbitCollapseBoostLow { get; }
            public float AbsorbOrbitCollapseBoostHigh { get; }
            public float AbsorbOrbitInwardCollapseFactor { get; }
            public float AbsorbOrbitMaxInwardSpeedMin { get; }
            public float AbsorbOrbitMaxInwardSpeedPullScale { get; }
            public float AbsorbConsumeExtraInwardFactor { get; }
            public float CoreHighlightScale { get; }
            public float CoreHighlightAlphaScale { get; }
            public float GlowScale { get; }
            public float GlowAlphaScale { get; }
            public float ShadowScale { get; }
            public float ShadowAlphaScale { get; }
            public float DeadPulseSpeedMin { get; }
            public float DeadPulseSpeedMax { get; }
            public float DeadPulseLowAlphaStart { get; }
            public float DeadPulseLowAlphaEnd { get; }
            public float ConsumeGrowthExponent { get; }
            public float VisualMergeScaleLerpPerSecond { get; }
            public float UnstableShowLerpPerSecond { get; }
            public float UnstableVisibilityEpsilon { get; }
            public float UnstableBurstRatePerSecond { get; }
            public float UnstableRandomKickFactorMin { get; }
            public float UnstableRandomKickFactorMax { get; }
            public float UnstableTangentialKickBaseFactor { get; }
            public float UnstableTangentialKickPressureFactor { get; }
            public float UnstableCenterPullBaseFactor { get; }
            public float UnstableCenterPullPressureFactor { get; }
            public float UnstableBurstFactorMin { get; }
            public float UnstableBurstFactorMax { get; }
            public float UnstableBoundaryBounceFactor { get; }
            public float UnstableBoundaryTangentialFactor { get; }
            public float UnstableAlphaNoiseAmplitude { get; }
            public float UnstableAlphaPulseWeight { get; }
            public float UnstableAlphaPressureWeight { get; }
            public float UnstableAlphaLerpPerSecond { get; }
        }

        private struct FreeClump
        {
            public int ID;
            public Vector2 Position;
            public Vector2 Velocity;
            public Color SourceColor;
            public float Radius;
            public float DeadZoneElapsed;
            public float FlickerPhase;
            public int LockedAgentID;
            public float OrbitAngle;
            public float OrbitRadius;
            public float OrbitAngularVelocity;
            public bool IsConsuming;
            public float ConsumeTimer;
            public float VisualSizeScale;
        }

        private sealed class DropState
        {
            public int SourceID;
            public int TotalClumps;
            public int SpawnedClumps;
            public int SliceCount;
            public int BasePerSlice;
            public int Remainder;
            public int NextSliceIndex;
            public Color SourceColor;
            public List<DropSeed> TransitionSeeds;
            public int NextTransitionSeed;
        }

        private struct DropSeed
        {
            public Vector2 Position;
            public Vector2 Velocity;
        }

        private struct UnstableClump
        {
            public Vector2 LocalOffset;
            public Vector2 LocalVelocity;
            public float Alpha;
            public float FlickerPhase;
        }

        private sealed class UnstableDropState
        {
            public int SourceID;
            public Vector2 Center;
            public float Radius;
            public Color SourceColor;
            public List<UnstableClump> UnstableClumps = new();
            public int LastSeenFrame;
            public float VisibilityAlpha = 1f;
            public float FadeOutTimer = 0f;
            public bool IsFadingOut = false;
        }

        private struct PickupWindow
        {
            public int Second;
            public int Count;
        }

        private static readonly List<FreeClump> _freeClumps = new();
        private static readonly Dictionary<int, DropState> _dropStates = new();
        private static readonly Dictionary<int, UnstableDropState> _unstableDropStates = new();
        private static readonly List<int> _staleUnstableDropIDs = new();

        private static readonly List<Agent> _liveAgents = new();
        private static readonly Dictionary<int, Agent> _liveAgentsById = new();
        private static readonly Dictionary<int, PickupWindow> _pickupWindows = new();
        private static readonly List<int> _stalePickupAgentIDs = new();

        private static readonly Dictionary<long, List<int>> _clusterGrid = new();
        private static readonly Stack<List<int>> _clusterGridListPool = new();
        private static Vector2[] _clusterDeltaVelocities = Array.Empty<Vector2>();
        private static float[] _visualDensityScores = Array.Empty<float>();
        private static Agent[] _nearestAgents = Array.Empty<Agent>();
        private static float[] _nearestAgentDistances = Array.Empty<float>();
        private static Vector2[] _playerMorphInfluences = Array.Empty<Vector2>();
        private static Vector2[] _neighborMorphInfluences = Array.Empty<Vector2>();
        private static readonly List<int> _removeIndices = new();

        private static readonly Random _random = new();

        private static ClumpSettings? _cachedSettings;
        private static int _nextClumpID = 1;
        private static int _pickupWindowSecond = int.MinValue;
        private static int _absorbedThisSecond;
        private static int _unstableStateFrame;

        private const int BlobVariantCount = 8;
        private const float BlobTextureIrregularity = 0.2f;
        private const float ShadowVisibilityBoost = 2.0f;
        private const float ShadowScaleBoost = 1.12f;
        private const float MinShadowAlphaFactor = 0.15f;
        private const float FreeShadowAlphaScale = 0.58f;
        private const float FreeShadowScaleLerp = 0.52f;
        private const float FreeShadowOffsetScale = 0.72f;
        private const float ClusterAttractGlobalScale = 0.78f;
        private const float ClusterAttractNearPlayerScale = 0.25f;
        private const float MorphPlayerWeight = 1.35f;
        private const float MorphNeighborWeight = 0.82f;
        private const float MorphVelocityWeight = 0.3f;
        private const float MorphMaxStretch = 1.3f;
        private static readonly Vector2 ShadowOffsetDirection = Vector2.Normalize(new Vector2(0.58f, 1f));

        private static Texture2D _orbCoreTexture;
        private static Texture2D _orbGlowTexture;
        private static Texture2D[] _orbCoreVariantTextures = Array.Empty<Texture2D>();
        private static Texture2D[] _orbGlowVariantTextures = Array.Empty<Texture2D>();

        public static int ActiveClumpCount => _freeClumps.Count;
        public static int ActiveUnstableClumpCount => CountUnstableClumps();
        public static int PendingDropCount => _dropStates.Count;
        public static int AbsorbedThisSecond => _absorbedThisSecond;
        public static int PickupPerSecond => Settings.PickupPerSecond;
        public static float DeadZoneRadius => Settings.DeadZoneRadius;
        public static float PullZoneRadius => Settings.PullZoneRadius;
        public static float AbsorbZoneRadius => Settings.AbsorbZoneRadius;

        private static ClumpSettings Settings => _cachedSettings ??= LoadSettings();

        public static void Reset()
        {
            _freeClumps.Clear();
            _dropStates.Clear();
            _unstableDropStates.Clear();
            _liveAgents.Clear();
            _liveAgentsById.Clear();
            _pickupWindows.Clear();
            _stalePickupAgentIDs.Clear();
            _staleUnstableDropIDs.Clear();
            _removeIndices.Clear();

            foreach (List<int> list in _clusterGrid.Values)
            {
                list.Clear();
                _clusterGridListPool.Push(list);
            }

            _clusterGrid.Clear();
            _clusterDeltaVelocities = Array.Empty<Vector2>();
            _visualDensityScores = Array.Empty<float>();
            _nearestAgents = Array.Empty<Agent>();
            _nearestAgentDistances = Array.Empty<float>();
            _playerMorphInfluences = Array.Empty<Vector2>();
            _neighborMorphInfluences = Array.Empty<Vector2>();

            _nextClumpID = 1;
            _pickupWindowSecond = int.MinValue;
            _absorbedThisSecond = 0;
            _unstableStateFrame = 0;
            _cachedSettings = null;
        }

        public static void LoadContent(GraphicsDevice graphicsDevice)
        {
            EnsureTextures(graphicsDevice);
        }

        public static void UpdateUnstableDropPreviews(List<GameObject> gameObjects, float dt)
        {
            if (gameObjects == null || gameObjects.Count == 0 || dt <= 0f)
            {
                _unstableDropStates.Clear();
                _staleUnstableDropIDs.Clear();
                return;
            }

            _unstableStateFrame++;
            ClumpSettings settings = Settings;

            foreach (GameObject go in gameObjects)
            {
                if (!IsValidDropSource(go) || go.CurrentHealth <= 0f || go.MaxHealth <= 0f)
                {
                    continue;
                }

                int clumpCount = (int)MathF.Floor(go.DeathPointReward);
                if (clumpCount <= 0)
                {
                    _unstableDropStates.Remove(go.ID);
                    continue;
                }

                float healthRatio = go.CurrentHealth / MathF.Max(go.MaxHealth, 0.001f);
                bool isBelowThreshold = healthRatio <= settings.UnstableHealthThresholdRatio;
                if (!isBelowThreshold && !_unstableDropStates.ContainsKey(go.ID))
                {
                    continue;
                }

                float sourceRadius = MathF.Max(go.BoundingRadius, settings.ClumpRadius * 2f);
                UnstableDropState state = GetOrCreateUnstableDropState(go, clumpCount, sourceRadius, settings);
                state.Center = go.Position;
                state.Radius = sourceRadius;
                state.SourceColor = GetDropSourceColor(go);
                state.LastSeenFrame = _unstableStateFrame;
                if (isBelowThreshold)
                {
                    state.IsFadingOut = false;
                    state.FadeOutTimer = settings.UnstableFadeOutSeconds;
                    float showBlend = 1f - MathF.Exp(-settings.UnstableShowLerpPerSecond * dt);
                    state.VisibilityAlpha = MathHelper.Lerp(state.VisibilityAlpha, 1f, showBlend);
                }
                else
                {
                    if (!state.IsFadingOut)
                    {
                        state.IsFadingOut = true;
                        state.FadeOutTimer = settings.UnstableFadeOutSeconds;
                    }

                    state.FadeOutTimer = MathF.Max(0f, state.FadeOutTimer - dt);
                    float fadeDuration = MathF.Max(settings.UnstableFadeOutSeconds, 0.001f);
                    state.VisibilityAlpha = MathHelper.Clamp(state.FadeOutTimer / fadeDuration, 0f, 1f);
                }

                UpdateUnstableClumps(state, settings, dt);

                if (state.VisibilityAlpha <= settings.UnstableVisibilityEpsilon)
                {
                    _unstableDropStates.Remove(go.ID);
                    continue;
                }

                _unstableDropStates[go.ID] = state;
            }

            _staleUnstableDropIDs.Clear();
            foreach (KeyValuePair<int, UnstableDropState> pair in _unstableDropStates)
            {
                if (pair.Value.LastSeenFrame != _unstableStateFrame)
                {
                    _staleUnstableDropIDs.Add(pair.Key);
                }
            }

            foreach (int id in _staleUnstableDropIDs)
            {
                _unstableDropStates.Remove(id);
            }
        }

        public static void BeginDeathDrop(GameObject source, int killerAgentID, float rewardXP, float fadeDurationSeconds)
        {
            if (source == null || killerAgentID < 0 || rewardXP <= 0f || source is Agent)
            {
                return;
            }

            int totalClumps = (int)MathF.Floor(rewardXP);
            if (totalClumps <= 0)
            {
                return;
            }

            List<DropSeed> transitionSeeds = ConsumeUnstableDropSeeds(source.ID);
            int sliceCount = Math.Max(1, (int)MathF.Ceiling(MathF.Max(fadeDurationSeconds, 0.001f)));
            DropState state = new()
            {
                SourceID = source.ID,
                TotalClumps = totalClumps,
                SpawnedClumps = 0,
                SliceCount = sliceCount,
                BasePerSlice = totalClumps / sliceCount,
                Remainder = totalClumps % sliceCount,
                NextSliceIndex = 0,
                SourceColor = GetDropSourceColor(source),
                TransitionSeeds = transitionSeeds,
                NextTransitionSeed = 0
            };

            _dropStates[source.ID] = state;

            if (fadeDurationSeconds <= 0f)
            {
                SpawnRemainingClumps(state, source.Position);
                _dropStates.Remove(source.ID);
            }
        }

        public static void UpdateDeathDrop(GameObject source, float elapsedBeforeSeconds, float elapsedAfterSeconds)
        {
            if (source == null || !_dropStates.TryGetValue(source.ID, out DropState state))
            {
                return;
            }

            float elapsedAfter = MathF.Max(0f, elapsedAfterSeconds);
            while (state.NextSliceIndex < state.SliceCount && elapsedAfter >= state.NextSliceIndex)
            {
                SpawnSlice(state, state.NextSliceIndex, source.Position);
                state.NextSliceIndex++;
            }

            if (state.SpawnedClumps >= state.TotalClumps)
            {
                _dropStates.Remove(source.ID);
            }
        }

        public static void CompleteDeathDrop(GameObject source)
        {
            if (source == null || !_dropStates.TryGetValue(source.ID, out DropState state))
            {
                _unstableDropStates.Remove(source?.ID ?? -1);
                return;
            }

            SpawnRemainingClumps(state, source.Position);
            _dropStates.Remove(source.ID);
            _unstableDropStates.Remove(source.ID);
        }

        public static void Update(float dt)
        {
            AdvancePickupWindow();
            if (dt <= 0f || _freeClumps.Count == 0)
            {
                return;
            }

            RebuildLiveAgentCache();
            ClumpSettings settings = Settings;
            int clumpCount = _freeClumps.Count;

            EnsureClusterVelocityBuffer(clumpCount);
            EnsureVisualDensityBuffer(clumpCount);
            EnsureNearestAgentBuffer(clumpCount);
            EnsureMorphInfluenceBuffers(clumpCount);
            Array.Clear(_clusterDeltaVelocities, 0, clumpCount);
            Array.Clear(_visualDensityScores, 0, clumpCount);
            Array.Clear(_playerMorphInfluences, 0, clumpCount);
            Array.Clear(_neighborMorphInfluences, 0, clumpCount);
            UpdateNearestAgentInfluence(settings);
            ApplyClusterForces(settings, dt);

            _removeIndices.Clear();

            for (int i = 0; i < _freeClumps.Count; i++)
            {
                FreeClump clump = _freeClumps[i];
                clump.Velocity += _clusterDeltaVelocities[i];

                Agent nearestAgent = _nearestAgents[i];
                float nearestDistance = _nearestAgentDistances[i];
                bool hasNearestAgent = nearestAgent != null;
                bool inAbsorbZone = hasNearestAgent && nearestDistance <= settings.AbsorbZoneRadius;
                bool inPullZone = hasNearestAgent && nearestDistance <= settings.PullZoneRadius;
                bool isDeadClump = !hasNearestAgent || nearestDistance > settings.DeadZoneRadius;

                // Free clump states:
                // dead clump   => no unit in range (dead zone)
                // pulled clump => pull-zone steering toward a nearby unit
                // locked clump => absorb-zone collector lock with direct pull (no orbit)
                clump.DeadZoneElapsed = isDeadClump ? clump.DeadZoneElapsed + dt : 0f;

                float deadZoneMaxLifetime = settings.DeadZoneStartSeconds + settings.DeadZoneDespawnSeconds;
                if (clump.DeadZoneElapsed >= deadZoneMaxLifetime)
                {
                    _removeIndices.Add(i);
                    continue;
                }

                if (clump.IsConsuming)
                {
                    Agent pullTarget = null;
                    if (!_liveAgentsById.TryGetValue(clump.LockedAgentID, out pullTarget))
                    {
                        pullTarget = nearestAgent;
                    }

                    if (pullTarget != null)
                    {
                        float pullDistance = Vector2.Distance(clump.Position, pullTarget.Position);
                        ApplyPullVelocity(ref clump, pullTarget.Position, pullDistance, settings, dt);
                    }

                    clump.OrbitRadius = 0f;
                    clump.OrbitAngle = 0f;
                    clump.OrbitAngularVelocity = 0f;
                    clump.ConsumeTimer -= dt;

                    if (clump.ConsumeTimer <= 0f)
                    {
                        if (_liveAgentsById.TryGetValue(clump.LockedAgentID, out Agent collector))
                        {
                            collector.CurrentXP += 1f;
                        }

                        _removeIndices.Add(i);
                        continue;
                    }
                }
                else if (inAbsorbZone && nearestAgent != null)
                {
                    int collectorID = nearestAgent.ID;
                    if (CanAgentAbsorbNow(collectorID))
                    {
                        // In absorb range we keep direct pull behavior so clumps do not bleed
                        // inward momentum during a lock/orbit transition.
                        clump.LockedAgentID = collectorID;
                        clump.OrbitRadius = 0f;
                        clump.OrbitAngle = 0f;
                        clump.OrbitAngularVelocity = 0f;
                        ApplyPullVelocity(ref clump, nearestAgent.Position, nearestDistance, settings, dt);

                        if (nearestDistance <= settings.AbsorbConsumeDistance && TryReservePickupSlot(collectorID))
                        {
                            clump.IsConsuming = true;
                            clump.ConsumeTimer = settings.AbsorbFadeSeconds;
                            clump.LockedAgentID = collectorID;
                        }
                    }
                    else
                    {
                        // Pulled clump behavior while absorb capacity is exhausted.
                        clump.LockedAgentID = -1;
                        clump.OrbitRadius = 0f;
                        clump.OrbitAngle = 0f;
                        clump.OrbitAngularVelocity = 0f;
                        ApplyPullVelocity(ref clump, nearestAgent.Position, nearestDistance, settings, dt);
                    }
                }
                else if (inPullZone && nearestAgent != null)
                {
                    clump.LockedAgentID = -1;
                    clump.OrbitRadius = 0f;
                    clump.OrbitAngle = 0f;
                    clump.OrbitAngularVelocity = 0f;
                    ApplyPullVelocity(ref clump, nearestAgent.Position, nearestDistance, settings, dt);
                }
                else
                {
                    clump.LockedAgentID = -1;
                    clump.OrbitRadius = 0f;
                    clump.OrbitAngle = 0f;
                    clump.OrbitAngularVelocity = 0f;
                }

                float damping = MathF.Exp(-settings.VelocityDampingPerSecond * dt);
                clump.Velocity *= damping;
                ClampVelocity(ref clump.Velocity, settings.MaxSpeed);
                clump.Position += clump.Velocity * dt;

                _freeClumps[i] = clump;
            }

            if (_removeIndices.Count > 0)
            {
                _removeIndices.Sort();
                for (int i = _removeIndices.Count - 1; i >= 0; i--)
                {
                    _freeClumps.RemoveAt(_removeIndices[i]);
                }
            }

            UpdateVisualSizeScales(settings, dt);
        }

        public static void DrawCore(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || (_freeClumps.Count == 0 && _unstableDropStates.Count == 0))
            {
                return;
            }

            EnsureTextures(spriteBatch.GraphicsDevice);
            if (_orbCoreTexture == null)
            {
                return;
            }

            ClumpSettings settings = Settings;
            DrawUnstableClumpsShadows(spriteBatch, settings);
            DrawUnstableClumpsCore(spriteBatch, settings);

            for (int i = 0; i < _freeClumps.Count; i++)
            {
                FreeClump clump = _freeClumps[i];
                float alpha = ComputeRenderAlpha(clump, settings);
                if (alpha <= 0f)
                {
                    continue;
                }

                Texture2D coreTexture = GetCoreTextureForSeed(clump.ID);
                Texture2D shadowTexture = coreTexture ?? _orbCoreTexture;
                if (coreTexture == null || shadowTexture == null)
                {
                    continue;
                }

                Vector2 origin = new(coreTexture.Width * 0.5f, coreTexture.Height * 0.5f);
                Vector2 shadowOrigin = new(shadowTexture.Width * 0.5f, shadowTexture.Height * 0.5f);
                float scaleMultiplier = MathF.Max(1f, clump.VisualSizeScale) * ComputeConsumeGrowthScale(clump, settings);
                float baseScale = (clump.Radius * 2f / coreTexture.Width) * scaleMultiplier;
                float shadowScaleMultiplier = 1f + ((scaleMultiplier - 1f) * FreeShadowScaleLerp);
                float shadowScale = (clump.Radius * 2f / shadowTexture.Width) * settings.ShadowScale * ShadowScaleBoost * shadowScaleMultiplier;
                Color shadowColor = Color.Black * ComputeShadowAlpha(alpha, settings, isFreeClump: true);
                if (shadowColor.A > 0)
                {
                    Vector2 shadowOffset = ComputeShadowOffset(clump.Radius, shadowScaleMultiplier, FreeShadowOffsetScale);
                    spriteBatch.Draw(shadowTexture, clump.Position + shadowOffset, null, shadowColor, 0f, shadowOrigin, shadowScale, SpriteEffects.None, 0f);
                }

                ComputeFreeMorphTransform(i, clump, settings, baseScale, out Vector2 morphScale, out float morphRotation);
                Color coreColor = GetCoreColor(clump.SourceColor) * alpha;
                spriteBatch.Draw(coreTexture, clump.Position, null, coreColor, morphRotation, origin, morphScale, SpriteEffects.None, 0f);

                // White-hot center keeps the orb viscous and readable while preserving source tint.
                Vector2 highlightScale = morphScale * settings.CoreHighlightScale;
                Color highlightColor = Color.White * (alpha * settings.CoreHighlightAlphaScale);
                spriteBatch.Draw(coreTexture, clump.Position, null, highlightColor, morphRotation, origin, highlightScale, SpriteEffects.None, 0f);
            }
        }

        public static void DrawGlow(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || (_freeClumps.Count == 0 && _unstableDropStates.Count == 0))
            {
                return;
            }

            EnsureTextures(spriteBatch.GraphicsDevice);
            if (_orbGlowTexture == null)
            {
                return;
            }

            ClumpSettings settings = Settings;
            DrawUnstableClumpsGlow(spriteBatch, settings);

            for (int i = 0; i < _freeClumps.Count; i++)
            {
                FreeClump clump = _freeClumps[i];
                float alpha = ComputeRenderAlpha(clump, settings);
                if (alpha <= 0f)
                {
                    continue;
                }

                Texture2D glowTexture = GetGlowTextureForSeed(clump.ID);
                if (glowTexture == null)
                {
                    continue;
                }

                Vector2 origin = new(glowTexture.Width * 0.5f, glowTexture.Height * 0.5f);
                float scaleMultiplier = MathF.Max(1f, clump.VisualSizeScale) * ComputeConsumeGrowthScale(clump, settings);
                float baseScale = (clump.Radius * 2f / glowTexture.Width) * settings.GlowScale * scaleMultiplier;
                ComputeFreeMorphTransform(i, clump, settings, baseScale, out Vector2 morphScale, out float morphRotation);
                Color glowColor = GetGlowColor(clump.SourceColor) * (alpha * settings.GlowAlphaScale);
                spriteBatch.Draw(glowTexture, clump.Position, null, glowColor, morphRotation, origin, morphScale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawUnstableClumpsShadows(SpriteBatch spriteBatch, in ClumpSettings settings)
        {
            if (_unstableDropStates.Count == 0 || settings.ShadowAlphaScale <= 0f)
            {
                return;
            }

            float unstableRadius = MathF.Max(settings.ClumpRadius * settings.UnstableRenderScale, 1f);

            foreach (UnstableDropState state in _unstableDropStates.Values)
            {
                float stateVisibility = MathHelper.Clamp(state.VisibilityAlpha, 0f, 1f);
                if (stateVisibility <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < state.UnstableClumps.Count; i++)
                {
                    UnstableClump unstableClump = state.UnstableClumps[i];
                    float alpha = MathHelper.Clamp(unstableClump.Alpha * stateVisibility, 0f, 1f);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    Texture2D shadowTexture = GetCoreTextureForSeed(HashSeed(state.SourceID, i));
                    if (shadowTexture == null)
                    {
                        continue;
                    }

                    Vector2 shadowOrigin = new(shadowTexture.Width * 0.5f, shadowTexture.Height * 0.5f);
                    float unstableScale = unstableRadius * 2f / shadowTexture.Width * settings.ShadowScale * ShadowScaleBoost;
                    Color shadowColor = Color.Black * ComputeShadowAlpha(alpha, settings, isFreeClump: false);
                    if (shadowColor.A == 0)
                    {
                        continue;
                    }

                    Vector2 worldPosition = state.Center + unstableClump.LocalOffset;
                    Vector2 shadowOffset = ComputeShadowOffset(unstableRadius, 1f);
                    spriteBatch.Draw(shadowTexture, worldPosition + shadowOffset, null, shadowColor, 0f, shadowOrigin, unstableScale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawUnstableClumpsCore(SpriteBatch spriteBatch, in ClumpSettings settings)
        {
            if (_unstableDropStates.Count == 0 || _orbCoreTexture == null)
            {
                return;
            }

            float unstableRadius = MathF.Max(settings.ClumpRadius * settings.UnstableRenderScale, 1f);

            foreach (UnstableDropState state in _unstableDropStates.Values)
            {
                float stateVisibility = MathHelper.Clamp(state.VisibilityAlpha, 0f, 1f);
                if (stateVisibility <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < state.UnstableClumps.Count; i++)
                {
                    UnstableClump unstableClump = state.UnstableClumps[i];
                    float alpha = MathHelper.Clamp(unstableClump.Alpha * stateVisibility, 0f, 1f);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    Texture2D coreTexture = GetCoreTextureForSeed(HashSeed(state.SourceID, i));
                    if (coreTexture == null)
                    {
                        continue;
                    }

                    Vector2 origin = new(coreTexture.Width * 0.5f, coreTexture.Height * 0.5f);
                    float unstableScale = unstableRadius * 2f / coreTexture.Width;
                    Vector2 worldPosition = state.Center + unstableClump.LocalOffset;
                    Color coreColor = GetCoreColor(state.SourceColor) * alpha;
                    spriteBatch.Draw(coreTexture, worldPosition, null, coreColor, 0f, origin, unstableScale, SpriteEffects.None, 0f);

                    float highlightScale = unstableScale * settings.CoreHighlightScale;
                    Color highlightColor = Color.White * (alpha * settings.CoreHighlightAlphaScale);
                    spriteBatch.Draw(coreTexture, worldPosition, null, highlightColor, 0f, origin, highlightScale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawUnstableClumpsGlow(SpriteBatch spriteBatch, in ClumpSettings settings)
        {
            if (_unstableDropStates.Count == 0 || _orbGlowTexture == null)
            {
                return;
            }

            float unstableRadius = MathF.Max(settings.ClumpRadius * settings.UnstableRenderScale, 1f);

            foreach (UnstableDropState state in _unstableDropStates.Values)
            {
                float stateVisibility = MathHelper.Clamp(state.VisibilityAlpha, 0f, 1f);
                if (stateVisibility <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < state.UnstableClumps.Count; i++)
                {
                    UnstableClump unstableClump = state.UnstableClumps[i];
                    float alpha = MathHelper.Clamp(unstableClump.Alpha * stateVisibility, 0f, 1f);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    Texture2D glowTexture = GetGlowTextureForSeed(HashSeed(state.SourceID, i));
                    if (glowTexture == null)
                    {
                        continue;
                    }

                    Vector2 origin = new(glowTexture.Width * 0.5f, glowTexture.Height * 0.5f);
                    float unstableScale = unstableRadius * 2f / glowTexture.Width * settings.GlowScale;
                    Vector2 worldPosition = state.Center + unstableClump.LocalOffset;
                    Color glowColor = GetGlowColor(state.SourceColor) * (alpha * settings.GlowAlphaScale);
                    spriteBatch.Draw(glowTexture, worldPosition, null, glowColor, 0f, origin, unstableScale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void RebuildLiveAgentCache()
        {
            _liveAgents.Clear();
            _liveAgentsById.Clear();

            List<GameObject> gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null)
            {
                return;
            }

            foreach (GameObject go in gameObjects)
            {
                if (go is not Agent agent || agent.IsDying || agent.CurrentHealth <= 0f)
                {
                    continue;
                }

                _liveAgents.Add(agent);
                _liveAgentsById[agent.ID] = agent;
            }
        }

        private static Agent FindNearestAgent(Vector2 clumpPosition, out float nearestDistance)
        {
            Agent nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;

            for (int i = 0; i < _liveAgents.Count; i++)
            {
                Agent agent = _liveAgents[i];
                float distSq = Vector2.DistanceSquared(clumpPosition, agent.Position);
                if (distSq < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distSq;
                    nearest = agent;
                }
            }

            nearestDistance = nearest != null ? MathF.Sqrt(nearestDistanceSquared) : float.PositiveInfinity;
            return nearest;
        }

        private static void ApplyPullVelocity(
            ref FreeClump clump,
            Vector2 targetPosition,
            float targetDistance,
            in ClumpSettings settings,
            float dt)
        {
            Vector2 toTarget = targetPosition - clump.Position;
            float lenSq = toTarget.LengthSquared();
            if (lenSq < 0.0001f)
            {
                return;
            }

            float len = MathF.Sqrt(lenSq);
            Vector2 direction = toTarget / len;

            float denominator = MathF.Max(settings.PullZoneRadius - settings.AbsorbZoneRadius, 0.001f);
            float pullProgress = 1f - MathHelper.Clamp((targetDistance - settings.AbsorbZoneRadius) / denominator, 0f, 1f);
            float desiredSpeed = MathHelper.Lerp(settings.PullSpeedMin, settings.PullSpeedMax, pullProgress);
            Vector2 desiredVelocity = direction * desiredSpeed;

            float blend = 1f - MathF.Exp(-settings.PullVelocityLerpPerSecond * dt);
            clump.Velocity = Vector2.Lerp(clump.Velocity, desiredVelocity, blend);
        }

        private static bool TryReservePickupSlot(int agentID)
        {
            if (agentID < 0)
            {
                return false;
            }

            AdvancePickupWindow();
            int pickupLimit = Settings.PickupPerSecond;

            if (!_pickupWindows.TryGetValue(agentID, out PickupWindow window) || window.Second != _pickupWindowSecond)
            {
                window = new PickupWindow
                {
                    Second = _pickupWindowSecond,
                    Count = 0
                };
            }

            if (window.Count >= pickupLimit)
            {
                return false;
            }

            window.Count++;
            _pickupWindows[agentID] = window;
            _absorbedThisSecond++;
            return true;
        }

        private static bool CanAgentAbsorbNow(int agentID)
        {
            if (agentID < 0)
            {
                return false;
            }

            AdvancePickupWindow();
            int pickupLimit = Settings.PickupPerSecond;

            if (!_pickupWindows.TryGetValue(agentID, out PickupWindow window) || window.Second != _pickupWindowSecond)
            {
                return pickupLimit > 0;
            }

            return window.Count < pickupLimit;
        }

        private static void AdvancePickupWindow()
        {
            int currentSecond = (int)MathF.Floor(Core.GAMETIME);
            if (currentSecond == _pickupWindowSecond)
            {
                return;
            }

            _pickupWindowSecond = currentSecond;
            _absorbedThisSecond = 0;

            _stalePickupAgentIDs.Clear();
            foreach (KeyValuePair<int, PickupWindow> pair in _pickupWindows)
            {
                if (pair.Value.Second != currentSecond)
                {
                    _stalePickupAgentIDs.Add(pair.Key);
                }
            }

            foreach (int id in _stalePickupAgentIDs)
            {
                _pickupWindows.Remove(id);
            }
        }

        private static void SpawnSlice(DropState state, int sliceIndex, Vector2 position)
        {
            int sliceCount = state.BasePerSlice + (sliceIndex < state.Remainder ? 1 : 0);
            if (sliceCount <= 0)
            {
                return;
            }

            SpawnClumps(position, state.SourceColor, sliceCount, state);
            state.SpawnedClumps += sliceCount;
        }

        private static void SpawnRemainingClumps(DropState state, Vector2 position)
        {
            for (int slice = state.NextSliceIndex; slice < state.SliceCount; slice++)
            {
                SpawnSlice(state, slice, position);
                state.NextSliceIndex = slice + 1;
            }

            int remaining = state.TotalClumps - state.SpawnedClumps;
            if (remaining > 0)
            {
                SpawnClumps(position, state.SourceColor, remaining, state);
                state.SpawnedClumps += remaining;
            }
        }

        private static void SpawnClumps(Vector2 position, Color sourceColor, int count, DropState state = null)
        {
            if (count <= 0)
            {
                return;
            }

            ClumpSettings settings = Settings;
            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPosition = position;
                Vector2 spawnVelocity;
                float launchAngle;
                if (state?.TransitionSeeds != null && state.NextTransitionSeed < state.TransitionSeeds.Count)
                {
                    DropSeed seed = state.TransitionSeeds[state.NextTransitionSeed++];
                    spawnPosition = seed.Position;
                    spawnVelocity = seed.Velocity;
                    launchAngle = spawnVelocity.LengthSquared() > 0.0001f
                        ? MathF.Atan2(spawnVelocity.Y, spawnVelocity.X)
                        : (float)(_random.NextDouble() * MathF.Tau);
                }
                else
                {
                    float spawnAngle = (float)(_random.NextDouble() * MathF.Tau);
                    float spawnRadius = MathF.Sqrt((float)_random.NextDouble()) * settings.SpawnSpreadRadius;
                    Vector2 spawnOffset = new(MathF.Cos(spawnAngle), MathF.Sin(spawnAngle));
                    spawnPosition += spawnOffset * spawnRadius;

                    launchAngle = (float)(_random.NextDouble() * MathF.Tau);
                    Vector2 launchDirection = new(MathF.Cos(launchAngle), MathF.Sin(launchAngle));
                    spawnVelocity = launchDirection * settings.SpawnInitialSpeed;
                }

                _freeClumps.Add(new FreeClump
                {
                    ID = _nextClumpID++,
                    Position = spawnPosition,
                    Velocity = spawnVelocity,
                    SourceColor = sourceColor,
                    Radius = settings.ClumpRadius,
                    DeadZoneElapsed = 0f,
                    FlickerPhase = (float)(_random.NextDouble() * MathF.Tau),
                    LockedAgentID = -1,
                    OrbitAngle = launchAngle,
                    OrbitRadius = 0f,
                    OrbitAngularVelocity = 0f,
                    IsConsuming = false,
                    ConsumeTimer = 0f,
                    VisualSizeScale = 1f
                });
            }
        }

        private static bool IsValidDropSource(GameObject go)
        {
            return go != null &&
                   go.IsDestructible &&
                   go is not Agent &&
                   go.DeathPointReward > 0f;
        }

        private static Color GetDropSourceColor(GameObject source)
        {
            Color c = source?.FillColor ?? Color.White;
            return new Color(c.R, c.G, c.B, (byte)255);
        }

        private static UnstableDropState GetOrCreateUnstableDropState(
            GameObject source,
            int clumpCount,
            float sourceRadius,
            in ClumpSettings settings)
        {
            if (!_unstableDropStates.TryGetValue(source.ID, out UnstableDropState state))
            {
                state = new UnstableDropState
                {
                    SourceID = source.ID,
                    Center = source.Position,
                    Radius = sourceRadius,
                    SourceColor = GetDropSourceColor(source),
                    VisibilityAlpha = 1f,
                    FadeOutTimer = settings.UnstableFadeOutSeconds,
                    IsFadingOut = false
                };
            }

            while (state.UnstableClumps.Count < clumpCount)
            {
                float angle = (float)(_random.NextDouble() * MathF.Tau);
                float radial = MathF.Sqrt((float)_random.NextDouble()) * sourceRadius * settings.UnstableRadiusScale;
                Vector2 localOffset = new(MathF.Cos(angle), MathF.Sin(angle));

                float speed = (float)_random.NextDouble() * settings.UnstableMaxSpeed;
                float speedAngle = (float)(_random.NextDouble() * MathF.Tau);
                Vector2 localVelocity = new Vector2(MathF.Cos(speedAngle), MathF.Sin(speedAngle)) * speed;

                float alpha = MathHelper.Lerp(settings.UnstableAlphaMin, settings.UnstableAlphaMax, (float)_random.NextDouble());
                float phase = (float)(_random.NextDouble() * MathF.Tau);

                state.UnstableClumps.Add(new UnstableClump
                {
                    LocalOffset = localOffset * radial,
                    LocalVelocity = localVelocity,
                    Alpha = alpha,
                    FlickerPhase = phase
                });
            }

            if (state.UnstableClumps.Count > clumpCount)
            {
                state.UnstableClumps.RemoveRange(clumpCount, state.UnstableClumps.Count - clumpCount);
            }

            return state;
        }

        private static void UpdateUnstableClumps(UnstableDropState state, in ClumpSettings settings, float dt)
        {
            float allowedRadius = MathF.Max(settings.ClumpRadius, state.Radius * settings.UnstableRadiusScale);
            float allowedRadiusSq = allowedRadius * allowedRadius;
            float damping = MathF.Exp(-settings.UnstableVelocityDampingPerSecond * dt);
            float alphaRange = MathF.Max(settings.UnstableAlphaMax - settings.UnstableAlphaMin, 0.01f);
            float burstChance = 1f - MathF.Exp(-settings.UnstableBurstRatePerSecond * dt);

            for (int i = 0; i < state.UnstableClumps.Count; i++)
            {
                UnstableClump clump = state.UnstableClumps[i];
                Vector2 toCenter = -clump.LocalOffset;
                float distToCenter = toCenter.Length();
                Vector2 toCenterDirection = distToCenter > 0.0001f
                    ? toCenter / distToCenter
                    : Vector2.Zero;

                float pressure = MathHelper.Clamp(distToCenter / MathF.Max(allowedRadius, 0.001f), 0f, 1f);
                float randomAngle = (float)(_random.NextDouble() * MathF.Tau);
                Vector2 randomKick = new(MathF.Cos(randomAngle), MathF.Sin(randomAngle));
                randomKick *= settings.UnstableJitterAccel * MathHelper.Lerp(settings.UnstableRandomKickFactorMin, settings.UnstableRandomKickFactorMax, pressure);

                Vector2 tangentDirection = new(-toCenterDirection.Y, toCenterDirection.X);
                float tangentSign = _random.NextDouble() < 0.5d ? -1f : 1f;
                Vector2 tangentialKick = tangentDirection * (
                    settings.UnstableJitterAccel *
                    (settings.UnstableTangentialKickBaseFactor + pressure * settings.UnstableTangentialKickPressureFactor) *
                    tangentSign);
                Vector2 centerPull = toCenterDirection * (
                    settings.UnstableCenterPullAccel *
                    (settings.UnstableCenterPullBaseFactor + pressure * settings.UnstableCenterPullPressureFactor));

                if (_random.NextDouble() < burstChance)
                {
                    float burstAngle = (float)(_random.NextDouble() * MathF.Tau);
                    Vector2 burstDirection = new(MathF.Cos(burstAngle), MathF.Sin(burstAngle));
                    float burstMagnitude = settings.UnstableJitterAccel * MathHelper.Lerp(settings.UnstableBurstFactorMin, settings.UnstableBurstFactorMax, (float)_random.NextDouble());
                    clump.LocalVelocity += burstDirection * burstMagnitude * dt;
                }

                clump.LocalVelocity += (randomKick + tangentialKick + centerPull) * dt;
                clump.LocalVelocity *= damping;
                ClampVelocity(ref clump.LocalVelocity, settings.UnstableMaxSpeed);
                clump.LocalOffset += clump.LocalVelocity * dt;

                float offsetSq = clump.LocalOffset.LengthSquared();
                if (offsetSq > allowedRadiusSq)
                {
                    float offsetLen = MathF.Sqrt(offsetSq);
                    Vector2 normal = clump.LocalOffset / MathF.Max(offsetLen, 0.001f);
                    clump.LocalOffset = normal * allowedRadius;

                    float outwardSpeed = Vector2.Dot(clump.LocalVelocity, normal);
                    if (outwardSpeed > 0f)
                    {
                        clump.LocalVelocity -= normal * (outwardSpeed * settings.UnstableBoundaryBounceFactor);
                    }

                    Vector2 boundaryTangent = new(-normal.Y, normal.X);
                    float boundarySign = _random.NextDouble() < 0.5d ? -1f : 1f;
                    clump.LocalVelocity += boundaryTangent * (settings.UnstableJitterAccel * settings.UnstableBoundaryTangentialFactor * boundarySign * dt);
                }

                float pulse = 0.5f + (0.5f * MathF.Sin((Core.GAMETIME * MathF.Tau * settings.UnstableAlphaPulseHz) + clump.FlickerPhase));
                float stochasticNoise = ((float)_random.NextDouble() - 0.5f) * settings.UnstableAlphaNoiseAmplitude;
                float alphaProgress = MathHelper.Clamp(
                    (pulse * settings.UnstableAlphaPulseWeight) +
                    stochasticNoise +
                    (pressure * settings.UnstableAlphaPressureWeight),
                    0f,
                    1f);
                float targetAlpha = settings.UnstableAlphaMin + (alphaRange * alphaProgress);
                float alphaBlend = 1f - MathF.Exp(-settings.UnstableAlphaLerpPerSecond * dt);
                clump.Alpha = MathHelper.Lerp(clump.Alpha, targetAlpha, alphaBlend);
                clump.Alpha = MathHelper.Clamp(clump.Alpha, settings.UnstableAlphaMin, settings.UnstableAlphaMax);

                state.UnstableClumps[i] = clump;
            }
        }

        private static List<DropSeed> ConsumeUnstableDropSeeds(int sourceID)
        {
            if (!_unstableDropStates.TryGetValue(sourceID, out UnstableDropState unstableState))
            {
                return null;
            }

            List<DropSeed> seeds = new(unstableState.UnstableClumps.Count);
            for (int i = 0; i < unstableState.UnstableClumps.Count; i++)
            {
                UnstableClump unstableClump = unstableState.UnstableClumps[i];
                seeds.Add(new DropSeed
                {
                    Position = unstableState.Center + unstableClump.LocalOffset,
                    Velocity = unstableClump.LocalVelocity
                });
            }

            _unstableDropStates.Remove(sourceID);
            return seeds;
        }

        private static int CountUnstableClumps()
        {
            int total = 0;
            foreach (UnstableDropState state in _unstableDropStates.Values)
            {
                total += state.UnstableClumps.Count;
            }

            return total;
        }

        private static void ApplyClusterForces(in ClumpSettings settings, float dt)
        {
            if (_freeClumps.Count < 2)
            {
                return;
            }

            bool clusterForcesEnabled = settings.ClusterAttractForce > 0f || settings.ClusterRepelForce > 0f;
            bool visualMergeEnabled = settings.VisualMergeGrowth > 0f && settings.VisualMergeRadius > 0f;
            if (!clusterForcesEnabled && !visualMergeEnabled)
            {
                return;
            }

            float interactionRadius = MathF.Max(MathF.Max(settings.ClusterRadius, settings.VisualMergeRadius), settings.ClumpRadius * 2f);
            float cellSize = interactionRadius;
            if (cellSize <= 0f)
            {
                return;
            }

            foreach (List<int> list in _clusterGrid.Values)
            {
                list.Clear();
                _clusterGridListPool.Push(list);
            }

            _clusterGrid.Clear();

            for (int i = 0; i < _freeClumps.Count; i++)
            {
                FreeClump clump = _freeClumps[i];
                int cx = (int)MathF.Floor(clump.Position.X / cellSize);
                int cy = (int)MathF.Floor(clump.Position.Y / cellSize);
                long key = ComposeGridKey(cx, cy);
                if (!_clusterGrid.TryGetValue(key, out List<int> list))
                {
                    list = _clusterGridListPool.Count > 0 ? _clusterGridListPool.Pop() : new List<int>(8);
                    _clusterGrid[key] = list;
                }

                list.Add(i);
            }

            float maxDistanceSquared = interactionRadius * interactionRadius;
            float homeostasis = MathF.Max(0.001f, settings.ClusterHomeostasisDistance);
            float attractRange = MathF.Max(settings.ClusterRadius - homeostasis, 0.001f);

            foreach (KeyValuePair<long, List<int>> cell in _clusterGrid)
            {
                long keyA = cell.Key;
                List<int> listA = cell.Value;
                DecomposeGridKey(keyA, out int cx, out int cy);

                for (int ny = cy - 1; ny <= cy + 1; ny++)
                {
                    for (int nx = cx - 1; nx <= cx + 1; nx++)
                    {
                        long keyB = ComposeGridKey(nx, ny);
                        if (keyB < keyA || !_clusterGrid.TryGetValue(keyB, out List<int> listB))
                        {
                            continue;
                        }

                        if (keyA == keyB)
                        {
                            for (int i = 0; i < listA.Count - 1; i++)
                            {
                                for (int j = i + 1; j < listA.Count; j++)
                                {
                                    ApplyClusterPair(
                                        listA[i],
                                        listA[j],
                                        settings,
                                        dt,
                                        maxDistanceSquared,
                                        homeostasis,
                                        attractRange);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < listA.Count; i++)
                            {
                                for (int j = 0; j < listB.Count; j++)
                                {
                                    ApplyClusterPair(
                                        listA[i],
                                        listB[j],
                                        settings,
                                        dt,
                                        maxDistanceSquared,
                                        homeostasis,
                                        attractRange);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ApplyClusterPair(
            int indexA,
            int indexB,
            in ClumpSettings settings,
            float dt,
            float maxDistanceSquared,
            float homeostasisDistance,
            float attractRange)
        {
            FreeClump a = _freeClumps[indexA];
            FreeClump b = _freeClumps[indexB];

            if (a.IsConsuming || b.IsConsuming)
            {
                return;
            }

            Vector2 delta = b.Position - a.Position;
            float distSq = delta.LengthSquared();
            if (distSq < 0.0001f || distSq > maxDistanceSquared)
            {
                return;
            }

            float dist = MathF.Sqrt(distSq);
            Vector2 direction = delta / dist;

            if (settings.ClusterAttractForce > 0f || settings.ClusterRepelForce > 0f)
            {
                float force;
                if (dist > homeostasisDistance)
                {
                    float t = MathHelper.Clamp((dist - homeostasisDistance) / attractRange, 0f, 1f);
                    force = settings.ClusterAttractForce * t * ClusterAttractGlobalScale;

                    float playerInfluence = MathF.Max(GetPlayerInfluenceStrength(indexA), GetPlayerInfluenceStrength(indexB));
                    float pullPriorityScale = MathHelper.Lerp(1f, ClusterAttractNearPlayerScale, playerInfluence);
                    force *= pullPriorityScale;
                }
                else
                {
                    float t = 1f - (dist / homeostasisDistance);
                    force = -settings.ClusterRepelForce * t;
                }

                Vector2 deltaVelocity = direction * (force * dt);
                _clusterDeltaVelocities[indexA] += deltaVelocity;
                _clusterDeltaVelocities[indexB] -= deltaVelocity;
            }

            if (settings.VisualMergeGrowth > 0f && settings.VisualMergeRadius > 0f && dist <= settings.VisualMergeRadius)
            {
                float proximity = 1f - MathHelper.Clamp(dist / MathF.Max(settings.VisualMergeRadius, 0.001f), 0f, 1f);
                float contribution = 0.2f + (proximity * proximity * 0.8f);
                _visualDensityScores[indexA] += contribution;
                _visualDensityScores[indexB] += contribution;
                _neighborMorphInfluences[indexA] += direction * contribution;
                _neighborMorphInfluences[indexB] -= direction * contribution;
            }
        }

        private static void ClampVelocity(ref Vector2 velocity, float maxSpeed)
        {
            if (maxSpeed <= 0f)
            {
                return;
            }

            float maxSpeedSq = maxSpeed * maxSpeed;
            float speedSq = velocity.LengthSquared();
            if (speedSq > maxSpeedSq)
            {
                velocity = Vector2.Normalize(velocity) * maxSpeed;
            }
        }

        private static void EnsureClusterVelocityBuffer(int count)
        {
            if (_clusterDeltaVelocities.Length >= count)
            {
                return;
            }

            int newSize = Math.Max(count, _clusterDeltaVelocities.Length * 2 + 8);
            _clusterDeltaVelocities = new Vector2[newSize];
        }

        private static void EnsureVisualDensityBuffer(int count)
        {
            if (_visualDensityScores.Length >= count)
            {
                return;
            }

            int newSize = Math.Max(count, _visualDensityScores.Length * 2 + 8);
            _visualDensityScores = new float[newSize];
        }

        private static void EnsureNearestAgentBuffer(int count)
        {
            if (_nearestAgents.Length >= count && _nearestAgentDistances.Length >= count)
            {
                return;
            }

            int newSize = Math.Max(count, Math.Max(_nearestAgents.Length, _nearestAgentDistances.Length) * 2 + 8);
            Agent[] newAgents = new Agent[newSize];
            float[] newDistances = new float[newSize];

            if (_nearestAgents.Length > 0)
            {
                Array.Copy(_nearestAgents, newAgents, _nearestAgents.Length);
            }

            if (_nearestAgentDistances.Length > 0)
            {
                Array.Copy(_nearestAgentDistances, newDistances, _nearestAgentDistances.Length);
            }

            _nearestAgents = newAgents;
            _nearestAgentDistances = newDistances;
        }

        private static void EnsureMorphInfluenceBuffers(int count)
        {
            if (_playerMorphInfluences.Length < count)
            {
                int newSize = Math.Max(count, _playerMorphInfluences.Length * 2 + 8);
                Vector2[] newBuffer = new Vector2[newSize];
                if (_playerMorphInfluences.Length > 0)
                {
                    Array.Copy(_playerMorphInfluences, newBuffer, _playerMorphInfluences.Length);
                }

                _playerMorphInfluences = newBuffer;
            }

            if (_neighborMorphInfluences.Length < count)
            {
                int newSize = Math.Max(count, _neighborMorphInfluences.Length * 2 + 8);
                Vector2[] newBuffer = new Vector2[newSize];
                if (_neighborMorphInfluences.Length > 0)
                {
                    Array.Copy(_neighborMorphInfluences, newBuffer, _neighborMorphInfluences.Length);
                }

                _neighborMorphInfluences = newBuffer;
            }
        }

        private static void UpdateNearestAgentInfluence(in ClumpSettings settings)
        {
            int count = _freeClumps.Count;
            for (int i = 0; i < count; i++)
            {
                FreeClump clump = _freeClumps[i];
                Agent nearestAgent = FindNearestAgent(clump.Position, out float nearestDistance);
                _nearestAgents[i] = nearestAgent;
                _nearestAgentDistances[i] = nearestDistance;

                if (nearestAgent == null || nearestDistance <= 0f || nearestDistance > settings.PullZoneRadius)
                {
                    _playerMorphInfluences[i] = Vector2.Zero;
                    continue;
                }

                Vector2 toAgent = nearestAgent.Position - clump.Position;
                float length = toAgent.Length();
                if (length <= 0.0001f)
                {
                    _playerMorphInfluences[i] = Vector2.Zero;
                    continue;
                }

                float pullInfluence = ComputePullInfluenceFromDistance(nearestDistance, settings);
                _playerMorphInfluences[i] = (toAgent / length) * pullInfluence;
            }
        }

        private static float ComputePullInfluenceFromDistance(float distance, in ClumpSettings settings)
        {
            if (distance <= settings.AbsorbZoneRadius)
            {
                return 1f;
            }

            float blendDenominator = MathF.Max(settings.PullZoneRadius - settings.AbsorbZoneRadius, 0.001f);
            float blend = MathHelper.Clamp((distance - settings.AbsorbZoneRadius) / blendDenominator, 0f, 1f);
            return 1f - blend;
        }

        private static float GetPlayerInfluenceStrength(int clumpIndex)
        {
            if (clumpIndex < 0 || clumpIndex >= _playerMorphInfluences.Length)
            {
                return 0f;
            }

            return MathHelper.Clamp(_playerMorphInfluences[clumpIndex].Length(), 0f, 1f);
        }

        private static long ComposeGridKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private static void DecomposeGridKey(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)key;
        }

        private static float ComputeRenderAlpha(in FreeClump clump, in ClumpSettings settings)
        {
            float alpha = 1f;

            if (clump.DeadZoneElapsed > settings.DeadZoneStartSeconds)
            {
                float despawnProgress = MathHelper.Clamp(
                    (clump.DeadZoneElapsed - settings.DeadZoneStartSeconds) / MathF.Max(settings.DeadZoneDespawnSeconds, 0.001f),
                    0f,
                    1f);

                float pulseSpeed = MathHelper.Lerp(settings.DeadPulseSpeedMin, settings.DeadPulseSpeedMax, despawnProgress);
                float primaryPulse = 0.5f + 0.5f * MathF.Sin((Core.GAMETIME + clump.FlickerPhase) * MathF.Tau * pulseSpeed);
                float secondaryPulse = 0.5f + 0.5f * MathF.Sin((Core.GAMETIME * 0.87f + clump.FlickerPhase * 1.23f) * MathF.Tau * (pulseSpeed * 0.46f));
                float pulse = MathHelper.Clamp((primaryPulse * 0.72f) + (secondaryPulse * 0.28f), 0f, 1f);
                pulse *= pulse;
                float lowAlpha = MathHelper.Lerp(settings.DeadPulseLowAlphaStart, settings.DeadPulseLowAlphaEnd, despawnProgress);
                alpha *= MathHelper.Lerp(lowAlpha, 1f, pulse);
            }

            if (clump.IsConsuming)
            {
                float consumeAlpha = MathHelper.Clamp(clump.ConsumeTimer / MathF.Max(settings.AbsorbFadeSeconds, 0.001f), 0f, 1f);
                alpha *= consumeAlpha;
            }

            return alpha;
        }

        private static float ComputeConsumeGrowthScale(in FreeClump clump, in ClumpSettings settings)
        {
            if (!clump.IsConsuming)
            {
                return 1f;
            }

            float fadeDuration = MathF.Max(settings.AbsorbFadeSeconds, 0.001f);
            float progress = 1f - MathHelper.Clamp(clump.ConsumeTimer / fadeDuration, 0f, 1f);
            float maxGrowthScale = MathF.Max(1f, settings.AbsorbConsumeGrowMaxScale);
            float growthProgress = 1f - MathF.Pow(1f - progress, settings.ConsumeGrowthExponent);
            return MathHelper.Lerp(1f, maxGrowthScale, growthProgress);
        }

        private static void UpdateVisualSizeScales(in ClumpSettings settings, float dt)
        {
            if (_freeClumps.Count == 0)
            {
                return;
            }

            float maxScale = MathF.Max(1f, settings.VisualMergeMaxScale);
            float blend = 1f - MathF.Exp(-settings.VisualMergeScaleLerpPerSecond * MathF.Max(dt, 0f));

            for (int i = 0; i < _freeClumps.Count; i++)
            {
                FreeClump clump = _freeClumps[i];
                float targetScale = 1f + (_visualDensityScores[i] * settings.VisualMergeGrowth);
                targetScale = MathHelper.Clamp(targetScale, 1f, maxScale);

                if (clump.VisualSizeScale <= 0f)
                {
                    clump.VisualSizeScale = 1f;
                }

                clump.VisualSizeScale = MathHelper.Lerp(clump.VisualSizeScale, targetScale, blend);
                _freeClumps[i] = clump;
            }
        }

        private static Color GetCoreColor(Color sourceColor)
        {
            return Color.White;
        }

        private static Color GetGlowColor(Color sourceColor)
        {
            return Color.White;
        }

        private static Vector3 GetSourceTintVector(Color sourceColor)
        {
            Vector3 tint = sourceColor.ToVector3();
            if (tint.LengthSquared() < 0.0001f)
            {
                tint = new Vector3(0.72f, 0.72f, 0.72f);
            }

            float maxComponent = MathF.Max(tint.X, MathF.Max(tint.Y, tint.Z));
            if (maxComponent < 0.12f)
            {
                tint += new Vector3(0.12f);
            }

            return Vector3.Clamp(tint, Vector3.Zero, Vector3.One);
        }

        private static Texture2D GetCoreTextureForSeed(int seed)
        {
            return GetVariantTexture(_orbCoreVariantTextures, _orbCoreTexture, seed);
        }

        private static Texture2D GetGlowTextureForSeed(int seed)
        {
            return GetVariantTexture(_orbGlowVariantTextures, _orbGlowTexture, seed);
        }

        private static Texture2D GetVariantTexture(Texture2D[] variants, Texture2D fallback, int seed)
        {
            if (variants != null && variants.Length > 0)
            {
                int variantIndex = GetVariantIndex(seed, variants.Length);
                Texture2D texture = variants[variantIndex];
                if (texture != null && !texture.IsDisposed)
                {
                    return texture;
                }
            }

            if (fallback != null && !fallback.IsDisposed)
            {
                return fallback;
            }

            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    Texture2D texture = variants[i];
                    if (texture != null && !texture.IsDisposed)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static int HashSeed(int seedA, int seedB)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + seedA;
                hash = (hash * 31) + seedB;
                return hash;
            }
        }

        private static int GetVariantIndex(int seed, int variantCount)
        {
            if (variantCount <= 0)
            {
                return 0;
            }

            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return (int)(x % (uint)variantCount);
            }
        }

        private static void ComputeFreeMorphTransform(int clumpIndex, in FreeClump clump, in ClumpSettings settings, float baseScale, out Vector2 morphScale, out float morphRotation)
        {
            Vector2 playerInfluence = SampleMorphInfluence(_playerMorphInfluences, clumpIndex, 1f);
            Vector2 neighborInfluence = SampleMorphInfluence(_neighborMorphInfluences, clumpIndex, 2.8f);

            Vector2 velocityDirection = Vector2.Zero;
            float speed = clump.Velocity.Length();
            if (speed > 0.001f)
            {
                velocityDirection = clump.Velocity / speed;
            }

            float velocityInfluence = settings.PullSpeedMax > 0f
                ? MathHelper.Clamp(speed / settings.PullSpeedMax, 0f, 1f)
                : 0f;

            Vector2 axis = (playerInfluence * MorphPlayerWeight) +
                (neighborInfluence * MorphNeighborWeight) +
                (velocityDirection * (MorphVelocityWeight * velocityInfluence));

            float axisLength = axis.Length();
            if (axisLength <= 0.0001f)
            {
                morphRotation = 0f;
                morphScale = new Vector2(baseScale, baseScale);
                return;
            }

            Vector2 axisDirection = axis / axisLength;
            morphRotation = MathF.Atan2(axisDirection.Y, axisDirection.X);

            float stretchStrength = MathHelper.Clamp(axisLength * 0.24f, 0f, MorphMaxStretch - 1f);
            float stretch = 1f + stretchStrength;
            float squash = MathHelper.Clamp(1f - (stretchStrength * 0.68f), 0.72f, 1f);

            morphScale = new Vector2(baseScale * stretch, baseScale * squash);
        }

        private static Vector2 SampleMorphInfluence(Vector2[] source, int index, float maxLength)
        {
            if (source == null || index < 0 || index >= source.Length || maxLength <= 0f)
            {
                return Vector2.Zero;
            }

            Vector2 value = source[index];
            float length = value.Length();
            if (length <= 0.0001f)
            {
                return Vector2.Zero;
            }

            float scaled = MathHelper.Clamp(length / maxLength, 0f, 1f);
            return value / length * scaled;
        }

        private static Vector2 ComputeShadowOffset(float radius, float scaleMultiplier, float offsetScale = 1f)
        {
            float offsetMagnitude = MathF.Max(1f, radius * 0.52f) * MathF.Pow(MathF.Max(1f, scaleMultiplier), 0.65f);
            return ShadowOffsetDirection * (offsetMagnitude * MathF.Max(0f, offsetScale));
        }

        private static float ComputeShadowAlpha(float clumpAlpha, in ClumpSettings settings, bool isFreeClump)
        {
            float boostedAlpha = clumpAlpha * settings.ShadowAlphaScale * ShadowVisibilityBoost;
            float floorAlpha = clumpAlpha * MinShadowAlphaFactor;

            if (isFreeClump)
            {
                boostedAlpha *= FreeShadowAlphaScale;
                floorAlpha *= FreeShadowAlphaScale;
            }

            return MathHelper.Clamp(MathF.Max(boostedAlpha, floorAlpha), 0f, 1f);
        }

        private static void EnsureTextures(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (!AreTextureVariantsValid(_orbCoreVariantTextures))
            {
                _orbCoreVariantTextures = BuildOrbTextureVariants(graphicsDevice, 28, 0.32f, 1.65f);
            }

            if (!AreTextureVariantsValid(_orbGlowVariantTextures))
            {
                _orbGlowVariantTextures = BuildOrbTextureVariants(graphicsDevice, 54, 0.08f, 2.6f);
            }

            _orbCoreTexture = GetFirstValidTexture(_orbCoreVariantTextures) ?? _orbCoreTexture;
            _orbGlowTexture = GetFirstValidTexture(_orbGlowVariantTextures) ?? _orbGlowTexture;

            if (_orbCoreTexture == null || _orbCoreTexture.IsDisposed)
            {
                _orbCoreTexture = BuildOrbTexture(graphicsDevice, 28, 0.32f, 1.65f);
            }

            if (_orbGlowTexture == null || _orbGlowTexture.IsDisposed)
            {
                _orbGlowTexture = BuildOrbTexture(graphicsDevice, 54, 0.08f, 2.6f);
            }
        }

        private static bool AreTextureVariantsValid(Texture2D[] textures)
        {
            if (textures == null || textures.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null || textures[i].IsDisposed)
                {
                    return false;
                }
            }

            return true;
        }

        private static Texture2D GetFirstValidTexture(Texture2D[] textures)
        {
            if (textures == null || textures.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                if (texture != null && !texture.IsDisposed)
                {
                    return texture;
                }
            }

            return null;
        }

        private static Texture2D[] BuildOrbTextureVariants(GraphicsDevice graphicsDevice, int diameter, float hardCoreRadius, float fadeExponent)
        {
            int count = Math.Max(1, BlobVariantCount);
            Texture2D[] textures = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                int variantSeed = 1009 + (i * 7919);
                textures[i] = BuildOrbTexture(
                    graphicsDevice,
                    diameter,
                    hardCoreRadius,
                    fadeExponent,
                    variantSeed,
                    BlobTextureIrregularity);
            }

            return textures;
        }

        private static Texture2D BuildOrbTexture(GraphicsDevice graphicsDevice, int diameter, float hardCoreRadius, float fadeExponent)
        {
            return BuildOrbTexture(graphicsDevice, diameter, hardCoreRadius, fadeExponent, variantSeed: 0, irregularity: 0f);
        }

        private static Texture2D BuildOrbTexture(GraphicsDevice graphicsDevice, int diameter, float hardCoreRadius, float fadeExponent, int variantSeed, float irregularity)
        {
            int pixelCount = diameter * diameter;
            Color[] data = new Color[pixelCount];
            float radius = (diameter - 1) * 0.5f;
            float hardRadius = MathHelper.Clamp(hardCoreRadius, 0f, 1f);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float distance = MathF.Sqrt(dx * dx + dy * dy) / MathF.Max(radius, 0.001f);
                    float boundaryScale = 1f;
                    if (irregularity > 0f)
                    {
                        float angle = MathF.Atan2(dy, dx);
                        boundaryScale = ComputeBlobBoundaryScale(angle, variantSeed, irregularity);
                    }

                    float normalizedDistance = distance / MathF.Max(boundaryScale, 0.001f);

                    if (normalizedDistance >= 1f)
                    {
                        data[(y * diameter) + x] = Color.Transparent;
                        continue;
                    }

                    float alpha;
                    if (normalizedDistance <= hardRadius)
                    {
                        alpha = 1f;
                    }
                    else
                    {
                        float t = MathHelper.Clamp((normalizedDistance - hardRadius) / MathF.Max(1f - hardRadius, 0.001f), 0f, 1f);
                        alpha = MathF.Pow(1f - t, fadeExponent);
                    }

                    byte a = (byte)MathHelper.Clamp(alpha * 255f, 0f, 255f);
                    data[(y * diameter) + x] = new Color((byte)255, (byte)255, (byte)255, a);
                }
            }

            Texture2D texture = new(graphicsDevice, diameter, diameter);
            texture.SetData(data);
            return texture;
        }

        private static float ComputeBlobBoundaryScale(float angle, int variantSeed, float irregularity)
        {
            float phaseA = Hash01(variantSeed, 1) * MathF.Tau;
            float phaseB = Hash01(variantSeed, 2) * MathF.Tau;
            float phaseC = Hash01(variantSeed, 3) * MathF.Tau;
            float phaseD = Hash01(variantSeed, 4) * MathF.Tau;

            float frequencyA = 2f + (Hash01(variantSeed, 5) * 1.5f);
            float frequencyB = 3.5f + (Hash01(variantSeed, 6) * 1.8f);
            float frequencyC = 5.5f + (Hash01(variantSeed, 7) * 2.2f);
            float rippleFrequency = 9f + (Hash01(variantSeed, 8) * 5f);

            float waveA = MathF.Sin((angle * frequencyA) + phaseA) * (0.7f + (0.3f * Hash01(variantSeed, 9)));
            float waveB = MathF.Sin((angle * frequencyB) + phaseB) * (0.55f + (0.45f * Hash01(variantSeed, 10)));
            float waveC = MathF.Sin((angle * frequencyC) + phaseC) * (0.5f + (0.5f * Hash01(variantSeed, 11)));
            float ripple = MathF.Sin((angle * rippleFrequency) + phaseD) * 0.2f;

            float blendedWave = ((waveA + waveB + waveC) / 3f * 0.88f) + ripple;
            float boundary = 1f + (blendedWave * irregularity);
            return MathHelper.Clamp(boundary, 0.72f, 1.3f);
        }

        private static float Hash01(int seed, int salt)
        {
            unchecked
            {
                uint x = (uint)(seed * 73856093) ^ (uint)(salt * 19349663);
                x ^= x >> 17;
                x *= 0xed5ad4bbu;
                x ^= x >> 11;
                x *= 0xac4c1b51u;
                x ^= x >> 15;
                x *= 0x31848babu;
                x ^= x >> 14;
                return (x & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static ClumpSettings LoadSettings()
        {
            int pickupPerSecond = Math.Max(1, DatabaseFetch.GetSetting<int>("FXSettings", "Value", "SettingKey", "XPClumpPickupPerSecond", 12));

            float absorbZoneRadius = MathF.Max(6f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbZoneRadius", 85f));
            float pullZoneRadius = MathF.Max(absorbZoneRadius + 1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpPullZoneRadius", 240f));
            float deadZoneRadius = MathF.Max(pullZoneRadius, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadZoneRadius", 240f));

            float deadZoneStartSeconds = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadZoneStartSeconds", 20f));
            float deadZoneDespawnSeconds = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadZoneDespawnSeconds", 10f));

            float pullSpeedMin = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpPullSpeedMin", 0f));
            float pullSpeedMax = MathF.Max(pullSpeedMin, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpPullSpeedMax", 250f));
            float pullVelocityLerp = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpPullVelocityLerpPerSecond", 4f));

            float clumpRadius = MathF.Max(2f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpRadius", 6f));
            float spawnSpreadRadius = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpSpawnSpreadRadius", 18f));
            float spawnInitialSpeed = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpSpawnInitialSpeed", 24f));
            float maxSpeed = MathF.Max(1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpMaxSpeed", 320f));
            float velocityDamping = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpVelocityDampingPerSecond", 1.8f));

            float clusterRadius = MathF.Max(clumpRadius * 2f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpClusterRadius", 110f));
            float clusterAttractForce = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpClusterAttractForce", 45f));
            float clusterHomeostasis = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpClusterHomeostasisDistance", 8f));
            float clusterRepelForce = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpClusterRepelForce", 95f));
            float visualMergeRadius = MathF.Max(clumpRadius, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpVisualMergeRadius", 24f));
            float visualMergeGrowth = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpVisualMergeGrowth", 0.19f));
            float visualMergeMaxScale = MathF.Max(1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpVisualMergeMaxScale", 4.5f));
            float visualMergeScaleLerpPerSecond = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpVisualMergeScaleLerpPerSecond", 8f));

            float absorbConsumeDistance = MathF.Max(2f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbConsumeDistance", 10f));
            float absorbFadeSeconds = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbFadeSeconds", 0.42f));
            float absorbConsumeGrowMaxScale = MathF.Max(1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbConsumeGrowMaxScale", 2.6f));
            float consumeGrowthExponent = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpConsumeGrowthExponent", 2.2f));

            float absorbOrbitMaxAngularSpeedDeg = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitMaxAngularSpeedDeg", 170f));
            float absorbOrbitAngularBlendPerSecond = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitAngularBlendPerSecond", 3.2f));
            float absorbOrbitAngularDampingPerSecond = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitAngularDampingPerSecond", 3.6f));
            float absorbOrbitCollapseSpeed = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitCollapseSpeed", 72f));
            float absorbConsumeCollapseSpeed = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbConsumeCollapseSpeed", 120f));
            float absorbOrbitFollowGain = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitFollowGain", 6.2f));
            float absorbVelocityLerpPerSecond = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbVelocityLerpPerSecond", 8f));
            float absorbOrbitTangentialVelocityWeight = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitTangentialVelocityWeight", 0.35f));
            float absorbOrbitInitialRadiusFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitInitialRadiusFactor", 0.35f));
            float absorbOrbitMinRadiusFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitMinRadiusFactor", 0.05f));
            float absorbOrbitMinRadiusAbsolute = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitMinRadiusAbsolute", 0.35f));
            float absorbOrbitCollapseBoostLow = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitCollapseBoostLow", 1.15f));
            float absorbOrbitCollapseBoostHigh = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitCollapseBoostHigh", 1f));
            float absorbOrbitInwardCollapseFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitInwardCollapseFactor", 0.12f));
            float absorbOrbitMaxInwardSpeedMin = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitMaxInwardSpeedMin", 40f));
            float absorbOrbitMaxInwardSpeedPullScale = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbOrbitMaxInwardSpeedPullScale", 1.5f));
            float absorbConsumeExtraInwardFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpAbsorbConsumeExtraInwardFactor", 0.35f));

            float coreHighlightScale = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpCoreHighlightScale", 0.45f));
            float coreHighlightAlphaScale = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpCoreHighlightAlphaScale", 0.24f));
            float glowScale = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpGlowScale", 1.9f));
            float glowAlphaScale = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpGlowAlphaScale", 0.58f));
            float shadowScale = MathF.Max(1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpShadowScale", 1.55f));
            float shadowAlphaScale = MathHelper.Clamp(DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpShadowAlphaScale", 0.4f), 0f, 1f);
            float deadPulseSpeedMin = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadPulseSpeedMin", 3.8f));
            float deadPulseSpeedMax = MathF.Max(deadPulseSpeedMin, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadPulseSpeedMax", 12f));
            float deadPulseLowAlphaStart = MathHelper.Clamp(DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadPulseLowAlphaStart", 0.55f), 0f, 1f);
            float deadPulseLowAlphaEnd = MathHelper.Clamp(DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPClumpDeadPulseLowAlphaEnd", 0.03f), 0f, 1f);

            float unstableHealthThresholdRatio = MathHelper.Clamp(
                DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableHealthThresholdRatio", 0.33333334f),
                0f,
                1f);
            float unstableJitterAccel = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableJitterAccel", 1500f));
            float unstableCenterPullAccel = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableCenterPullAccel", 300f));
            float unstableVelocityDamping = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableVelocityDampingPerSecond", 2.7f));
            float unstableMaxSpeed = MathF.Max(1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableMaxSpeed", 360f));
            float unstableAlphaMin = MathHelper.Clamp(DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaMin", 0.2f), 0f, 1f);
            float unstableAlphaMax = MathHelper.Clamp(DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaMax", 0.95f), unstableAlphaMin, 1f);
            float unstableAlphaPulseHz = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaPulseHz", 10.5f));
            float unstableRadiusScale = MathHelper.Clamp(DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableRadiusScale", 0.49f), 0.05f, 1f);
            float unstableRenderScale = MathF.Max(0.1f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableRenderScale", 1f));
            float unstableFadeOutSeconds = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableFadeOutSeconds", 0.55f));
            float unstableShowLerpPerSecond = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableShowLerpPerSecond", 12f));
            float unstableVisibilityEpsilon = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableVisibilityEpsilon", 0.001f));
            float unstableBurstRatePerSecond = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableBurstRatePerSecond", 2.6f));
            float unstableRandomKickFactorMin = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableRandomKickFactorMin", 1f));
            float unstableRandomKickFactorMax = MathF.Max(unstableRandomKickFactorMin, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableRandomKickFactorMax", 2.15f));
            float unstableTangentialKickBaseFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableTangentialKickBaseFactor", 0.34f));
            float unstableTangentialKickPressureFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableTangentialKickPressureFactor", 0.62f));
            float unstableCenterPullBaseFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableCenterPullBaseFactor", 0.18f));
            float unstableCenterPullPressureFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableCenterPullPressureFactor", 0.5f));
            float unstableBurstFactorMin = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableBurstFactorMin", 1.3f));
            float unstableBurstFactorMax = MathF.Max(unstableBurstFactorMin, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableBurstFactorMax", 2.35f));
            float unstableBoundaryBounceFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableBoundaryBounceFactor", 2.2f));
            float unstableBoundaryTangentialFactor = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableBoundaryTangentialFactor", 0.35f));
            float unstableAlphaNoiseAmplitude = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaNoiseAmplitude", 0.9f));
            float unstableAlphaPulseWeight = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaPulseWeight", 0.62f));
            float unstableAlphaPressureWeight = MathF.Max(0f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaPressureWeight", 0.28f));
            float unstableAlphaLerpPerSecond = MathF.Max(0.01f, DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "XPDropUnstableAlphaLerpPerSecond", 14f));

            return new ClumpSettings(
                pickupPerSecond,
                deadZoneRadius,
                pullZoneRadius,
                absorbZoneRadius,
                deadZoneStartSeconds,
                deadZoneDespawnSeconds,
                pullSpeedMin,
                pullSpeedMax,
                pullVelocityLerp,
                clumpRadius,
                spawnSpreadRadius,
                spawnInitialSpeed,
                maxSpeed,
                velocityDamping,
                clusterRadius,
                clusterAttractForce,
                clusterHomeostasis,
                clusterRepelForce,
                visualMergeRadius,
                visualMergeGrowth,
                visualMergeMaxScale,
                absorbConsumeDistance,
                absorbFadeSeconds,
                absorbConsumeGrowMaxScale,
                unstableHealthThresholdRatio,
                unstableJitterAccel,
                unstableCenterPullAccel,
                unstableVelocityDamping,
                unstableMaxSpeed,
                unstableAlphaMin,
                unstableAlphaMax,
                unstableAlphaPulseHz,
                unstableRadiusScale,
                unstableRenderScale,
                unstableFadeOutSeconds,
                absorbOrbitMaxAngularSpeedDeg,
                absorbOrbitAngularBlendPerSecond,
                absorbOrbitAngularDampingPerSecond,
                absorbOrbitCollapseSpeed,
                absorbConsumeCollapseSpeed,
                absorbOrbitFollowGain,
                absorbVelocityLerpPerSecond,
                absorbOrbitTangentialVelocityWeight,
                absorbOrbitInitialRadiusFactor,
                absorbOrbitMinRadiusFactor,
                absorbOrbitMinRadiusAbsolute,
                absorbOrbitCollapseBoostLow,
                absorbOrbitCollapseBoostHigh,
                absorbOrbitInwardCollapseFactor,
                absorbOrbitMaxInwardSpeedMin,
                absorbOrbitMaxInwardSpeedPullScale,
                absorbConsumeExtraInwardFactor,
                coreHighlightScale,
                coreHighlightAlphaScale,
                glowScale,
                glowAlphaScale,
                shadowScale,
                shadowAlphaScale,
                deadPulseSpeedMin,
                deadPulseSpeedMax,
                deadPulseLowAlphaStart,
                deadPulseLowAlphaEnd,
                consumeGrowthExponent,
                visualMergeScaleLerpPerSecond,
                unstableShowLerpPerSecond,
                unstableVisibilityEpsilon,
                unstableBurstRatePerSecond,
                unstableRandomKickFactorMin,
                unstableRandomKickFactorMax,
                unstableTangentialKickBaseFactor,
                unstableTangentialKickPressureFactor,
                unstableCenterPullBaseFactor,
                unstableCenterPullPressureFactor,
                unstableBurstFactorMin,
                unstableBurstFactorMax,
                unstableBoundaryBounceFactor,
                unstableBoundaryTangentialFactor,
                unstableAlphaNoiseAmplitude,
                unstableAlphaPulseWeight,
                unstableAlphaPressureWeight,
                unstableAlphaLerpPerSecond);
        }
    }
}



