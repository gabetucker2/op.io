#nullable enable

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 &&
                string.Equals(args[0], "terrain", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "spawns", StringComparison.OrdinalIgnoreCase))
            {
                return RunSpawnProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-metrics", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainMetricsProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-edge", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainEdgeProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-window", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainWindowProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-startup-readiness", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainStartupReadinessProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "level-startup", StringComparison.OrdinalIgnoreCase))
            {
                return RunLevelStartupProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-level-scout", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainLevelScoutProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "player-spawn-relocation", StringComparison.OrdinalIgnoreCase))
            {
                return RunPlayerSpawnRelocationProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-background-streaming", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainBackgroundStreamingProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-full-map-ocean-borders", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainFullMapOceanBordersProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-stale-materialization", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainStaleMaterializationProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-impulse", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainImpulseProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-access-gate", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainAccessGateProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "terrain-phase", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainPhaseProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "ambience-transition", StringComparison.OrdinalIgnoreCase))
            {
                return RunAmbienceTransitionProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "ocean-zones", StringComparison.OrdinalIgnoreCase))
            {
                return RunOceanZonesProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "ocean-zone-authority", StringComparison.OrdinalIgnoreCase))
            {
                return RunOceanZoneAuthorityProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "ocean-debug-borders", StringComparison.OrdinalIgnoreCase))
            {
                return RunOceanDebugBordersProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "ocean-debug-vision", StringComparison.OrdinalIgnoreCase))
            {
                return RunOceanDebugVisionProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "ocean-debug-render", StringComparison.OrdinalIgnoreCase))
            {
                return RunOceanDebugRenderProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "overlap", StringComparison.OrdinalIgnoreCase))
            {
                return RunOverlapProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "dead-input", StringComparison.OrdinalIgnoreCase))
            {
                return RunDeadInputProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "bullet-barrel-collision", StringComparison.OrdinalIgnoreCase))
            {
                return RunBulletBarrelCollisionProbe();
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "shape-lazy-load", StringComparison.OrdinalIgnoreCase))
            {
                return RunShapeLazyLoadProbe();
            }

            ResetRuntimeState();

            Console.WriteLine($"BaseDirectory={AppContext.BaseDirectory}");
            Console.WriteLine($"ProjectRoot={DatabaseConfig.ProjectRootPath}");
            Console.WriteLine($"DatabasePath={DatabaseConfig.DatabaseFilePath}");
            Console.WriteLine($"DatabaseExists={File.Exists(DatabaseConfig.DatabaseFilePath)}");

            Core core = CreateHeadlessCore();
            Core.Instance = core;
            core.GameObjects = new List<GameObject>();
            core.StaticObjects = new List<GameObject>();
            core.PhysicsManager = new PhysicsManager();

            List<Agent> agents = AgentLoader.LoadAgents();
            Console.WriteLine($"Loaded agents: {agents.Count}");
            foreach (Agent loadedAgent in agents)
            {
                Console.WriteLine($"  agent id={loadedAgent.ID} name={loadedAgent.Name} isPlayer={loadedAgent.IsPlayer} dynamic={loadedAgent.DynamicPhysics}");
            }
            foreach (Agent agent in agents)
            {
                ApplyDatabaseLoadout(agent);
            }

            List<GameObject> mapObjects = MapObjectLoader.LoadMapObjects();
            core.GameObjects.AddRange(mapObjects);
            core.StaticObjects.AddRange(mapObjects.Where(static go => !go.DynamicPhysics));
            core.GameObjects.AddRange(agents);

            Agent? player = agents.FirstOrDefault(static a => a.IsPlayer);
            Agent? scout = agents.FirstOrDefault(static a => string.Equals(a.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
            if (player == null || scout == null)
            {
                Console.WriteLine("Simulation setup failed: player or ScoutSentry1 not found.");
                return 1;
            }

            core.Player = player;

            RunScenario("OpenLane", player, scout, includeWorldPhysics: false);
            RunScenario("WorldLoaded", player, scout, includeWorldPhysics: true);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Simulation failed: {ex}");
            return 1;
        }
    }

    private static int RunTerrainProbe()
    {
        const int seed = 1337;
        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        MethodInfo resolveSeedAnchorMethod = terrainType.GetMethod("ResolveSeedAnchorCentifoot", staticFlags)!;
        MethodInfo buildChunkMethod = terrainType.GetMethod("BuildChunkData", staticFlags)!;
        FieldInfo terrainSeedField = terrainType.GetField("_terrainWorldSeed", staticFlags)!;
        FieldInfo terrainAnchorField = terrainType.GetField("_terrainSeedAnchorCentifoot", staticFlags)!;
        FieldInfo settingsLoadedField = terrainType.GetField("_settingsLoaded", staticFlags)!;
        FieldInfo chunkTextureResolutionField = terrainType.GetField("ChunkTextureResolution", staticFlags)!;

        Type chunkKeyType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkKey", throwOnError: true)!;
        Type generatedChunkType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+GeneratedChunkData", throwOnError: true)!;
        ConstructorInfo chunkKeyConstructor = chunkKeyType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int)], modifiers: null)!;
        PropertyInfo hasLandProperty = generatedChunkType.GetProperty("HasLand", instanceFlags)!;
        PropertyInfo landMaskProperty = generatedChunkType.GetProperty("LandMask", instanceFlags)!;

        terrainSeedField.SetValue(null, seed);
        object anchor = resolveSeedAnchorMethod.Invoke(null, [seed])!;
        terrainAnchorField.SetValue(null, anchor);
        settingsLoadedField.SetValue(null, true);

        float anchorX = (float)anchor.GetType().GetField("X", instanceFlags)!.GetValue(anchor)!;
        float anchorY = (float)anchor.GetType().GetField("Y", instanceFlags)!.GetValue(anchor)!;
        Console.WriteLine($"Terrain seed={seed}");
        Console.WriteLine($"Anchor={anchorX:0.###}, {anchorY:0.###} cft");

        for (int chunkY = -1; chunkY <= 2; chunkY++)
        {
            List<string> row = [];
            for (int chunkX = -1; chunkX <= 2; chunkX++)
            {
                object key = chunkKeyConstructor.Invoke([chunkX, chunkY]);
                object generatedChunk = buildChunkMethod.Invoke(null, [key])!;
                bool hasLand = (bool)hasLandProperty.GetValue(generatedChunk)!;
                byte[] pixels = (byte[])landMaskProperty.GetValue(generatedChunk)!;
                int opaquePixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i] != 0)
                    {
                        opaquePixels++;
                    }
                }

                row.Add($"({chunkX},{chunkY}) land={hasLand} opaque={opaquePixels}");
            }

            Console.WriteLine(string.Join(" | ", row));
        }

        return 0;
    }

    private static int RunAmbienceTransitionProbe()
    {
        ResetRuntimeState();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        Type ambienceType = gameplayAssembly.GetType("op.io.AmbienceSettings", throwOnError: true)!;
        Type fogType = gameplayAssembly.GetType("op.io.FogOfWarManager", throwOnError: true)!;

        MethodInfo initializeMethod = ambienceType.GetMethod("Initialize", staticFlags)!;
        MethodInfo setOceanWaterColorMethod = ambienceType.GetMethod(
            "SetOceanWaterColor",
            staticFlags,
            binder: null,
            [typeof(Color), typeof(bool)],
            modifiers: null)!;
        MethodInfo syncFogMethod = ambienceType.GetMethod(
            "SyncFogOfWarWithOceanWater",
            staticFlags,
            binder: null,
            [typeof(Color)],
            modifiers: null)!;

        PropertyInfo visualParametersProperty = fogType.GetProperty("VisualParameters", staticFlags)!;
        PropertyInfo fogColorIntensityProperty = fogType.GetProperty("FogColorIntensity", staticFlags)!;
        PropertyInfo currentBaseColorProperty = fogType.GetProperty("CurrentBaseColor", staticFlags)!;
        FieldInfo frontierBuildRequestIdField = fogType.GetField("_frontierBuildRequestId", staticFlags)!;

        initializeMethod.Invoke(null, null);

        Color configuredOceanColor = new(40, 176, 206, 255);
        setOceanWaterColorMethod.Invoke(null, [configuredOceanColor, false]);
        syncFogMethod.Invoke(null, [configuredOceanColor]);

        Color stableFogBaseColor = GetFogBaseColor();
        int stableFrontierRequestId = (int)frontierBuildRequestIdField.GetValue(null)!;
        float initialIntensity = (float)fogColorIntensityProperty.GetValue(null)!;

        Color darkOceanColor = new(5, 23, 27, 255);
        float lastIntensity = initialIntensity;
        Color lastCurrentColor = configuredOceanColor;

        for (int i = 1; i <= 30; i++)
        {
            float progress = i / 30f;
            Color liveOceanColor = LerpColor(configuredOceanColor, darkOceanColor, progress);
            syncFogMethod.Invoke(null, [liveOceanColor]);

            Color currentFogBaseColor = GetFogBaseColor();
            if (currentFogBaseColor.PackedValue != stableFogBaseColor.PackedValue)
            {
                Console.WriteLine($"FAIL: fog base color changed during live transition: {stableFogBaseColor} -> {currentFogBaseColor}");
                return 1;
            }

            int currentFrontierRequestId = (int)frontierBuildRequestIdField.GetValue(null)!;
            if (currentFrontierRequestId != stableFrontierRequestId)
            {
                Console.WriteLine($"FAIL: FOW frontier reset during live transition: {stableFrontierRequestId} -> {currentFrontierRequestId}");
                return 1;
            }

            lastIntensity = (float)fogColorIntensityProperty.GetValue(null)!;
            lastCurrentColor = (Color)currentBaseColorProperty.GetValue(null)!;
        }

        if (lastIntensity >= initialIntensity - 0.01f)
        {
            Console.WriteLine($"FAIL: fog intensity did not animate downward: initial={initialIntensity:0.000}, final={lastIntensity:0.000}");
            return 1;
        }

        if (lastCurrentColor.R == 0 && lastCurrentColor.G == 0 && lastCurrentColor.B == 0)
        {
            Console.WriteLine("FAIL: live fog color resolved to black.");
            return 1;
        }

        Console.WriteLine($"PASS: fog base stayed {stableFogBaseColor}, frontierResetId={stableFrontierRequestId}, intensity {initialIntensity:0.000}->{lastIntensity:0.000}, live={lastCurrentColor}");
        return 0;

        Color GetFogBaseColor()
        {
            object visualParameters = visualParametersProperty.GetValue(null)!;
            return (Color)visualParameters.GetType().GetField("BaseColor", instanceFlags)!.GetValue(visualParameters)!;
        }
    }

    private static Color LerpColor(Color start, Color end, float progress)
    {
        float clamped = Math.Clamp(progress, 0f, 1f);
        return new Color(
            (byte)Math.Clamp((int)MathF.Round(MathHelper.Lerp(start.R, end.R, clamped)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(MathHelper.Lerp(start.G, end.G, clamped)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(MathHelper.Lerp(start.B, end.B, clamped)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(MathHelper.Lerp(start.A, end.A, clamped)), 0, 255));
    }

    private static int RunOceanZonesProbe()
    {
        const int seed = 1337;
        const float scanHalfExtent = 2200f;
        const float gridStep = 160f;
        const float coastStep = 24f;
        const float maxCoastProbeDistance = 192f;
        const int targetCoastSamples = 64;

        ResetRuntimeState();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        Type waterType = gameplayAssembly.GetType("op.io.TerrainWaterType", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        PrepareTerrainForSeed(terrainType, staticFlags, seed);

        MethodInfo sampleMaskMethod = terrainType.GetMethod(
            "SampleTerrainMaskAtWorldPosition",
            staticFlags,
            binder: null,
            [typeof(float), typeof(float)],
            modifiers: null)!;
        MethodInfo resolveOceanZoneMethod = terrainType.GetMethod(
            "TryResolveOceanZoneAtWorldPosition",
            staticFlags,
            binder: null,
            [typeof(Vector2), waterType.MakeByRefType(), typeof(float).MakeByRefType(), typeof(float).MakeByRefType()],
            modifiers: null)!;
        MethodInfo resolveWaterTypeFromOffshoreDistanceMethod = terrainType.GetMethod(
            "ResolveWaterTypeFromOffshoreDistance",
            staticFlags,
            binder: null,
            [typeof(float)],
            modifiers: null)!;

        float waterZoneDistanceScale = GetStaticFloatProperty(terrainType, staticFlags, "TerrainWaterZoneDistanceScale");
        float shallowBaseDistance = GetStaticFloatProperty(terrainType, staticFlags, "TerrainWaterShallowDistance");
        float sunlitBaseDistance = GetStaticFloatProperty(terrainType, staticFlags, "TerrainWaterSunlitDistance");
        float twilightBaseDistance = GetStaticFloatProperty(terrainType, staticFlags, "TerrainWaterTwilightDistance");
        float midnightBaseDistance = GetStaticFloatProperty(terrainType, staticFlags, "TerrainWaterMidnightDistance");
        float shallowDistance = shallowBaseDistance;
        float sunlitDistance = sunlitBaseDistance;
        float twilightDistance = twilightBaseDistance;
        float midnightDistance = midnightBaseDistance;
        float transitionVolumeDistance = Convert.ToSingle(terrainType
            .GetProperty("TerrainOceanZoneMinimumTransitionVolumeDistance", staticFlags)!
            .GetValue(null)!);
        shallowDistance += transitionVolumeDistance;
        sunlitDistance += transitionVolumeDistance * 2f;
        twilightDistance += transitionVolumeDistance * 3f;
        midnightDistance += transitionVolumeDistance * 4f;
        bool zoneDistanceScaleValid =
            MathF.Abs(waterZoneDistanceScale - 1.0f) <= 0.001f &&
            MathF.Abs(shallowBaseDistance - 220f) <= 0.001f &&
            MathF.Abs(sunlitBaseDistance - 560f) <= 0.001f &&
            MathF.Abs(twilightBaseDistance - 1040f) <= 0.001f &&
            MathF.Abs(midnightBaseDistance - 1520f) <= 0.001f;
        bool directThresholdsValid =
            Convert.ToInt32(resolveWaterTypeFromOffshoreDistanceMethod.Invoke(null, [0f])!) == 0 &&
            Convert.ToInt32(resolveWaterTypeFromOffshoreDistanceMethod.Invoke(null, [shallowDistance + 1f])!) == 1 &&
            Convert.ToInt32(resolveWaterTypeFromOffshoreDistanceMethod.Invoke(null, [sunlitDistance + 1f])!) == 2 &&
            Convert.ToInt32(resolveWaterTypeFromOffshoreDistanceMethod.Invoke(null, [twilightDistance + 1f])!) == 3 &&
            Convert.ToInt32(resolveWaterTypeFromOffshoreDistanceMethod.Invoke(null, [midnightDistance + 1f])!) == 4;
        Vector2[] directions = BuildProbeDirections(32);

        List<OceanZoneSample> sampledWater = [];
        List<(Vector2 Position, Vector2 Direction)> coastRays = [];
        HashSet<(int X, int Y)> coastWaterKeys = [];
        int coastSamples = 0;
        int nonShallowCoastSamples = 0;
        int nearIslandWaterSamples = 0;
        int deepNearIslandWaterSamples = 0;
        float maxCoastOffshoreDistance = 0f;
        bool enoughCoastSamples = false;
        OceanZoneSample firstDeepNearIslandSample = default;
        float firstDeepNearIslandRayDistance = 0f;

        for (float y = -scanHalfExtent; y <= scanHalfExtent; y += gridStep)
        {
            for (float x = -scanHalfExtent; x <= scanHalfExtent; x += gridStep)
            {
                if (enoughCoastSamples)
                {
                    break;
                }

                Vector2 position = new(x, y);
                if (!SampleTerrainMask(sampleMaskMethod, position))
                {
                    continue;
                }

                for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                {
                    if (enoughCoastSamples)
                    {
                        break;
                    }

                    Vector2 direction = directions[directionIndex];
                    for (float distance = coastStep; distance <= maxCoastProbeDistance; distance += coastStep)
                    {
                        Vector2 waterPosition = position + (direction * distance);
                        if (SampleTerrainMask(sampleMaskMethod, waterPosition))
                        {
                            continue;
                        }

                        if (!TryProbeOceanZone(resolveOceanZoneMethod, waterPosition, out OceanZoneSample coastSample))
                        {
                            break;
                        }

                        nearIslandWaterSamples++;
                        if (!string.Equals(coastSample.ZoneName, "Shallow", StringComparison.Ordinal))
                        {
                            if (deepNearIslandWaterSamples == 0)
                            {
                                firstDeepNearIslandSample = coastSample;
                                firstDeepNearIslandRayDistance = distance;
                            }

                            deepNearIslandWaterSamples++;
                        }

                        sampledWater.Add(coastSample);
                        if (coastSample.OffshoreDistance <= shallowDistance + coastStep)
                        {
                            (int X, int Y) key = (
                                (int)MathF.Round(waterPosition.X / coastStep),
                                (int)MathF.Round(waterPosition.Y / coastStep));
                            if (coastWaterKeys.Add(key))
                            {
                                coastSamples++;
                                coastRays.Add((waterPosition, direction));
                                maxCoastOffshoreDistance = MathF.Max(maxCoastOffshoreDistance, coastSample.OffshoreDistance);
                                if (!string.Equals(coastSample.ZoneName, "Shallow", StringComparison.Ordinal))
                                {
                                    nonShallowCoastSamples++;
                                }

                                enoughCoastSamples = coastSamples >= targetCoastSamples;
                            }
                        }

                        break;
                    }
                }
            }

            if (enoughCoastSamples)
            {
                break;
            }
        }

        float[] offshoreProbeOffsets =
        [
            0f,
            shallowDistance * 0.75f,
            (shallowDistance + sunlitDistance) * 0.5f,
            (sunlitDistance + twilightDistance) * 0.5f,
            (twilightDistance + midnightDistance) * 0.5f,
            midnightDistance + (shallowDistance * 1.5f)
        ];
        for (int rayIndex = 0; rayIndex < coastRays.Count; rayIndex++)
        {
            (Vector2 coastPosition, Vector2 direction) = coastRays[rayIndex];
            for (int offsetIndex = 0; offsetIndex < offshoreProbeOffsets.Length; offsetIndex++)
            {
                Vector2 samplePosition = coastPosition + (direction * offshoreProbeOffsets[offsetIndex]);
                if (SampleTerrainMask(sampleMaskMethod, samplePosition))
                {
                    break;
                }

                if (TryProbeOceanZone(resolveOceanZoneMethod, samplePosition, out OceanZoneSample sample))
                {
                    sampledWater.Add(sample);
                }
            }
        }

        bool foundOuterGradationSample = sampledWater.Any(static sample => sample.ZoneIndex >= 2);
        for (float y = -7200f; y <= 7200f && !foundOuterGradationSample; y += 720f)
        {
            for (float x = -7200f; x <= 7200f && !foundOuterGradationSample; x += 720f)
            {
                Vector2 samplePosition = new(x, y);
                if (SampleTerrainMask(sampleMaskMethod, samplePosition))
                {
                    continue;
                }

                if (!TryProbeOceanZone(resolveOceanZoneMethod, samplePosition, out OceanZoneSample sample))
                {
                    continue;
                }

                sampledWater.Add(sample);
                foundOuterGradationSample = sample.ZoneIndex >= 2;
            }
        }

        bool foundAbyssSample = sampledWater.Any(static sample => sample.ZoneIndex >= 4);
        float[] farProbeDistances =
        [
            midnightDistance * 2f,
            midnightDistance * 3f,
            midnightDistance * 5f,
            midnightDistance * 8f,
            midnightDistance * 16f,
            midnightDistance * 32f,
            midnightDistance * 64f
        ];
        for (int distanceIndex = 0; distanceIndex < farProbeDistances.Length && !foundAbyssSample; distanceIndex++)
        {
            float distance = farProbeDistances[distanceIndex];
            for (int directionIndex = 0; directionIndex < directions.Length && !foundAbyssSample; directionIndex += 2)
            {
                Vector2 samplePosition = directions[directionIndex] * distance;
                if (SampleTerrainMask(sampleMaskMethod, samplePosition))
                {
                    continue;
                }

                if (!TryProbeOceanZone(resolveOceanZoneMethod, samplePosition, out OceanZoneSample sample))
                {
                    continue;
                }

                sampledWater.Add(sample);
                foundAbyssSample = sample.ZoneIndex >= 4;
            }
        }

        const float gradienceHalfExtent = 3600f;
        const float gradienceStep = 128f;
        int gradienceColumns = ((int)MathF.Floor((gradienceHalfExtent * 2f) / gradienceStep)) + 1;
        OceanZoneSample[] previousRow = new OceanZoneSample[gradienceColumns];
        bool[] previousRowWater = new bool[gradienceColumns];
        int checkedGradienceEdges = 0;
        int nonAdjacentZoneEdges = 0;
        OceanZoneSample firstGradienceA = default;
        OceanZoneSample firstGradienceB = default;
        for (int row = 0; row < gradienceColumns; row++)
        {
            float y = -gradienceHalfExtent + (row * gradienceStep);
            OceanZoneSample leftSample = default;
            bool leftWater = false;
            for (int column = 0; column < gradienceColumns; column++)
            {
                float x = -gradienceHalfExtent + (column * gradienceStep);
                bool currentWater = TryProbeOceanZone(resolveOceanZoneMethod, new Vector2(x, y), out OceanZoneSample currentSample);
                if (currentWater)
                {
                    sampledWater.Add(currentSample);
                }

                if (currentWater && leftWater)
                {
                    checkedGradienceEdges++;
                    if (Math.Abs(currentSample.ZoneIndex - leftSample.ZoneIndex) > 1)
                    {
                        if (nonAdjacentZoneEdges == 0)
                        {
                            firstGradienceA = leftSample;
                            firstGradienceB = currentSample;
                        }

                        nonAdjacentZoneEdges++;
                    }
                }

                if (currentWater && previousRowWater[column])
                {
                    checkedGradienceEdges++;
                    if (Math.Abs(currentSample.ZoneIndex - previousRow[column].ZoneIndex) > 1)
                    {
                        if (nonAdjacentZoneEdges == 0)
                        {
                            firstGradienceA = previousRow[column];
                            firstGradienceB = currentSample;
                        }

                        nonAdjacentZoneEdges++;
                    }
                }

                previousRow[column] = currentSample;
                previousRowWater[column] = currentWater;
                leftSample = currentSample;
                leftWater = currentWater;
            }
        }

        bool[] zonesSeen = new bool[5];
        bool thresholdsMatchZones = true;
        OceanZoneSample firstBadSample = default;
        foreach (OceanZoneSample sample in sampledWater)
        {
            int expectedZoneIndex = ResolveExpectedOceanZoneIndex(
                sample.OffshoreDistance,
                shallowDistance,
                sunlitDistance,
                twilightDistance,
                midnightDistance);
            if (sample.ZoneIndex >= 0 && sample.ZoneIndex < zonesSeen.Length)
            {
                zonesSeen[sample.ZoneIndex] = true;
            }

            if (sample.ZoneIndex != expectedZoneIndex)
            {
                thresholdsMatchZones = false;
                firstBadSample = sample;
                break;
            }
        }

        int zonesSeenCount = zonesSeen.Count(static seen => seen);
        bool coastSamplesValid = coastSamples >= 12;
        bool nearIslandSamplesValid = nearIslandWaterSamples >= 12;
        bool gradationsValid =
            zonesSeen[0] &&
            zonesSeen[1] &&
            zonesSeen[2] &&
            zonesSeen[4] &&
            zoneDistanceScaleValid &&
            directThresholdsValid;
        bool strictGradienceValid = checkedGradienceEdges > 0 && nonAdjacentZoneEdges == 0;

        Console.WriteLine("OceanZonesProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"SampledWater={sampledWater.Count}");
        Console.WriteLine($"CoastWaterSamples={coastSamples}");
        Console.WriteLine($"NonShallowCoastSamples={nonShallowCoastSamples}");
        Console.WriteLine($"NearIslandWaterSamples={nearIslandWaterSamples}");
        Console.WriteLine($"DeepNearIslandWaterSamples={deepNearIslandWaterSamples}");
        Console.WriteLine($"MaxCoastOffshoreDistance={maxCoastOffshoreDistance:0.###}");
        Console.WriteLine($"WaterZoneDistanceScale={waterZoneDistanceScale:0.###}");
        Console.WriteLine($"ZoneDistanceScaleValid={zoneDistanceScaleValid}");
        Console.WriteLine($"DirectThresholdsValid={directThresholdsValid}");
        Console.WriteLine($"ZonesSeen={FormatZonesSeen(zonesSeen)}");
        Console.WriteLine($"CoastSamplesValid={coastSamplesValid}");
        Console.WriteLine($"NearIslandSamplesValid={nearIslandSamplesValid}");
        Console.WriteLine($"GradationsValid={gradationsValid}");
        Console.WriteLine($"CheckedGradienceEdges={checkedGradienceEdges}");
        Console.WriteLine($"NonAdjacentZoneEdges={nonAdjacentZoneEdges}");
        Console.WriteLine($"StrictGradienceValid={strictGradienceValid}");
        Console.WriteLine($"ThresholdsMatchZones={thresholdsMatchZones}");
        if (deepNearIslandWaterSamples > 0)
        {
            Console.WriteLine(
                $"FirstDeepNearIslandSample zone={firstDeepNearIslandSample.ZoneName} " +
                $"offshore={firstDeepNearIslandSample.OffshoreDistance:0.###} " +
                $"rayDistance={firstDeepNearIslandRayDistance:0.###} " +
                $"position=({firstDeepNearIslandSample.Position.X:0.###},{firstDeepNearIslandSample.Position.Y:0.###})");
        }

        if (!thresholdsMatchZones)
        {
            Console.WriteLine(
                $"FirstBadSample zone={firstBadSample.ZoneName} index={firstBadSample.ZoneIndex} " +
                $"offshore={firstBadSample.OffshoreDistance:0.###} position=({firstBadSample.Position.X:0.###},{firstBadSample.Position.Y:0.###})");
        }

        if (!strictGradienceValid && nonAdjacentZoneEdges > 0)
        {
            Console.WriteLine(
                $"FirstNonAdjacentZoneEdge {firstGradienceA.ZoneName}->{firstGradienceB.ZoneName} " +
                $"a=({firstGradienceA.Position.X:0.###},{firstGradienceA.Position.Y:0.###}) offshore={firstGradienceA.OffshoreDistance:0.###} " +
                $"b=({firstGradienceB.Position.X:0.###},{firstGradienceB.Position.Y:0.###}) offshore={firstGradienceB.OffshoreDistance:0.###}");
        }

        return coastSamplesValid &&
            nearIslandSamplesValid &&
            gradationsValid &&
            strictGradienceValid &&
            zoneDistanceScaleValid &&
            directThresholdsValid &&
            thresholdsMatchZones
                ? 0
                : 1;
    }

    private static int RunOceanZoneAuthorityProbe()
    {
        const int seed = 1337;
        const int minChunk = -2;
        const int maxChunk = 2;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        Type waterType = gameplayAssembly.GetType("op.io.TerrainWaterType", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        PrepareTerrainForSeed(terrainType, staticFlags, seed);

        Type chunkBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkBounds", throwOnError: true)!;
        Type combinedResidentMaskType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+CombinedResidentMask", throwOnError: true)!;
        Type terrainWorldBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainWorldBounds", throwOnError: true)!;
        ConstructorInfo chunkBoundsConstructor = chunkBoundsType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int), typeof(int), typeof(int)], modifiers: null)!;
        ConstructorInfo combinedResidentMaskConstructor = combinedResidentMaskType.GetConstructor(instanceFlags, binder: null, [typeof(byte[]), typeof(int), typeof(int), typeof(int), typeof(int)], modifiers: null)!;
        FieldInfo chunkTextureResolutionField = terrainType.GetField("ChunkTextureResolution", staticFlags)!;
        FieldInfo terrainMaterializationRequestIdField = terrainType.GetField("_terrainMaterializationRequestId", staticFlags)!;
        MethodInfo buildChunkWorldBoundsMethod = terrainType.GetMethod("BuildChunkWorldBounds", staticFlags)!;
        MethodInfo buildTerrainMaterializationResultMethod = terrainType.GetMethod(
            "BuildTerrainMaterializationResult",
            staticFlags,
            binder: null,
            [combinedResidentMaskType, typeof(int), terrainWorldBoundsType],
            modifiers: null)!;
        MethodInfo applyTerrainMaterializationResultMethod = terrainType.GetMethod("ApplyTerrainMaterializationResult", staticFlags)!;
        MethodInfo resolveOceanZoneMethod = terrainType.GetMethod(
            "TryResolveOceanZoneAtWorldPosition",
            staticFlags,
            binder: null,
            [typeof(Vector2), waterType.MakeByRefType(), typeof(float).MakeByRefType(), typeof(float).MakeByRefType()],
            modifiers: null)!;

        int chunkTextureResolution = (int)chunkTextureResolutionField.GetRawConstantValue()!;
        int chunkSpan = (maxChunk - minChunk) + 1;
        int maskWidth = chunkSpan * chunkTextureResolution;
        int maskHeight = chunkSpan * chunkTextureResolution;
        object chunkWindow = chunkBoundsConstructor.Invoke([minChunk, maxChunk, minChunk, maxChunk]);
        object emptyMask = combinedResidentMaskConstructor.Invoke([new byte[maskWidth * maskHeight], maskWidth, maskHeight, minChunk, minChunk]);
        object worldBounds = buildChunkWorldBoundsMethod.Invoke(null, [chunkWindow])!;
        terrainMaterializationRequestIdField.SetValue(null, 1);
        object materialization = buildTerrainMaterializationResultMethod.Invoke(null, [emptyMask, 1, worldBounds])!;
        applyTerrainMaterializationResultMethod.Invoke(null, [materialization]);

        int samples = 0;
        int abyssSamples = 0;
        int nonAbyssSamples = 0;
        int unresolvedSamples = 0;
        OceanZoneSample firstNonAbyss = default;
        for (float y = -256f; y <= 1280f; y += 128f)
        {
            for (float x = -256f; x <= 1280f; x += 128f)
            {
                samples++;
                if (!TryProbeOceanZone(resolveOceanZoneMethod, new Vector2(x, y), out OceanZoneSample sample))
                {
                    unresolvedSamples++;
                    continue;
                }

                if (string.Equals(sample.ZoneName, "Abyss", StringComparison.Ordinal))
                {
                    abyssSamples++;
                    continue;
                }

                if (nonAbyssSamples == 0)
                {
                    firstNonAbyss = sample;
                }

                nonAbyssSamples++;
            }
        }

        bool emptyAppliedTerrainForcesAbyss = samples > 0 && abyssSamples == samples;

        Console.WriteLine("OceanZoneAuthorityProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"AppliedChunkWindow={minChunk}..{maxChunk}, {minChunk}..{maxChunk}");
        Console.WriteLine($"Samples={samples}");
        Console.WriteLine($"AbyssSamples={abyssSamples}");
        Console.WriteLine($"NonAbyssSamples={nonAbyssSamples}");
        Console.WriteLine($"UnresolvedSamples={unresolvedSamples}");
        Console.WriteLine($"EmptyAppliedTerrainForcesAbyss={emptyAppliedTerrainForcesAbyss}");
        if (nonAbyssSamples > 0)
        {
            Console.WriteLine(
                $"FirstNonAbyss zone={firstNonAbyss.ZoneName} offshore={firstNonAbyss.OffshoreDistance:0.###} " +
                $"position=({firstNonAbyss.Position.X:0.###},{firstNonAbyss.Position.Y:0.###})");
        }

        return emptyAppliedTerrainForcesAbyss ? 0 : 1;
    }

    private static int RunOceanDebugBordersProbe()
    {
        const int seed = 1337;
        const float minX = -512f;
        const float maxX = 512f;
        const float minY = -512f;
        const float maxY = 512f;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        Type waterType = gameplayAssembly.GetType("op.io.TerrainWaterType", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        PrepareTerrainForSeed(terrainType, staticFlags, seed);
        MethodInfo prepareStartupAroundMethod = terrainType.GetMethod(
            "PrepareStartupTerrainAroundWorldPosition",
            staticFlags,
            binder: null,
            [typeof(Vector2)],
            modifiers: null)!;
        bool vectorTerrainReady = (bool)prepareStartupAroundMethod.Invoke(null, [new Vector2(100f, 100f)])!;
        MethodInfo countDebugBordersMethod = terrainType.GetMethod(
            "CountOceanZoneDebugBorderSegmentsForWorldBounds",
            staticFlags,
            binder: null,
            [typeof(float), typeof(float), typeof(float), typeof(float)],
            modifiers: null)!;
        MethodInfo formatDebugLabelMethod = terrainType.GetMethod(
            "FormatOceanZoneDebugLabelForProbe",
            staticFlags,
            binder: null,
            [waterType],
            modifiers: null)!;
        MethodInfo validateDebugBorderMethod = terrainType.GetMethod(
            "ValidateOceanZoneDebugBorderConsistencyForProbe",
            staticFlags,
            binder: null,
            [
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType()
            ],
            modifiers: null)!;
        MethodInfo gridSignatureMethod = terrainType.GetMethod(
            "ResolveOceanZoneDebugBuildGridSignatureForProbe",
            staticFlags,
            binder: null,
            [typeof(float), typeof(float), typeof(float), typeof(float)],
            modifiers: null)!;
        MethodInfo tileRangeSignatureMethod = terrainType.GetMethod(
            "ResolveOceanZoneDebugTileRangeSignatureForProbe",
            staticFlags,
            binder: null,
            [typeof(float), typeof(float), typeof(float), typeof(float)],
            modifiers: null)!;
        MethodInfo labelScaleMethod = terrainType.GetMethod(
            "ResolveOceanZoneDebugLabelScreenScaleForProbe",
            staticFlags,
            binder: null,
            Type.EmptyTypes,
            modifiers: null)!;

        int segmentCount = (int)countDebugBordersMethod.Invoke(null, [minX, maxX, minY, maxY])!;
        object shallowWaterType = Enum.Parse(waterType, "Shallow");
        string sampleLabel = (string)formatDebugLabelMethod.Invoke(null, [shallowWaterType])!;
        bool labelPresent = !string.IsNullOrWhiteSpace(sampleLabel);
        float labelScreenScale = Convert.ToSingle(labelScaleMethod.Invoke(null, [])!);
        bool labelScaleReadable = labelScreenScale >= 0.35f && labelScreenScale <= 1.5f;
        object[] consistencyArgs = [minX, maxX, minY, maxY, 0, 0];
        bool vectorBordersMatchResolver = (bool)validateDebugBorderMethod.Invoke(null, consistencyArgs)!;
        int checkedSegments = Convert.ToInt32(consistencyArgs[4]);
        int mismatchedSegments = Convert.ToInt32(consistencyArgs[5]);
        string baseGridSignature = (string)gridSignatureMethod.Invoke(null, [minX, maxX, minY, maxY])!;
        string shiftedGridSignature = (string)gridSignatureMethod.Invoke(null, [minX + 12f, maxX + 12f, minY - 11f, maxY - 11f])!;
        bool cameraShiftKeepsGridStable = string.Equals(baseGridSignature, shiftedGridSignature, StringComparison.Ordinal);
        string baseTileRangeSignature = (string)tileRangeSignatureMethod.Invoke(null, [minX, maxX, minY, maxY])!;
        string shiftedTileRangeSignature = (string)tileRangeSignatureMethod.Invoke(null, [minX + 12f, maxX + 12f, minY - 11f, maxY - 11f])!;

        Console.WriteLine("OceanDebugBordersProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"VectorTerrainReady={vectorTerrainReady}");
        Console.WriteLine($"SegmentCount={segmentCount}");
        Console.WriteLine($"SampleLabel={sampleLabel}");
        Console.WriteLine($"LabelPresent={labelPresent}");
        Console.WriteLine($"LabelScreenScale={labelScreenScale:0.###}");
        Console.WriteLine($"LabelScaleReadable={labelScaleReadable}");
        Console.WriteLine($"CheckedSegments={checkedSegments}");
        Console.WriteLine($"MismatchedSegments={mismatchedSegments}");
        Console.WriteLine($"VectorBordersMatchResolver={vectorBordersMatchResolver}");
        Console.WriteLine($"BaseGridSignature={baseGridSignature}");
        Console.WriteLine($"ShiftedGridSignature={shiftedGridSignature}");
        Console.WriteLine($"CameraShiftKeepsGridStable={cameraShiftKeepsGridStable}");
        Console.WriteLine($"BaseTileRangeSignature={baseTileRangeSignature}");
        Console.WriteLine($"ShiftedTileRangeSignature={shiftedTileRangeSignature}");

        return vectorTerrainReady &&
            segmentCount > 0 &&
            labelPresent &&
            labelScaleReadable &&
            vectorBordersMatchResolver &&
            cameraShiftKeepsGridStable
                ? 0
                : 1;
    }

    private static int RunOceanDebugVisionProbe()
    {
        const int seed = 1337;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("OceanDebugVisionProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        player.Position = Vector2.Zero;
        Attributes_Body body = player.BodyAttributes;
        body.Sight = 45f;
        player.BodyAttributes = body;
        core.Player = player;
        core.GameObjects.Add(player);

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        PrepareTerrainForSeed(terrainType, staticFlags, seed);
        MethodInfo countVisionClipPartsMethod = terrainType.GetMethod(
            "CountOceanZoneDebugVisionClipPartsForProbe",
            staticFlags,
            binder: null,
            [typeof(Vector2), typeof(Vector2)],
            modifiers: null)!;
        Type fogType = gameplayAssembly.GetType("op.io.FogOfWarManager", throwOnError: true)!;
        PropertyInfo playerSightRadiusProperty = fogType.GetProperty("PlayerSightRadius", staticFlags)!;

        int insideClipParts = (int)countVisionClipPartsMethod.Invoke(null, [new Vector2(-80f, 0f), new Vector2(80f, 0f)])!;
        int outsideClipParts = (int)countVisionClipPartsMethod.Invoke(null, [new Vector2(1100f, 0f), new Vector2(1260f, 0f)])!;
        int crossingClipParts = (int)countVisionClipPartsMethod.Invoke(null, [new Vector2(-1200f, 0f), new Vector2(1200f, 0f)])!;
        float playerSightRadius = Convert.ToSingle(playerSightRadiusProperty.GetValue(null)!);
        bool insideRenders = insideClipParts > 0;
        bool outsideHidden = outsideClipParts == 0;
        bool crossingClipped = crossingClipParts > 0;

        Console.WriteLine("OceanDebugVisionProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"PlayerSight={body.Sight:0.###}");
        Console.WriteLine($"PlayerSightRadius={playerSightRadius:0.###}");
        Console.WriteLine($"InsideClipParts={insideClipParts}");
        Console.WriteLine($"OutsideClipParts={outsideClipParts}");
        Console.WriteLine($"CrossingClipParts={crossingClipParts}");
        Console.WriteLine($"InsideRenders={insideRenders}");
        Console.WriteLine($"OutsideHidden={outsideHidden}");
        Console.WriteLine($"CrossingClipped={crossingClipped}");

        return insideRenders &&
            outsideHidden &&
            crossingClipped
                ? 0
                : 1;
    }

    private static int RunOceanDebugRenderProbe()
    {
        using OceanDebugRenderProbeGame game = new();
        game.Run();
        Console.Write(game.Report);
        return game.ExitCode;
    }

    private static int RunTerrainStartupReadinessProbe()
    {
        const int seed = 1337;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        PrepareTerrainForSeed(terrainType, staticFlags, seed);

        MethodInfo prepareStartupAroundMethod = terrainType.GetMethod(
            "PrepareStartupTerrainAroundWorldPosition",
            staticFlags,
            binder: null,
            [typeof(Vector2)],
            modifiers: null)!;
        bool nearbyReady = (bool)prepareStartupAroundMethod.Invoke(null, [new Vector2(100f, 100f)])!;

        bool visibleReady = (bool)terrainType.GetProperty("TerrainStartupVisibleTerrainReady", staticFlags)!.GetValue(null)!;
        string readinessSummary = (string)terrainType.GetProperty("TerrainStartupReadinessSummary", staticFlags)!.GetValue(null)!;
        string startupPhase = (string)terrainType.GetProperty("TerrainStartupPhase", staticFlags)!.GetValue(null)!;

        Console.WriteLine("TerrainStartupReadinessProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"NearbyReady={nearbyReady}");
        Console.WriteLine($"VisibleReady={visibleReady}");
        Console.WriteLine($"StartupPhase={startupPhase}");
        Console.WriteLine($"ReadinessSummary={readinessSummary}");

        return nearbyReady && !visibleReady ? 0 : 1;
    }

    private static int RunTerrainBackgroundStreamingProbe()
    {
        const int seed = 1337;
        const int simulatedFrames = 180;
        const int promotionDrainFrames = 120;
        const double maxAllowedMainThreadMilliseconds = 25.0;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("TerrainBackgroundStreamingProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        player.Position = Vector2.Zero;
        core.Player = player;
        core.GameObjects.Add(player);

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        PrepareTerrainForSeed(terrainType, staticFlags, seed);
        ((IDictionary)terrainType.GetField("ResidentChunks", staticFlags)!.GetValue(null)!).Clear();
        terrainType.GetField("_terrainBackgroundQueuedChunkBuildCount", staticFlags)!.SetValue(null, 0);
        terrainType.GetMethod("ResetTerrainChunkWorkerQueues", staticFlags)!.Invoke(null, []);

        MethodInfo prepareStartupAroundMethod = terrainType.GetMethod(
            "PrepareStartupTerrainAroundWorldPosition",
            staticFlags,
            binder: null,
            [typeof(Vector2)],
            modifiers: null)!;
        bool startupReady = (bool)prepareStartupAroundMethod.Invoke(null, [Vector2.Zero])!;

        MethodInfo tryResolveWindowsMethod = terrainType.GetMethod("TryResolveTerrainStreamingWindows", staticFlags)!;
        MethodInfo applyWindowStateMethod = terrainType.GetMethod("ApplyTerrainStreamingWindowState", staticFlags)!;
        MethodInfo queueChunkBuildsMethod = terrainType.GetMethod("QueueChunkBuilds", staticFlags)!;
        MethodInfo tryPromoteCompletedChunksMethod = terrainType.GetMethod("TryPromoteCompletedChunks", staticFlags)!;
        MethodInfo pruneResidentChunksMethod = terrainType.GetMethod("PruneResidentChunks", staticFlags)!;

        Rectangle panelBounds = new(0, 0, 800, 600);
        Stopwatch stopwatch = new();
        double totalMainThreadMilliseconds = 0.0;
        double maxMainThreadMilliseconds = 0.0;
        int resolvedFrameCount = 0;

        for (int frame = 0; frame < simulatedFrames; frame++)
        {
            Core.GAMETIME = frame / 60f;
            Vector2 focus = new(frame * 18f, MathF.Sin(frame * 0.12f) * 180f);
            player.Position = focus;
            Matrix cameraTransform = Matrix.CreateTranslation(
                -focus.X + (panelBounds.Width * 0.5f),
                -focus.Y + (panelBounds.Height * 0.5f),
                0f);

            object[] resolveArgs =
            [
                cameraTransform,
                panelBounds,
                null!
            ];
            bool resolved = (bool)tryResolveWindowsMethod.Invoke(null, resolveArgs)!;
            if (!resolved)
            {
                continue;
            }

            object windows = resolveArgs[2]!;
            object visibleWindow = windows.GetType().GetProperty("VisibleChunkWindow", instanceFlags)!.GetValue(windows)!;
            object preloadWindow = windows.GetType().GetProperty("PreloadChunkWindow", instanceFlags)!.GetValue(windows)!;
            object retainWindow = windows.GetType().GetProperty("RetainChunkWindow", instanceFlags)!.GetValue(windows)!;

            stopwatch.Restart();
            applyWindowStateMethod.Invoke(null, [windows]);
            queueChunkBuildsMethod.Invoke(null, [preloadWindow, visibleWindow]);
            tryPromoteCompletedChunksMethod.Invoke(null, [retainWindow]);
            pruneResidentChunksMethod.Invoke(null, [retainWindow]);
            stopwatch.Stop();

            double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            totalMainThreadMilliseconds += elapsedMilliseconds;
            maxMainThreadMilliseconds = Math.Max(maxMainThreadMilliseconds, elapsedMilliseconds);
            resolvedFrameCount++;

            System.Threading.Thread.Sleep(4);
        }

        Vector2 finalFocus = new((simulatedFrames - 1) * 18f, MathF.Sin((simulatedFrames - 1) * 0.12f) * 180f);
        for (int i = 0; i < promotionDrainFrames; i++)
        {
            object[] resolveArgs =
            [
                Matrix.CreateTranslation(
                    -finalFocus.X + (panelBounds.Width * 0.5f),
                    -finalFocus.Y + (panelBounds.Height * 0.5f),
                    0f),
                panelBounds,
                null!
            ];
            if ((bool)tryResolveWindowsMethod.Invoke(null, resolveArgs)!)
            {
                object windows = resolveArgs[2]!;
                object retainWindow = windows.GetType().GetProperty("RetainChunkWindow", instanceFlags)!.GetValue(windows)!;
                stopwatch.Restart();
                applyWindowStateMethod.Invoke(null, [windows]);
                tryPromoteCompletedChunksMethod.Invoke(null, [retainWindow]);
                pruneResidentChunksMethod.Invoke(null, [retainWindow]);
                stopwatch.Stop();
                double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                totalMainThreadMilliseconds += elapsedMilliseconds;
                maxMainThreadMilliseconds = Math.Max(maxMainThreadMilliseconds, elapsedMilliseconds);
                resolvedFrameCount++;
            }

            System.Threading.Thread.Sleep(4);
        }

        int residentChunkCount = (int)terrainType.GetProperty("TerrainResidentChunkCount", staticFlags)!.GetValue(null)!;
        int residentChunkMemoryCap = (int)terrainType.GetProperty("TerrainResidentChunkMemoryCap", staticFlags)!.GetValue(null)!;
        int pendingChunkCount = (int)terrainType.GetProperty("TerrainPendingChunkCount", staticFlags)!.GetValue(null)!;
        int queuedChunkCount = (int)terrainType.GetProperty("TerrainBackgroundQueuedChunkCount", staticFlags)!.GetValue(null)!;
        int activeChunkCount = (int)terrainType.GetProperty("TerrainBackgroundActiveChunkBuildCount", staticFlags)!.GetValue(null)!;
        int completedChunkCount = (int)terrainType.GetProperty("TerrainBackgroundCompletedChunkQueueCount", staticFlags)!.GetValue(null)!;
        int queuedChunkBuilds = (int)terrainType.GetProperty("TerrainBackgroundQueuedChunkBuildCount", staticFlags)!.GetValue(null)!;
        string workerStatus = (string)terrainType.GetProperty("TerrainBackgroundWorkerStatus", staticFlags)!.GetValue(null)!;
        double averageMainThreadMilliseconds = resolvedFrameCount > 0
            ? totalMainThreadMilliseconds / resolvedFrameCount
            : double.PositiveInfinity;
        bool mainThreadBounded = maxMainThreadMilliseconds <= maxAllowedMainThreadMilliseconds;
        bool memoryCapRespected = residentChunkCount <= residentChunkMemoryCap;
        bool workerStreamedChunks = queuedChunkBuilds > 0 && residentChunkCount > 1;

        Console.WriteLine("TerrainBackgroundStreamingProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"StartupReady={startupReady}");
        Console.WriteLine($"Frames={resolvedFrameCount}");
        Console.WriteLine($"MaxMainThreadMs={maxMainThreadMilliseconds:0.###}");
        Console.WriteLine($"AverageMainThreadMs={averageMainThreadMilliseconds:0.###}");
        Console.WriteLine($"MainThreadBounded={mainThreadBounded}");
        Console.WriteLine($"ResidentChunks={residentChunkCount}");
        Console.WriteLine($"ResidentChunkMemoryCap={residentChunkMemoryCap}");
        Console.WriteLine($"MemoryCapRespected={memoryCapRespected}");
        Console.WriteLine($"PendingChunks={pendingChunkCount}");
        Console.WriteLine($"QueuedChunks={queuedChunkCount}");
        Console.WriteLine($"ActiveChunks={activeChunkCount}");
        Console.WriteLine($"CompletedChunks={completedChunkCount}");
        Console.WriteLine($"QueuedChunkBuilds={queuedChunkBuilds}");
        Console.WriteLine($"WorkerStatus={workerStatus}");

        return startupReady &&
            resolvedFrameCount == simulatedFrames + promotionDrainFrames &&
            mainThreadBounded &&
            memoryCapRespected &&
            workerStreamedChunks
                ? 0
                : 1;
    }

    private static int RunTerrainFullMapOceanBordersProbe()
    {
        const int seed = 1337;
        const double maxAllowedMainThreadMilliseconds = 25.0;
        const double maxAllowedTotalMilliseconds = 120000.0;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("TerrainFullMapOceanBordersProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        player.Position = Vector2.Zero;
        core.Player = player;
        core.GameObjects.Add(player);

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        PrepareTerrainForSeed(terrainType, staticFlags, seed);
        terrainType.GetMethod("ResetRuntimeTerrainObjectsForLevelLoad", staticFlags)!.Invoke(null, []);
        ((IDictionary)terrainType.GetField("ResidentChunks", staticFlags)!.GetValue(null)!).Clear();
        terrainType.GetMethod("ResetTerrainChunkWorkerQueues", staticFlags)!.Invoke(null, []);

        MethodInfo prepareStartupAroundMethod = terrainType.GetMethod(
            "PrepareStartupTerrainAroundWorldPosition",
            staticFlags,
            binder: null,
            [typeof(Vector2)],
            modifiers: null)!;
        bool startupReady = (bool)prepareStartupAroundMethod.Invoke(null, [Vector2.Zero])!;

        MethodInfo resolveFullMapWindowMethod = terrainType.GetMethod("ResolveFullTerrainMapChunkWindow", staticFlags)!;
        MethodInfo queueFullMapBuildsMethod = terrainType.GetMethod("QueueFullTerrainMapBuilds", staticFlags)!;
        MethodInfo tryPromoteCompletedChunksMethod = terrainType.GetMethod("TryPromoteCompletedChunks", staticFlags)!;
        MethodInfo updateFullMapStateMethod = terrainType.GetMethod("UpdateFullTerrainMapGenerationState", staticFlags)!;
        MethodInfo tryApplyFullOceanDebugBuildMethod = terrainType.GetMethod("TryApplyCompletedFullOceanZoneDebugBuild", staticFlags)!;
        MethodInfo validateStableZoneRadiusMethod = terrainType.GetMethod(
            "ValidateOceanZoneDebugMinimumStableZoneRadiusForProbe",
            staticFlags,
            binder: null,
            [
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType()
            ],
            modifiers: null)!;

        object fullMapWindow = resolveFullMapWindowMethod.Invoke(null, [])!;
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        Stopwatch frameStopwatch = new();
        double maxMainThreadMilliseconds = 0.0;
        int frames = 0;
        bool fullMapComplete = false;
        bool snapshotReady = false;
        bool fullOceanReady = false;

        while (totalStopwatch.Elapsed.TotalMilliseconds < maxAllowedTotalMilliseconds)
        {
            frameStopwatch.Restart();
            queueFullMapBuildsMethod.Invoke(null, [fullMapWindow]);
            tryPromoteCompletedChunksMethod.Invoke(null, [fullMapWindow]);
            updateFullMapStateMethod.Invoke(null, []);
            tryApplyFullOceanDebugBuildMethod.Invoke(null, []);
            frameStopwatch.Stop();
            maxMainThreadMilliseconds = Math.Max(maxMainThreadMilliseconds, frameStopwatch.Elapsed.TotalMilliseconds);
            frames++;

            fullMapComplete = (bool)terrainType.GetProperty("TerrainFullMapGenerationComplete", staticFlags)!.GetValue(null)!;
            snapshotReady = (bool)terrainType.GetProperty("TerrainFullMapSnapshotReady", staticFlags)!.GetValue(null)!;
            fullOceanReady = (bool)terrainType.GetProperty("TerrainOceanDebugFullMapReady", staticFlags)!.GetValue(null)!;
            if (fullMapComplete && snapshotReady && fullOceanReady)
            {
                break;
            }

            System.Threading.Thread.Sleep(2);
        }

        totalStopwatch.Stop();

        int fullMapChunks = (int)terrainType.GetProperty("TerrainFullMapChunkCount", staticFlags)!.GetValue(null)!;
        int generatedChunks = (int)terrainType.GetProperty("TerrainFullMapGeneratedChunkCount", staticFlags)!.GetValue(null)!;
        int pendingChunks = (int)terrainType.GetProperty("TerrainFullMapPendingChunkCount", staticFlags)!.GetValue(null)!;
        int residentChunks = (int)terrainType.GetProperty("TerrainResidentChunkCount", staticFlags)!.GetValue(null)!;
        int fullMapSegments = (int)terrainType.GetProperty("TerrainOceanDebugFullMapSegmentCount", staticFlags)!.GetValue(null)!;
        int suppressedTinyZoneCount = (int)terrainType.GetProperty("TerrainOceanDebugSuppressedTinyZoneCount", staticFlags)!.GetValue(null)!;
        float minimumStableZoneRadius = (float)terrainType.GetProperty("TerrainOceanDebugMinimumStableZoneRadius", staticFlags)!.GetValue(null)!;
        double fullMapBuildMs = (double)terrainType.GetProperty("TerrainOceanDebugFullMapBuildMilliseconds", staticFlags)!.GetValue(null)!;
        string fullMapWindowSummary = (string)terrainType.GetProperty("TerrainFullMapChunkWindow", staticFlags)!.GetValue(null)!;
        string status = (string)terrainType.GetProperty("TerrainOceanDebugFullMapStatus", staticFlags)!.GetValue(null)!;
        string tinyZoneViolationSummary = (string)terrainType.GetProperty("TerrainOceanDebugTinyZoneViolationSummary", staticFlags)!.GetValue(null)!;
        object[] stableZoneArgs = [-4096f, 4096f, -4096f, 4096f, 256f, 0, 0];
        bool stableZoneRadiusValid = (bool)validateStableZoneRadiusMethod.Invoke(null, stableZoneArgs)!;
        int probeSuppressedComponents = Convert.ToInt32(stableZoneArgs[5]);
        int remainingTinyComponents = Convert.ToInt32(stableZoneArgs[6]);
        tinyZoneViolationSummary = (string)terrainType.GetProperty("TerrainOceanDebugTinyZoneViolationSummary", staticFlags)!.GetValue(null)!;
        bool mainThreadBounded = maxMainThreadMilliseconds <= maxAllowedMainThreadMilliseconds;
        bool completedInOrder = fullMapComplete && snapshotReady && fullOceanReady;
        bool residentFullMapRemembered = fullMapChunks > 0 && residentChunks >= fullMapChunks && generatedChunks >= fullMapChunks;
        bool fullMapBordersBuilt = fullMapSegments > 0;

        Console.WriteLine("TerrainFullMapOceanBordersProbe");
        Console.WriteLine($"Seed={seed}");
        Console.WriteLine($"StartupReady={startupReady}");
        Console.WriteLine($"Frames={frames}");
        Console.WriteLine($"ElapsedMs={totalStopwatch.Elapsed.TotalMilliseconds:0.###}");
        Console.WriteLine($"MaxMainThreadMs={maxMainThreadMilliseconds:0.###}");
        Console.WriteLine($"MainThreadBounded={mainThreadBounded}");
        Console.WriteLine($"FullMapWindow={fullMapWindowSummary}");
        Console.WriteLine($"FullMapChunks={fullMapChunks}");
        Console.WriteLine($"GeneratedChunks={generatedChunks}");
        Console.WriteLine($"PendingChunks={pendingChunks}");
        Console.WriteLine($"ResidentChunks={residentChunks}");
        Console.WriteLine($"FullMapComplete={fullMapComplete}");
        Console.WriteLine($"SnapshotReady={snapshotReady}");
        Console.WriteLine($"FullOceanReady={fullOceanReady}");
        Console.WriteLine($"FullOceanSegments={fullMapSegments}");
        Console.WriteLine($"FullOceanBuildMs={fullMapBuildMs:0.###}");
        Console.WriteLine($"SuppressedTinyZoneCount={suppressedTinyZoneCount}");
        Console.WriteLine($"MinimumStableZoneRadius={minimumStableZoneRadius:0.###}");
        Console.WriteLine($"StableZoneRadiusValid={stableZoneRadiusValid}");
        Console.WriteLine($"ProbeSuppressedComponents={probeSuppressedComponents}");
        Console.WriteLine($"RemainingTinyComponents={remainingTinyComponents}");
        Console.WriteLine($"TinyZoneViolationSummary={tinyZoneViolationSummary}");
        Console.WriteLine($"Status={status}");

        return startupReady &&
            completedInOrder &&
            residentFullMapRemembered &&
            fullMapBordersBuilt &&
            mainThreadBounded &&
            stableZoneRadiusValid
                ? 0
                : 1;
    }

    private static int RunLevelStartupProbe()
    {
        const double MaxStartupMilliseconds = 2000.0;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();
        core.ViewportWidth = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
        core.ViewportHeight = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

        Stopwatch stopwatch = Stopwatch.StartNew();
        GameObjectInitializer.Initialize();
        stopwatch.Stop();

        Agent? player = core.PlayerOrNull;
        Agent? scout = core.GameObjects.OfType<Agent>()
            .FirstOrDefault(static agent => string.Equals(agent.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
        Agent? registeredScout = GameObjectRegister.GetRegisteredGameObjects().OfType<Agent>()
            .FirstOrDefault(static agent => string.Equals(agent.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
        GameObject? redWall = core.GameObjects
            .FirstOrDefault(static gameObject => string.Equals(gameObject.Name, "RedWall", StringComparison.OrdinalIgnoreCase));
        int agentCount = core.GameObjects.Count(static go => go is Agent);
        int farmCount = core.GameObjects.Count(static go => go.IsFarmObject);
        int nonAgentNonFarmCount = core.GameObjects.Count(static go => go is not Agent && !go.IsFarmObject);
        float playerClearance = player == null ? 0f : MathF.Max(12f, player.BoundingRadius + 8f);
        bool playerTerrainFree = player != null && IsTerrainFreeForProbe(player.Position, playerClearance);
        bool naturalLevelLoaded = string.Equals(GameTracker.GameLevelActiveKey, "natural", StringComparison.OrdinalIgnoreCase);
        bool naturalSceneLoadoutCorrect = !naturalLevelLoaded ||
            (agentCount == 1 && scout == null && registeredScout == null && redWall == null && farmCount == 0 && nonAgentNonFarmCount == 0);
        bool warmedNearSpawn = GameTracker.TerrainStartupWarmupChunkCount >= 1 &&
            GameTracker.TerrainResidentChunkCount >= GameTracker.TerrainStartupWarmupChunkCount &&
            GameTracker.TerrainPendingCriticalChunkCount == 0;
        bool startupFastEnough = stopwatch.Elapsed.TotalMilliseconds <= MaxStartupMilliseconds;

        Console.WriteLine("LevelStartupProbe");
        Console.WriteLine($"ElapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.###}");
        Console.WriteLine($"ActiveLevel={GameTracker.GameLevelActiveKey}");
        Console.WriteLine($"PlayerPresent={player != null}");
        Console.WriteLine($"PlayerPosition={player?.Position.ToString() ?? "<none>"}");
        Console.WriteLine($"ScoutPresent={scout != null}");
        Console.WriteLine($"RegisteredScoutPresent={registeredScout != null}");
        Console.WriteLine($"ScoutDynamic={scout?.DynamicPhysics.ToString() ?? "<none>"}");
        Console.WriteLine($"ScoutCollidable={scout?.IsCollidable.ToString() ?? "<none>"}");
        Console.WriteLine($"RedWallPresent={redWall != null}");
        Console.WriteLine($"Agents={agentCount}");
        Console.WriteLine($"Farms={farmCount}");
        Console.WriteLine($"NonAgentNonFarmObjects={nonAgentNonFarmCount}");
        Console.WriteLine($"TerrainWarmupChunks={GameTracker.TerrainStartupWarmupChunkCount}");
        Console.WriteLine($"TerrainResidentChunks={GameTracker.TerrainResidentChunkCount}");
        Console.WriteLine($"TerrainPendingCritical={GameTracker.TerrainPendingCriticalChunkCount}");
        Console.WriteLine($"TerrainSynchronousBuilds={GameTracker.TerrainStartupSynchronousChunkBuildCount}");
        Console.WriteLine($"PlayerTerrainFree={playerTerrainFree}");
        Console.WriteLine($"PlayerSpawnRelocated={GameTracker.GameLevelPlayerSpawnRelocated}");
        Console.WriteLine($"PlayerSpawnRelocationDistance={GameTracker.GameLevelPlayerSpawnRelocationDistance:0.###}");
        Console.WriteLine($"PlayerSpawnSearchAttempts={GameTracker.GameLevelPlayerSpawnSearchAttempts}");
        Console.WriteLine($"StartupFastEnough={startupFastEnough}");
        Console.WriteLine($"WarmedNearSpawn={warmedNearSpawn}");
        Console.WriteLine($"NaturalSceneLoadoutCorrect={naturalSceneLoadoutCorrect}");

        return player != null &&
            playerTerrainFree &&
            warmedNearSpawn &&
            startupFastEnough &&
            naturalSceneLoadoutCorrect
                ? 0
                : 1;
    }

    private static int RunTerrainLevelScoutProbe()
    {
        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();
        core.ViewportWidth = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
        core.ViewportHeight = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

        Type levelManagerType = typeof(Core).Assembly.GetType("op.io.GameLevelManager", throwOnError: true)!;
        MethodInfo tryLoadLevelMethod = levelManagerType.GetMethod(
            "TryLoadLevel",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(bool)],
            modifiers: null)!;

        bool naturalLoadRequested = (bool)tryLoadLevelMethod.Invoke(null, ["natural", true])!;
        Agent? naturalPlayer = core.PlayerOrNull;
        Agent? naturalScout = core.GameObjects.OfType<Agent>()
            .FirstOrDefault(static agent => string.Equals(agent.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
        Agent? naturalRegisteredScout = GameObjectRegister.GetRegisteredGameObjects().OfType<Agent>()
            .FirstOrDefault(static agent => string.Equals(agent.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
        GameObject? naturalRedWall = core.GameObjects
            .FirstOrDefault(static gameObject => string.Equals(gameObject.Name, "RedWall", StringComparison.OrdinalIgnoreCase));
        int naturalAgentCount = core.GameObjects.Count(static go => go is Agent);
        int naturalFarmCount = core.GameObjects.Count(static go => go.IsFarmObject);
        int naturalStaticSceneObjectCount = core.GameObjects.Count(static go => go is not Agent && !go.IsFarmObject);

        bool manualLoadRequested = (bool)tryLoadLevelMethod.Invoke(null, ["manual", true])!;
        Agent? manualPlayer = core.PlayerOrNull;
        Agent? manualScout = core.GameObjects.OfType<Agent>()
            .FirstOrDefault(static agent => string.Equals(agent.Name, "ScoutSentry1", StringComparison.OrdinalIgnoreCase));
        GameObject? manualRedWall = core.GameObjects
            .FirstOrDefault(static gameObject => string.Equals(gameObject.Name, "RedWall", StringComparison.OrdinalIgnoreCase));
        int manualAgentCount = core.GameObjects.Count(static go => go is Agent);

        bool contactObserved = false;
        bool responseObserved = false;
        float maxPhysicsVelocity = 0f;
        float maxDisplacement = 0f;

        if (manualPlayer != null && manualScout != null)
        {
            Core.Instance.GameObjects = core.GameObjects
                .Where(static go => go is Agent || string.Equals(go.Name, "RedWall", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Core.Instance.StaticObjects = Core.Instance.GameObjects.Where(static go => !go.DynamicPhysics).ToList();
            Core.Instance.Player = manualPlayer;

            manualScout.Position = new Vector2(420f, 100f);
            manualScout.PreviousPosition = manualScout.Position;
            manualScout.PhysicsVelocity = Vector2.Zero;
            manualScout.MovementVelocity = Vector2.Zero;

            manualPlayer.Position = manualScout.Position + new Vector2(-220f, 0f);
            manualPlayer.PreviousPosition = manualPlayer.Position;
            manualPlayer.PhysicsVelocity = Vector2.Zero;
            manualPlayer.MovementVelocity = Vector2.Zero;
            manualPlayer.Rotation = 0f;

            Vector2 startScoutPosition = manualScout.Position;
            BulletManager.SpawnBullet(manualPlayer);

            for (int frame = 0; frame < 240; frame++)
            {
                StepFrame();

                float displacement = Vector2.Distance(manualScout.Position, startScoutPosition);
                float physicsSpeed = manualScout.PhysicsVelocity.Length();
                maxPhysicsVelocity = MathF.Max(maxPhysicsVelocity, physicsSpeed);
                maxDisplacement = MathF.Max(maxDisplacement, displacement);

                IReadOnlyList<Bullet> bullets = BulletManager.GetBullets();
                Bullet? bullet = bullets.Count > 0 ? bullets[0] : null;
                if (bullet != null &&
                    Vector2.Distance(bullet.Position, manualScout.Position) <= ((bullet.Shape.Width + manualScout.Shape.Width) * 0.5f + 2f))
                {
                    contactObserved = true;
                }

                if (physicsSpeed > 0.01f || displacement > 0.01f)
                {
                    responseObserved = true;
                }
            }
        }

        bool naturalLoadoutValid =
            naturalLoadRequested &&
            naturalPlayer != null &&
            naturalScout == null &&
            naturalRegisteredScout == null &&
            naturalRedWall == null &&
            naturalAgentCount == 1 &&
            naturalFarmCount == 0 &&
            naturalStaticSceneObjectCount == 0;
        bool manualScoutLoadoutValid =
            manualLoadRequested &&
            manualPlayer != null &&
            manualScout != null &&
            manualRedWall != null &&
            manualScout.DynamicPhysics &&
            manualScout.IsCollidable &&
            manualScout.Mass > 0f &&
            manualScout.BodyAttributes.Mass > 0f &&
            manualAgentCount >= 2;
        bool scoutPhysicsValid = contactObserved && responseObserved && maxPhysicsVelocity > 0.01f && maxDisplacement > 0.01f;

        Console.WriteLine("TerrainLevelScoutProbe");
        Console.WriteLine($"NaturalLoadRequested={naturalLoadRequested}");
        Console.WriteLine($"NaturalPlayerPresent={naturalPlayer != null}");
        Console.WriteLine($"NaturalScoutPresent={naturalScout != null}");
        Console.WriteLine($"NaturalRegisteredScoutPresent={naturalRegisteredScout != null}");
        Console.WriteLine($"NaturalRedWallPresent={naturalRedWall != null}");
        Console.WriteLine($"NaturalAgentCount={naturalAgentCount}");
        Console.WriteLine($"NaturalFarmCount={naturalFarmCount}");
        Console.WriteLine($"NaturalStaticSceneObjectCount={naturalStaticSceneObjectCount}");
        Console.WriteLine($"ManualLoadRequested={manualLoadRequested}");
        Console.WriteLine($"ActiveLevel={GameTracker.GameLevelActiveKey}");
        Console.WriteLine($"ManualPlayerPresent={manualPlayer != null}");
        Console.WriteLine($"ManualScoutPresent={manualScout != null}");
        Console.WriteLine($"ManualRedWallPresent={manualRedWall != null}");
        Console.WriteLine($"ManualAgentCount={manualAgentCount}");
        Console.WriteLine($"ScoutDynamic={manualScout?.DynamicPhysics.ToString() ?? "<none>"}");
        Console.WriteLine($"ScoutCollidable={manualScout?.IsCollidable.ToString() ?? "<none>"}");
        Console.WriteLine($"ScoutMass={manualScout?.Mass.ToString("0.###") ?? "<none>"}");
        Console.WriteLine($"ScoutBodyMass={manualScout?.BodyAttributes.Mass.ToString("0.###") ?? "<none>"}");
        Console.WriteLine($"ContactObserved={contactObserved}");
        Console.WriteLine($"ResponseObserved={responseObserved}");
        Console.WriteLine($"MaxPhysicsVelocity={maxPhysicsVelocity:0.###}");
        Console.WriteLine($"MaxDisplacement={maxDisplacement:0.###}");
        Console.WriteLine($"NaturalLoadoutValid={naturalLoadoutValid}");
        Console.WriteLine($"ManualScoutLoadoutValid={manualScoutLoadoutValid}");
        Console.WriteLine($"ScoutPhysicsValid={scoutPhysicsValid}");

        return naturalLoadoutValid && manualScoutLoadoutValid && scoutPhysicsValid ? 0 : 1;
    }

    private static int RunPlayerSpawnRelocationProbe()
    {
        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("PlayerSpawnRelocationProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        Vector2 requestedPosition = player.Position;
        GameObject blocker = CreateSpawnBlocker(player, requestedPosition);
        core.GameObjects.Add(blocker);
        core.StaticObjects.Add(blocker);

        MethodInfo prepareSpawnMethod = typeof(GameObjectInitializer).GetMethod(
            "PreparePlayerTerrainAndSpawnPosition",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        prepareSpawnMethod.Invoke(null, [player, new List<Agent> { player }]);

        float relocationDistance = Vector2.Distance(requestedPosition, player.Position);
        float playerClearance = MathF.Max(12f, player.BoundingRadius + 8f);
        bool noBlockerOverlap = !CollisionManager.CheckCollision(player, blocker);
        bool terrainFree = IsTerrainFreeForProbe(player.Position, playerClearance);
        bool telemetryMatches = GameObjectInitializer.PlayerSpawnRelocated &&
            GameObjectInitializer.PlayerSpawnRelocationDistance > 0.5f &&
            GameObjectInitializer.PlayerSpawnSearchAttempts > 0;

        Console.WriteLine("PlayerSpawnRelocationProbe");
        Console.WriteLine($"RequestedPosition={requestedPosition}");
        Console.WriteLine($"ResolvedPosition={player.Position}");
        Console.WriteLine($"RelocationDistance={relocationDistance:0.###}");
        Console.WriteLine($"NoBlockerOverlap={noBlockerOverlap}");
        Console.WriteLine($"TerrainFree={terrainFree}");
        Console.WriteLine($"TelemetryRelocated={GameObjectInitializer.PlayerSpawnRelocated}");
        Console.WriteLine($"TelemetryDistance={GameObjectInitializer.PlayerSpawnRelocationDistance:0.###}");
        Console.WriteLine($"TelemetryAttempts={GameObjectInitializer.PlayerSpawnSearchAttempts}");
        Console.WriteLine($"TerrainWarmupChunks={GameTracker.TerrainStartupWarmupChunkCount}");
        Console.WriteLine($"TerrainResidentChunks={GameTracker.TerrainResidentChunkCount}");

        return relocationDistance > 0.5f &&
            noBlockerOverlap &&
            terrainFree &&
            telemetryMatches &&
            GameTracker.TerrainStartupWarmupChunkCount >= 1
                ? 0
                : 1;
    }

    private readonly record struct OceanZoneSample(
        Vector2 Position,
        string ZoneName,
        int ZoneIndex,
        float Depth,
        float OffshoreDistance);

    private static void PrepareTerrainForSeed(Type terrainType, BindingFlags staticFlags, int seed)
    {
        MethodInfo resolveSeedAnchorMethod = terrainType.GetMethod("ResolveSeedAnchorCentifoot", staticFlags)!;
        FieldInfo terrainSeedField = terrainType.GetField("_terrainWorldSeed", staticFlags)!;
        FieldInfo terrainAnchorField = terrainType.GetField("_terrainSeedAnchorCentifoot", staticFlags)!;
        FieldInfo settingsLoadedField = terrainType.GetField("_settingsLoaded", staticFlags)!;
        FieldInfo? terrainWorldBoundsInitializedField = terrainType.GetField("_terrainWorldBoundsInitialized", staticFlags);
        FieldInfo? oceanZoneCacheValidField = terrainType.GetField("_oceanZoneCacheValid", staticFlags);

        terrainSeedField.SetValue(null, seed);
        terrainAnchorField.SetValue(null, resolveSeedAnchorMethod.Invoke(null, [seed])!);
        settingsLoadedField.SetValue(null, true);
        terrainWorldBoundsInitializedField?.SetValue(null, false);
        oceanZoneCacheValidField?.SetValue(null, false);
    }

    private static float GetDefaultWaterDistance(Type defaultsType, BindingFlags staticFlags, string fieldName)
    {
        FieldInfo field = defaultsType.GetField(fieldName, staticFlags)!;
        return Convert.ToSingle(field.GetRawConstantValue() ?? field.GetValue(null));
    }

    private static float GetStaticFloatProperty(Type type, BindingFlags staticFlags, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, staticFlags)!;
        return Convert.ToSingle(property.GetValue(null)!);
    }

    private static bool SampleTerrainMask(MethodInfo sampleMaskMethod, Vector2 position)
    {
        return Convert.ToByte(sampleMaskMethod.Invoke(null, [position.X, position.Y])!) != 0;
    }

    private static bool TryProbeOceanZone(MethodInfo resolveOceanZoneMethod, Vector2 position, out OceanZoneSample sample)
    {
        object[] args = [position, null!, 0f, 0f];
        bool resolved = (bool)resolveOceanZoneMethod.Invoke(null, args)!;
        if (!resolved)
        {
            sample = default;
            return false;
        }

        object waterType = args[1]!;
        sample = new OceanZoneSample(
            position,
            waterType.ToString() ?? string.Empty,
            Convert.ToInt32(waterType),
            Convert.ToSingle(args[2]),
            Convert.ToSingle(args[3]));
        return true;
    }

    private static Vector2[] BuildProbeDirections(int count)
    {
        int directionCount = Math.Max(4, count);
        Vector2[] directions = new Vector2[directionCount];
        for (int i = 0; i < directionCount; i++)
        {
            float angle = (i / (float)directionCount) * MathF.Tau;
            directions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return directions;
    }

    private static int ResolveExpectedOceanZoneIndex(
        float offshoreDistance,
        float shallowDistance,
        float sunlitDistance,
        float twilightDistance,
        float midnightDistance)
    {
        if (offshoreDistance <= shallowDistance)
        {
            return 0;
        }

        if (offshoreDistance <= sunlitDistance)
        {
            return 1;
        }

        if (offshoreDistance <= twilightDistance)
        {
            return 2;
        }

        return offshoreDistance <= midnightDistance ? 3 : 4;
    }

    private static string FormatZonesSeen(bool[] zonesSeen)
    {
        string[] names = ["Shallow", "Sunlit", "Twilight", "Midnight", "Abyss"];
        List<string> zones = [];
        for (int i = 0; i < Math.Min(zonesSeen.Length, names.Length); i++)
        {
            if (zonesSeen[i])
            {
                zones.Add(names[i]);
            }
        }

        return zones.Count == 0 ? "<none>" : string.Join(",", zones);
    }

    private static int RunSpawnProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();
        core.ViewportWidth = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
        core.ViewportHeight = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

        GameObjectInitializer.Initialize();

        int farmCount = core.GameObjects.Count(static go => go.IsFarmObject);
        int agentCount = core.GameObjects.Count(static go => go is Agent);
        int dynamicCount = core.GameObjects.Count(static go => go.DynamicPhysics);
        Agent? player = core.Player;
        float halfWidth = core.ViewportWidth * 0.5f;
        float halfHeight = core.ViewportHeight * 0.5f;
        int visibleApproxCount = player == null
            ? 0
            : core.GameObjects.Count(go =>
                go.Shape != null &&
                MathF.Abs(go.Position.X - player.Position.X) <= halfWidth &&
                MathF.Abs(go.Position.Y - player.Position.Y) <= halfHeight);

        Console.WriteLine($"GameObjects={core.GameObjects.Count}");
        Console.WriteLine($"StaticObjects={core.StaticObjects.Count}");
        Console.WriteLine($"Agents={agentCount}");
        Console.WriteLine($"Farms={farmCount}");
        Console.WriteLine($"Dynamic={dynamicCount}");
        Console.WriteLine($"ApproxVisibleAtSpawn={visibleApproxCount}");
        Console.WriteLine($"TerrainSpawnRelocationCount={GameTracker.TerrainSpawnRelocationCount}");
        Console.WriteLine($"PhysicsStartupOverlapResolvedPairCount={GameTracker.PhysicsStartupOverlapResolvedPairCount}");
        Console.WriteLine($"PhysicsStartupOverlapIterationCount={GameTracker.PhysicsStartupOverlapIterationCount}");

        foreach (GameObject gameObject in core.GameObjects
            .Where(static go => go.DynamicPhysics)
            .OrderBy(static go => go.ID)
            .Take(8))
        {
            Console.WriteLine($"  dynamic id={gameObject.ID} name={gameObject.Name} pos={gameObject.Position}");
        }

        return 0;
    }

    private static int RunTerrainMetricsProbe()
    {
        const int seed = 1337;

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        MethodInfo resolveSeedAnchorMethod = terrainType.GetMethod("ResolveSeedAnchorCentifoot", staticFlags)!;
        MethodInfo buildChunkMethod = terrainType.GetMethod("BuildChunkData", staticFlags)!;
        MethodInfo collectConnectedComponentMethod = terrainType.GetMethod("CollectConnectedComponent", staticFlags)!;
        MethodInfo buildRefinedTerrainComponentMethod = terrainType.GetMethod("BuildRefinedTerrainComponent", staticFlags)!;
        MethodInfo buildTerrainLoopsMethod = terrainType.GetMethod("BuildTerrainLoops", staticFlags)!;
        MethodInfo simplifyLoopMethod = terrainType.GetMethod("SimplifyLoop", staticFlags)!;
        MethodInfo buildChunkBoundsMethod = terrainType.GetMethod("BuildChunkBounds", staticFlags)!;
        MethodInfo indexMethod = terrainType.GetMethod("Index", staticFlags)!;

        FieldInfo terrainSeedField = terrainType.GetField("_terrainWorldSeed", staticFlags)!;
        FieldInfo terrainAnchorField = terrainType.GetField("_terrainSeedAnchorCentifoot", staticFlags)!;
        FieldInfo settingsLoadedField = terrainType.GetField("_settingsLoaded", staticFlags)!;
        FieldInfo terrainContourResolutionMultiplierField = terrainType.GetField("TerrainContourResolutionMultiplier", staticFlags)!;
        FieldInfo chunkTextureResolutionField = terrainType.GetField("ChunkTextureResolution", staticFlags)!;
        FieldInfo residentChunksField = terrainType.GetField("ResidentChunks", staticFlags)!;
        Type fogType = gameplayAssembly.GetType("op.io.FogOfWarManager", throwOnError: true)!;
        FieldInfo? playerSightRadiusField = fogType.GetField("_cachedPlayerSightRadius", staticFlags);

        Type chunkKeyType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkKey", throwOnError: true)!;
        Type generatedChunkType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+GeneratedChunkData", throwOnError: true)!;
        Type terrainChunkRecordType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainChunkRecord", throwOnError: true)!;
        Type terrainComponentBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainComponentBounds", throwOnError: true)!;
        Type chunkBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkBounds", throwOnError: true)!;
        Type refinedTerrainComponentType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+RefinedTerrainComponent", throwOnError: true)!;
        Type terrainLoopType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainLoop", throwOnError: true)!;

        ConstructorInfo chunkKeyConstructor = chunkKeyType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int)], modifiers: null)!;
        ConstructorInfo terrainChunkRecordConstructor = terrainChunkRecordType.GetConstructor(instanceFlags, binder: null, [typeof(byte[]), typeof(bool)], modifiers: null)!;
        ConstructorInfo terrainComponentBoundsConstructor = terrainComponentBoundsType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int), typeof(int), typeof(int)], modifiers: null)!;

        PropertyInfo hasLandProperty = generatedChunkType.GetProperty("HasLand", instanceFlags)!;
        PropertyInfo landMaskProperty = generatedChunkType.GetProperty("LandMask", instanceFlags)!;

        terrainSeedField.SetValue(null, seed);
        object anchor = resolveSeedAnchorMethod.Invoke(null, [seed])!;
        terrainAnchorField.SetValue(null, anchor);
        settingsLoadedField.SetValue(null, true);
        if (playerSightRadiusField != null)
        {
            playerSightRadiusField.SetValue(null, 0f);
        }

        float minX = -960f;
        float maxX = 960f;
        float minY = -540f;
        float maxY = 540f;
        const float materializationMargin = 256f;
        object materializedBounds = buildChunkBoundsMethod.Invoke(
            null,
            [minX - materializationMargin, maxX + materializationMargin, minY - materializationMargin, maxY + materializationMargin])!;

        int minChunkX = (int)chunkBoundsType.GetProperty("MinChunkX", instanceFlags)!.GetValue(materializedBounds)!;
        int maxChunkX = (int)chunkBoundsType.GetProperty("MaxChunkX", instanceFlags)!.GetValue(materializedBounds)!;
        int minChunkY = (int)chunkBoundsType.GetProperty("MinChunkY", instanceFlags)!.GetValue(materializedBounds)!;
        int maxChunkY = (int)chunkBoundsType.GetProperty("MaxChunkY", instanceFlags)!.GetValue(materializedBounds)!;

        object residentChunks = residentChunksField.GetValue(null)!;
        MethodInfo residentChunksClearMethod = residentChunks.GetType().GetMethod("Clear", instanceFlags)!;
        MethodInfo residentChunksAddMethod = residentChunks.GetType().GetMethod("Add", instanceFlags)!;
        residentChunksClearMethod.Invoke(residentChunks, null);

        Console.WriteLine($"TerrainMetrics seed={seed}");
        Console.WriteLine($"MaterializedChunkWindow=({minChunkX}..{maxChunkX}, {minChunkY}..{maxChunkY})");

        bool foundLandChunk = false;
        int landChunkCount = 0;
        for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
        {
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                object key = chunkKeyConstructor.Invoke([chunkX, chunkY]);
                object generatedChunk = buildChunkMethod.Invoke(null, [key])!;
                bool hasLand = (bool)hasLandProperty.GetValue(generatedChunk)!;
                byte[] landMask = (byte[])landMaskProperty.GetValue(generatedChunk)!;
                if (!hasLand)
                {
                    continue;
                }

                object residentRecord = terrainChunkRecordConstructor.Invoke([landMask, hasLand]);
                residentChunksAddMethod.Invoke(residentChunks, [key, residentRecord]);
                foundLandChunk = true;
                landChunkCount++;
            }
        }

        Console.WriteLine($"ResidentLandChunks={landChunkCount}");
        if (!foundLandChunk)
        {
            return 0;
        }

        int chunkTextureResolution = (int)chunkTextureResolutionField.GetRawConstantValue()!;
        int maskWidth = ((maxChunkX - minChunkX) + 1) * chunkTextureResolution;
        int maskHeight = ((maxChunkY - minChunkY) + 1) * chunkTextureResolution;
        byte[] combinedMask = new byte[maskWidth * maskHeight];

        for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
        {
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                object key = chunkKeyConstructor.Invoke([chunkX, chunkY]);
                object generatedChunk = buildChunkMethod.Invoke(null, [key])!;
                bool hasLand = (bool)hasLandProperty.GetValue(generatedChunk)!;
                if (!hasLand)
                {
                    continue;
                }

                byte[] landMask = (byte[])landMaskProperty.GetValue(generatedChunk)!;
                int offsetX = (chunkX - minChunkX) * chunkTextureResolution;
                int offsetY = (chunkY - minChunkY) * chunkTextureResolution;
                for (int row = 0; row < chunkTextureResolution; row++)
                {
                    Array.Copy(
                        landMask,
                        row * chunkTextureResolution,
                        combinedMask,
                        offsetX + ((offsetY + row) * maskWidth),
                        chunkTextureResolution);
                }
            }
        }

        byte[] visited = new byte[combinedMask.Length];
        List<int> queue = new();
        List<int> componentCells = new();
        float chunkWorldSize = 1024f;
        float sampleStepWorldUnits = chunkWorldSize / chunkTextureResolution;
        int resolutionMultiplier = (int)terrainContourResolutionMultiplierField.GetRawConstantValue()!;
        int componentIndex = 0;
        long totalEstimatedRefinedSamples = 0;
        int totalRawLoopPoints = 0;
        int totalSimplifiedLoopPoints = 0;

        for (int y = 0; y < maskHeight; y++)
        {
            for (int x = 0; x < maskWidth; x++)
            {
                int start = (int)indexMethod.Invoke(null, [x, y, maskWidth])!;
                if (combinedMask[start] == 0 || visited[start] != 0)
                {
                    continue;
                }

                object boundsBox = terrainComponentBoundsConstructor.Invoke([0, 0, 0, 0]);
                object[] invokeArgs = [combinedMask, maskWidth, maskHeight, start, visited, queue, componentCells, boundsBox];
                collectConnectedComponentMethod.Invoke(null, invokeArgs);
                boundsBox = invokeArgs[7];

                int boundsMinX = (int)terrainComponentBoundsType.GetProperty("MinX", instanceFlags)!.GetValue(boundsBox)!;
                int boundsMaxX = (int)terrainComponentBoundsType.GetProperty("MaxX", instanceFlags)!.GetValue(boundsBox)!;
                int boundsMinY = (int)terrainComponentBoundsType.GetProperty("MinY", instanceFlags)!.GetValue(boundsBox)!;
                int boundsMaxY = (int)terrainComponentBoundsType.GetProperty("MaxY", instanceFlags)!.GetValue(boundsBox)!;

                int componentWidth = boundsMaxX - boundsMinX + 1;
                int componentHeight = boundsMaxY - boundsMinY + 1;
                byte[] componentMask = new byte[componentWidth * componentHeight];
                for (int i = 0; i < componentCells.Count; i++)
                {
                    int combinedIndex = componentCells[i];
                    int combinedX = combinedIndex % maskWidth;
                    int combinedY = combinedIndex / maskWidth;
                    int localX = combinedX - boundsMinX;
                    int localY = combinedY - boundsMinY;
                    componentMask[localX + (localY * componentWidth)] = 1;
                }

                float worldLeft = (minChunkX * chunkWorldSize) + (boundsMinX * sampleStepWorldUnits);
                float worldTop = (minChunkY * chunkWorldSize) + (boundsMinY * sampleStepWorldUnits);

                object? refinedComponent = buildRefinedTerrainComponentMethod.Invoke(
                    null,
                    [componentMask, componentWidth, componentHeight, worldLeft, worldTop, sampleStepWorldUnits]);
                if (refinedComponent == null)
                {
                    Console.WriteLine($"Component[{componentIndex}] low={componentWidth}x{componentHeight} cells={componentCells.Count} refined=<null>");
                    componentIndex++;
                    continue;
                }

                int refinedWidth = (int)refinedTerrainComponentType.GetProperty("Width", instanceFlags)!.GetValue(refinedComponent)!;
                int refinedHeight = (int)refinedTerrainComponentType.GetProperty("Height", instanceFlags)!.GetValue(refinedComponent)!;
                byte[] refinedMask = (byte[])refinedTerrainComponentType.GetProperty("Mask", instanceFlags)!.GetValue(refinedComponent)!;
                object loopsObject = buildTerrainLoopsMethod.Invoke(null, [refinedMask, refinedWidth, refinedHeight])!;
                System.Collections.IEnumerable loops = (System.Collections.IEnumerable)loopsObject;

                int rawLoopPoints = 0;
                int simplifiedLoopPoints = 0;
                int loopCount = 0;
                foreach (object loop in loops)
                {
                    IReadOnlyList<Vector2> points = (IReadOnlyList<Vector2>)terrainLoopType.GetProperty("Points", instanceFlags)!.GetValue(loop)!;
                    rawLoopPoints += points.Count;
                    List<Vector2> simplified = (List<Vector2>)simplifyLoopMethod.Invoke(null, [points, 0.08f])!;
                    simplifiedLoopPoints += simplified.Count;
                    loopCount++;
                }

                long estimatedSamples = (long)componentWidth * componentHeight * resolutionMultiplier * resolutionMultiplier;
                totalEstimatedRefinedSamples += estimatedSamples;
                totalRawLoopPoints += rawLoopPoints;
                totalSimplifiedLoopPoints += simplifiedLoopPoints;

                Console.WriteLine(
                    $"Component[{componentIndex}] low={componentWidth}x{componentHeight} cells={componentCells.Count} " +
                    $"refined={refinedWidth}x{refinedHeight} estSamples={estimatedSamples:n0} loops={loopCount} rawPts={rawLoopPoints} simpPts={simplifiedLoopPoints}");
                componentIndex++;
            }
        }

        Console.WriteLine($"ComponentCount={componentIndex}");
        Console.WriteLine($"TotalEstimatedRefinedSamples={totalEstimatedRefinedSamples:n0}");
        Console.WriteLine($"TotalRawLoopPoints={totalRawLoopPoints:n0}");
        Console.WriteLine($"TotalSimplifiedLoopPoints={totalSimplifiedLoopPoints:n0}");

        return 0;
    }

    private static int RunTerrainEdgeProbe()
    {
        const int seed = 1337;

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        MethodInfo resolveSeedAnchorMethod = terrainType.GetMethod("ResolveSeedAnchorCentifoot", staticFlags)!;
        MethodInfo buildChunkMethod = terrainType.GetMethod("BuildChunkData", staticFlags)!;
        MethodInfo buildChunkBoundsMethod = terrainType.GetMethod("BuildChunkBounds", staticFlags)!;
        MethodInfo buildChunkWorldBoundsMethod = terrainType.GetMethod("BuildChunkWorldBounds", staticFlags)!;
        MethodInfo applyTerrainMaterializationResultMethod = terrainType.GetMethod("ApplyTerrainMaterializationResult", staticFlags)!;
        MethodInfo overlapsTerrainAtWorldPositionMethod = terrainType.GetMethod("OverlapsTerrainAtWorldPosition", staticFlags)!;
        FieldInfo terrainSeedField = terrainType.GetField("_terrainWorldSeed", staticFlags)!;
        FieldInfo terrainAnchorField = terrainType.GetField("_terrainSeedAnchorCentifoot", staticFlags)!;
        FieldInfo settingsLoadedField = terrainType.GetField("_settingsLoaded", staticFlags)!;
        FieldInfo chunkTextureResolutionField = terrainType.GetField("ChunkTextureResolution", staticFlags)!;

        Type chunkKeyType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkKey", throwOnError: true)!;
        Type generatedChunkType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+GeneratedChunkData", throwOnError: true)!;
        Type chunkBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkBounds", throwOnError: true)!;
        Type combinedResidentMaskType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+CombinedResidentMask", throwOnError: true)!;
        Type materializationResultType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainMaterializationResult", throwOnError: true)!;
        Type terrainWorldBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainWorldBounds", throwOnError: true)!;
        MethodInfo buildTerrainMaterializationResultMethod = terrainType.GetMethod(
            "BuildTerrainMaterializationResult",
            staticFlags,
            binder: null,
            [combinedResidentMaskType, typeof(int), terrainWorldBoundsType],
            modifiers: null)!;

        ConstructorInfo chunkKeyConstructor = chunkKeyType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int)], modifiers: null)!;
        ConstructorInfo combinedResidentMaskConstructor = combinedResidentMaskType.GetConstructor(instanceFlags, binder: null, [typeof(byte[]), typeof(int), typeof(int), typeof(int), typeof(int)], modifiers: null)!;
        PropertyInfo hasLandProperty = generatedChunkType.GetProperty("HasLand", instanceFlags)!;
        PropertyInfo landMaskProperty = generatedChunkType.GetProperty("LandMask", instanceFlags)!;

        terrainSeedField.SetValue(null, seed);
        object anchor = resolveSeedAnchorMethod.Invoke(null, [seed])!;
        terrainAnchorField.SetValue(null, anchor);
        settingsLoadedField.SetValue(null, true);

        const float minX = -960f;
        const float maxX = 960f;
        const float minY = -540f;
        const float maxY = 540f;
        const float visualPreloadMargin = 1440f;
        const float colliderMargin = 256f;
        object materializedBounds = buildChunkBoundsMethod.Invoke(
            null,
            [minX - visualPreloadMargin, maxX + visualPreloadMargin, minY - visualPreloadMargin, maxY + visualPreloadMargin])!;
        object colliderBounds = buildChunkBoundsMethod.Invoke(
            null,
            [minX - colliderMargin, maxX + colliderMargin, minY - colliderMargin, maxY + colliderMargin])!;
        object colliderWorldBounds = buildChunkWorldBoundsMethod.Invoke(null, [colliderBounds])!;

        int minChunkX = (int)chunkBoundsType.GetProperty("MinChunkX", instanceFlags)!.GetValue(materializedBounds)!;
        int maxChunkX = (int)chunkBoundsType.GetProperty("MaxChunkX", instanceFlags)!.GetValue(materializedBounds)!;
        int minChunkY = (int)chunkBoundsType.GetProperty("MinChunkY", instanceFlags)!.GetValue(materializedBounds)!;
        int maxChunkY = (int)chunkBoundsType.GetProperty("MaxChunkY", instanceFlags)!.GetValue(materializedBounds)!;
        int colliderMinChunkX = (int)chunkBoundsType.GetProperty("MinChunkX", instanceFlags)!.GetValue(colliderBounds)!;
        int colliderMaxChunkX = (int)chunkBoundsType.GetProperty("MaxChunkX", instanceFlags)!.GetValue(colliderBounds)!;
        int colliderMinChunkY = (int)chunkBoundsType.GetProperty("MinChunkY", instanceFlags)!.GetValue(colliderBounds)!;
        int colliderMaxChunkY = (int)chunkBoundsType.GetProperty("MaxChunkY", instanceFlags)!.GetValue(colliderBounds)!;

        int chunkTextureResolution = (int)chunkTextureResolutionField.GetRawConstantValue()!;
        int maskWidth = ((maxChunkX - minChunkX) + 1) * chunkTextureResolution;
        int maskHeight = ((maxChunkY - minChunkY) + 1) * chunkTextureResolution;
        byte[] combinedMask = new byte[maskWidth * maskHeight];
        int residentLandChunks = 0;

        for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
        {
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                object key = chunkKeyConstructor.Invoke([chunkX, chunkY]);
                object generatedChunk = buildChunkMethod.Invoke(null, [key])!;
                bool hasLand = (bool)hasLandProperty.GetValue(generatedChunk)!;
                if (!hasLand)
                {
                    continue;
                }

                residentLandChunks++;
                byte[] landMask = (byte[])landMaskProperty.GetValue(generatedChunk)!;
                int offsetX = (chunkX - minChunkX) * chunkTextureResolution;
                int offsetY = (chunkY - minChunkY) * chunkTextureResolution;
                for (int row = 0; row < chunkTextureResolution; row++)
                {
                    Array.Copy(
                        landMask,
                        row * chunkTextureResolution,
                        combinedMask,
                        offsetX + ((offsetY + row) * maskWidth),
                        chunkTextureResolution);
                }
            }
        }

        object combinedMaskBox = combinedResidentMaskConstructor.Invoke([combinedMask, maskWidth, maskHeight, minChunkX, minChunkY]);
        object result = buildTerrainMaterializationResultMethod.Invoke(null, [combinedMaskBox, 1, colliderWorldBounds])!;

        int edgeLoopCount = (int)materializationResultType.GetProperty("ComponentCount", instanceFlags)!.GetValue(result)!;
        int colliderCount = (int)materializationResultType.GetProperty("ColliderCount", instanceFlags)!.GetValue(result)!;
        int triangleCount = (int)materializationResultType.GetProperty("VisualTriangleCount", instanceFlags)!.GetValue(result)!;
        double buildMilliseconds = (double)materializationResultType.GetProperty("BuildMilliseconds", instanceFlags)!.GetValue(result)!;
        bool hitboxMatchesVisual = ProbeTerrainVisualHitboxAgreement(
            result,
            applyTerrainMaterializationResultMethod,
            overlapsTerrainAtWorldPositionMethod,
            instanceFlags);

        Console.WriteLine("TerrainEdgeProbe");
        Console.WriteLine($"VisualChunkWindow=({minChunkX}..{maxChunkX}, {minChunkY}..{maxChunkY})");
        Console.WriteLine($"ColliderChunkWindow=({colliderMinChunkX}..{colliderMaxChunkX}, {colliderMinChunkY}..{colliderMaxChunkY})");
        Console.WriteLine($"ResidentLandChunks={residentLandChunks}");
        Console.WriteLine($"EdgeLoopCount={edgeLoopCount}");
        Console.WriteLine($"SceneProxyColliderCount={colliderCount}");
        Console.WriteLine($"VisualTriangleCount={triangleCount}");
        Console.WriteLine($"HitboxMatchesVisual={hitboxMatchesVisual}");
        Console.WriteLine($"BuildMilliseconds={buildMilliseconds:0.###}");

        return edgeLoopCount > 0 && colliderCount == 0 && triangleCount > 0 && hitboxMatchesVisual ? 0 : 1;
    }

    private static int RunTerrainWindowProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("TerrainWindowProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        player.Position = new Vector2(100f, 100f);
        core.Player = player;
        core.GameObjects.Add(player);

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        MethodInfo tryResolveWindowsMethod = terrainType.GetMethod("TryResolveTerrainStreamingWindows", staticFlags)!;
        object[] args =
        [
            Matrix.Identity,
            new Rectangle(0, 0, 800, 600),
            null!
        ];
        bool resolved = (bool)tryResolveWindowsMethod.Invoke(null, args)!;
        if (!resolved)
        {
            Console.WriteLine("TerrainWindowProbe: failed to resolve streaming windows.");
            return 1;
        }

        object windows = args[2]!;
        object visible = windows.GetType().GetProperty("VisibleChunkWindow", instanceFlags)!.GetValue(windows)!;
        object targetVisual = windows.GetType().GetProperty("TerrainObjectChunkWindow", instanceFlags)!.GetValue(windows)!;
        object preload = windows.GetType().GetProperty("PreloadChunkWindow", instanceFlags)!.GetValue(windows)!;

        int leftMargin = GetChunkBoundsValue(visible, "MinChunkX", instanceFlags) - GetChunkBoundsValue(targetVisual, "MinChunkX", instanceFlags);
        int rightMargin = GetChunkBoundsValue(targetVisual, "MaxChunkX", instanceFlags) - GetChunkBoundsValue(visible, "MaxChunkX", instanceFlags);
        int topMargin = GetChunkBoundsValue(visible, "MinChunkY", instanceFlags) - GetChunkBoundsValue(targetVisual, "MinChunkY", instanceFlags);
        int bottomMargin = GetChunkBoundsValue(targetVisual, "MaxChunkY", instanceFlags) - GetChunkBoundsValue(visible, "MaxChunkY", instanceFlags);
        bool objectAheadOfVisible = leftMargin >= 2 && rightMargin >= 2 && topMargin >= 2 && bottomMargin >= 2;
        bool preloadContainsObject = ChunkBoundsContains(preload, targetVisual, instanceFlags);

        Console.WriteLine("TerrainWindowProbe");
        Console.WriteLine($"VisibleChunkWindow={FormatChunkBoundsForProbe(visible, instanceFlags)}");
        Console.WriteLine($"TerrainObjectChunkWindow={FormatChunkBoundsForProbe(targetVisual, instanceFlags)}");
        Console.WriteLine($"PreloadChunkWindow={FormatChunkBoundsForProbe(preload, instanceFlags)}");
        Console.WriteLine($"AheadMargins=left:{leftMargin} right:{rightMargin} top:{topMargin} bottom:{bottomMargin}");
        Console.WriteLine($"ObjectAheadOfVisible={objectAheadOfVisible}");
        Console.WriteLine($"PreloadContainsObject={preloadContainsObject}");

        return objectAheadOfVisible && preloadContainsObject ? 0 : 1;
    }

    private static int RunTerrainStaleMaterializationProbe()
    {
        const int seed = 1337;

        ResetRuntimeState();
        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        MethodInfo resolveSeedAnchorMethod = terrainType.GetMethod("ResolveSeedAnchorCentifoot", staticFlags)!;
        MethodInfo buildChunkMethod = terrainType.GetMethod("BuildChunkData", staticFlags)!;
        MethodInfo buildChunkWorldBoundsMethod = terrainType.GetMethod("BuildChunkWorldBounds", staticFlags)!;
        MethodInfo tryApplyCompletedTerrainMaterializationMethod = terrainType.GetMethod("TryApplyCompletedTerrainMaterialization", staticFlags)!;
        MethodInfo clearResidentTerrainWorldObjectsMethod = terrainType.GetMethod("ClearResidentTerrainWorldObjects", staticFlags)!;
        FieldInfo terrainSeedField = terrainType.GetField("_terrainWorldSeed", staticFlags)!;
        FieldInfo terrainAnchorField = terrainType.GetField("_terrainSeedAnchorCentifoot", staticFlags)!;
        FieldInfo settingsLoadedField = terrainType.GetField("_settingsLoaded", staticFlags)!;
        FieldInfo terrainWorldBoundsInitializedField = terrainType.GetField("_terrainWorldBoundsInitialized", staticFlags)!;
        FieldInfo terrainWorldObjectsDirtyField = terrainType.GetField("_terrainWorldObjectsDirty", staticFlags)!;
        FieldInfo terrainMaterializationTaskField = terrainType.GetField("_terrainMaterializationTask", staticFlags)!;
        FieldInfo terrainMaterializationRequestIdField = terrainType.GetField("_terrainMaterializationRequestId", staticFlags)!;
        FieldInfo discardedStaleMaterializationCountField = terrainType.GetField("_terrainDiscardedStaleMaterializationCount", staticFlags)!;
        FieldInfo lastTerrainVisualChunkWindowField = terrainType.GetField("_lastTerrainVisualChunkWindow", staticFlags)!;
        FieldInfo lastMaterializedChunkWindowField = terrainType.GetField("_lastMaterializedChunkWindow", staticFlags)!;
        FieldInfo lastTerrainColliderChunkWindowField = terrainType.GetField("_lastTerrainColliderChunkWindow", staticFlags)!;
        FieldInfo residentTerrainVisualObjectsField = terrainType.GetField("ResidentTerrainVisualObjects", staticFlags)!;
        FieldInfo chunkTextureResolutionField = terrainType.GetField("ChunkTextureResolution", staticFlags)!;

        Type chunkKeyType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkKey", throwOnError: true)!;
        Type generatedChunkType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+GeneratedChunkData", throwOnError: true)!;
        Type chunkBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkBounds", throwOnError: true)!;
        Type combinedResidentMaskType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+CombinedResidentMask", throwOnError: true)!;
        Type materializationResultType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainMaterializationResult", throwOnError: true)!;
        Type terrainWorldBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+TerrainWorldBounds", throwOnError: true)!;
        MethodInfo buildTerrainMaterializationResultMethod = terrainType.GetMethod(
            "BuildTerrainMaterializationResult",
            staticFlags,
            binder: null,
            [combinedResidentMaskType, typeof(int), terrainWorldBoundsType],
            modifiers: null)!;

        ConstructorInfo chunkKeyConstructor = chunkKeyType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int)], modifiers: null)!;
        ConstructorInfo chunkBoundsConstructor = chunkBoundsType.GetConstructor(instanceFlags, binder: null, [typeof(int), typeof(int), typeof(int), typeof(int)], modifiers: null)!;
        ConstructorInfo combinedResidentMaskConstructor = combinedResidentMaskType.GetConstructor(instanceFlags, binder: null, [typeof(byte[]), typeof(int), typeof(int), typeof(int), typeof(int)], modifiers: null)!;
        PropertyInfo hasLandProperty = generatedChunkType.GetProperty("HasLand", instanceFlags)!;
        PropertyInfo landMaskProperty = generatedChunkType.GetProperty("LandMask", instanceFlags)!;
        PropertyInfo visualObjectsProperty = materializationResultType.GetProperty("VisualObjects", instanceFlags)!;

        terrainSeedField.SetValue(null, seed);
        object anchor = resolveSeedAnchorMethod.Invoke(null, [seed])!;
        terrainAnchorField.SetValue(null, anchor);
        settingsLoadedField.SetValue(null, true);
        terrainWorldBoundsInitializedField.SetValue(null, false);
        terrainMaterializationTaskField.SetValue(null, null);
        terrainMaterializationRequestIdField.SetValue(null, 0);
        discardedStaleMaterializationCountField.SetValue(null, 0);
        clearResidentTerrainWorldObjectsMethod.Invoke(null, []);

        object staleChunkWindow = chunkBoundsConstructor.Invoke([-3, 2, -2, 1]);
        object currentChunkWindow = chunkBoundsConstructor.Invoke([1, 6, -2, 1]);
        object staleResult = BuildResult(staleChunkWindow, 1);
        object currentResult = BuildResult(currentChunkWindow, 2);
        int staleResultVisualCount = ((IList)visualObjectsProperty.GetValue(staleResult)!).Count;
        int currentResultVisualCount = ((IList)visualObjectsProperty.GetValue(currentResult)!).Count;
        if (staleResultVisualCount == 0 || currentResultVisualCount == 0)
        {
            Console.WriteLine("TerrainStaleMaterializationProbe");
            Console.WriteLine($"  setupFailed staleVisuals={staleResultVisualCount} currentVisuals={currentResultVisualCount}");
            return 1;
        }

        MethodInfo taskFromResultMethod = typeof(System.Threading.Tasks.Task)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(static method => method.Name == nameof(System.Threading.Tasks.Task.FromResult) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(materializationResultType);

        terrainMaterializationRequestIdField.SetValue(null, 1);
        lastTerrainVisualChunkWindowField.SetValue(null, currentChunkWindow);
        lastMaterializedChunkWindowField.SetValue(null, currentChunkWindow);
        lastTerrainColliderChunkWindowField.SetValue(null, currentChunkWindow);
        terrainWorldObjectsDirtyField.SetValue(null, true);
        terrainMaterializationTaskField.SetValue(null, taskFromResultMethod.Invoke(null, [staleResult]));

        tryApplyCompletedTerrainMaterializationMethod.Invoke(null, []);

        IList residentVisualObjects = (IList)residentTerrainVisualObjectsField.GetValue(null)!;
        int residentVisualCountAfterStale = residentVisualObjects.Count;
        bool dirtyAfterStale = (bool)terrainWorldObjectsDirtyField.GetValue(null)!;
        int discardCountAfterStale = (int)discardedStaleMaterializationCountField.GetValue(null)!;
        bool staleWasDiscarded = residentVisualCountAfterStale == 0 && dirtyAfterStale && discardCountAfterStale == 1;

        terrainMaterializationRequestIdField.SetValue(null, 2);
        lastTerrainVisualChunkWindowField.SetValue(null, currentChunkWindow);
        lastMaterializedChunkWindowField.SetValue(null, currentChunkWindow);
        lastTerrainColliderChunkWindowField.SetValue(null, currentChunkWindow);
        terrainWorldObjectsDirtyField.SetValue(null, false);
        terrainMaterializationTaskField.SetValue(null, taskFromResultMethod.Invoke(null, [currentResult]));

        tryApplyCompletedTerrainMaterializationMethod.Invoke(null, []);

        int residentVisualCountAfterCurrent = residentVisualObjects.Count;
        bool dirtyAfterCurrent = (bool)terrainWorldObjectsDirtyField.GetValue(null)!;
        int discardCountAfterCurrent = (int)discardedStaleMaterializationCountField.GetValue(null)!;
        bool currentWasApplied = residentVisualCountAfterCurrent > 0 && !dirtyAfterCurrent && discardCountAfterCurrent == discardCountAfterStale;

        Console.WriteLine("TerrainStaleMaterializationProbe");
        Console.WriteLine($"  staleResultVisuals={staleResultVisualCount}");
        Console.WriteLine($"  currentResultVisuals={currentResultVisualCount}");
        Console.WriteLine($"  staleWasDiscarded={staleWasDiscarded}");
        Console.WriteLine($"  currentWasApplied={currentWasApplied}");
        Console.WriteLine($"  residentAfterStale={residentVisualCountAfterStale}");
        Console.WriteLine($"  residentAfterCurrent={residentVisualCountAfterCurrent}");
        Console.WriteLine($"  discardedStaleMaterializations={discardCountAfterCurrent}");

        return staleWasDiscarded && currentWasApplied ? 0 : 1;

        object BuildResult(object chunkWindow, int requestId)
        {
            int minChunkX = (int)chunkBoundsType.GetProperty("MinChunkX", instanceFlags)!.GetValue(chunkWindow)!;
            int maxChunkX = (int)chunkBoundsType.GetProperty("MaxChunkX", instanceFlags)!.GetValue(chunkWindow)!;
            int minChunkY = (int)chunkBoundsType.GetProperty("MinChunkY", instanceFlags)!.GetValue(chunkWindow)!;
            int maxChunkY = (int)chunkBoundsType.GetProperty("MaxChunkY", instanceFlags)!.GetValue(chunkWindow)!;

            int chunkTextureResolution = (int)chunkTextureResolutionField.GetRawConstantValue()!;
            int maskWidth = ((maxChunkX - minChunkX) + 1) * chunkTextureResolution;
            int maskHeight = ((maxChunkY - minChunkY) + 1) * chunkTextureResolution;
            byte[] combinedMask = new byte[maskWidth * maskHeight];

            for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
            {
                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    object key = chunkKeyConstructor.Invoke([chunkX, chunkY]);
                    object generatedChunk = buildChunkMethod.Invoke(null, [key])!;
                    bool hasLand = (bool)hasLandProperty.GetValue(generatedChunk)!;
                    if (!hasLand)
                    {
                        continue;
                    }

                    byte[] landMask = (byte[])landMaskProperty.GetValue(generatedChunk)!;
                    int offsetX = (chunkX - minChunkX) * chunkTextureResolution;
                    int offsetY = (chunkY - minChunkY) * chunkTextureResolution;
                    for (int row = 0; row < chunkTextureResolution; row++)
                    {
                        Array.Copy(
                            landMask,
                            row * chunkTextureResolution,
                            combinedMask,
                            offsetX + ((offsetY + row) * maskWidth),
                            chunkTextureResolution);
                    }
                }
            }

            object combinedMaskBox = combinedResidentMaskConstructor.Invoke([combinedMask, maskWidth, maskHeight, minChunkX, minChunkY]);
            object colliderWorldBounds = buildChunkWorldBoundsMethod.Invoke(null, [chunkWindow])!;
            return buildTerrainMaterializationResultMethod.Invoke(null, [combinedMaskBox, requestId, colliderWorldBounds])!;
        }
    }

    private static int RunTerrainImpulseProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        GameObject redWall = CreateStaticProbeWall(101, "RedWallLike");
        Vector2 redWallImpulse = SimulateStaticWallImpulse(
            100,
            redWall);
        (Vector2 redWallAgentImpulse, Vector2 redWallAgentMovement) = SimulateAgentStaticWallResponse(
            150,
            redWall);

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo resolveBulletTerrainIntrusionsMethod = terrainType.GetMethod(
            "ResolveBulletTerrainIntrusions",
            staticFlags,
            binder: null,
            [typeof(IReadOnlyList<Bullet>)],
            modifiers: null)!;
        MethodInfo overlapsTerrainAtCollisionHullMethod = terrainType.GetMethod(
            "OverlapsTerrainAtCollisionHull",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        SeedTerrainPhaseProbeCollisionLoop(terrainType, 16f);

        Shape bulletShape = new("Circle", 16, 16, 0, Color.White, Color.Transparent, 0);
        Bullet terrainBullet = new(
            200,
            new Vector2(32f, 0f),
            new Vector2(600f, 0f),
            mass: 1f,
            maxLifespan: 10f,
            dragFactor: 1f,
            shape: bulletShape,
            bulletHealth: 1f,
            bulletDamage: 1f,
            bulletPenetration: 0f,
            bulletKnockback: 0f,
            maxSpeed: 1000f,
            Color.White,
            Color.Transparent,
            0);
        terrainBullet.PreviousPosition = new Vector2(-32f, 0f);
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        resolveBulletTerrainIntrusionsMethod.Invoke(null, [new List<Bullet> { terrainBullet }]);
        bool terrainBulletStillOverlaps = (bool)overlapsTerrainAtCollisionHullMethod.Invoke(
            null,
            [terrainBullet, terrainBullet.Position])!;

        Vector2 terrainFallbackImpulse = SimulateTerrainFallbackImpulse(
            redWallImpulse,
            alreadyHasColliderImpulse: false);
        Vector2 terrainFallbackAfterColliderImpulse = SimulateTerrainFallbackImpulse(
            redWallImpulse,
            alreadyHasColliderImpulse: true);

        bool redWallBounced = redWallImpulse.X < -1f;
        bool redWallAgentBounced = redWallAgentImpulse.X < -1f;
        bool redWallAgentMovementClipped = redWallAgentMovement.X <= 0.001f;
        bool terrainBulletBounced = terrainBullet.Velocity.X < -1f;
        bool terrainBulletResolvedOutside = !terrainBulletStillOverlaps;
        int sceneTerrainColliderCount = core.GameObjects.Count(static gameObject => gameObject?.Name == "TerrainCollider") +
            core.StaticObjects.Count(static gameObject => gameObject?.Name == "TerrainCollider");
        int residentTerrainColliderCount = (int)terrainType.GetProperty("TerrainResidentColliderCount", staticFlags)!.GetValue(null)!;
        int activeTerrainColliderCount = (int)terrainType.GetProperty("TerrainActiveColliderCount", staticFlags)!.GetValue(null)!;
        int terrainBulletCorrections = (int)terrainType.GetProperty("TerrainBulletCollisionCorrectionCount", staticFlags)!.GetValue(null)!;
        bool fallbackMatchesWall = MathF.Abs(terrainFallbackImpulse.X - redWallImpulse.X) <= 0.001f;
        bool fallbackDoesNotBoostWallImpulse = MathF.Abs(terrainFallbackAfterColliderImpulse.X - redWallImpulse.X) <= 0.001f;

        Console.WriteLine("TerrainImpulseProbe");
        Console.WriteLine($"  redWallImpulse=({redWallImpulse.X:0.###},{redWallImpulse.Y:0.###}) bounced={redWallBounced}");
        Console.WriteLine($"  redWallAgentImpulse=({redWallAgentImpulse.X:0.###},{redWallAgentImpulse.Y:0.###}) bounced={redWallAgentBounced}");
        Console.WriteLine($"  redWallAgentMovement=({redWallAgentMovement.X:0.###},{redWallAgentMovement.Y:0.###}) clipped={redWallAgentMovementClipped}");
        Console.WriteLine($"  terrainBulletVelocity=({terrainBullet.Velocity.X:0.###},{terrainBullet.Velocity.Y:0.###}) bounced={terrainBulletBounced}");
        Console.WriteLine($"  terrainBulletPosition=({terrainBullet.Position.X:0.###},{terrainBullet.Position.Y:0.###}) resolvedOutside={terrainBulletResolvedOutside}");
        Console.WriteLine($"  sceneTerrainColliderCount={sceneTerrainColliderCount}");
        Console.WriteLine($"  residentTerrainColliderCount={residentTerrainColliderCount}");
        Console.WriteLine($"  activeTerrainColliderCount={activeTerrainColliderCount}");
        Console.WriteLine($"  terrainBulletCorrections={terrainBulletCorrections}");
        Console.WriteLine($"  terrainFallbackImpulse=({terrainFallbackImpulse.X:0.###},{terrainFallbackImpulse.Y:0.###}) matchesWall={fallbackMatchesWall}");
        Console.WriteLine($"  terrainFallbackAfterColliderImpulse=({terrainFallbackAfterColliderImpulse.X:0.###},{terrainFallbackAfterColliderImpulse.Y:0.###}) noBoost={fallbackDoesNotBoostWallImpulse}");

        return redWallBounced &&
               redWallAgentBounced &&
               redWallAgentMovementClipped &&
               terrainBulletBounced &&
               terrainBulletResolvedOutside &&
               sceneTerrainColliderCount == 0 &&
               residentTerrainColliderCount == 0 &&
               activeTerrainColliderCount == 0 &&
               terrainBulletCorrections == 1 &&
               fallbackMatchesWall &&
               fallbackDoesNotBoostWallImpulse
            ? 0
            : 1;
    }

    private static int RunTerrainAccessGateProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("TerrainAccessGateProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        core.Player = player;
        core.GameObjects.Add(player);

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Type chunkBoundsType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground+ChunkBounds", throwOnError: true)!;
        ConstructorInfo chunkBoundsConstructor = chunkBoundsType.GetConstructor(
            instanceFlags,
            binder: null,
            [typeof(int), typeof(int), typeof(int), typeof(int)],
            modifiers: null)!;
        object appliedSingleChunk = chunkBoundsConstructor.Invoke([0, 0, 0, 0]);
        object colliderExpandedChunkWindow = chunkBoundsConstructor.Invoke([0, 1, 0, 0]);

        terrainType.GetMethod("ResetRuntimeTerrainObjectsForLevelLoad", staticFlags)!.Invoke(null, []);
        terrainType.GetMethod("ResetTerrainChunkWorkerQueues", staticFlags)!.Invoke(null, []);
        terrainType.GetField("_startupVisibleTerrainReady", staticFlags)!.SetValue(null, true);
        terrainType.GetField("_hasAppliedTerrainVisualChunkWindow", staticFlags)!.SetValue(null, true);
        terrainType.GetField("_lastAppliedVisualChunkWindow", staticFlags)!.SetValue(null, appliedSingleChunk);
        terrainType.GetField("_lastAppliedColliderChunkWindow", staticFlags)!.SetValue(null, appliedSingleChunk);
        terrainType.GetField("_terrainAccessRequestActive", staticFlags)!.SetValue(null, false);

        MethodInfo requestAccessMethod = terrainType.GetMethod(
            "RequestPlayerTerrainAccess",
            staticFlags,
            binder: null,
            [typeof(Agent)],
            modifiers: null)!;

        Vector2 centerPreviousPosition = Vector2.Zero;
        Vector2 centerTargetPosition = new(200f, 200f);
        player.PreviousPosition = centerPreviousPosition;
        player.Position = centerTargetPosition;
        requestAccessMethod.Invoke(null, [player]);
        bool centerNotReverted = Vector2.DistanceSquared(player.Position, centerTargetPosition) <= 0.001f;
        bool centerDidNotRequestAccess = !(bool)terrainType.GetProperty("TerrainAccessRequestActive", staticFlags)!.GetValue(null)!;

        terrainType.GetField("_lastAppliedColliderChunkWindow", staticFlags)!.SetValue(null, colliderExpandedChunkWindow);
        Vector2 edgePreviousPosition = new(900f, 0f);
        Vector2 edgeTargetPosition = new(1100f, 0f);
        player.PreviousPosition = edgePreviousPosition;
        player.Position = edgeTargetPosition;
        requestAccessMethod.Invoke(null, [player]);
        bool edgeNotReverted = Vector2.DistanceSquared(player.Position, edgeTargetPosition) <= 0.001f;
        bool edgeRequestedAccess = (bool)terrainType.GetProperty("TerrainAccessRequestActive", staticFlags)!.GetValue(null)!;
        int blockedCount = (int)terrainType.GetProperty("TerrainMovementBlockedUntilReadyCount", staticFlags)!.GetValue(null)!;

        Console.WriteLine("TerrainAccessGateProbe");
        Console.WriteLine($"CenterNotReverted={centerNotReverted}");
        Console.WriteLine($"CenterDidNotRequestAccess={centerDidNotRequestAccess}");
        Console.WriteLine($"EdgeNotReverted={edgeNotReverted}");
        Console.WriteLine($"EdgeRequestedAccess={edgeRequestedAccess}");
        Console.WriteLine($"BlockedCount={blockedCount}");

        return centerNotReverted &&
            centerDidNotRequestAccess &&
            edgeNotReverted &&
            edgeRequestedAccess &&
            blockedCount == 0
                ? 0
                : 1;
    }

    private static Vector2 SimulateTerrainFallbackImpulse(
        Vector2 wallImpulse,
        bool alreadyHasColliderImpulse)
    {
        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        MethodInfo resolveTerrainCollisionPhysicsVelocityMethod = terrainType.GetMethod(
            "ResolveTerrainCollisionPhysicsVelocity",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        GameObject mover = CreateProbeObject(
            alreadyHasColliderImpulse ? 301 : 300,
            "TerrainFallbackMover",
            new Vector2(0f, 0f),
            mass: 3f,
            dynamicPhysics: true);
        mover.Shape = new Shape("Rectangle", 32, 32, 0, Color.White, Color.Transparent, 0);
        mover.PreviousPosition = alreadyHasColliderImpulse
            ? new Vector2(-100f, 0f)
            : new Vector2(-20f, 0f);
        mover.PhysicsVelocity = alreadyHasColliderImpulse
            ? wallImpulse
            : Vector2.Zero;

        return (Vector2)resolveTerrainCollisionPhysicsVelocityMethod.Invoke(
            null,
            [mover, new Vector2(-1f, 0f)])!;
    }

    private static int RunTerrainPhaseProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Assembly gameplayAssembly = typeof(Core).Assembly;
        Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        MethodInfo resolveDynamicTerrainIntrusionsMethod = terrainType.GetMethod(
            "ResolveDynamicTerrainIntrusions",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo loadSettingsIfNeededMethod = terrainType.GetMethod(
            "LoadSettingsIfNeeded",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo ensureTerrainWorldBoundsInitializedMethod = terrainType.GetMethod(
            "EnsureTerrainWorldBoundsInitialized",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            Type.EmptyTypes,
            modifiers: null)!;
        MethodInfo overlapsTerrainAtCollisionHullMethod = terrainType.GetMethod(
            "OverlapsTerrainAtCollisionHull",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        const float terrainMaxX = 4096f;
        const float coreRadius = 25f;
        const float outlineWidth = 5f;

        loadSettingsIfNeededMethod.Invoke(null, []);
        ensureTerrainWorldBoundsInitializedMethod.Invoke(null, []);
        SeedTerrainPhaseProbeCollisionLoop(terrainType, terrainMaxX);
        float boundaryY = ResolveTerrainPhaseProbeBoundaryY(
            overlapsTerrainAtCollisionHullMethod,
            terrainMaxX,
            coreRadius);

        GameObject outlineOnlyMover = CreateTerrainPhaseProbeMover(
            350,
            "OutlineOnlyTerrainPhaseMover",
            new Vector2(terrainMaxX - coreRadius - 1f, boundaryY));
        float outlineOnlyVisiblePenetration =
            (outlineOnlyMover.Position.X + coreRadius + outlineWidth) - terrainMaxX;
        bool outlineCorrected = RunTerrainPhaseCorrectionProbe(
            resolveDynamicTerrainIntrusionsMethod,
            outlineOnlyMover);

        GameObject corePenetratingMover = CreateTerrainPhaseProbeMover(
            351,
            "CoreTerrainPenetrationMover",
            new Vector2(terrainMaxX - coreRadius + 2f, boundaryY));
        float startingCorePenetration =
            (corePenetratingMover.Position.X + coreRadius) - terrainMaxX;
        bool coreCorrected = RunTerrainPhaseCorrectionProbe(
            resolveDynamicTerrainIntrusionsMethod,
            corePenetratingMover);

        float corePenetration =
            (corePenetratingMover.Position.X + coreRadius) - terrainMaxX;

        Console.WriteLine("TerrainPhaseProbe");
        Console.WriteLine($"  boundaryY={boundaryY:0.###}");
        Console.WriteLine($"  outlineOnlyVisiblePenetration={outlineOnlyVisiblePenetration:0.###} corrected={outlineCorrected}");
        Console.WriteLine($"  corePenetrationStart={startingCorePenetration:0.###} afterResolve={corePenetration:0.###} corrected={coreCorrected}");

        return !outlineCorrected && coreCorrected && corePenetration <= 0.001f
            ? 0
            : 1;
    }

    private static void SeedTerrainPhaseProbeCollisionLoop(Type terrainType, float terrainMinX)
    {
        BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        Type loopRecordType = terrainType.Assembly.GetType("op.io.GameBlockTerrainBackground+TerrainCollisionLoopRecord", throwOnError: true)!;
        Type terrainWorldBoundsType = terrainType.Assembly.GetType("op.io.GameBlockTerrainBackground+TerrainWorldBounds", throwOnError: true)!;
        ConstructorInfo terrainWorldBoundsConstructor = terrainWorldBoundsType.GetConstructor(
            instanceFlags,
            binder: null,
            [typeof(float), typeof(float), typeof(float), typeof(float)],
            modifiers: null)!;
        ConstructorInfo loopRecordConstructor = loopRecordType.GetConstructor(
            instanceFlags,
            binder: null,
            [terrainWorldBoundsType, typeof(List<Vector2>)],
            modifiers: null)!;
        FieldInfo residentLoopsField = terrainType.GetField("ResidentTerrainCollisionLoops", staticFlags)!;

        IList residentLoops = (IList)residentLoopsField.GetValue(null)!;
        residentLoops.Clear();

        const float terrainMinY = -4096f;
        const float terrainHeight = 8192f;
        const float terrainWidth = 512f;
        List<Vector2> points =
        [
            new(terrainMinX, terrainMinY),
            new(terrainMinX + terrainWidth, terrainMinY),
            new(terrainMinX + terrainWidth, terrainMinY + terrainHeight),
            new(terrainMinX, terrainMinY + terrainHeight)
        ];
        object bounds = terrainWorldBoundsConstructor.Invoke([terrainMinX, terrainMinY, terrainWidth, terrainHeight]);
        object loopRecord = loopRecordConstructor.Invoke([bounds, points]);
        residentLoops.Add(loopRecord);
    }

    private static float ResolveTerrainPhaseProbeBoundaryY(
        MethodInfo overlapsTerrainAtCollisionHullMethod,
        float terrainMaxX,
        float coreRadius)
    {
        for (float y = -3800f; y <= 3800f; y += 64f)
        {
            GameObject outlineOnlyMover = CreateTerrainPhaseProbeMover(
                349,
                "TerrainPhaseBoundaryCandidate",
                new Vector2(terrainMaxX - coreRadius - 1f, y));
            bool overlaps = (bool)overlapsTerrainAtCollisionHullMethod.Invoke(
                null,
                [outlineOnlyMover, outlineOnlyMover.Position])!;
            if (!overlaps)
            {
                return y;
            }
        }

        return -3900f;
    }

    private static GameObject CreateTerrainPhaseProbeMover(int id, string name, Vector2 position)
    {
        Shape shape = new("Circle", 50, 50, 0, Color.White, Color.Cyan, 5);
        GameObject mover = new(
            id,
            name,
            position,
            0f,
            3f,
            isDestructible: false,
            isCollidable: true,
            dynamicPhysics: true,
            shape,
            Color.White,
            Color.Cyan,
            5,
            registerWithShapeManager: false);
        mover.PreviousPosition = position - new Vector2(20f, 0f);
        return mover;
    }

    private static bool RunTerrainPhaseCorrectionProbe(
        MethodInfo resolveDynamicTerrainIntrusionsMethod,
        GameObject mover)
    {
        Vector2 startPosition = mover.Position;
        Core.Instance.GameObjects = new List<GameObject> { mover };
        Core.Instance.StaticObjects = new List<GameObject>();
        resolveDynamicTerrainIntrusionsMethod.Invoke(null, [Core.Instance.GameObjects]);
        return Vector2.DistanceSquared(startPosition, mover.Position) > 0.25f;
    }

    private static Vector2 SimulateStaticWallImpulse(int movingId, GameObject staticWall)
    {
        ResetFrameState();

        GameObject mover = CreateProbeObject(
            movingId,
            $"Mover{movingId}",
            new Vector2(0f, 0f),
            mass: 3f,
            dynamicPhysics: true);
        mover.Shape = new Shape("Rectangle", 32, 32, 0, Color.White, Color.Transparent, 0);
        mover.PreviousPosition = new Vector2(-20f, 0f);

        Core.Instance.GameObjects = new List<GameObject> { mover, staticWall };
        Core.Instance.StaticObjects = new List<GameObject> { staticWall };
        Core.DELTATIME = 1f / 60f;

        CollisionResolver.ResolveCollisions(Core.Instance.GameObjects);
        return mover.PhysicsVelocity;
    }

    private static (Vector2 PhysicsVelocity, Vector2 MovementVelocity) SimulateAgentStaticWallResponse(int movingId, GameObject staticWall)
    {
        ResetFrameState();

        Shape shape = new("Rectangle", 32, 32, 0, Color.White, Color.Transparent, 0);
        Agent mover = new(
            movingId,
            $"AgentMover{movingId}",
            new Vector2(0f, 0f),
            0f,
            3f,
            isDestructible: false,
            isCollidable: true,
            dynamicPhysics: true,
            shape,
            baseSpeed: 0f,
            isPlayer: true,
            Color.White,
            Color.Transparent,
            0);
        mover.PreviousPosition = new Vector2(-20f, 0f);
        mover.MovementVelocity = new Vector2(1200f, 0f);

        Core.Instance.GameObjects = new List<GameObject> { mover, staticWall };
        Core.Instance.StaticObjects = new List<GameObject> { staticWall };
        Core.DELTATIME = 1f / 60f;

        CollisionResolver.ResolveCollisions(Core.Instance.GameObjects);
        return (mover.PhysicsVelocity, mover.MovementVelocity);
    }

    private static GameObject CreateStaticProbeWall(int id, string name)
    {
        Shape shape = new("Rectangle", 16, 128, 0, Color.Red, Color.Transparent, 0);
        return new GameObject(
            id,
            name,
            new Vector2(16f, 0f),
            0f,
            0f,
            isDestructible: false,
            isCollidable: true,
            dynamicPhysics: false,
            shape,
            Color.Red,
            Color.Transparent,
            0,
            registerWithShapeManager: false);
    }

    private static bool ProbeTerrainVisualHitboxAgreement(
        object materializationResult,
        MethodInfo applyTerrainMaterializationResultMethod,
        MethodInfo overlapsTerrainAtWorldPositionMethod,
        BindingFlags instanceFlags)
    {
        IList visualObjects = (IList)materializationResult
            .GetType()
            .GetProperty("VisualObjects", instanceFlags)!
            .GetValue(materializationResult)!;
        IList collisionLoops = (IList)materializationResult
            .GetType()
            .GetProperty("CollisionLoops", instanceFlags)!
            .GetValue(materializationResult)!;
        if (visualObjects.Count == 0 || collisionLoops.Count == 0)
        {
            return false;
        }

        bool collisionLoopsMatchVisuals = visualObjects.Count == collisionLoops.Count;
        if (collisionLoopsMatchVisuals)
        {
            for (int i = 0; i < visualObjects.Count; i++)
            {
                object visualBounds = visualObjects[i]!.GetType().GetProperty("Bounds", instanceFlags)!.GetValue(visualObjects[i])!;
                object collisionBounds = collisionLoops[i]!.GetType().GetProperty("Bounds", instanceFlags)!.GetValue(collisionLoops[i])!;
                if (!TerrainBoundsApproximatelyEqual(visualBounds, collisionBounds, instanceFlags))
                {
                    collisionLoopsMatchVisuals = false;
                    break;
                }
            }
        }

        applyTerrainMaterializationResultMethod.Invoke(null, [materializationResult]);

        bool insideVisualHitsTerrain = false;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int visualIndex = 0; visualIndex < visualObjects.Count; visualIndex++)
        {
            object visualObject = visualObjects[visualIndex]!;
            object bounds = visualObject.GetType().GetProperty("Bounds", instanceFlags)!.GetValue(visualObject)!;
            maxX = MathF.Max(maxX, (float)bounds.GetType().GetProperty("MaxX", instanceFlags)!.GetValue(bounds)!);
            maxY = MathF.Max(maxY, (float)bounds.GetType().GetProperty("MaxY", instanceFlags)!.GetValue(bounds)!);

            VertexPositionColor[] vertices = (VertexPositionColor[])visualObject
                .GetType()
                .GetProperty("FillVertices", instanceFlags)!
                .GetValue(visualObject)!;

            for (int vertexIndex = 0; vertexIndex + 2 < vertices.Length; vertexIndex += 3)
            {
                Vector3 a = vertices[vertexIndex].Position;
                Vector3 b = vertices[vertexIndex + 1].Position;
                Vector3 c = vertices[vertexIndex + 2].Position;
                Vector2 centroid = new(
                    (a.X + b.X + c.X) / 3f,
                    (a.Y + b.Y + c.Y) / 3f);
                if ((bool)overlapsTerrainAtWorldPositionMethod.Invoke(null, [centroid, 1f])!)
                {
                    insideVisualHitsTerrain = true;
                    break;
                }
            }

        }

        for (int collisionLoopIndex = 0; collisionLoopIndex < collisionLoops.Count; collisionLoopIndex++)
        {
            object collisionLoop = collisionLoops[collisionLoopIndex]!;
            object bounds = collisionLoop.GetType().GetProperty("Bounds", instanceFlags)!.GetValue(collisionLoop)!;
            maxX = MathF.Max(maxX, (float)bounds.GetType().GetProperty("MaxX", instanceFlags)!.GetValue(bounds)!);
            maxY = MathF.Max(maxY, (float)bounds.GetType().GetProperty("MaxY", instanceFlags)!.GetValue(bounds)!);
        }

        Vector2 outsideSample = new(maxX + 512f, maxY + 512f);
        bool outsideVisualDoesNotHitTerrain = !(bool)overlapsTerrainAtWorldPositionMethod.Invoke(
            null,
            [outsideSample, 1f])!;
        if (!collisionLoopsMatchVisuals || !insideVisualHitsTerrain || !outsideVisualDoesNotHitTerrain)
        {
            Console.WriteLine(
                $"HitboxAgreementDetail collisionLoopsMatchVisuals={collisionLoopsMatchVisuals} visualObjects={visualObjects.Count} collisionLoops={collisionLoops.Count} insideVisualHitsTerrain={insideVisualHitsTerrain} outsideVisualDoesNotHitTerrain={outsideVisualDoesNotHitTerrain} outsideSample={outsideSample}");
        }

        return collisionLoopsMatchVisuals && insideVisualHitsTerrain && outsideVisualDoesNotHitTerrain;
    }

    private static bool TerrainBoundsApproximatelyEqual(object leftBounds, object rightBounds, BindingFlags instanceFlags)
    {
        const float tolerance = 0.001f;
        return MathF.Abs(GetTerrainBoundsValue(leftBounds, "MinX", instanceFlags) - GetTerrainBoundsValue(rightBounds, "MinX", instanceFlags)) <= tolerance &&
            MathF.Abs(GetTerrainBoundsValue(leftBounds, "MinY", instanceFlags) - GetTerrainBoundsValue(rightBounds, "MinY", instanceFlags)) <= tolerance &&
            MathF.Abs(GetTerrainBoundsValue(leftBounds, "MaxX", instanceFlags) - GetTerrainBoundsValue(rightBounds, "MaxX", instanceFlags)) <= tolerance &&
            MathF.Abs(GetTerrainBoundsValue(leftBounds, "MaxY", instanceFlags) - GetTerrainBoundsValue(rightBounds, "MaxY", instanceFlags)) <= tolerance;
    }

    private static float GetTerrainBoundsValue(object bounds, string propertyName, BindingFlags instanceFlags)
    {
        return (float)bounds.GetType().GetProperty(propertyName, instanceFlags)!.GetValue(bounds)!;
    }

    private static int GetChunkBoundsValue(object bounds, string propertyName, BindingFlags instanceFlags)
    {
        return (int)bounds.GetType().GetProperty(propertyName, instanceFlags)!.GetValue(bounds)!;
    }

    private static bool ChunkBoundsContains(object outerBounds, object innerBounds, BindingFlags instanceFlags)
    {
        return GetChunkBoundsValue(innerBounds, "MinChunkX", instanceFlags) >= GetChunkBoundsValue(outerBounds, "MinChunkX", instanceFlags) &&
            GetChunkBoundsValue(innerBounds, "MaxChunkX", instanceFlags) <= GetChunkBoundsValue(outerBounds, "MaxChunkX", instanceFlags) &&
            GetChunkBoundsValue(innerBounds, "MinChunkY", instanceFlags) >= GetChunkBoundsValue(outerBounds, "MinChunkY", instanceFlags) &&
            GetChunkBoundsValue(innerBounds, "MaxChunkY", instanceFlags) <= GetChunkBoundsValue(outerBounds, "MaxChunkY", instanceFlags);
    }

    private static string FormatChunkBoundsForProbe(object bounds, BindingFlags instanceFlags)
    {
        return $"{GetChunkBoundsValue(bounds, "MinChunkX", instanceFlags)}..{GetChunkBoundsValue(bounds, "MaxChunkX", instanceFlags)}, " +
            $"{GetChunkBoundsValue(bounds, "MinChunkY", instanceFlags)}..{GetChunkBoundsValue(bounds, "MaxChunkY", instanceFlags)}";
    }

    private static int RunOverlapProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Console.WriteLine("OverlapProbe");
        RunOverlapScenario(
            "DynamicDynamicEqual",
            CreateProbeObject(1, "ProbeA", new Vector2(0f, 0f), mass: 20f, dynamicPhysics: true),
            CreateProbeObject(2, "ProbeB", new Vector2(0f, 0f), mass: 20f, dynamicPhysics: true));
        RunOverlapScenario(
            "DynamicDynamicHeavy",
            CreateProbeObject(3, "ProbeLight", new Vector2(0f, 0f), mass: 1f, dynamicPhysics: true),
            CreateProbeObject(4, "ProbeHeavy", new Vector2(0f, 0f), mass: 500f, dynamicPhysics: true));
        RunOverlapScenario(
            "DynamicStatic",
            CreateProbeObject(5, "ProbeMover", new Vector2(0f, 0f), mass: 20f, dynamicPhysics: true),
            CreateProbeObject(6, "ProbeWall", new Vector2(0f, 0f), mass: 500f, dynamicPhysics: false));
        return 0;
    }

    private static int RunDeadInputProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
        if (player == null)
        {
            Console.WriteLine("DeadInputProbe: player not found.");
            return 1;
        }

        ApplyDatabaseLoadout(player);
        core.Player = player;
        core.GameObjects.Add(player);

        player.CurrentHealth = 0f;
        player.IsDying = true;
        player.MovementVelocity = new Vector2(120f, 0f);
        player.PhysicsVelocity = new Vector2(120f, 0f);
        player.DeathImpulse = new Vector2(120f, 0f);

        bool fireInputSuppressed = !InputManager.IsInputActive("Fire");
        BulletManager.SpawnBullet(player);
        ActionHandler.Fire(player);
        int bulletCountAfterFireAttempts = BulletManager.GetBullets().Count;

        player.Update();

        bool motionCleared =
            player.MovementVelocity == Vector2.Zero &&
            player.PhysicsVelocity == Vector2.Zero;

        Console.WriteLine("DeadInputProbe");
        Console.WriteLine($"  inputSuppressed={InputManager.IsPlayerGameplayInputSuppressed}");
        Console.WriteLine($"  fireInputSuppressed={fireInputSuppressed}");
        Console.WriteLine($"  bulletCountAfterFireAttempts={bulletCountAfterFireAttempts}");
        Console.WriteLine($"  motionCleared={motionCleared}");

        return InputManager.IsPlayerGameplayInputSuppressed &&
               fireInputSuppressed &&
               bulletCountAfterFireAttempts == 0 &&
               motionCleared
            ? 0
            : 1;
    }

    private static int RunBulletBarrelCollisionProbe()
    {
        ResetRuntimeState();

        Core core = CreateHeadlessCore();
        Core.Instance = core;
        core.GameObjects = new List<GameObject>();
        core.StaticObjects = new List<GameObject>();
        core.PhysicsManager = new PhysicsManager();

        Attributes_Body body = new()
        {
            Mass = 3f,
            Speed = 1f,
            Control = 1f,
            Sight = 50f,
            BulletDamageResistance = 0f,
            CollisionDamageResistance = 0f
        };
        Attributes_Barrel barrel = new()
        {
            BulletDamage = 10f,
            BulletPenetration = 0f,
            BulletSpeed = 400f,
            ReloadSpeed = 1f,
            BulletMaxLifespan = 3f,
            BulletMass = 0.5f,
            BulletHealth = 20f,
            BulletFillAlphaRaw = -1,
            BulletOutlineAlphaRaw = -1,
            BulletOutlineWidth = -1
        };

        Shape ownerShape = new("Circle", 50, 50, 0, Color.White, Color.Transparent, 0);
        Agent owner = new(
            1,
            "ProbeOwner",
            Vector2.Zero,
            0f,
            body.Mass,
            isDestructible: true,
            isCollidable: true,
            dynamicPhysics: true,
            ownerShape,
            baseSpeed: 0f,
            isPlayer: true,
            Color.White,
            Color.Transparent,
            0,
            barrel,
            body);

        core.Player = owner;
        core.GameObjects.Add(owner);

        Core.DELTATIME = 1f / 60f;
        Core.GAMETIME = 0f;
        BulletManager.SpawnBullet(owner);
        Bullet? bullet = BulletManager.GetBullets().FirstOrDefault();
        bool initiallyLocked = bullet != null &&
            bullet.IsBarrelLocked &&
            bullet.IsOwnerImmune &&
            BulletManager.BarrelLockedBulletCount == 1 &&
            BulletManager.CollisionReadyBulletCount == 0;

        int frames = 0;
        while (bullet != null && bullet.IsBarrelLocked && frames < 120)
        {
            Core.GAMETIME += Core.DELTATIME;
            owner.PreviousPosition = owner.Position;
            BulletManager.Update();
            frames++;
        }

        bool exitedBarrel = bullet != null && !bullet.IsBarrelLocked;
        bool collisionReadyAtExit = exitedBarrel &&
            !bullet!.IsOwnerImmune &&
            BulletManager.BarrelLockedBulletCount == 0 &&
            BulletManager.CollisionReadyBulletCount == 1;

        float healthBefore = owner.CurrentHealth;
        if (bullet != null)
        {
            bullet.PreviousPosition = owner.Position;
            bullet.Position = owner.Position;
            bullet.Velocity = Vector2.Zero;
        }

        BulletCollisionSystem.Update(Core.DELTATIME);
        bool ownerCollisionAfterExit = owner.CurrentHealth < healthBefore;

        Console.WriteLine("BulletBarrelCollisionProbe");
        Console.WriteLine($"InitiallyLocked={initiallyLocked}");
        Console.WriteLine($"ExitedBarrel={exitedBarrel}");
        Console.WriteLine($"FramesUntilExit={frames}");
        Console.WriteLine($"CollisionReadyAtExit={collisionReadyAtExit}");
        Console.WriteLine($"OwnerHealthBefore={healthBefore:0.###}");
        Console.WriteLine($"OwnerHealthAfter={owner.CurrentHealth:0.###}");
        Console.WriteLine($"OwnerCollisionAfterExit={ownerCollisionAfterExit}");

        return initiallyLocked &&
            exitedBarrel &&
            collisionReadyAtExit &&
            ownerCollisionAfterExit
                ? 0
                : 1;
    }

    private static int RunShapeLazyLoadProbe()
    {
        using ShapeLazyLoadProbeGame game = new();
        game.Run();
        Console.Write(game.Report);
        return game.ExitCode;
    }

    private static void RunOverlapScenario(string name, GameObject objectA, GameObject objectB)
    {
        ResetFrameState();
        Core.Instance.GameObjects = new List<GameObject> { objectA, objectB };
        Core.Instance.StaticObjects = Core.Instance.GameObjects.Where(static go => !go.DynamicPhysics).ToList();

        bool finiteThroughout = true;
        bool separated = false;
        float maxSpeedA = 0f;
        float maxSpeedB = 0f;

        for (int frame = 0; frame < 90; frame++)
        {
            const float dt = 1f / 60f;
            Core.DELTATIME = dt;
            Core.GAMETIME += dt;
            foreach (GameObject gameObject in Core.Instance.GameObjects)
            {
                gameObject.PreviousPosition = gameObject.Position;
            }

            CollisionResolver.ResolveCollisions(Core.Instance.GameObjects);

            maxSpeedA = MathF.Max(maxSpeedA, objectA.PhysicsVelocity.Length());
            maxSpeedB = MathF.Max(maxSpeedB, objectB.PhysicsVelocity.Length());
            finiteThroughout &= IsFiniteVector(objectA.Position) &&
                               IsFiniteVector(objectB.Position) &&
                               IsFiniteVector(objectA.PhysicsVelocity) &&
                               IsFiniteVector(objectB.PhysicsVelocity);

            if (!CollisionManager.CheckCollision(objectA, objectB))
            {
                separated = true;
                break;
            }
        }

        Console.WriteLine(
            $"{name}: separated={separated} finite={finiteThroughout} " +
            $"posA=({objectA.Position.X:0.###},{objectA.Position.Y:0.###}) " +
            $"posB=({objectB.Position.X:0.###},{objectB.Position.Y:0.###}) " +
            $"maxSpeedA={maxSpeedA:0.###} maxSpeedB={maxSpeedB:0.###}");
    }

    private static GameObject CreateProbeObject(int id, string name, Vector2 position, float mass, bool dynamicPhysics)
    {
        Shape shape = new("Rectangle", 64, 64, 0, Color.White, Color.Transparent, 0);
        return new GameObject(
            id,
            name,
            position,
            0f,
            mass,
            isDestructible: false,
            isCollidable: true,
            dynamicPhysics: dynamicPhysics,
            shape,
            Color.White,
            Color.Transparent,
            0,
            registerWithShapeManager: false);
    }

    private static void RunScenario(string name, Agent player, Agent scout, bool includeWorldPhysics)
    {
        ResetFrameState();

        List<GameObject> scenarioObjects = includeWorldPhysics
            ? Core.Instance.GameObjects
            : Core.Instance.GameObjects.Where(static go => go is Agent).ToList();

        Core.Instance.GameObjects = scenarioObjects;
        Core.Instance.StaticObjects = scenarioObjects.Where(static go => !go.DynamicPhysics).ToList();
        Core.Instance.Player = player;

        scout.Position = new Vector2(420f, 100f);
        scout.PreviousPosition = scout.Position;
        scout.PhysicsVelocity = Vector2.Zero;
        scout.MovementVelocity = Vector2.Zero;

        player.Position = scout.Position + new Vector2(-220f, 0f);
        player.PreviousPosition = player.Position;
        player.PhysicsVelocity = Vector2.Zero;
        player.MovementVelocity = Vector2.Zero;
        player.Rotation = 0f;

        Vector2 startScoutPosition = scout.Position;
        BulletManager.SpawnBullet(player);

        Console.WriteLine($"Scenario {name}");
        Console.WriteLine($"  scout dynamic={scout.DynamicPhysics} collidable={scout.IsCollidable} mass={scout.Mass:0.###} bodyMass={scout.BodyAttributes.Mass:0.###}");

        bool knockbackObserved = false;
        bool contactObserved = false;
        float maxPhysicsVelocity = 0f;
        float maxDisplacement = 0f;

        for (int frame = 0; frame < 240; frame++)
        {
            StepFrame();

            float displacement = Vector2.Distance(scout.Position, startScoutPosition);
            float physicsSpeed = scout.PhysicsVelocity.Length();
            maxPhysicsVelocity = MathF.Max(maxPhysicsVelocity, physicsSpeed);
            maxDisplacement = MathF.Max(maxDisplacement, displacement);

            IReadOnlyList<Bullet> bullets = BulletManager.GetBullets();
            Bullet? bullet = bullets.Count > 0 ? bullets[0] : null;
            if (bullet != null && Vector2.Distance(bullet.Position, scout.Position) <= ((bullet.Shape.Width + scout.Shape.Width) * 0.5f + 2f))
            {
                contactObserved = true;
            }

            if (!knockbackObserved && (physicsSpeed > 0.01f || displacement > 0.01f))
            {
                knockbackObserved = true;
                Console.WriteLine($"  first response frame={frame} scoutPos=({scout.Position.X:0.###},{scout.Position.Y:0.###}) scoutVel=({scout.PhysicsVelocity.X:0.###},{scout.PhysicsVelocity.Y:0.###})");
            }
        }

        Console.WriteLine($"  contactObserved={contactObserved}");
        Console.WriteLine($"  maxPhysicsVelocity={maxPhysicsVelocity:0.###}");
        Console.WriteLine($"  maxDisplacement={maxDisplacement:0.###}");
        Console.WriteLine();
    }

    private static void StepFrame()
    {
        const float dt = 1f / 60f;
        Core.DELTATIME = dt;
        Core.GAMETIME += dt;

        foreach (GameObject go in Core.Instance.GameObjects)
        {
            go.PreviousPosition = go.Position;
        }

        foreach (GameObject go in Core.Instance.GameObjects)
        {
            go.Update();
        }

        BulletManager.Update();
        BulletCollisionResolver.ResolveCollisions(BulletManager.GetBullets(), Core.Instance.GameObjects);
        BulletCollisionSystem.Update(dt);
        PhysicsManager.Update(Core.Instance.GameObjects);
    }

    private static void ApplyDatabaseLoadout(Agent agent)
    {
        List<Attributes_Barrel> barrels = BarrelLoader.LoadBarrelsForAgent(agent.ID);
        if (barrels.Count > 0)
        {
            agent.ClearBarrels();
            foreach (Attributes_Barrel barrel in barrels)
            {
                agent.AddBarrel(barrel);
            }
        }

        List<(string Name, Attributes_Body Attrs, Color? FillColor, Color? OutlineColor, int? OutlineWidth)> bodies = BodyLoader.LoadBodiesForAgent(agent.ID);
        if (bodies.Count > 0)
        {
            agent.ClearBodies();
            for (int i = 0; i < bodies.Count; i++)
            {
                agent.AddBody(bodies[i].Attrs);
                if (!string.IsNullOrWhiteSpace(bodies[i].Name))
                {
                    agent.Bodies[i].Name = bodies[i].Name;
                }

                if (bodies[i].FillColor is Color fillColor)
                {
                    agent.Bodies[i].FillColor = fillColor;
                }

                if (bodies[i].OutlineColor is Color outlineColor)
                {
                    agent.Bodies[i].OutlineColor = outlineColor;
                }

                if (bodies[i].OutlineWidth is int outlineWidth)
                {
                    agent.Bodies[i].OutlineWidth = outlineWidth;
                }
            }
        }
    }

    private sealed class ShapeLazyLoadProbeGame : Core
    {
        private SpriteBatch? _spriteBatch;
        private GameObject? _probeObject;
        private bool _rendered;

        public int ExitCode { get; private set; } = 1;
        public string Report { get; private set; } = string.Empty;

        public ShapeLazyLoadProbeGame()
        {
            Graphics.PreferredBackBufferWidth = 320;
            Graphics.PreferredBackBufferHeight = 240;
            Graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            Window.Title = "op.io shape lazy load probe";
        }

        protected override void Initialize()
        {
            ResetRuntimeState();
            Core.Instance = this;
            GameObjects = new List<GameObject>();
            StaticObjects = new List<GameObject>();
            PhysicsManager = new PhysicsManager();

            Shape shape = new("Rectangle", 32, 32, 0, Color.White, Color.Transparent, 0);
            _probeObject = new GameObject(
                -9101,
                "LazyShapeProbe",
                Vector2.Zero,
                0f,
                1f,
                isDestructible: false,
                isCollidable: false,
                dynamicPhysics: false,
                shape,
                Color.White,
                Color.Transparent,
                0,
                registerWithShapeManager: false);
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            SpriteBatch = _spriteBatch;
        }

        protected override void LoadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            if (_rendered)
            {
                Exit();
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_rendered)
            {
                return;
            }

            int runtimeLoadsBefore = Shape.RuntimeContentLoadCount;
            int skippedBefore = Shape.DrawSkippedMissingTextureCount;
            try
            {
                GraphicsDevice.Clear(Color.Black);
                _spriteBatch!.Begin();
                _probeObject!.Shape.Draw(_spriteBatch, _probeObject);
                _spriteBatch.End();

                int runtimeLoadsAfter = Shape.RuntimeContentLoadCount;
                int skippedAfter = Shape.DrawSkippedMissingTextureCount;
                bool lazyLoaded = runtimeLoadsAfter > runtimeLoadsBefore;
                bool noSkippedDraw = skippedAfter == skippedBefore;

                Report =
                    "ShapeLazyLoadProbe\n" +
                    $"RuntimeLoadsBefore={runtimeLoadsBefore}\n" +
                    $"RuntimeLoadsAfter={runtimeLoadsAfter}\n" +
                    $"SkippedBefore={skippedBefore}\n" +
                    $"SkippedAfter={skippedAfter}\n" +
                    $"LazyLoaded={lazyLoaded}\n" +
                    $"NoSkippedDraw={noSkippedDraw}\n";
                ExitCode = lazyLoaded && noSkippedDraw ? 0 : 1;
            }
            catch (Exception ex)
            {
                Report = $"ShapeLazyLoadProbe\nFAIL: {ex.GetBaseException().Message}\n{ex}\n";
                ExitCode = 1;
            }

            _rendered = true;
            Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spriteBatch?.Dispose();
                _probeObject?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class OceanDebugRenderProbeGame : Core
    {
        private const int RenderWidth = 960;
        private const int RenderHeight = 540;
        private const int Seed = 1337;
        private static readonly RasterizerState PoisonScissorRasterizerState = new()
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };

        private SpriteBatch? _spriteBatch;
        private Texture2D? _pixel;
        private RenderTarget2D? _target;
        private Type? _terrainType;
        private MethodInfo? _drawFinalOverlayMethod;
        private readonly Color[] _pixels = new Color[RenderWidth * RenderHeight];
        private readonly List<int> _redPixelCounts = new();
        private Vector2[] _probeCameraCenters = [Vector2.Zero, Vector2.Zero, Vector2.Zero];
        private bool _probeCameraCentersResolved;
        private bool _rendered;

        public int ExitCode { get; private set; } = 1;
        public string Report { get; private set; } = string.Empty;

        public OceanDebugRenderProbeGame()
        {
            Graphics.PreferredBackBufferWidth = RenderWidth;
            Graphics.PreferredBackBufferHeight = RenderHeight;
            Graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            Window.Title = "op.io ocean debug render probe";
        }

        protected override void Initialize()
        {
            Core.ForceDebugMode = true;
            Core.GAMETIME = 0f;
            Core.DELTATIME = 1f / 60f;
            GameObjects = new List<GameObject>();
            StaticObjects = new List<GameObject>();
            PhysicsManager = new PhysicsManager();

            Agent? player = AgentLoader.LoadAgents().FirstOrDefault(static agent => agent.IsPlayer);
            if (player != null)
            {
                ApplyDatabaseLoadout(player);
                Attributes_Body body = player.BodyAttributes;
                body.Sight = Math.Max(body.Sight, 45f);
                player.BodyAttributes = body;
                player.Position = Vector2.Zero;
                player.PreviousPosition = Vector2.Zero;
                Player = player;
                GameObjects.Add(player);
            }

            Assembly gameplayAssembly = typeof(Core).Assembly;
            Type terrainType = gameplayAssembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
            _terrainType = terrainType;
            BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            PrepareTerrainForSeed(terrainType, staticFlags, Seed);

            MethodInfo initializeMethod = terrainType.GetMethod(
                "Initialize",
                staticFlags,
                binder: null,
                [typeof(GraphicsDevice)],
                modifiers: null)!;
            initializeMethod.Invoke(null, [GraphicsDevice]);

            MethodInfo prepareStartupAroundMethod = terrainType.GetMethod(
                "PrepareStartupTerrainAroundWorldPosition",
                staticFlags,
                binder: null,
                [typeof(Vector2)],
                modifiers: null)!;
            prepareStartupAroundMethod.Invoke(null, [Vector2.Zero]);

            MethodInfo prepareStartupVisibleMethod = terrainType.GetMethod(
                "PrepareStartupVisibleTerrain",
                staticFlags,
                binder: null,
                [typeof(GraphicsDevice), typeof(Rectangle), typeof(Matrix)],
                modifiers: null)!;
            prepareStartupVisibleMethod.Invoke(
                null,
                [GraphicsDevice, new Rectangle(0, 0, RenderWidth, RenderHeight), BuildCamera(Vector2.Zero)]);

            _drawFinalOverlayMethod = terrainType.GetMethod(
                "DrawOceanZoneDebugFinalOverlay",
                staticFlags,
                binder: null,
                [typeof(SpriteBatch), typeof(Matrix)],
                modifiers: null)!;

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            SpriteBatch = _spriteBatch;
            _pixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            _pixel.SetData([Color.White]);
            _target = new RenderTarget2D(GraphicsDevice, RenderWidth, RenderHeight, false, SurfaceFormat.Color, DepthFormat.None);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_rendered)
            {
                Exit();
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_rendered)
            {
                return;
            }

            try
            {
                RenderProbeFrames();
            }
            catch (Exception ex)
            {
                Report = $"OceanDebugRenderProbe\nFAIL: {ex.GetBaseException().Message}\n{ex}\n";
                ExitCode = 1;
            }

            _rendered = true;
            Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _target?.Dispose();
                _pixel?.Dispose();
                _spriteBatch?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void RenderProbeFrames()
        {
            if (_spriteBatch == null || _pixel == null || _target == null || _drawFinalOverlayMethod == null)
            {
                throw new InvalidOperationException("Render probe resources were not initialized.");
            }

            const int maxProbeFrames = 240;
            const int redPixelVisibilityThreshold = 50;
            const double maxAllowedOverlayMilliseconds = 25.0;
            Matrix[] cameras =
            [
                BuildCamera(new Vector2(0f, 0f)),
                BuildCamera(new Vector2(24f, -18f)),
                BuildCamera(new Vector2(48f, -36f))
            ];
            int[] visibleFramesByCamera = new int[cameras.Length];
            double maxOverlayMilliseconds = 0.0;
            double maxWarmOverlayMilliseconds = 0.0;

            for (int frameIndex = 0; frameIndex < maxProbeFrames; frameIndex++)
            {
                if (!_probeCameraCentersResolved &&
                    TryResolveOceanDebugProbeCameraCenters(out Vector2[] resolvedCenters))
                {
                    _probeCameraCenters = resolvedCenters;
                    _probeCameraCentersResolved = true;
                    Array.Clear(visibleFramesByCamera);
                    _redPixelCounts.Clear();
                }

                int cameraIndex = frameIndex % cameras.Length;
                Vector2 cameraCenter = _probeCameraCentersResolved
                    ? _probeCameraCenters[cameraIndex]
                    : new Vector2(cameraIndex * 24f, cameraIndex * -18f);
                cameras[cameraIndex] = BuildCamera(cameraCenter);
                if (Player != null)
                {
                    Player.Position = cameraCenter;
                    Player.PreviousPosition = cameraCenter;
                }

                Core.GAMETIME = frameIndex / 60f;
                GraphicsDevice.SetRenderTarget(_target);
                GraphicsDevice.Viewport = new Viewport(0, 0, RenderWidth, RenderHeight);
                GraphicsDevice.Clear(new Color(8, 18, 31, 255));

                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, RenderWidth, RenderHeight), new Color(12, 54, 72, 255));
                _spriteBatch.End();

                GraphicsDevice.ScissorRectangle = new Rectangle(3, 5, 17, 19);
                GraphicsDevice.RasterizerState = PoisonScissorRasterizerState;
                Stopwatch overlayStopwatch = Stopwatch.StartNew();
                _drawFinalOverlayMethod.Invoke(null, [_spriteBatch, cameras[cameraIndex]]);
                overlayStopwatch.Stop();
                maxOverlayMilliseconds = Math.Max(maxOverlayMilliseconds, overlayStopwatch.Elapsed.TotalMilliseconds);
                if (frameIndex >= cameras.Length)
                {
                    maxWarmOverlayMilliseconds = Math.Max(maxWarmOverlayMilliseconds, overlayStopwatch.Elapsed.TotalMilliseconds);
                }

                GraphicsDevice.SetRenderTarget(null);
                _target.GetData(_pixels);
                int redPixels = CountOceanDebugRedPixels(_pixels);
                _redPixelCounts.Add(redPixels);
                if (redPixels >= redPixelVisibilityThreshold)
                {
                    visibleFramesByCamera[cameraIndex]++;
                }

                bool allCamerasHaveVisibleBorders = true;
                for (int i = 0; i < visibleFramesByCamera.Length; i++)
                {
                    if (visibleFramesByCamera[i] <= 0)
                    {
                        allCamerasHaveVisibleBorders = false;
                        break;
                    }
                }

                if (allCamerasHaveVisibleBorders && frameIndex >= cameras.Length)
                {
                    break;
                }

                System.Threading.Thread.Sleep(10);
            }

            int minRedPixels = _redPixelCounts.Count > 0 ? _redPixelCounts.Min() : 0;
            int maxRedPixels = _redPixelCounts.Count > 0 ? _redPixelCounts.Max() : 0;
            bool allCamerasVisible = visibleFramesByCamera.All(static count => count > 0);
            bool noMainThreadLag = maxWarmOverlayMilliseconds <= maxAllowedOverlayMilliseconds;
            int sampleStart = Math.Max(0, _redPixelCounts.Count - 18);
            string sampledRedPixels = string.Join(",", _redPixelCounts.Skip(sampleStart));

            Report =
                "OceanDebugRenderProbe\n" +
                $"Seed={Seed}\n" +
                $"SampledFrameRedPixels={sampledRedPixels}\n" +
                $"MinRedPixels={minRedPixels}\n" +
                $"MaxRedPixels={maxRedPixels}\n" +
                $"VisibleFramesByCamera={string.Join(",", visibleFramesByCamera)}\n" +
                $"MaxOverlayMilliseconds={maxOverlayMilliseconds:0.###}\n" +
                $"MaxWarmOverlayMilliseconds={maxWarmOverlayMilliseconds:0.###}\n" +
                $"TerrainOceanDebugSegments={GetTerrainPropertyValue("TerrainOceanDebugBorderSegmentCount")}\n" +
                $"TerrainOceanDebugCache={GetTerrainPropertyValue("TerrainOceanDebugTileCacheCount")}\n" +
                $"TerrainOceanDebugQueued={GetTerrainPropertyValue("TerrainOceanDebugQueuedTileCount")}\n" +
                $"TerrainOceanDebugActive={GetTerrainPropertyValue("TerrainOceanDebugActiveTileBuildCount")}\n" +
                $"TerrainOceanDebugCompleted={GetTerrainPropertyValue("TerrainOceanDebugCompletedTileQueueCount")}\n" +
                $"TerrainOceanDebugWorkerStatus={GetTerrainPropertyValue("TerrainOceanDebugWorkerStatus")}\n" +
                $"TerrainOceanDebugSuppressedTinyZones={GetTerrainPropertyValue("TerrainOceanDebugSuppressedTinyZoneCount")}\n" +
                $"TerrainOceanDebugTinyZoneViolations={GetTerrainPropertyValue("TerrainOceanDebugTinyZoneViolationSummary")}\n" +
                $"ProbeCameraCentersResolved={_probeCameraCentersResolved}\n" +
                $"AllCamerasVisible={allCamerasVisible}\n" +
                $"NoMainThreadLag={noMainThreadLag}\n";
            ExitCode = allCamerasVisible && noMainThreadLag ? 0 : 1;
        }

        private bool TryResolveOceanDebugProbeCameraCenters(out Vector2[] centers)
        {
            centers = [Vector2.Zero, Vector2.Zero, Vector2.Zero];
            if (_terrainType == null ||
                GetTerrainPropertyValue("TerrainOceanDebugFullMapReady") is not bool ready ||
                !ready)
            {
                return false;
            }

            FieldInfo? segmentsField = _terrainType.GetField(
                "FullOceanZoneDebugSegments",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (segmentsField?.GetValue(null) is not IEnumerable segments)
            {
                return false;
            }

            int count = 0;
            foreach (object segment in segments)
            {
                Type segmentType = segment.GetType();
                if (segmentType.GetProperty("From")?.GetValue(segment) is not Vector2 from ||
                    segmentType.GetProperty("To")?.GetValue(segment) is not Vector2 to)
                {
                    continue;
                }

                Vector2 midpoint = (from + to) * 0.5f;
                if (!IsFiniteVector(midpoint))
                {
                    continue;
                }

                bool farEnough = true;
                for (int i = 0; i < count; i++)
                {
                    if (Vector2.DistanceSquared(centers[i], midpoint) < 128f * 128f)
                    {
                        farEnough = false;
                        break;
                    }
                }

                if (!farEnough && count > 0)
                {
                    continue;
                }

                centers[count] = midpoint;
                count++;
                if (count >= centers.Length)
                {
                    return true;
                }
            }

            for (int i = count; i < centers.Length && count > 0; i++)
            {
                centers[i] = centers[0];
            }

            return count > 0;
        }

        private object? GetTerrainPropertyValue(string propertyName)
        {
            return _terrainType?
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
                .GetValue(null);
        }

        private static Matrix BuildCamera(Vector2 cameraOffset)
        {
            const float zoom = 0.25f;
            return Matrix.CreateTranslation(-cameraOffset.X, -cameraOffset.Y, 0f)
                * Matrix.CreateScale(zoom, zoom, 1f)
                * Matrix.CreateTranslation(RenderWidth / 2f, RenderHeight / 2f, 0f);
        }

        private static int CountOceanDebugRedPixels(Color[] pixels)
        {
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                bool cyanOrBlueBorder = pixel.G >= 130 && pixel.B >= 170 && pixel.R <= 180;
                bool violetBorder = pixel.R >= 95 && pixel.B >= 170 && pixel.G <= 170;
                bool orangeBorder = pixel.R >= 200 && pixel.G >= 130 && pixel.B <= 120;
                if (pixel.A >= 180 && (cyanOrBlueBorder || violetBorder || orangeBorder))
                {
                    count++;
                }
            }

            return count;
        }
    }

    private static bool IsTerrainFreeForProbe(Vector2 worldPosition, float clearanceRadiusWorldUnits)
    {
        Type terrainType = typeof(Core).Assembly.GetType("op.io.GameBlockTerrainBackground", throwOnError: true)!;
        MethodInfo method = terrainType.GetMethod(
            "IsTerrainFreeWorldPosition",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(Vector2), typeof(float)],
            modifiers: null)!;
        return (bool)method.Invoke(null, [worldPosition, clearanceRadiusWorldUnits])!;
    }

    private static GameObject CreateSpawnBlocker(Agent player, Vector2 position)
    {
        int width = Math.Max(24, player.Shape?.Width ?? 64);
        int height = Math.Max(24, player.Shape?.Height ?? 64);
        Shape shape = new("Rectangle", width, height, 0, Color.Red, Color.Transparent, 0);
        return new GameObject(
            -9001,
            "SpawnBlocker",
            position,
            player.Rotation,
            0f,
            isDestructible: false,
            isCollidable: true,
            dynamicPhysics: false,
            shape,
            Color.Red,
            Color.Transparent,
            0,
            registerWithShapeManager: false);
    }

    private static Core CreateHeadlessCore()
    {
        return new Core();
    }

    private static void ResetRuntimeState()
    {
        ResetFrameState();
        GameObjectRegister.ClearAllGameObjects();
    }

    private static void ResetFrameState()
    {
        Core.GAMETIME = 0f;
        Core.DELTATIME = 1f / 60f;
        ClearBulletManager();
        ClearBulletCollisionSystemContacts();
    }

    private static void ClearBulletManager()
    {
        FieldInfo bulletsField = typeof(BulletManager).GetField("_bullets", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo toRemoveField = typeof(BulletManager).GetField("_toRemove", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((IList<Bullet>)bulletsField.GetValue(null)!).Clear();
        ((HashSet<Bullet>)toRemoveField.GetValue(null)!).Clear();
    }

    private static void ClearBulletCollisionSystemContacts()
    {
        FieldInfo prevField = typeof(BulletCollisionSystem).GetField("_prevContacts", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo currField = typeof(BulletCollisionSystem).GetField("_currContacts", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((HashSet<long>)prevField.GetValue(null)!).Clear();
        ((HashSet<long>)currField.GetValue(null)!).Clear();
    }

    private static bool IsFiniteVector(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
