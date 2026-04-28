using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TerrainDevInterface;

internal enum TerrainOverlayMode
{
    SolidLandWater,
    Elevation,
    WaterDepth,
    Lithology,
    FractureStrength,
    Dissolution,
    WaveExposure,
    Sediment,
    ReefSuitability,
    ClassifiedLandforms,
    LagoonOpenings,
    IslandClusters
}

internal enum TerrainLithology
{
    Limestone,
    VolcanicRock,
    SedimentaryRock,
    ReefLimestone,
    SandMud
}

[Flags]
internal enum TerrainLandformTag
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

internal readonly struct TerrainProcessCell
{
    public TerrainProcessCell(
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
        float shallowOceanInfluence,
        TerrainLithology lithology,
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
        ShallowOceanInfluence = shallowOceanInfluence;
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
    public float ShallowOceanInfluence { get; }
    public bool IsWater => Elevation <= SeaLevel;
    public bool IsLand => Elevation > SeaLevel;
    public bool IsDeepOcean => IsWater && ShallowOceanInfluence < 0.50f;
    public bool IsCoast => MathF.Abs(Elevation - SeaLevel) <= 0.09f;
    public TerrainLithology Lithology { get; }
    public TerrainLandformTag LandformTags { get; }
}

internal static class TerrainProcessPreview
{
    private const float SeaLevel = 0.05f;
    private const float DraftRenderResolutionScale = 1.0f;
    private const float SettledRenderResolutionScale = 1.0f;
    private const float RegionInfluenceFloor = 0.001f;

    private sealed class TerrainSamplingContext
    {
        public TerrainSamplingContext(TerrainScene scene)
            : this(scene, viewportBounds: null)
        {
        }

        public TerrainSamplingContext(TerrainScene scene, RectangleF? viewportBounds)
        {
            Scene = scene;
            Regions = new RegionSamplingContext[scene.Landforms.Count];
            for (int i = 0; i < scene.Landforms.Count; i++)
            {
                Regions[i] = new RegionSamplingContext(scene, scene.Landforms[i], i);
            }

            ActiveRegionIndexes = viewportBounds.HasValue
                ? BuildActiveRegionIndexes(viewportBounds.Value)
                : BuildAllRegionIndexes(Regions.Length);
        }

        public TerrainScene Scene { get; }
        public RegionSamplingContext[] Regions { get; }
        public int[] ActiveRegionIndexes { get; }

        private static int[] BuildAllRegionIndexes(int count)
        {
            int[] indexes = new int[count];
            for (int i = 0; i < count; i++)
            {
                indexes[i] = i;
            }

            return indexes;
        }

        private int[] BuildActiveRegionIndexes(RectangleF viewportBounds)
        {
            bool[] active = new bool[Regions.Length];
            for (int i = 0; i < Regions.Length; i++)
            {
                RegionSamplingContext region = Regions[i];
                if (region.Region.Kind == LandformKind.ArchipelagoRegion || IntersectsInfluenceBounds(region, viewportBounds))
                {
                    ActivateWithAncestors(i, active);
                }
            }

            List<int> indexes = [];
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i])
                {
                    indexes.Add(i);
                }
            }

            return indexes.Count == 0 && Regions.Length > 0 ? [0] : indexes.ToArray();
        }

        private void ActivateWithAncestors(int index, bool[] active)
        {
            int currentIndex = index;
            int guard = 0;
            while (currentIndex >= 0 && currentIndex < Regions.Length && guard++ < 32)
            {
                if (active[currentIndex])
                {
                    return;
                }

                active[currentIndex] = true;
                currentIndex = Regions[currentIndex].ParentIndex;
            }
        }

        private static bool IntersectsInfluenceBounds(RegionSamplingContext region, RectangleF viewportBounds)
        {
            float bound = IsOceanZoneProducer(region.Region.Kind) ? region.OceanZoneBound : region.InfluenceBound;
            return region.Region.CenterX + bound >= viewportBounds.Left &&
                region.Region.CenterX - bound <= viewportBounds.Right &&
                region.Region.CenterY + bound >= viewportBounds.Top &&
                region.Region.CenterY - bound <= viewportBounds.Bottom;
        }
    }

    private sealed class TerrainRenderScratch
    {
        public TerrainRenderScratch(int regionCount)
        {
            RegionInfluences = new float[regionCount];
            RegionLocalX = new float[regionCount];
            RegionLocalY = new float[regionCount];
            RegionRadius = new float[regionCount];
        }

        public float[] RegionInfluences { get; }
        public float[] RegionLocalX { get; }
        public float[] RegionLocalY { get; }
        public float[] RegionRadius { get; }
    }

    private readonly struct RegionSamplingContext
    {
        public RegionSamplingContext(TerrainScene scene, LandformInstance region, int index)
        {
            Region = region;
            Index = index;
            ParentIndex = region.ParentId == 0 ? -1 : scene.ResolveLandformIndex(region.ParentId);
            EffectiveScale = scene.ResolveEffectiveScale(region);
            EffectiveWidthScale = scene.ResolveEffectiveWidthScale(region);
            Scale = EffectiveScale * scene.GlobalScale;
            Radius = Math.Max(48f, region.BaseRadius * Scale);
            Length = Math.Max(Radius, region.BaseLength * Scale);
            WidthScale = Math.Clamp(EffectiveWidthScale * scene.GlobalWidthScale, 0.25f, 3.2f);
            InfluenceRadiusY = Radius * (0.92f + (EffectiveWidthScale * 0.24f));
            InfluenceBound = Math.Max(Length, InfluenceRadiusY) * 1.18f + Math.Max(36f, Radius * 0.08f);
            OceanZoneBound = InfluenceBound + Math.Max(0f, scene.DeepOceanBorderDistance) + Math.Max(0f, scene.DeepOceanBorderFeather);
            InfluenceBoundSquared = InfluenceBound * InfluenceBound;
            float angle = DegreesToRadians(region.RotationDegrees);
            CosRotation = MathF.Cos(angle);
            SinRotation = MathF.Sin(angle);
            ProcessStrength = Math.Clamp(scene.ResolveEffectiveOpeningStrength(region) * scene.GlobalOpeningStrength, 0.12f, 2.8f);
            BasinStrength = Math.Clamp(scene.ResolveEffectiveBasinCut(region) * scene.GlobalBasinCut, 0f, 2.4f);
            Roughness = Math.Clamp(scene.ResolveEffectiveRoughness(region) + scene.GlobalRoughness, 0f, 0.5f);
            Emergence = Math.Clamp(scene.ResolveEffectiveEmergence(region) * scene.GlobalEmergence, 0.05f, 3.2f);
        }

        public LandformInstance Region { get; }
        public int Index { get; }
        public int ParentIndex { get; }
        public float EffectiveScale { get; }
        public float EffectiveWidthScale { get; }
        public float Scale { get; }
        public float Radius { get; }
        public float Length { get; }
        public float WidthScale { get; }
        public float InfluenceRadiusY { get; }
        public float InfluenceBound { get; }
        public float OceanZoneBound { get; }
        public float InfluenceBoundSquared { get; }
        public float CosRotation { get; }
        public float SinRotation { get; }
        public float ProcessStrength { get; }
        public float BasinStrength { get; }
        public float Roughness { get; }
        public float Emergence { get; }
    }

    public static Bitmap RenderBitmap(TerrainScene scene, Size size, float cameraX, float cameraY, float zoom, bool settledQuality, CancellationToken cancellationToken)
    {
        float renderResolutionScale = settledQuality ? SettledRenderResolutionScale : DraftRenderResolutionScale;
        int viewportWidth = Math.Max(1, size.Width);
        int viewportHeight = Math.Max(1, size.Height);
        float inverseZoom = 1f / Math.Max(0.05f, zoom);
        RectangleF viewportBounds = new(
            cameraX - ((viewportWidth * 0.5f) * inverseZoom),
            cameraY - ((viewportHeight * 0.5f) * inverseZoom),
            viewportWidth * inverseZoom,
            viewportHeight * inverseZoom);
        TerrainSamplingContext samplingContext = new(scene, viewportBounds);
        int width = Math.Max(1, (int)MathF.Ceiling(viewportWidth * renderResolutionScale));
        int height = Math.Max(1, (int)MathF.Ceiling(viewportHeight * renderResolutionScale));
        int[] pixels = new int[width * height];
        byte[] landMask = scene.OverlayMode == TerrainOverlayMode.SolidLandWater && scene.RasterSmoothing > 0.01f
            ? new byte[width * height]
            : null;
        byte[] deepOceanMask = scene.OverlayMode == TerrainOverlayMode.SolidLandWater
            ? new byte[width * height]
            : null;

        ParallelOptions options = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
        };
        bool includeDebugFields = scene.OverlayMode != TerrainOverlayMode.SolidLandWater;
        bool solidLandWater = scene.OverlayMode == TerrainOverlayMode.SolidLandWater;
        int solidLandColor = TerrainRenderer.LandColor.ToArgb();
        int solidWaterColor = TerrainRenderer.WaterColor.ToArgb();
        int solidDeepOceanColor = TerrainRenderer.DeepOceanColor.ToArgb();
        int regionCount = samplingContext.Regions.Length;

        Parallel.For(
            0,
            height,
            options,
            () => new TerrainRenderScratch(regionCount),
            (y, _, scratch) =>
            {
                float screenY = (y + 0.5f) / renderResolutionScale;
                float worldY = cameraY + ((screenY - (viewportHeight * 0.5f)) * inverseZoom);
                int rowStart = y * width;

                for (int x = 0; x < width; x++)
                {
                    float screenX = (x + 0.5f) / renderResolutionScale;
                    float worldX = cameraX + ((screenX - (viewportWidth * 0.5f)) * inverseZoom);
                    TerrainProcessCell cell = SampleCell(worldX, worldY, samplingContext, includeDebugFields, scratch);
                    int pixelIndex = rowStart + x;
                    if (landMask != null)
                    {
                        landMask[pixelIndex] = cell.IsLand ? (byte)1 : (byte)0;
                        deepOceanMask[pixelIndex] = cell.IsDeepOcean ? (byte)1 : (byte)0;
                        continue;
                    }

                    pixels[pixelIndex] = solidLandWater
                        ? cell.IsLand ? solidLandColor : cell.IsDeepOcean ? solidDeepOceanColor : solidWaterColor
                        : ResolveColor(cell, scene.OverlayMode).ToArgb();
                }

                return scratch;
            },
            _ => { });

        cancellationToken.ThrowIfCancellationRequested();
        if (landMask != null)
        {
            ApplyLandWaterCoherenceSmoothing(pixels, landMask, deepOceanMask, width, height, scene.RasterSmoothing, cancellationToken);
        }

        Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width, IntPtr.Add(data.Scan0, y * data.Stride), width);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static void ApplyLandWaterCoherenceSmoothing(
        int[] pixels,
        byte[] landMask,
        byte[] deepOceanMask,
        int width,
        int height,
        float strength,
        CancellationToken cancellationToken)
    {
        int area = width * height;
        if (area == 0)
        {
            return;
        }

        int passes = Math.Clamp((int)MathF.Ceiling(strength), 1, 4);
        int landColor = TerrainRenderer.LandColor.ToArgb();
        int waterColor = TerrainRenderer.WaterColor.ToArgb();
        int deepOceanColor = TerrainRenderer.DeepOceanColor.ToArgb();
        byte[] source = landMask;
        byte[] target = new byte[area];

        for (int pass = 0; pass < passes; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool broadPass = strength >= 2.35f && pass == passes - 1;
            int radius = broadPass ? 2 : 1;
            int span = (radius * 2) + 1;
            int total = span * span;
            int landThreshold = Math.Max(1, (int)MathF.Ceiling(total * 0.58f));
            int waterThreshold = Math.Max(0, (int)MathF.Floor(total * 0.42f));

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = rowStart + x;
                    int landCount = 0;
                    int sampleCount = 0;

                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        int ny = y + oy;
                        if ((uint)ny >= (uint)height)
                        {
                            continue;
                        }

                        int neighborRow = ny * width;
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int nx = x + ox;
                            if ((uint)nx >= (uint)width)
                            {
                                continue;
                            }

                            landCount += source[neighborRow + nx];
                            sampleCount++;
                        }
                    }

                    int adjustedLandThreshold = Math.Min(sampleCount, landThreshold);
                    int adjustedWaterThreshold = Math.Min(sampleCount, waterThreshold);
                    target[index] = landCount >= adjustedLandThreshold
                        ? (byte)1
                        : landCount <= adjustedWaterThreshold
                            ? (byte)0
                            : source[index];
                }
            }

            (source, target) = (target, source);
        }

        for (int i = 0; i < area; i++)
        {
            pixels[i] = source[i] == 1 ? landColor : deepOceanMask[i] == 1 ? deepOceanColor : waterColor;
        }
    }

    public static TerrainProcessCell SampleCell(float worldX, float worldY, TerrainScene scene, bool includeDebugFields = true)
    {
        return SampleCell(worldX, worldY, new TerrainSamplingContext(scene), includeDebugFields);
    }

    private static TerrainProcessCell SampleCell(float worldX, float worldY, TerrainSamplingContext context, bool includeDebugFields)
    {
        int regionCount = context.Regions.Length;
        Span<float> regionInfluences = regionCount <= 64 ? stackalloc float[regionCount] : new float[regionCount];
        Span<float> regionLocalX = regionCount <= 64 ? stackalloc float[regionCount] : new float[regionCount];
        Span<float> regionLocalY = regionCount <= 64 ? stackalloc float[regionCount] : new float[regionCount];
        Span<float> regionRadius = regionCount <= 64 ? stackalloc float[regionCount] : new float[regionCount];
        return SampleCell(worldX, worldY, context, includeDebugFields, regionInfluences, regionLocalX, regionLocalY, regionRadius);
    }

    private static TerrainProcessCell SampleCell(float worldX, float worldY, TerrainSamplingContext context, bool includeDebugFields, TerrainRenderScratch scratch)
    {
        int regionCount = context.Regions.Length;
        return SampleCell(
            worldX,
            worldY,
            context,
            includeDebugFields,
            scratch.RegionInfluences.AsSpan(0, regionCount),
            scratch.RegionLocalX.AsSpan(0, regionCount),
            scratch.RegionLocalY.AsSpan(0, regionCount),
            scratch.RegionRadius.AsSpan(0, regionCount));
    }

    private static TerrainProcessCell SampleCell(
        float worldX,
        float worldY,
        TerrainSamplingContext context,
        bool includeDebugFields,
        Span<float> regionInfluences,
        Span<float> regionLocalX,
        Span<float> regionLocalY,
        Span<float> regionRadius)
    {
        TerrainScene scene = context.Scene;
        float globalScale = Math.Max(0.25f, scene.GlobalScale);
        float sampleX = worldX / globalScale;
        float sampleY = worldY / globalScale;
        int seed = scene.Seed;

        int regionCount = context.Regions.Length;
        ReadOnlySpan<int> activeRegionIndexes = context.ActiveRegionIndexes;
        float clusterInfluence = 0f;
        LandformInstance strongestRegion = null;

        for (int activeIndex = 0; activeIndex < activeRegionIndexes.Length; activeIndex++)
        {
            int i = activeRegionIndexes[activeIndex];
            RegionSamplingContext regionContext = context.Regions[i];
            LandformInstance region = regionContext.Region;
            float influence = ResolveRegionInfluence(worldX, worldY, regionContext, seed, out float localX, out float localY, out float radius);
            regionInfluences[i] = influence;
            regionLocalX[i] = localX;
            regionLocalY[i] = localY;
            regionRadius[i] = radius;

            if (region.Kind == LandformKind.ArchipelagoRegion || influence <= clusterInfluence)
            {
                continue;
            }

            clusterInfluence = influence;
            strongestRegion = region;
        }

        float shallowOceanInfluence = ResolveShallowOceanInfluence(worldX, worldY, context, activeRegionIndexes);
        if (!includeDebugFields && clusterInfluence <= RegionInfluenceFloor)
        {
            return new TerrainProcessCell(
                -0.38f,
                SeaLevel,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                shallowOceanInfluence,
                TerrainLithology.SandMud,
                TerrainLandformTag.None);
        }

        float macroShelf = MacroArchipelagoMask(sampleX, sampleY, seed);
        TerrainLithologyWeights lithology = ResolveLithologyFromRegionInfluences(sampleX, sampleY, context, regionInfluences);
        float fracture = ResolveFractureStrength(sampleX, sampleY, seed, strongestRegion, scene);

        float elevation = -0.38f + (macroShelf * 0.035f);
        elevation += clusterInfluence * scene.GlobalEmergence * 0.020f;
        elevation += Fbm(sampleX * 0.0028f, sampleY * 0.0028f, seed + 101, 4) * 0.025f;
        elevation += RidgedFbm(sampleX * 0.0048f, sampleY * 0.0048f, seed + 102, 3) * 0.018f;

        float enclosure = 0f;
        float tidalFlow = 0f;
        for (int activeIndex = 0; activeIndex < activeRegionIndexes.Length; activeIndex++)
        {
            int i = activeRegionIndexes[activeIndex];
            RegionSamplingContext regionContext = context.Regions[i];
            float influence = regionInfluences[i] * ResolveParentInfluenceGate(regionContext, context, regionInfluences);
            if (influence <= RegionInfluenceFloor)
            {
                continue;
            }

            ApplyRegionProcess(
                scene,
                regionContext,
                regionLocalX[i],
                regionLocalY[i],
                regionRadius[i],
                influence,
                ref elevation,
                ref enclosure,
                ref tidalFlow);
        }

        float dissolution = Math.Clamp(
            lithology.Limestone * fracture * (0.50f + (scene.GlobalBasinCut * 0.35f)),
            0f,
            1f);
        elevation -= dissolution * (0.22f + (scene.GlobalBasinCut * 0.20f));

        float coastBand = Math.Clamp(1f - (MathF.Abs(elevation - SeaLevel) / 0.34f), 0f, 1f);
        float waveExposure = ResolveWaveExposure(sampleX, sampleY, seed, macroShelf, clusterInfluence, coastBand);
        float openingCut = ResolveOpeningCut(sampleX, sampleY, seed, fracture, waveExposure, scene);
        elevation -= openingCut;
        tidalFlow = Math.Clamp(tidalFlow + (openingCut * 1.25f), 0f, 1f);

        float waterDepth = MathF.Max(0f, SeaLevel - elevation);
        if (!includeDebugFields)
        {
            return new TerrainProcessCell(
                elevation,
                SeaLevel,
                0f,
                waveExposure,
                0f,
                0f,
                dissolution,
                fracture,
                tidalFlow,
                clusterInfluence,
                enclosure,
                shallowOceanInfluence,
                lithology.Dominant,
                TerrainLandformTag.None);
        }

        float sediment = ResolveSediment(sampleX, sampleY, seed, lithology, waveExposure, waterDepth, coastBand, scene);
        float reefSuitability = ResolveReefSuitability(sampleX, sampleY, seed, lithology, waveExposure, waterDepth, macroShelf, scene);
        elevation += sediment * coastBand * 0.14f;
        elevation += reefSuitability * Math.Clamp(1f - (waterDepth / 0.55f), 0f, 1f) * 0.10f;

        float roughness = Math.Clamp(scene.GlobalRoughness + (strongestRegion == null ? 0f : scene.ResolveEffectiveRoughness(strongestRegion)), 0f, 0.4f);
        elevation += Fbm(sampleX * 0.015f, sampleY * 0.015f, seed + 120, 4) * roughness * 0.16f;

        float slope = Math.Clamp(
            (MathF.Abs(elevation - SeaLevel) * 1.15f) +
            (fracture * 0.34f) +
            (dissolution * 0.24f),
            0f,
            1f);
        TerrainLandformTag tags = ClassifyLandforms(
            elevation,
            SeaLevel,
            slope,
            waveExposure,
            sediment,
            reefSuitability,
            dissolution,
            fracture,
            tidalFlow,
            clusterInfluence,
            enclosure,
            lithology);

        return new TerrainProcessCell(
            elevation,
            SeaLevel,
            slope,
            waveExposure,
            sediment,
            reefSuitability,
            dissolution,
            fracture,
            tidalFlow,
            clusterInfluence,
            enclosure,
            shallowOceanInfluence,
            lithology.Dominant,
            tags);
    }

    public static string FormatOverlayMode(TerrainOverlayMode mode)
    {
        return mode switch
        {
            TerrainOverlayMode.SolidLandWater => "Solid land/water",
            TerrainOverlayMode.WaterDepth => "Water depth",
            TerrainOverlayMode.FractureStrength => "Fracture strength",
            TerrainOverlayMode.ReefSuitability => "Reef suitability",
            TerrainOverlayMode.ClassifiedLandforms => "Classified landforms",
            TerrainOverlayMode.LagoonOpenings => "Lagoon openings",
            TerrainOverlayMode.IslandClusters => "Island clusters",
            _ => mode.ToString()
        };
    }

    private static float ResolveShallowOceanInfluence(float worldX, float worldY, TerrainSamplingContext context, ReadOnlySpan<int> activeRegionIndexes)
    {
        TerrainScene scene = context.Scene;
        float border = Math.Max(0f, scene.DeepOceanBorderDistance);
        float feather = Math.Max(1f, scene.DeepOceanBorderFeather);
        float rounding = Math.Clamp(scene.DeepOceanBorderRounding, 0f, 1f);
        float shallowInfluence = 0f;

        for (int activeIndex = 0; activeIndex < activeRegionIndexes.Length; activeIndex++)
        {
            int i = activeRegionIndexes[activeIndex];
            RegionSamplingContext regionContext = context.Regions[i];
            LandformInstance region = regionContext.Region;
            if (!IsOceanZoneProducer(region.Kind))
            {
                continue;
            }

            float zoneLength = Math.Max(1f, regionContext.Length + border);
            float zoneHeight = Math.Max(1f, regionContext.InfluenceRadiusY + border);
            float dx = worldX - region.CenterX;
            float dy = worldY - region.CenterY;
            float localX = (dx * regionContext.CosRotation) + (dy * regionContext.SinRotation);
            float localY = (-dx * regionContext.SinRotation) + (dy * regionContext.CosRotation);
            float nx = MathF.Abs(localX) / zoneLength;
            float ny = MathF.Abs(localY) / zoneHeight;
            float ellipseDistance = MathF.Sqrt((nx * nx) + (ny * ny));
            float roundedBoxDistance = MathF.Max(nx, ny);
            float distance = Lerp(roundedBoxDistance, ellipseDistance, rounding);
            float featherRatio = Math.Clamp(feather / MathF.Min(zoneLength, zoneHeight), 0.015f, 0.75f);
            shallowInfluence = MathF.Max(shallowInfluence, SmoothStep(1f, 1f - featherRatio, distance));
            if (shallowInfluence >= 0.999f)
            {
                return 1f;
            }
        }

        return shallowInfluence;
    }

    private static bool IsOceanZoneProducer(LandformKind kind)
    {
        return kind is LandformKind.MainIsland or LandformKind.IslandCluster or LandformKind.ReefShelf or LandformKind.SedimentShelf;
    }

    private static void ApplyRegionProcess(
        TerrainScene scene,
        RegionSamplingContext regionContext,
        float localX,
        float localY,
        float radius,
        float influence,
        ref float elevation,
        ref float enclosure,
        ref float tidalFlow)
    {
        LandformInstance region = regionContext.Region;
        float processStrength = regionContext.ProcessStrength;
        float basinStrength = regionContext.BasinStrength;
        float widthScale = regionContext.WidthScale;
        float roughness = regionContext.Roughness;
        float emergence = regionContext.Emergence;
        int seed = scene.Seed + (region.Id * 911);

        float length = regionContext.Length;
        float islandWidth = Math.Max(32f, radius * (0.62f + (widthScale * 0.22f)));
        float normalizedX = localX / Math.Max(1f, length);
        float normalizedY = localY / Math.Max(1f, islandWidth);
        float radial = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
        float processNoise = Fbm((localX * 0.010f) + region.Id, (localY * 0.010f) - region.Id, seed + 7, 5);
        float ridgeNoise = RidgedFbm(localX * 0.012f, localY * 0.012f, seed + 11, 5);
        float fractured = ResolveLocalFractures(localX, localY, seed, region.RotationDegrees);
        float organicMass = ResolveOrganicIsland(localX, localY, length, islandWidth, seed, roughness);

        switch (region.Kind)
        {
            case LandformKind.ArchipelagoRegion:
            {
                float shelfLift = SmoothStep(1.12f, 0.26f, radial);
                elevation += influence * emergence * shelfLift * 0.004f;
                elevation -= influence * basinStrength * shelfLift * 0.010f;
                enclosure = MathF.Max(enclosure, influence * shelfLift * 0.16f);
                break;
            }

            case LandformKind.ReefShelf:
            {
                float shelf = SmoothStep(1.00f, 0.18f, radial);
                float rim = SmoothStep(0.22f * widthScale, 0.04f * widthScale, MathF.Abs(radial - 0.62f));
                elevation += influence * emergence * ((shelf * 0.018f) + (rim * processStrength * 0.10f));
                elevation -= influence * basinStrength * shelf * 0.060f;
                enclosure = MathF.Max(enclosure, influence * MathF.Max(shelf * 0.24f, rim * 0.58f));
                break;
            }

            case LandformKind.SedimentShelf:
            {
                float shoal = SmoothStep(1.00f, 0.20f, radial);
                float bars = ResolveSinuousBarrier(localX, localY, radius, widthScale * 1.16f, seed + 17);
                elevation += influence * emergence * ((shoal * 0.016f) + (bars * processStrength * 0.12f));
                elevation -= influence * basinStrength * shoal * 0.055f;
                enclosure = MathF.Max(enclosure, influence * MathF.Max(shoal * 0.20f, bars * 0.52f));
                tidalFlow = MathF.Max(tidalFlow, influence * ResolveBarrierPass(localX, localY, radius, widthScale, seed + 18));
                break;
            }

            case LandformKind.IslandCluster:
            {
                float beads = ResolveIslandBeads(localX, localY, length, islandWidth, seed, 7, 0.42f);
                elevation += influence * emergence * beads * processStrength * 0.66f;
                elevation -= influence * fractured * basinStrength * beads * 0.08f;
                enclosure = MathF.Max(enclosure, influence * beads * 0.36f);
                break;
            }

            case LandformKind.MainIsland:
            {
                float islandCore = organicMass;
                float coastBand = SmoothStep(1.06f, 0.88f, radial) * SmoothStep(0.62f, 0.86f, radial);
                float coves = fractured * basinStrength * coastBand;
                elevation += influence * emergence * islandCore * (0.60f + (ridgeNoise * processStrength * 0.055f));
                elevation -= influence * coves * 0.045f;
                enclosure = MathF.Max(enclosure, influence * islandCore * 0.38f);
                break;
            }

            case LandformKind.CliffCoast:
            {
                float coastSide = SmoothStep(0.74f, 0.15f, MathF.Abs(normalizedY - 0.62f)) * SmoothStep(1.04f, 0.16f, MathF.Abs(normalizedX));
                float geos = fractured * coastSide;
                elevation += influence * emergence * coastSide * processStrength * 0.18f;
                elevation -= influence * geos * basinStrength * 0.25f;
                enclosure = MathF.Max(enclosure, influence * coastSide * 0.48f);
                tidalFlow = MathF.Max(tidalFlow, influence * geos * 0.18f);
                break;
            }

            case LandformKind.StacksAndArches:
            {
                float stacks = ResolveIslandBeads(localX, localY, length, islandWidth, seed, 5, 0.26f);
                elevation += influence * emergence * stacks * processStrength * 0.56f;
                elevation -= influence * fractured * basinStrength * stacks * 0.10f;
                enclosure = MathF.Max(enclosure, influence * stacks * 0.30f);
                tidalFlow = MathF.Max(tidalFlow, influence * fractured * stacks * 0.20f);
                break;
            }

            case LandformKind.GiantRiver:
            {
                float river = ResolveGiantRiver(localX, localY, length, MathF.Max(24f, region.BaseRadius * widthScale), seed);
                float valley = ResolveGiantRiver(localX, localY, length, MathF.Max(42f, region.BaseRadius * widthScale * 1.65f), seed + 9);
                elevation -= influence * ((river * basinStrength * processStrength * 0.92f) + (valley * basinStrength * 0.18f));
                enclosure = MathF.Max(enclosure, influence * valley * 0.38f);
                tidalFlow = MathF.Max(tidalFlow, influence * river);
                break;
            }

            case LandformKind.AtollLagoon:
            case LandformKind.Atoll:
            {
                float reefRadius = 0.56f + (processNoise * 0.06f);
                float reefWidth = 0.11f * widthScale;
                float reefRim = SmoothStep(reefWidth * 3.4f, reefWidth * 0.42f, MathF.Abs(radial - reefRadius));
                float pass = ResolveAngularOpening(localX, localY, region, scene, radius, 2, seed + 13);
                float centralBasin = SmoothStep(reefRadius * 0.86f, reefRadius * 0.28f, radial);
                elevation += influence * ((reefRim * processStrength * emergence * 0.58f) - (centralBasin * basinStrength * 0.38f) - (pass * 0.62f));
                enclosure = MathF.Max(enclosure, influence * reefRim);
                tidalFlow = MathF.Max(tidalFlow, influence * pass);
                break;
            }

            case LandformKind.ReefBarrierLagoon:
            case LandformKind.BarrierReef:
            {
                float islandMass = SmoothStep(0.38f, 0.88f, ridgeNoise + (processNoise * 0.42f));
                float barrier = ResolveSinuousBarrier(localX, localY, radius, widthScale, seed + 23);
                float backBasin = SmoothStep(0.58f, 0.12f, MathF.Abs(normalizedY + 0.18f)) *
                    SmoothStep(0.92f, 0.22f, MathF.Abs(normalizedX));
                elevation += influence * (((islandMass * 0.16f) + (barrier * processStrength * 0.42f)) * emergence - (backBasin * basinStrength * 0.34f));
                enclosure = MathF.Max(enclosure, influence * barrier);
                tidalFlow = MathF.Max(tidalFlow, influence * ResolveBarrierPass(localX, localY, radius, widthScale, seed + 24));
                break;
            }

            case LandformKind.BarrierIslandLagoon:
            case LandformKind.BarrierIsland:
            case LandformKind.SpitAndTombolo:
            {
                float lagoonCurve = MathF.Sin((localX / Math.Max(1f, length * 0.76f)) * MathF.PI * 0.92f + (Hash(seed, seed + 31) * MathF.Tau)) * radius * 0.10f;
                float basinCenterY = radius * 0.36f * widthScale;
                float barrierCenterY = -radius * 0.18f * widthScale;
                float basin = SmoothStep(1.02f, 0.22f, EllipseDistance(localX, localY - basinCenterY - (lagoonCurve * 0.28f), length * 0.72f, radius * 0.56f * widthScale));
                float basinBack = SmoothStep(0.82f, 0.18f, EllipseDistance(localX, localY - (basinCenterY + (radius * 0.20f * widthScale)), length * 0.48f, radius * 0.34f * widthScale));
                float barrier = ResolveSinuousBarrier(localX, localY - barrierCenterY, radius * 1.22f, widthScale * 0.86f, seed + 31);
                float spit = ResolveSpitHook(localX, localY - barrierCenterY, radius * 1.10f, widthScale, seed + 32);
                float pass = ResolveBarrierPass(localX, localY - barrierCenterY, radius * 1.10f, widthScale * 0.86f, seed + 33);
                float tidalChannel = pass * SmoothStep(radius * 0.72f * widthScale, radius * 0.10f * widthScale, MathF.Abs(localY - ((basinCenterY + barrierCenterY) * 0.50f)));
                float backBarrierWater = MathF.Max(basin, basinBack * 0.74f);
                float barrierRidge = MathF.Max(barrier, spit * 0.72f);
                elevation += influence * ((barrierRidge * processStrength * emergence * 0.72f) - (backBarrierWater * basinStrength * 1.08f) - (tidalChannel * 0.62f) - (pass * 0.22f));
                enclosure = MathF.Max(enclosure, influence * MathF.Max(barrierRidge, backBarrierWater * 0.48f));
                tidalFlow = MathF.Max(tidalFlow, influence * MathF.Max(pass, tidalChannel));
                break;
            }

            case LandformKind.KarstHongLagoon:
            case LandformKind.KarstTowerField:
            case LandformKind.KarstHong:
            {
                float towers = region.Kind == LandformKind.KarstTowerField
                    ? ResolveIslandBeads(localX, localY, length, islandWidth, seed, 13, 0.32f)
                    : MathF.Pow(Math.Clamp(ridgeNoise + 0.08f, 0f, 1f), 2.8f);
                float fractureChannels = fractured * (0.58f + (basinStrength * 0.34f));
                float sinkhole = SmoothStep(0.42f, 0.10f, radial + (processNoise * 0.12f));
                float rim = SmoothStep(0.20f, 0.55f, radial) * SmoothStep(0.96f, 0.58f, radial);
                float breach = ResolveAngularOpening(localX, localY, region, scene, radius, 2, seed + 42);
                float fieldLift = region.Kind == LandformKind.KarstTowerField ? organicMass * 0.08f : 0f;
                elevation += influence * (((towers * processStrength * 0.72f) + (rim * 0.18f) + fieldLift) * emergence - (fractureChannels * 0.28f) - (sinkhole * basinStrength * 0.54f) - (breach * 0.46f));
                enclosure = MathF.Max(enclosure, influence * MathF.Max(rim, towers * 0.55f));
                tidalFlow = MathF.Max(tidalFlow, influence * breach);
                break;
            }

            case LandformKind.IslandRingLagoon:
            case LandformKind.IslandRing:
            {
                float ringDistance = MathF.Abs(radial - (0.52f + (processNoise * 0.12f)));
                float ring = SmoothStep(0.24f * widthScale, 0.06f * widthScale, ringDistance);
                float beadMask = SmoothStep(0.08f, 0.52f, RidgedFbm(localX * 0.010f, localY * 0.010f, seed + 51, 3));
                float channels = ResolveAngularOpening(localX, localY, region, scene, radius, 2, seed + 52);
                float basin = SmoothStep(0.44f, 0.12f, radial);
                elevation += influence * (((ring * beadMask) * processStrength * emergence * 0.68f) - (basin * basinStrength * 0.36f) - (channels * 0.60f));
                enclosure = MathF.Max(enclosure, influence * ring * beadMask);
                tidalFlow = MathF.Max(tidalFlow, influence * channels);
                break;
            }

            case LandformKind.CalanqueCoveLagoon:
            case LandformKind.CalanqueCove:
            {
                float ravine = ResolveRavine(localX, localY, radius, widthScale, seed + 61);
                float sideWalls = SmoothStep(radius * 0.34f * widthScale, radius * 0.12f * widthScale, MathF.Abs(localY));
                float interior = SmoothStep(0.38f, 0.08f, MathF.Abs(normalizedY)) * SmoothStep(0.40f, -0.12f, normalizedX);
                elevation += influence * (sideWalls * processStrength * emergence * 0.10f - (ravine * 0.88f) - (interior * basinStrength * 0.52f));
                enclosure = MathF.Max(enclosure, influence * sideWalls);
                tidalFlow = MathF.Max(tidalFlow, influence * ravine);
                break;
            }
        }

        float roughnessStrength = region.Kind == LandformKind.MainIsland ? 0.035f : 0.085f;
        elevation += influence * roughness * processNoise * roughnessStrength;
    }

    private static float ResolveOrganicIsland(float localX, float localY, float halfLength, float halfWidth, int seed, float roughness)
    {
        float nx = localX / Math.Max(1f, halfLength);
        float ny = localY / Math.Max(1f, halfWidth);
        float baseDistance = MathF.Sqrt((nx * nx) + (ny * ny));
        float theta = MathF.Atan2(ny, nx);
        float prevailingExposure = Hash(seed + 1601, seed + 1603) * MathF.Tau;
        float exposureBand = 0.5f + (MathF.Cos(theta - prevailingExposure) * 0.5f);
        float radialScale =
            0.92f +
            (MathF.Sin((theta * 2f) + (Hash(seed + 1501, seed + 1503) * MathF.Tau)) * 0.20f) +
            (MathF.Sin((theta * 3f) + (Hash(seed + 1505, seed + 1507) * MathF.Tau)) * 0.16f) +
            (MathF.Sin((theta * 5f) + (Hash(seed + 1509, seed + 1511) * MathF.Tau)) * 0.070f) -
            (exposureBand * 0.09f);

        for (int i = 0; i < 7; i++)
        {
            float angle = ((i / 7f) * MathF.Tau) + ((Hash(seed + i * 83, seed + i * 89) - 0.5f) * 1.05f);
            float width = 0.46f + (Hash(seed + i * 97, seed + i * 101) * 0.44f);
            float lobe = SmoothStep(width, 0f, AngularDistance(theta, angle));
            float exposure = 0.5f + (MathF.Cos(angle - prevailingExposure) * 0.5f);
            radialScale += lobe * (0.10f + (Hash(seed + i * 103, seed + i * 107) * 0.16f) + (exposure * 0.045f));
        }

        for (int i = 0; i < 6; i++)
        {
            float angle = Hash(seed + i * 109, seed + i * 113) * MathF.Tau;
            float width = 0.34f + (Hash(seed + i * 127, seed + i * 131) * 0.40f);
            float bite = SmoothStep(width, 0f, AngularDistance(theta, angle));
            float exposure = 0.5f + (MathF.Cos(angle - prevailingExposure) * 0.5f);
            radialScale -= bite * (0.070f + (exposure * 0.130f) + (Hash(seed + i * 137, seed + i * 139) * 0.055f));
        }

        radialScale = Math.Clamp(radialScale, 0.48f, 1.74f);
        float shapedDistance = baseDistance / radialScale;
        float core = MathF.Max(SmoothStep(0.62f, 0.18f, shapedDistance), SmoothStep(0.30f, 0.12f, baseDistance));
        float body = SmoothStep(1.08f, 0.54f, shapedDistance) * 0.90f;

        float lobes = 0f;
        for (int i = 0; i < 7; i++)
        {
            float angle = ((i / 7f) * MathF.Tau) + ((Hash(seed + i * 101, seed + i * 103) - 0.5f) * 0.86f);
            float distance = 0.24f + (Hash(seed + i * 107, seed + i * 109) * 0.58f);
            float centerX = MathF.Cos(angle) * halfLength * distance;
            float centerY = MathF.Sin(angle) * halfWidth * distance;
            float radiusX = halfLength * (0.28f + (Hash(seed + i * 113, seed + i * 127) * 0.36f));
            float radiusY = halfWidth * (0.26f + (Hash(seed + i * 131, seed + i * 137) * 0.40f));
            float lobeDistance = EllipseDistance(localX - centerX, localY - centerY, radiusX, radiusY);
            float lobeStrength = 0.66f + (Hash(seed + i * 139, seed + i * 149) * 0.44f);
            lobes = MathF.Max(lobes, SmoothStep(1.16f, 0.26f, lobeDistance) * lobeStrength);
        }

        float bites = 0f;
        for (int i = 0; i < 5; i++)
        {
            float angle = Hash(seed + i * 151, seed + i * 157) * MathF.Tau;
            float distance = 0.76f + (Hash(seed + i * 163, seed + i * 167) * 0.26f);
            float centerX = MathF.Cos(angle) * halfLength * distance;
            float centerY = MathF.Sin(angle) * halfWidth * distance;
            Rotate(localX - centerX, localY - centerY, -angle, out float along, out float across);
            float processScale = MathF.Min(halfLength, halfWidth);
            float radiusAlong = processScale * (0.30f + (Hash(seed + i * 173, seed + i * 179) * 0.20f));
            float radiusAcross = processScale * (0.34f + (Hash(seed + i * 181, seed + i * 191) * 0.26f));
            float exposure = 0.5f + (MathF.Cos(angle - prevailingExposure) * 0.5f);
            float biteDistance = EllipseDistance(along, across, radiusAlong, radiusAcross);
            float biteStrength = 0.20f + (exposure * 0.18f) + (Hash(seed + i * 193, seed + i * 197) * 0.09f);
            bites = MathF.Max(bites, SmoothStep(1.00f, 0.12f, biteDistance) * biteStrength);
        }

        float edgeBand = SmoothStep(0.62f, 0.92f, shapedDistance) * SmoothStep(1.18f, 0.98f, shapedDistance);
        float headlandCycle = MathF.Sin((theta * 2f) + (Hash(seed + 1705, seed + 1707) * MathF.Tau)) * 0.018f;
        headlandCycle += MathF.Sin((theta * 3f) + (Hash(seed + 1709, seed + 1711) * MathF.Tau)) * 0.012f;
        float macroWarp = ((exposureBand - 0.5f) * -0.030f) + headlandCycle;
        float coastalRoughness = Fbm(localX * 0.0028f, localY * 0.0028f, seed + 1702, 2) * (0.002f + (roughness * 0.010f));
        float silhouette = MathF.Max(body, lobes);
        silhouette += edgeBand * (macroWarp + coastalRoughness);
        silhouette -= bites;

        return Math.Clamp(MathF.Max(core, silhouette), 0f, 1f);
    }

    private static float ResolveIslandBeads(float localX, float localY, float halfLength, float halfWidth, int seed, int count, float beadScale)
    {
        float field = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float angle = (Hash(seed + i * 31, seed + i * 37) * MathF.Tau) + (t * MathF.PI * 0.8f);
            float ring = 0.18f + (Hash(seed + i * 41, seed + i * 43) * 0.62f);
            float centerX = MathF.Cos(angle) * halfLength * ring * (0.72f + (Hash(seed + i * 47, seed + i * 53) * 0.20f));
            float centerY = MathF.Sin(angle) * halfWidth * ring * (0.70f + (Hash(seed + i * 59, seed + i * 61) * 0.24f));
            float radiusX = halfLength * beadScale * (0.50f + (Hash(seed + i * 67, seed + i * 71) * 0.36f));
            float radiusY = halfWidth * beadScale * (0.46f + (Hash(seed + i * 73, seed + i * 79) * 0.34f));
            float distance = EllipseDistance(localX - centerX, localY - centerY, radiusX, radiusY);
            float bead = SmoothStep(1.00f, 0.62f, distance);
            field = MathF.Max(field, bead);
        }

        return field;
    }

    private static float ResolveGiantRiver(float localX, float localY, float halfLength, float halfWidth, int seed)
    {
        float normalizedX = localX / Math.Max(1f, halfLength);
        float phase = Hash(seed, seed + 7) * MathF.Tau;
        float meander =
            MathF.Sin((normalizedX * MathF.PI * 1.65f) + phase) * halfWidth * 0.32f +
            MathF.Sin((normalizedX * MathF.PI * 3.10f) + phase * 0.63f) * halfWidth * 0.12f;
        float along = SmoothStep(1.14f, 0.94f, MathF.Abs(normalizedX));
        float across = SmoothStep(halfWidth, halfWidth * 0.28f, MathF.Abs(localY - meander));
        return along * across;
    }

    private static TerrainLandformTag ClassifyLandforms(
        float elevation,
        float seaLevel,
        float slope,
        float waveExposure,
        float sediment,
        float reefSuitability,
        float dissolution,
        float fracture,
        float tidalFlow,
        float islandCluster,
        float enclosure,
        TerrainLithologyWeights lithology)
    {
        TerrainLandformTag tags = TerrainLandformTag.None;
        bool isLand = elevation > seaLevel;
        bool isWater = !isLand;
        bool isCoast = MathF.Abs(elevation - seaLevel) <= 0.10f;
        float waterDepth = MathF.Max(0f, seaLevel - elevation);

        if (islandCluster > 0.28f)
        {
            tags |= TerrainLandformTag.ArchipelagoCluster;
        }

        if (isLand)
        {
            tags |= TerrainLandformTag.Island;
            if (isCoast && slope > 0.62f && waveExposure > 0.48f)
            {
                tags |= TerrainLandformTag.Headland;
            }
            if (slope > 0.72f && islandCluster < 0.62f)
            {
                tags |= TerrainLandformTag.Stack;
                tags |= TerrainLandformTag.Islet;
            }
        }

        if (isWater && waterDepth < 0.52f && reefSuitability > 0.58f)
        {
            tags |= TerrainLandformTag.Reef;
            if (enclosure > 0.42f)
            {
                tags |= TerrainLandformTag.Atoll;
            }
        }

        if (isWater && waterDepth < 0.62f && enclosure > 0.38f && tidalFlow > 0.10f)
        {
            tags |= TerrainLandformTag.Lagoon;
            if (reefSuitability > 0.56f)
            {
                tags |= TerrainLandformTag.ReefLagoon;
            }
            if (sediment > 0.52f)
            {
                tags |= TerrainLandformTag.BarrierLagoon;
            }
            if (lithology.Limestone > 0.46f && dissolution > 0.34f)
            {
                tags |= TerrainLandformTag.KarstHongLagoon;
            }
            if (enclosure > 0.62f && reefSuitability < 0.48f)
            {
                tags |= TerrainLandformTag.IslandRingLagoon;
            }
            if (fracture > 0.56f && slope > 0.54f)
            {
                tags |= TerrainLandformTag.CoveLagoon;
            }
        }

        if (isCoast && sediment > 0.56f && waveExposure < 0.45f)
        {
            tags |= TerrainLandformTag.Beach;
            if (enclosure > 0.42f || fracture > 0.55f)
            {
                tags |= TerrainLandformTag.PocketBeach;
            }
        }

        if (isCoast && fracture > 0.58f && waveExposure > 0.42f && slope > 0.48f)
        {
            tags |= TerrainLandformTag.SeaCave;
            if (fracture > 0.72f)
            {
                tags |= TerrainLandformTag.Geo;
            }
            if (waveExposure > 0.62f && dissolution > 0.22f)
            {
                tags |= TerrainLandformTag.Arch;
            }
        }

        if (isWater && fracture > 0.54f && tidalFlow > 0.18f)
        {
            tags |= TerrainLandformTag.Channel;
            if (tidalFlow > 0.48f)
            {
                tags |= TerrainLandformTag.Strait;
            }
        }

        if (isCoast && fracture > 0.62f && enclosure > 0.46f && sediment > 0.50f)
        {
            tags |= TerrainLandformTag.Cove;
            if (slope > 0.58f)
            {
                tags |= TerrainLandformTag.Calanque;
                tags |= TerrainLandformTag.StinivaLikePocketCove;
            }
            if (tidalFlow > 0.20f && waveExposure < 0.36f)
            {
                tags |= TerrainLandformTag.PorcoRossoLikeHiddenBase;
            }
        }

        if (lithology.Limestone > 0.58f && dissolution > 0.42f && islandCluster > 0.48f)
        {
            tags |= TerrainLandformTag.PhangNgaLikeKarstBay;
        }

        if (sediment > 0.62f && isCoast)
        {
            tags |= TerrainLandformTag.Spit;
            if (enclosure > 0.34f)
            {
                tags |= TerrainLandformTag.BarrierIsland;
            }
        }

        return tags;
    }

    private static Color ResolveColor(TerrainProcessCell cell, TerrainOverlayMode overlayMode)
    {
        if (overlayMode == TerrainOverlayMode.SolidLandWater)
        {
            return cell.IsLand ? TerrainRenderer.LandColor : cell.IsDeepOcean ? TerrainRenderer.DeepOceanColor : TerrainRenderer.WaterColor;
        }

        return overlayMode switch
        {
            TerrainOverlayMode.Elevation => Ramp(cell.Elevation, -0.72f, 0.92f, Color.FromArgb(0, 30, 96), Color.White),
            TerrainOverlayMode.WaterDepth => cell.IsWater
                ? Ramp(cell.WaterDepth, 0f, 1.10f, Color.FromArgb(90, 190, 255), Color.FromArgb(0, 12, 64))
                : Color.Black,
            TerrainOverlayMode.Lithology => cell.Lithology switch
            {
                TerrainLithology.Limestone => Color.FromArgb(212, 214, 190),
                TerrainLithology.VolcanicRock => Color.FromArgb(62, 62, 66),
                TerrainLithology.SedimentaryRock => Color.FromArgb(148, 112, 84),
                TerrainLithology.ReefLimestone => Color.FromArgb(104, 205, 216),
                TerrainLithology.SandMud => Color.FromArgb(210, 188, 112),
                _ => Color.Magenta
            },
            TerrainOverlayMode.FractureStrength => Ramp(cell.FractureStrength, 0f, 1f, Color.Black, Color.FromArgb(255, 235, 104)),
            TerrainOverlayMode.Dissolution => Ramp(cell.Dissolution, 0f, 1f, Color.Black, Color.FromArgb(170, 255, 196)),
            TerrainOverlayMode.WaveExposure => Ramp(cell.WaveExposure, 0f, 1f, Color.FromArgb(12, 35, 80), Color.FromArgb(255, 92, 64)),
            TerrainOverlayMode.Sediment => Ramp(cell.Sediment, 0f, 1f, Color.FromArgb(18, 48, 58), Color.FromArgb(244, 211, 112)),
            TerrainOverlayMode.ReefSuitability => Ramp(cell.ReefSuitability, 0f, 1f, Color.FromArgb(3, 32, 64), Color.FromArgb(108, 255, 230)),
            TerrainOverlayMode.ClassifiedLandforms => ResolveTagColor(cell.LandformTags, cell.IsLand),
            TerrainOverlayMode.LagoonOpenings => Ramp(cell.TidalFlow, 0f, 1f, cell.IsLand ? Color.Black : TerrainRenderer.WaterColor, Color.White),
            TerrainOverlayMode.IslandClusters => Ramp(cell.IslandCluster, 0f, 1f, TerrainRenderer.WaterColor, Color.Black),
            _ => cell.IsLand ? TerrainRenderer.LandColor : cell.IsDeepOcean ? TerrainRenderer.DeepOceanColor : TerrainRenderer.WaterColor
        };
    }

    private static Color ResolveTagColor(TerrainLandformTag tags, bool isLand)
    {
        if ((tags & TerrainLandformTag.PorcoRossoLikeHiddenBase) != 0)
        {
            return Color.FromArgb(255, 116, 70);
        }
        if ((tags & TerrainLandformTag.StinivaLikePocketCove) != 0)
        {
            return Color.FromArgb(255, 224, 106);
        }
        if ((tags & TerrainLandformTag.KarstHongLagoon) != 0)
        {
            return Color.FromArgb(142, 255, 172);
        }
        if ((tags & TerrainLandformTag.Lagoon) != 0)
        {
            return Color.FromArgb(74, 224, 255);
        }
        if ((tags & TerrainLandformTag.Reef) != 0)
        {
            return Color.FromArgb(92, 255, 220);
        }
        if ((tags & TerrainLandformTag.SeaCave) != 0)
        {
            return Color.FromArgb(172, 126, 255);
        }
        if ((tags & TerrainLandformTag.PocketBeach) != 0)
        {
            return Color.FromArgb(236, 218, 126);
        }
        if ((tags & TerrainLandformTag.Stack) != 0)
        {
            return Color.FromArgb(210, 210, 220);
        }

        return isLand ? TerrainRenderer.LandColor : TerrainRenderer.WaterColor;
    }

    private static float ResolveMaxRegionInfluence(float worldX, float worldY, TerrainScene scene, out LandformInstance strongestRegion)
    {
        float strongest = 0f;
        strongestRegion = null;
        for (int i = 0; i < scene.Landforms.Count; i++)
        {
            LandformInstance region = scene.Landforms[i];
            if (region.Kind == LandformKind.ArchipelagoRegion)
            {
                continue;
            }

            float influence = ResolveRegionInfluence(worldX, worldY, region, scene, out _, out _, out _);
            if (influence <= strongest)
            {
                continue;
            }

            strongest = influence;
            strongestRegion = region;
        }

        return strongest;
    }

    private static float ResolveParentInfluenceGate(LandformInstance region, TerrainScene scene, ReadOnlySpan<float> regionInfluences)
    {
        if (region.ParentId == 0)
        {
            return 1f;
        }

        float gate = 1f;
        LandformInstance current = region;
        int guard = 0;
        while (current.ParentId != 0 && guard++ < 32)
        {
            int parentIndex = FindLandformIndex(scene, current.ParentId);
            if (parentIndex < 0)
            {
                break;
            }

            LandformInstance parent = scene.Landforms[parentIndex];
            if (parent.Kind != LandformKind.ArchipelagoRegion)
            {
                gate *= SmoothStep(0.06f, 0.30f, regionInfluences[parentIndex]);
            }

            current = parent;
        }

        return gate;
    }

    private static float ResolveParentInfluenceGate(RegionSamplingContext regionContext, TerrainSamplingContext context, ReadOnlySpan<float> regionInfluences)
    {
        if (regionContext.ParentIndex < 0)
        {
            return 1f;
        }

        float gate = 1f;
        int parentIndex = regionContext.ParentIndex;
        int guard = 0;
        while (parentIndex >= 0 && guard++ < 32)
        {
            RegionSamplingContext parent = context.Regions[parentIndex];
            if (parent.Region.Kind != LandformKind.ArchipelagoRegion)
            {
                gate *= SmoothStep(0.06f, 0.30f, regionInfluences[parentIndex]);
            }

            parentIndex = parent.ParentIndex;
        }

        return gate;
    }

    private static int FindLandformIndex(TerrainScene scene, int id)
    {
        int cachedIndex = scene.ResolveLandformIndex(id);
        if (cachedIndex >= 0)
        {
            return cachedIndex;
        }

        for (int i = 0; i < scene.Landforms.Count; i++)
        {
            if (scene.Landforms[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static float ResolveRegionInfluence(
        float worldX,
        float worldY,
        LandformInstance region,
        TerrainScene scene,
        out float localX,
        out float localY,
        out float radius)
    {
        float scale = scene.ResolveEffectiveScale(region);
        float widthScale = scene.ResolveEffectiveWidthScale(region);
        radius = Math.Max(48f, region.BaseRadius * scale * scene.GlobalScale);
        float length = Math.Max(radius, region.BaseLength * scale * scene.GlobalScale);
        float influenceRadiusY = radius * (0.92f + (widthScale * 0.24f));
        float influenceBound = Math.Max(length, influenceRadiusY) * 1.18f + Math.Max(36f, radius * 0.08f);
        float dx = worldX - region.CenterX;
        float dy = worldY - region.CenterY;
        if ((dx * dx) + (dy * dy) > influenceBound * influenceBound)
        {
            localX = 0f;
            localY = 0f;
            return 0f;
        }

        float angle = DegreesToRadians(region.RotationDegrees);
        Rotate(dx, dy, -angle, out localX, out localY);

        float warp = Fbm((worldX * 0.0016f) + region.Id, (worldY * 0.0016f) - region.Id, scene.Seed + region.Id * 313, 3) * radius * 0.025f;
        localX += warp;
        localY -= warp * 0.55f;

        float nx = localX / Math.Max(1f, length);
        float ny = localY / Math.Max(1f, influenceRadiusY);
        float distance = MathF.Sqrt((nx * nx) + (ny * ny));
        return SmoothStep(1.10f, 0.08f, distance);
    }

    private static float ResolveRegionInfluence(
        float worldX,
        float worldY,
        RegionSamplingContext regionContext,
        int seed,
        out float localX,
        out float localY,
        out float radius)
    {
        LandformInstance region = regionContext.Region;
        radius = regionContext.Radius;
        float dx = worldX - region.CenterX;
        float dy = worldY - region.CenterY;
        if ((dx * dx) + (dy * dy) > regionContext.InfluenceBoundSquared)
        {
            localX = 0f;
            localY = 0f;
            return 0f;
        }

        localX = (dx * regionContext.CosRotation) + (dy * regionContext.SinRotation);
        localY = (-dx * regionContext.SinRotation) + (dy * regionContext.CosRotation);

        float warp = Fbm((worldX * 0.0016f) + region.Id, (worldY * 0.0016f) - region.Id, seed + region.Id * 313, 3) * radius * 0.025f;
        localX += warp;
        localY -= warp * 0.55f;

        float nx = localX / Math.Max(1f, regionContext.Length);
        float ny = localY / Math.Max(1f, regionContext.InfluenceRadiusY);
        float distance = MathF.Sqrt((nx * nx) + (ny * ny));
        return SmoothStep(1.10f, 0.08f, distance);
    }

    private static TerrainLithologyWeights ResolveLithology(float x, float y, TerrainScene scene)
    {
        int seed = scene.Seed;
        float limestone = 0.18f + (Fbm(x * 0.0028f, y * 0.0028f, seed + 201, 4) * 0.45f);
        float volcanic = 0.18f + (RidgedFbm(x * 0.0024f, y * 0.0024f, seed + 202, 4) * 0.45f);
        float sedimentary = 0.16f + (Fbm((x + 700f) * 0.0022f, (y - 300f) * 0.0022f, seed + 203, 4) * 0.36f);
        float reef = 0.12f + (Fbm((x - 450f) * 0.0026f, (y + 280f) * 0.0026f, seed + 204, 4) * 0.42f);
        float sand = 0.14f + (Fbm((x + y) * 0.0019f, (y - x) * 0.0019f, seed + 205, 4) * 0.40f);

        for (int i = 0; i < scene.Landforms.Count; i++)
        {
            LandformInstance region = scene.Landforms[i];
            float influence = ResolveRegionInfluence(x, y, region, scene, out _, out _, out _);
            AccumulateLithologyBias(region, influence, scene, ref limestone, ref volcanic, ref sedimentary, ref reef, ref sand);
        }

        return TerrainLithologyWeights.Normalize(limestone, volcanic, sedimentary, reef, sand);
    }

    private static TerrainLithologyWeights ResolveLithologyFromRegionInfluences(float x, float y, TerrainScene scene, ReadOnlySpan<float> regionInfluences)
    {
        int seed = scene.Seed;
        float limestone = 0.18f + (Fbm(x * 0.0028f, y * 0.0028f, seed + 201, 4) * 0.45f);
        float volcanic = 0.18f + (RidgedFbm(x * 0.0024f, y * 0.0024f, seed + 202, 4) * 0.45f);
        float sedimentary = 0.16f + (Fbm((x + 700f) * 0.0022f, (y - 300f) * 0.0022f, seed + 203, 4) * 0.36f);
        float reef = 0.12f + (Fbm((x - 450f) * 0.0026f, (y + 280f) * 0.0026f, seed + 204, 4) * 0.42f);
        float sand = 0.14f + (Fbm((x + y) * 0.0019f, (y - x) * 0.0019f, seed + 205, 4) * 0.40f);

        int count = Math.Min(scene.Landforms.Count, regionInfluences.Length);
        for (int i = 0; i < count; i++)
        {
            AccumulateLithologyBias(scene.Landforms[i], regionInfluences[i], scene, ref limestone, ref volcanic, ref sedimentary, ref reef, ref sand);
        }

        return TerrainLithologyWeights.Normalize(limestone, volcanic, sedimentary, reef, sand);
    }

    private static TerrainLithologyWeights ResolveLithologyFromRegionInfluences(float x, float y, TerrainSamplingContext context, ReadOnlySpan<float> regionInfluences)
    {
        TerrainScene scene = context.Scene;
        int seed = scene.Seed;
        float limestone = 0.18f + (Fbm(x * 0.0028f, y * 0.0028f, seed + 201, 4) * 0.45f);
        float volcanic = 0.18f + (RidgedFbm(x * 0.0024f, y * 0.0024f, seed + 202, 4) * 0.45f);
        float sedimentary = 0.16f + (Fbm((x + 700f) * 0.0022f, (y - 300f) * 0.0022f, seed + 203, 4) * 0.36f);
        float reef = 0.12f + (Fbm((x - 450f) * 0.0026f, (y + 280f) * 0.0026f, seed + 204, 4) * 0.42f);
        float sand = 0.14f + (Fbm((x + y) * 0.0019f, (y - x) * 0.0019f, seed + 205, 4) * 0.40f);

        ReadOnlySpan<int> activeRegionIndexes = context.ActiveRegionIndexes;
        for (int activeIndex = 0; activeIndex < activeRegionIndexes.Length; activeIndex++)
        {
            int i = activeRegionIndexes[activeIndex];
            if (i >= regionInfluences.Length)
            {
                continue;
            }

            RegionSamplingContext regionContext = context.Regions[i];
            AccumulateLithologyBias(regionContext.Region, regionInfluences[i], regionContext.EffectiveWidthScale, ref limestone, ref volcanic, ref sedimentary, ref reef, ref sand);
        }

        return TerrainLithologyWeights.Normalize(limestone, volcanic, sedimentary, reef, sand);
    }

    private static void AccumulateLithologyBias(
        LandformInstance region,
        float influence,
        TerrainScene scene,
        ref float limestone,
        ref float volcanic,
        ref float sedimentary,
        ref float reef,
        ref float sand)
    {
        float widthScale = scene.ResolveEffectiveWidthScale(region);
        AccumulateLithologyBias(region, influence, widthScale, ref limestone, ref volcanic, ref sedimentary, ref reef, ref sand);
    }

    private static void AccumulateLithologyBias(
        LandformInstance region,
        float influence,
        float widthScale,
        ref float limestone,
        ref float volcanic,
        ref float sedimentary,
        ref float reef,
        ref float sand)
    {
        switch (region.Kind)
        {
            case LandformKind.ArchipelagoRegion:
                limestone += influence * 0.18f;
                volcanic += influence * 0.14f;
                sedimentary += influence * 0.16f;
                reef += influence * 0.10f;
                sand += influence * 0.10f;
                break;
            case LandformKind.ReefShelf:
            case LandformKind.AtollLagoon:
            case LandformKind.Atoll:
                reef += influence * 1.35f * widthScale;
                sand += influence * 0.35f;
                break;
            case LandformKind.BarrierReef:
            case LandformKind.ReefBarrierLagoon:
                reef += influence * 0.82f;
                sedimentary += influence * 0.34f;
                break;
            case LandformKind.SedimentShelf:
            case LandformKind.SpitAndTombolo:
            case LandformKind.BarrierIsland:
            case LandformKind.BarrierIslandLagoon:
                sand += influence * 1.25f * widthScale;
                sedimentary += influence * 0.42f;
                break;
            case LandformKind.KarstTowerField:
            case LandformKind.KarstHong:
            case LandformKind.KarstHongLagoon:
                limestone += influence * 1.45f * widthScale;
                break;
            case LandformKind.IslandRing:
            case LandformKind.IslandRingLagoon:
                volcanic += influence * 0.62f;
                limestone += influence * 0.34f;
                reef += influence * 0.38f;
                break;
            case LandformKind.IslandCluster:
            case LandformKind.MainIsland:
                volcanic += influence * 0.42f;
                sedimentary += influence * 0.32f;
                limestone += influence * 0.22f;
                break;
            case LandformKind.CliffCoast:
            case LandformKind.StacksAndArches:
                volcanic += influence * 0.92f;
                limestone += influence * 0.28f;
                break;
            case LandformKind.GiantRiver:
                sedimentary += influence * 0.22f;
                sand += influence * 0.54f;
                break;
            case LandformKind.CalanqueCove:
            case LandformKind.CalanqueCoveLagoon:
                volcanic += influence * 0.96f;
                limestone += influence * 0.42f;
                break;
        }
    }

    private static float MacroArchipelagoMask(float x, float y, int seed)
    {
        float shelf = (Fbm(x * 0.0011f, y * 0.0011f, seed + 301, 5) + 1f) * 0.5f;
        float ridges = RidgedFbm(x * 0.0019f, y * 0.0019f, seed + 302, 4);
        float protectedBasins = 1f - RidgedFbm((x + 1000f) * 0.0014f, (y - 600f) * 0.0014f, seed + 303, 4);
        float cluster = ResolveProceduralClusterField(x, y, seed);
        return Math.Clamp((shelf * 0.36f) + (ridges * 0.22f) + (protectedBasins * 0.10f) + (cluster * 0.62f), 0f, 1f);
    }

    private static float ResolveProceduralClusterField(float x, float y, int seed)
    {
        const float cellSize = 780f;
        int cellX = FastFloor(x / cellSize);
        int cellY = FastFloor(y / cellSize);
        float best = 0f;

        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int cx = cellX + offsetX;
                int cy = cellY + offsetY;
                float presence = Hash(cx, cy, seed + 320);
                if (presence < 0.35f)
                {
                    continue;
                }

                float centerX = (cx + 0.22f + (Hash(cx, cy, seed + 321) * 0.56f)) * cellSize;
                float centerY = (cy + 0.22f + (Hash(cx, cy, seed + 322) * 0.56f)) * cellSize;
                float radius = cellSize * (0.45f + (Hash(cx, cy, seed + 323) * 0.46f));
                float distance = Distance(x, y, centerX, centerY);
                float cluster = SmoothStep(radius, radius * 0.16f, distance);
                best = MathF.Max(best, cluster);
            }
        }

        return best;
    }

    private static float ResolveFractureStrength(float x, float y, int seed, LandformInstance strongestRegion, TerrainScene scene)
    {
        float angle = strongestRegion != null
            ? DegreesToRadians(strongestRegion.RotationDegrees)
            : Fbm(x * 0.0012f, y * 0.0012f, seed + 401, 3) * MathF.PI;
        Rotate(x, y, angle, out float fx, out float fy);
        float jointSetA = SmoothStep(0.62f, 0.92f, RidgedFbm(fx * 0.006f, fy * 0.044f, seed + 402, 5));
        Rotate(x + 900f, y - 500f, angle + 1.18f, out fx, out fy);
        float jointSetB = SmoothStep(0.68f, 0.95f, RidgedFbm(fx * 0.005f, fy * 0.036f, seed + 403, 5));
        float radialCracks = strongestRegion == null ? 0f : ResolveRegionInfluence(x, y, strongestRegion, scene, out float localX, out float localY, out _) *
            SmoothStep(0.70f, 0.96f, RidgedFbm(localX * 0.014f, localY * 0.014f, seed + strongestRegion.Id * 409, 4));
        return Math.Clamp(MathF.Max(jointSetA, jointSetB) + (radialCracks * 0.35f), 0f, 1f);
    }

    private static float ResolveLocalFractures(float localX, float localY, int seed, float degrees)
    {
        Rotate(localX, localY, DegreesToRadians(degrees), out float aX, out float aY);
        float a = SmoothStep(0.68f, 0.96f, RidgedFbm(aX * 0.010f, aY * 0.062f, seed + 501, 5));
        Rotate(localX, localY, DegreesToRadians(degrees + 64f), out float bX, out float bY);
        float b = SmoothStep(0.70f, 0.96f, RidgedFbm(bX * 0.012f, bY * 0.056f, seed + 502, 5));
        return MathF.Max(a, b);
    }

    private static float ResolveWaveExposure(float x, float y, int seed, float macroShelf, float clusterInfluence, float coastBand)
    {
        float fetch = 1f - macroShelf;
        float stormTrack = (Fbm((x - 800f) * 0.0016f, (y + 400f) * 0.0016f, seed + 601, 4) + 1f) * 0.5f;
        float exposure = (fetch * 0.54f) + (stormTrack * 0.32f) + ((1f - clusterInfluence) * 0.24f);
        exposure *= 0.72f + (coastBand * 0.36f);
        return Math.Clamp(exposure, 0f, 1f);
    }

    private static float ResolveOpeningCut(float x, float y, int seed, float fracture, float waveExposure, TerrainScene scene)
    {
        float openingScale = Math.Clamp(scene.GlobalOpeningWidth, 0.25f, 3.0f);
        Rotate(x, y, Fbm(x * 0.001f, y * 0.001f, seed + 701, 3) * MathF.PI, out float fx, out float fy);
        float seaGate = SmoothStep(0.74f, 0.97f, RidgedFbm(fx * 0.0042f / openingScale, fy * 0.032f, seed + 702, 4));
        float stormCuts = SmoothStep(0.72f, 0.95f, RidgedFbm((x + 200f) * 0.0048f, (y - 700f) * 0.0048f, seed + 703, 3));
        return Math.Clamp(((seaGate * fracture * 0.16f) + (stormCuts * waveExposure * 0.045f)) * scene.GlobalOpeningStrength, 0f, 0.34f);
    }

    private static float ResolveSediment(
        float x,
        float y,
        int seed,
        TerrainLithologyWeights lithology,
        float waveExposure,
        float waterDepth,
        float coastBand,
        TerrainScene scene)
    {
        float lowEnergy = 1f - waveExposure;
        float shallow = 1f - Math.Clamp(waterDepth / 0.62f, 0f, 1f);
        float supply = Math.Clamp(lithology.SandMud + (lithology.SedimentaryRock * 0.45f), 0f, 1f);
        float transport = (Fbm(x * 0.006f, y * 0.006f, seed + 801, 4) + 1f) * 0.5f;
        return Math.Clamp((supply * 0.54f) + (lowEnergy * shallow * 0.48f) + (transport * coastBand * 0.18f), 0f, 1f) *
            Math.Clamp(scene.GlobalWidthScale, 0.4f, 2.4f);
    }

    private static float ResolveReefSuitability(
        float x,
        float y,
        int seed,
        TerrainLithologyWeights lithology,
        float waveExposure,
        float waterDepth,
        float macroShelf,
        TerrainScene scene)
    {
        float shallow = 1f - Math.Clamp(MathF.Abs(waterDepth - 0.24f) / 0.64f, 0f, 1f);
        float clearWater = 1f - Math.Clamp(lithology.SandMud * 0.64f, 0f, 1f);
        float moderateEnergy = 1f - MathF.Abs(waveExposure - 0.48f) / 0.58f;
        float tropicalBias = (Fbm((x - 1200f) * 0.0014f, (y + 900f) * 0.0014f, seed + 901, 4) + 1f) * 0.5f;
        return Math.Clamp(
            (lithology.ReefLimestone * 0.54f) +
            (shallow * clearWater * moderateEnergy * 0.58f) +
            (macroShelf * tropicalBias * 0.26f),
            0f,
            1f) * Math.Clamp(scene.GlobalWidthScale, 0.4f, 2.2f);
    }

    private static float ResolveAngularOpening(float localX, float localY, LandformInstance region, TerrainScene scene, float radius, int maxCount, int seed)
    {
        float theta = MathF.Atan2(localY, localX);
        float distance = MathF.Sqrt((localX * localX) + (localY * localY));
        float openingCount = Hash(region.Id, seed) < 0.58f ? 1 : Math.Clamp(maxCount, 1, 2);
        float opening = 0f;
        float width = Math.Clamp(scene.ResolveEffectiveOpeningWidth(region) * scene.GlobalOpeningWidth, 0.18f, 2.8f) * 0.22f;

        for (int i = 0; i < openingCount; i++)
        {
            float angle = Hash(region.Id + (i * 17), seed + 3) * MathF.Tau;
            float angular = SmoothStep(width, width * 0.16f, AngularDistance(theta, angle));
            float radial = SmoothStep(radius * 0.38f, radius * 0.06f, MathF.Abs(distance - (radius * 0.54f)));
            opening = MathF.Max(opening, angular * radial);
        }

        return opening;
    }

    private static float ResolveSinuousBarrier(float localX, float localY, float radius, float widthScale, int seed)
    {
        float halfLength = radius * 0.88f;
        float phase = Hash(seed, seed + 1) * MathF.Tau;
        float curve = MathF.Sin((localX / Math.Max(1f, halfLength)) * MathF.PI * 0.88f + phase) * radius * 0.12f;
        float axial = SmoothStep(1.10f, 0.70f, MathF.Abs(localX) / Math.Max(1f, halfLength));
        float lateral = SmoothStep(radius * 0.18f * widthScale, radius * 0.026f * widthScale, MathF.Abs(localY - curve));
        float inlet = ResolveBarrierPass(localX, localY - curve, radius, widthScale, seed + 2);
        return Math.Clamp((axial * lateral) - (inlet * 0.94f), 0f, 1f);
    }

    private static float ResolveBarrierPass(float localX, float localY, float radius, float widthScale, int seed)
    {
        float pass = 0f;
        int count = Hash(seed, seed + 3) < 0.62f ? 1 : 2;
        for (int i = 0; i < count; i++)
        {
            float x = (-0.58f + (Hash(seed + i * 11, seed + i * 13) * 1.16f)) * radius;
            float along = SmoothStep(radius * 0.16f, radius * 0.026f, MathF.Abs(localX - x));
            float across = SmoothStep(radius * 0.24f * widthScale, radius * 0.022f * widthScale, MathF.Abs(localY));
            pass = MathF.Max(pass, along * across);
        }

        return pass;
    }

    private static float ResolveSpitHook(float localX, float localY, float radius, float widthScale, int seed)
    {
        float side = Hash(seed, seed + 9) < 0.5f ? -1f : 1f;
        float hookX = (radius * 0.62f) * side;
        float hookY = radius * 0.14f * side;
        float dx = localX - hookX;
        float dy = localY - hookY;
        float distance = MathF.Sqrt((dx * dx) + (dy * dy));
        return SmoothStep(radius * 0.26f, radius * 0.040f * widthScale, distance);
    }

    private static float ResolveRavine(float localX, float localY, float radius, float widthScale, int seed)
    {
        float halfLength = radius * 0.86f;
        float mouth = SmoothStep(radius * 0.80f, -radius * 0.22f, localX);
        float widening = 1f + (SmoothStep(radius * 0.18f, -radius * 0.36f, localX) * 1.85f);
        float across = SmoothStep(radius * 0.13f * widthScale * widening, radius * 0.018f * widthScale, MathF.Abs(localY));
        float along = SmoothStep(1.05f, 0.66f, MathF.Abs(localX) / Math.Max(1f, halfLength));
        float caveCollapse = SmoothStep(radius * 0.34f, radius * 0.06f, Distance(localX, localY, -radius * 0.22f, 0f));
        return Math.Clamp((mouth * across * along) + (caveCollapse * 0.32f), 0f, 1f);
    }

    private static Color Ramp(float value, float min, float max, Color low, Color high)
    {
        float t = Math.Clamp((value - min) / Math.Max(0.0001f, max - min), 0f, 1f);
        int r = (int)MathF.Round(low.R + ((high.R - low.R) * t));
        int g = (int)MathF.Round(low.G + ((high.G - low.G) * t));
        int b = (int)MathF.Round(low.B + ((high.B - low.B) * t));
        return Color.FromArgb(r, g, b);
    }

    private static float Fbm(float x, float y, int seed, int octaves)
    {
        float amplitude = 0.5f;
        float frequency = 1f;
        float sum = 0f;
        float norm = 0f;
        for (int octave = 0; octave < octaves; octave++)
        {
            sum += ValueNoise(x * frequency, y * frequency, seed + (octave * 1013)) * amplitude;
            norm += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return norm <= 0f ? 0f : sum / norm;
    }

    private static float RidgedFbm(float x, float y, int seed, int octaves)
    {
        return 1f - MathF.Abs(Fbm(x, y, seed, octaves));
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        float tx = x - x0;
        float ty = y - y0;
        tx = tx * tx * (3f - (2f * tx));
        ty = ty * ty * (3f - (2f * ty));

        float a = (Hash(x0, y0, seed) * 2f) - 1f;
        float b = (Hash(x0 + 1, y0, seed) * 2f) - 1f;
        float c = (Hash(x0, y0 + 1, seed) * 2f) - 1f;
        float d = (Hash(x0 + 1, y0 + 1, seed) * 2f) - 1f;
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float denominator = edge1 - edge0;
        if (MathF.Abs(denominator) < 0.0001f)
        {
            return value >= edge1 ? 1f : 0f;
        }

        float t = Math.Clamp((value - edge0) / denominator, 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * t);
    }

    private static void Rotate(float x, float y, float angle, out float rotatedX, out float rotatedY)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        rotatedX = (x * cos) - (y * sin);
        rotatedY = (x * sin) + (y * cos);
    }

    private static float Distance(float x, float y, float centerX, float centerY)
    {
        float dx = x - centerX;
        float dy = y - centerY;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float EllipseDistance(float x, float y, float radiusX, float radiusY)
    {
        float nx = x / Math.Max(1f, radiusX);
        float ny = y / Math.Max(1f, radiusY);
        return MathF.Sqrt((nx * nx) + (ny * ny));
    }

    private static float AngularDistance(float left, float right)
    {
        float delta = MathF.Abs(left - right) % MathF.Tau;
        return delta > MathF.PI ? MathF.Tau - delta : delta;
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    private static int FastFloor(float value)
    {
        int integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private static float Hash(int x, int seed)
    {
        return Hash(x, x * 31, seed);
    }

    private static float Hash(int x, int y, int seed = 0)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 374761393u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 668265263u;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }

    private readonly struct TerrainLithologyWeights
    {
        private TerrainLithologyWeights(float limestone, float volcanicRock, float sedimentaryRock, float reefLimestone, float sandMud)
        {
            Limestone = limestone;
            VolcanicRock = volcanicRock;
            SedimentaryRock = sedimentaryRock;
            ReefLimestone = reefLimestone;
            SandMud = sandMud;
        }

        public float Limestone { get; }
        public float VolcanicRock { get; }
        public float SedimentaryRock { get; }
        public float ReefLimestone { get; }
        public float SandMud { get; }

        public TerrainLithology Dominant
        {
            get
            {
                TerrainLithology dominant = TerrainLithology.Limestone;
                float best = Limestone;
                if (VolcanicRock > best)
                {
                    dominant = TerrainLithology.VolcanicRock;
                    best = VolcanicRock;
                }
                if (SedimentaryRock > best)
                {
                    dominant = TerrainLithology.SedimentaryRock;
                    best = SedimentaryRock;
                }
                if (ReefLimestone > best)
                {
                    dominant = TerrainLithology.ReefLimestone;
                    best = ReefLimestone;
                }
                if (SandMud > best)
                {
                    dominant = TerrainLithology.SandMud;
                }

                return dominant;
            }
        }

        public static TerrainLithologyWeights Normalize(float limestone, float volcanicRock, float sedimentaryRock, float reefLimestone, float sandMud)
        {
            limestone = MathF.Max(0.01f, limestone);
            volcanicRock = MathF.Max(0.01f, volcanicRock);
            sedimentaryRock = MathF.Max(0.01f, sedimentaryRock);
            reefLimestone = MathF.Max(0.01f, reefLimestone);
            sandMud = MathF.Max(0.01f, sandMud);
            float sum = limestone + volcanicRock + sedimentaryRock + reefLimestone + sandMud;
            return new TerrainLithologyWeights(
                limestone / sum,
                volcanicRock / sum,
                sedimentaryRock / sum,
                reefLimestone / sum,
                sandMud / sum);
        }
    }
}
