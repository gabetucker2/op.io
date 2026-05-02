using System;
using System.Collections.Generic;

namespace op.io
{
    internal static class TerrainWorldDefaults
    {
        public const int DefaultSeed = 1337;
        public const int WorldIslandGenerationLimit = 64;
        public const int WorldLandformTypeGenerationLimit = 64;

        public const float GlobalScale = 2f;
        public const float GlobalWidthScale = 1f;
        public const float GlobalBasinCut = 1f;
        public const float GlobalOpeningWidth = 1f;
        public const float GlobalOpeningStrength = 1f;
        public const float GlobalRoughness = 0f;
        public const float GlobalEmergence = 1f;
        public const float RasterSmoothing = 1.35f;

        public const float WaterZoneDistanceScale = 0.6f;
        public const float WaterShallowDistance = 220f * WaterZoneDistanceScale;
        public const float WaterSunlitDistance = 560f * WaterZoneDistanceScale;
        public const float WaterTwilightDistance = 1040f * WaterZoneDistanceScale;
        public const float WaterMidnightDistance = 1520f * WaterZoneDistanceScale;
        public const float WaterStochasticReach = 160f;
        public const float WaterStochasticScale = 840f;
        public const float WaterCoastShapeRounding = 0.70f;
        public const float WaterDepthRampMax = 1.10f;

        public const float WorldIslandCount = 18f;
        public const float WorldMinimumSpacing = 691f;
        public const float WorldInteractionSpacing = 980f;
        public const float WorldPlacementJitter = 223.44f;
        public const float WorldClusterCountMinimum = 1f;
        public const float WorldClusterCountMaximum = 6f;
        public const float WorldClusterSpread = 0.72f;
        public const float WorldChainBias = 0.62f;
        public const int WorldClusterShapeSeed = 41003;
        public const int WorldIslandScatterSeed = 57191;
        public const int WorldIslandVariationSeed = 74471;
        public const float WorldIslandSizeVariance = 0.26f;
        public const float WorldGiantRiverRngMinimum = 0f;
        public const float WorldGiantRiverRngMaximum = 1f;
        public const float WorldCalanqueRngMinimum = 0f;
        public const float WorldCalanqueRngMaximum = 2.95f;
        public const float WorldBarrierLagoonRngMinimum = 0f;
        public const float WorldBarrierLagoonRngMaximum = 3.6f;
        public const float WorldTowerStacksRngMinimum = 0f;
        public const float WorldTowerStacksRngMaximum = 1.8f;
        public const float WorldGiantRiverSizeMinimum = 0.90f;
        public const float WorldGiantRiverSizeMaximum = 1.8925f;
        public const float WorldCalanqueSizeMinimum = 0.82f;
        public const float WorldCalanqueSizeMaximum = 3.325f;
        public const float WorldBarrierLagoonSizeMinimum = 0.88f;
        public const float WorldBarrierLagoonSizeMaximum = 1.6f;
        public const float WorldTowerStacksSizeMinimum = 0.78f;
        public const float WorldTowerStacksSizeMaximum = 1.28f;

        public const float RootCenterX = 0f;
        public const float RootCenterY = 0f;
        public const float RootBaseRadius = 980f;
        public const float RootBaseLength = 1680f;
        public const float RootBaseWidth = 92f;
        public const float RootRotationDegrees = 0f;
        public const float RootLabelOffsetY = -18f;
        public const float RootScale = 1f;
        public const float RootWidthScale = 1f;
        public const float RootBasinCut = 0.50f;
        public const float RootOpeningWidth = 0.80f;
        public const float RootOpeningStrength = 0.40f;
        public const float RootRoughness = 0.02f;
        public const float RootEmergence = 1f;

        public const float MainIslandCenterX = 0f;
        public const float MainIslandCenterY = 0f;
        public const float MainIslandBaseRadius = 390f;
        public const float MainIslandBaseLength = 560f;
        public const float MainIslandBaseWidth = 100f;
        public const float MainIslandRotationDegrees = -8f;
        public const float MainIslandLabelOffsetY = -8f;
        public const float MainIslandScale = 1f;
        public const float MainIslandWidthScale = 1f;
        public const float MainIslandBasinCut = 0.24f;
        public const float MainIslandOpeningWidth = 0.82f;
        public const float MainIslandOpeningStrength = 0.86f;
        public const float MainIslandRoughness = 0.05f;
        public const float MainIslandEmergence = 1.48f;

        public const float GiantRiverBaseRadius = 84f;
        public const float GiantRiverBaseLength = 720f;
        public const float GiantRiverBaseWidth = 78f;
        public const float GiantRiverLabelOffsetY = -6f;
        public const float GiantRiverWidthScale = 1.0f;
        public const float GiantRiverBasinCut = 1.28f;
        public const float GiantRiverOpeningWidth = 1.50f;
        public const float GiantRiverOpeningStrength = 1.34f;
        public const float GiantRiverRoughness = 0.018f;
        public const float GiantRiverEmergence = 0.72f;

        public const float CalanqueBaseRadius = 126f;
        public const float CalanqueBaseLength = 360f;
        public const float CalanqueBaseWidth = 56f;
        public const float CalanqueLabelOffsetY = -6f;
        public const float CalanqueScaleMultiplier = 1.10f;
        public const float CalanqueWidthScale = 0.94f;
        public const float CalanqueBasinCut = 1.12f;
        public const float CalanqueOpeningWidth = 0.68f;
        public const float CalanqueOpeningStrength = 1.04f;
        public const float CalanqueRoughness = 0.010f;
        public const float CalanqueEmergence = 0.86f;

        public const float BarrierLagoonBaseRadius = 182f;
        public const float BarrierLagoonBaseLength = 540f;
        public const float BarrierLagoonBaseWidth = 68f;
        public const float BarrierLagoonLabelOffsetY = -6f;
        public const float BarrierLagoonScaleMultiplier = 1.08f;
        public const float BarrierLagoonWidthScale = 0.96f;
        public const float BarrierLagoonBasinCut = 1.12f;
        public const float BarrierLagoonOpeningWidth = 1.04f;
        public const float BarrierLagoonOpeningStrength = 1.06f;
        public const float BarrierLagoonRoughness = 0.010f;
        public const float BarrierLagoonEmergence = 0.86f;

        public const float TowerStacksBaseRadius = 116f;
        public const float TowerStacksBaseLength = 300f;
        public const float TowerStacksBaseWidth = 52f;
        public const float TowerStacksLabelOffsetY = -6f;
        public const float TowerStacksWidthScale = 0.90f;
        public const float TowerStacksBasinCut = 0.62f;
        public const float TowerStacksOpeningWidth = 0.64f;
        public const float TowerStacksOpeningStrength = 0.78f;
        public const float TowerStacksRoughness = 0.010f;
        public const float TowerStacksEmergence = 0.82f;
    }

    internal readonly struct TerrainWorldPlacement
    {
        public TerrainWorldPlacement(
            string name,
            float x,
            float y,
            float radius,
            float scale,
            float widthScale,
            float rotationDegrees,
            int clusterIndex,
            int giantRiverCount,
            int calanqueCount,
            int barrierLagoonCount,
            int towerStacksCount)
        {
            Name = name;
            X = x;
            Y = y;
            Radius = radius;
            Scale = scale;
            WidthScale = widthScale;
            RotationDegrees = rotationDegrees;
            ClusterIndex = clusterIndex;
            GiantRiverCount = giantRiverCount;
            CalanqueCount = calanqueCount;
            BarrierLagoonCount = barrierLagoonCount;
            TowerStacksCount = towerStacksCount;
        }

        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public float Radius { get; }
        public float Scale { get; }
        public float WidthScale { get; }
        public float RotationDegrees { get; }
        public int ClusterIndex { get; }
        public int GiantRiverCount { get; }
        public int CalanqueCount { get; }
        public int BarrierLagoonCount { get; }
        public int TowerStacksCount { get; }
        public int LandformCount => GiantRiverCount + CalanqueCount + BarrierLagoonCount + TowerStacksCount;
    }

    internal readonly struct TerrainWorldClusterAnchor
    {
        public TerrainWorldClusterAnchor(float x, float y, float radius, float angle)
        {
            X = x;
            Y = y;
            Radius = radius;
            Angle = angle;
        }

        public float X { get; }
        public float Y { get; }
        public float Radius { get; }
        public float Angle { get; }
    }

    internal static class TerrainWorldPlacementGenerator
    {
        public static List<TerrainWorldPlacement> BuildDefaultArchipelagoPlacements(int seed)
        {
            int count = Math.Clamp((int)MathF.Round(TerrainWorldDefaults.WorldIslandCount), 1, TerrainWorldDefaults.WorldIslandGenerationLimit);
            float variance = Math.Clamp(TerrainWorldDefaults.WorldIslandSizeVariance, 0f, 2f);
            float globalScale = Math.Max(0.25f, TerrainWorldDefaults.GlobalScale);
            float baseRadius = TerrainWorldDefaults.MainIslandBaseRadius * TerrainWorldDefaults.MainIslandScale * globalScale;
            float minSpacing = Math.Max(baseRadius * 0.82f, TerrainWorldDefaults.WorldMinimumSpacing * globalScale);
            float archipelagoRadius = Math.Max(TerrainWorldDefaults.WorldInteractionSpacing * globalScale, minSpacing) * (1.25f + (count * 0.18f));
            int clusterCount = ResolveWorldClusterCount(count);
            float clusterSpread = Math.Clamp(TerrainWorldDefaults.WorldClusterSpread, 0.12f, 3.0f);
            float chainBias = Math.Clamp(TerrainWorldDefaults.WorldChainBias, 0f, 1f);
            List<TerrainWorldClusterAnchor> clusters = BuildWorldClusterAnchors(clusterCount, archipelagoRadius, minSpacing, clusterSpread, chainBias);
            int[] clusterUseCounts = new int[clusterCount];
            List<TerrainWorldPlacement> placements = new(count);

            for (int i = 0; i < count; i++)
            {
                int clusterIndex = ResolveIslandClusterIndex(i, clusterUseCounts);
                TerrainWorldClusterAnchor cluster = clusters[clusterIndex];
                float scale = Math.Clamp(1f + ((Hash(TerrainWorldDefaults.WorldIslandVariationSeed + i * 17, i) - 0.5f) * 2f * variance), 0.24f, 3.50f);
                float widthScale = Math.Clamp(1f + ((Hash(TerrainWorldDefaults.WorldIslandVariationSeed + i * 19, i) - 0.5f) * variance * 1.15f), 0.25f, 3.00f);
                float radius = baseRadius * scale;
                float rotation = NormalizeDegrees(TerrainWorldDefaults.MainIslandRotationDegrees + ((Hash(TerrainWorldDefaults.WorldIslandVariationSeed + i * 21, i) - 0.5f) * 120f));
                int giantRiverCount = ResolveWorldLandformCount(seed, TerrainWorldLandformKind.GiantRiver, i);
                int calanqueCount = ResolveWorldLandformCount(seed, TerrainWorldLandformKind.CalanqueCove, i);
                int barrierLagoonCount = ResolveWorldLandformCount(seed, TerrainWorldLandformKind.BarrierIsland, i);
                int towerStacksCount = ResolveWorldLandformCount(seed, TerrainWorldLandformKind.StacksAndArches, i);
                float x;
                float y;
                if (i == 0)
                {
                    x = cluster.X;
                    y = cluster.Y;
                }
                else
                {
                    float bestScore = float.NegativeInfinity;
                    x = cluster.X;
                    y = cluster.Y;
                    for (int attempt = 0; attempt < 128; attempt++)
                    {
                        ResolveClusteredIslandCandidate(cluster, clusterUseCounts[clusterIndex], i, attempt, minSpacing, chainBias, out float candidateX, out float candidateY);
                        float score = ScoreClusteredIslandCandidate(candidateX, candidateY, radius, clusterIndex, cluster, placements, minSpacing, archipelagoRadius, chainBias);
                        score += Hash(TerrainWorldDefaults.WorldIslandScatterSeed + i * 113, attempt * 31) * 0.35f;
                        if (score <= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        x = candidateX;
                        y = candidateY;
                    }
                }

                placements.Add(new TerrainWorldPlacement(
                    $"island {i + 1}",
                    x,
                    y,
                    radius,
                    scale,
                    widthScale,
                    rotation,
                    clusterIndex,
                    giantRiverCount,
                    calanqueCount,
                    barrierLagoonCount,
                    towerStacksCount));
                clusterUseCounts[clusterIndex]++;
            }

            return placements;
        }

        private static int ResolveWorldClusterCount(int islandCount)
        {
            int upperLimit = Math.Clamp(islandCount, 1, 12);
            int minimum = Math.Clamp((int)MathF.Round(TerrainWorldDefaults.WorldClusterCountMinimum), 1, upperLimit);
            int maximum = Math.Clamp((int)MathF.Round(TerrainWorldDefaults.WorldClusterCountMaximum), 1, upperLimit);
            if (maximum < minimum)
            {
                (minimum, maximum) = (maximum, minimum);
            }

            if (maximum <= minimum)
            {
                return minimum;
            }

            float roll = Hash(TerrainWorldDefaults.WorldClusterShapeSeed + 4093, islandCount + 17);
            int span = (maximum - minimum) + 1;
            return Math.Clamp(minimum + (int)MathF.Floor(roll * span), minimum, maximum);
        }

        private static List<TerrainWorldClusterAnchor> BuildWorldClusterAnchors(
            int clusterCount,
            float archipelagoRadius,
            float minSpacing,
            float clusterSpread,
            float chainBias)
        {
            List<TerrainWorldClusterAnchor> clusters = [];
            float ridgeAngle = Hash(TerrainWorldDefaults.WorldClusterShapeSeed + 3031, clusterCount * 17) * MathF.Tau;
            float ridgeLength = archipelagoRadius * (0.52f + (clusterCount * 0.11f));

            for (int clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
            {
                float centerX = 0f;
                float centerY = 0f;
                if (clusterIndex > 0)
                {
                    int ring = (clusterIndex + 1) / 2;
                    float side = clusterIndex % 2 == 0 ? -1f : 1f;
                    float t = side * ring / Math.Max(1f, (clusterCount + 1) * 0.5f);
                    float along = t * ridgeLength * (0.84f + (Hash(TerrainWorldDefaults.WorldClusterShapeSeed + clusterIndex * 307, clusterIndex) * 0.34f));
                    float crossRange = archipelagoRadius * (0.16f + (clusterSpread * 0.11f)) * (1f - (chainBias * 0.42f));
                    float cross = (Hash(TerrainWorldDefaults.WorldClusterShapeSeed + clusterIndex * 311, clusterIndex + 9) - 0.5f) * 2f * crossRange;
                    RotateVector(along, cross, ridgeAngle, out centerX, out centerY);
                }

                float radius = Math.Max(
                    minSpacing * (1.05f + (clusterSpread * 0.58f)),
                    archipelagoRadius * (0.12f + (clusterSpread * 0.16f)) * (0.72f + (Hash(TerrainWorldDefaults.WorldClusterShapeSeed + clusterIndex * 313, clusterIndex + 3) * 0.68f)));
                float angle = ridgeAngle + ((Hash(TerrainWorldDefaults.WorldClusterShapeSeed + clusterIndex * 317, clusterIndex + 5) - 0.5f) * (0.42f + (clusterSpread * 0.48f)));
                clusters.Add(new TerrainWorldClusterAnchor(centerX, centerY, radius, angle));
            }

            return clusters;
        }

        private static int ResolveIslandClusterIndex(int islandIndex, int[] clusterUseCounts)
        {
            if (clusterUseCounts.Length <= 1)
            {
                return 0;
            }

            if (islandIndex < clusterUseCounts.Length)
            {
                return islandIndex;
            }

            float totalWeight = 0f;
            for (int i = 0; i < clusterUseCounts.Length; i++)
            {
                totalWeight += ResolveClusterAssignmentWeight(i, clusterUseCounts[i]);
            }

            float roll = Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 331, islandIndex + 11) * Math.Max(0.0001f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < clusterUseCounts.Length; i++)
            {
                cumulative += ResolveClusterAssignmentWeight(i, clusterUseCounts[i]);
                if (roll <= cumulative)
                {
                    return i;
                }
            }

            return clusterUseCounts.Length - 1;
        }

        private static float ResolveClusterAssignmentWeight(int clusterIndex, int clusterUseCount)
        {
            float weight = 0.72f + (Hash(TerrainWorldDefaults.WorldClusterShapeSeed + clusterIndex * 337, clusterIndex + 13) * 1.65f);
            if (clusterIndex == 0)
            {
                weight *= 1.18f;
            }

            return weight / (1f + (clusterUseCount * 0.055f));
        }

        private static void ResolveClusteredIslandCandidate(
            TerrainWorldClusterAnchor cluster,
            int clusterUseCount,
            int islandIndex,
            int attempt,
            float minSpacing,
            float chainBias,
            out float candidateX,
            out float candidateY)
        {
            float localRadius = Math.Max(minSpacing * 0.82f, cluster.Radius * (0.58f + Math.Min(1.05f, clusterUseCount * 0.09f)));
            float along;
            float cross;
            float chainRoll = Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 347, attempt + 19);
            if (chainRoll < 0.46f + (chainBias * 0.44f))
            {
                float rank = ((clusterUseCount * 0.6180339f) + Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 349, attempt + 23)) % 1f;
                along = (rank - 0.5f) * 2f * localRadius * (0.82f + (chainBias * 0.86f));
                cross = (Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 353, attempt + 29) - 0.5f) * 2f * localRadius * (0.68f - (chainBias * 0.38f));
            }
            else
            {
                float angle = Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 359, attempt + 31) * MathF.Tau;
                float distance = (0.16f + (MathF.Sqrt(Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 367, attempt + 37)) * 0.92f)) * localRadius;
                along = MathF.Cos(angle) * distance;
                cross = MathF.Sin(angle) * distance * (1f - (chainBias * 0.36f));
            }

            RotateVector(along, cross, cluster.Angle, out float rotatedX, out float rotatedY);
            float jitterScale = Math.Clamp(TerrainWorldDefaults.WorldPlacementJitter, 0f, 1200f) * 0.42f;
            candidateX = cluster.X + rotatedX + ((Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 373, attempt + 41) - 0.5f) * jitterScale);
            candidateY = cluster.Y + rotatedY + ((Hash(TerrainWorldDefaults.WorldIslandScatterSeed + islandIndex * 379, attempt + 43) - 0.5f) * jitterScale);
        }

        private static float ScoreClusteredIslandCandidate(
            float candidateX,
            float candidateY,
            float radius,
            int clusterIndex,
            TerrainWorldClusterAnchor cluster,
            List<TerrainWorldPlacement> placements,
            float minSpacing,
            float archipelagoRadius,
            float chainBias)
        {
            float dxCluster = candidateX - cluster.X;
            float dyCluster = candidateY - cluster.Y;
            float axisX = MathF.Cos(cluster.Angle);
            float axisY = MathF.Sin(cluster.Angle);
            float along = (dxCluster * axisX) + (dyCluster * axisY);
            float cross = (dxCluster * -axisY) + (dyCluster * axisX);
            float clusterRadiusX = cluster.Radius * (1.18f + (chainBias * 0.52f));
            float clusterRadiusY = cluster.Radius * (1.08f - (chainBias * 0.36f));
            float clusterPenalty =
                Square(along / Math.Max(1f, clusterRadiusX)) +
                Square(cross / Math.Max(1f, clusterRadiusY));
            float score = -clusterPenalty * 1.55f;
            float nearestAny = float.PositiveInfinity;
            float nearestSameCluster = float.PositiveInfinity;
            int sameClusterCount = 0;

            for (int i = 0; i < placements.Count; i++)
            {
                TerrainWorldPlacement other = placements[i];
                float dx = candidateX - other.X;
                float dy = candidateY - other.Y;
                float distance = MathF.Sqrt((dx * dx) + (dy * dy));
                nearestAny = MathF.Min(nearestAny, distance);
                float desiredClearance = Math.Max(minSpacing, (radius + other.Radius) * 0.68f);
                if (distance < desiredClearance)
                {
                    float overlap = (desiredClearance - distance) / Math.Max(1f, desiredClearance);
                    score -= overlap * overlap * 42f;
                }

                if (other.ClusterIndex == clusterIndex)
                {
                    sameClusterCount++;
                    nearestSameCluster = MathF.Min(nearestSameCluster, distance);
                }
            }

            if (sameClusterCount > 0 && !float.IsPositiveInfinity(nearestSameCluster))
            {
                float preferredClusterSpacing = minSpacing * (0.84f + (chainBias * 0.34f));
                float closeness = 1f - MathF.Abs((nearestSameCluster / Math.Max(1f, preferredClusterSpacing)) - 1f);
                score += Math.Clamp(closeness, -0.75f, 1f) * 1.45f;
                if (nearestSameCluster > cluster.Radius * 1.65f)
                {
                    score -= (nearestSameCluster / Math.Max(1f, cluster.Radius * 1.65f) - 1f) * 1.25f;
                }
            }
            else
            {
                score += Math.Clamp(1f - clusterPenalty, 0f, 1f) * 0.95f;
            }

            if (!float.IsPositiveInfinity(nearestAny) && nearestAny > minSpacing * 4.8f)
            {
                score -= (nearestAny / Math.Max(1f, minSpacing * 4.8f) - 1f) * 1.10f;
            }

            float outerDistance = MathF.Sqrt((candidateX * candidateX) + (candidateY * candidateY * 1.42f * 1.42f));
            if (outerDistance > archipelagoRadius * 1.16f)
            {
                score -= (outerDistance / Math.Max(1f, archipelagoRadius * 1.16f) - 1f) * 4.2f;
            }

            return score;
        }

        private static int ResolveWorldLandformCount(int seed, TerrainWorldLandformKind kind, int islandIndex)
        {
            (float minimum, float maximum, int seedOffset) = ResolveWorldLandformRange(kind);
            int min = Math.Clamp((int)MathF.Round(minimum), 0, TerrainWorldDefaults.WorldLandformTypeGenerationLimit);
            int max = Math.Clamp((int)MathF.Round(maximum), 0, TerrainWorldDefaults.WorldLandformTypeGenerationLimit);
            if (max < min)
            {
                (min, max) = (max, min);
            }

            int range = Math.Max(0, max - min);
            float roll = Hash(seed + (islandIndex * 227) + seedOffset, islandIndex + seedOffset * 17);
            return min + (int)MathF.Floor(roll * (range + 0.999f));
        }

        private static (float Minimum, float Maximum, int SeedOffset) ResolveWorldLandformRange(TerrainWorldLandformKind kind)
        {
            return kind switch
            {
                TerrainWorldLandformKind.GiantRiver => (TerrainWorldDefaults.WorldGiantRiverRngMinimum, TerrainWorldDefaults.WorldGiantRiverRngMaximum, 31),
                TerrainWorldLandformKind.CalanqueCove => (TerrainWorldDefaults.WorldCalanqueRngMinimum, TerrainWorldDefaults.WorldCalanqueRngMaximum, 47),
                TerrainWorldLandformKind.BarrierIsland => (TerrainWorldDefaults.WorldBarrierLagoonRngMinimum, TerrainWorldDefaults.WorldBarrierLagoonRngMaximum, 59),
                TerrainWorldLandformKind.StacksAndArches => (TerrainWorldDefaults.WorldTowerStacksRngMinimum, TerrainWorldDefaults.WorldTowerStacksRngMaximum, 71),
                _ => (0f, 0f, 83)
            };
        }

        private static void RotateVector(float x, float y, float radians, out float rotatedX, out float rotatedY)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            rotatedX = (x * cos) - (y * sin);
            rotatedY = (x * sin) + (y * cos);
        }

        private static float Square(float value)
        {
            return value * value;
        }

        private static float NormalizeDegrees(float degrees)
        {
            float normalized = degrees % 360f;
            if (normalized <= -180f)
            {
                normalized += 360f;
            }
            else if (normalized > 180f)
            {
                normalized -= 360f;
            }

            return normalized;
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393);
                h ^= (uint)y * 668265263u;
                h = (h << 13) | (h >> 19);
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }
    }

    internal enum TerrainWorldLandformKind
    {
        GiantRiver,
        CalanqueCove,
        BarrierIsland,
        StacksAndArches
    }
}
