#nullable enable

using System.Collections;
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
                string.Equals(args[0], "terrain-phase", StringComparison.OrdinalIgnoreCase))
            {
                return RunTerrainPhaseProbe();
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

        const int chunkTextureResolution = 128;
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
        Console.WriteLine($"BorderColliderCount={colliderCount}");
        Console.WriteLine($"VisualTriangleCount={triangleCount}");
        Console.WriteLine($"HitboxMatchesVisual={hitboxMatchesVisual}");
        Console.WriteLine($"BuildMilliseconds={buildMilliseconds:0.###}");

        return edgeLoopCount > 0 && colliderCount > 0 && triangleCount > 0 && hitboxMatchesVisual ? 0 : 1;
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
        FieldInfo lastMaterializedChunkWindowField = terrainType.GetField("_lastMaterializedChunkWindow", staticFlags)!;
        FieldInfo lastTerrainColliderChunkWindowField = terrainType.GetField("_lastTerrainColliderChunkWindow", staticFlags)!;
        FieldInfo residentTerrainVisualObjectsField = terrainType.GetField("ResidentTerrainVisualObjects", staticFlags)!;

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

            const int chunkTextureResolution = 128;
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
        MethodInfo createTerrainColliderObjectMethod = terrainType.GetMethod(
            "CreateTerrainColliderObject",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        GameObject terrainCollider = (GameObject)createTerrainColliderObjectMethod.Invoke(
            null,
            [new Vector2(16f, 0f), MathF.PI / 2f, 128f, 16f])!;
        Vector2 terrainImpulse = SimulateStaticWallImpulse(
            200,
            terrainCollider);
        Vector2 terrainFallbackImpulse = SimulateTerrainFallbackImpulse(
            redWallImpulse,
            alreadyHasColliderImpulse: false);
        Vector2 terrainFallbackAfterColliderImpulse = SimulateTerrainFallbackImpulse(
            redWallImpulse,
            alreadyHasColliderImpulse: true);

        bool redWallBounced = redWallImpulse.X < -1f;
        bool redWallAgentBounced = redWallAgentImpulse.X < -1f;
        bool redWallAgentMovementClipped = redWallAgentMovement.X <= 0.001f;
        bool terrainBounced = terrainImpulse.X < -1f;
        bool terrainAllowsImpulse = !terrainCollider.SuppressCollisionImpulse;
        bool fallbackMatchesWall = MathF.Abs(terrainFallbackImpulse.X - redWallImpulse.X) <= 0.001f;
        bool fallbackDoesNotBoostWallImpulse = MathF.Abs(terrainFallbackAfterColliderImpulse.X - redWallImpulse.X) <= 0.001f;

        Console.WriteLine("TerrainImpulseProbe");
        Console.WriteLine($"  redWallImpulse=({redWallImpulse.X:0.###},{redWallImpulse.Y:0.###}) bounced={redWallBounced}");
        Console.WriteLine($"  redWallAgentImpulse=({redWallAgentImpulse.X:0.###},{redWallAgentImpulse.Y:0.###}) bounced={redWallAgentBounced}");
        Console.WriteLine($"  redWallAgentMovement=({redWallAgentMovement.X:0.###},{redWallAgentMovement.Y:0.###}) clipped={redWallAgentMovementClipped}");
        Console.WriteLine($"  terrainImpulse=({terrainImpulse.X:0.###},{terrainImpulse.Y:0.###}) bounced={terrainBounced}");
        Console.WriteLine($"  terrainFallbackImpulse=({terrainFallbackImpulse.X:0.###},{terrainFallbackImpulse.Y:0.###}) matchesWall={fallbackMatchesWall}");
        Console.WriteLine($"  terrainFallbackAfterColliderImpulse=({terrainFallbackAfterColliderImpulse.X:0.###},{terrainFallbackAfterColliderImpulse.Y:0.###}) noBoost={fallbackDoesNotBoostWallImpulse}");
        Console.WriteLine($"  terrainSuppressCollisionImpulse={terrainCollider.SuppressCollisionImpulse}");

        return redWallBounced &&
               redWallAgentBounced &&
               redWallAgentMovementClipped &&
               terrainBounced &&
               terrainAllowsImpulse &&
               fallbackMatchesWall &&
               fallbackDoesNotBoostWallImpulse
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
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo overlapsTerrainAtCollisionHullMethod = terrainType.GetMethod(
            "OverlapsTerrainAtCollisionHull",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        const float terrainMaxX = 4096f;
        const float coreRadius = 25f;
        const float outlineWidth = 5f;

        loadSettingsIfNeededMethod.Invoke(null, []);
        ensureTerrainWorldBoundsInitializedMethod.Invoke(null, []);
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
        if (visualObjects.Count == 0 || collisionLoops.Count != visualObjects.Count)
        {
            return false;
        }

        applyTerrainMaterializationResultMethod.Invoke(null, [materializationResult]);

        Vector2? insideSample = null;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int visualIndex = 0; visualIndex < visualObjects.Count; visualIndex++)
        {
            object visualObject = visualObjects[visualIndex]!;
            object bounds = visualObject.GetType().GetProperty("Bounds", instanceFlags)!.GetValue(visualObject)!;
            maxX = MathF.Max(maxX, (float)bounds.GetType().GetProperty("MaxX", instanceFlags)!.GetValue(bounds)!);
            maxY = MathF.Max(maxY, (float)bounds.GetType().GetProperty("MaxY", instanceFlags)!.GetValue(bounds)!);

            if (insideSample.HasValue)
            {
                continue;
            }

            VertexPositionColor[] vertices = (VertexPositionColor[])visualObject
                .GetType()
                .GetProperty("FillVertices", instanceFlags)!
                .GetValue(visualObject)!;
            if (vertices.Length < 3)
            {
                continue;
            }

            Vector3 a = vertices[0].Position;
            Vector3 b = vertices[1].Position;
            Vector3 c = vertices[2].Position;
            insideSample = new Vector2(
                (a.X + b.X + c.X) / 3f,
                (a.Y + b.Y + c.Y) / 3f);
        }

        if (!insideSample.HasValue)
        {
            return false;
        }

        bool insideVisualHitsTerrain = (bool)overlapsTerrainAtWorldPositionMethod.Invoke(
            null,
            [insideSample.Value, 1f])!;
        bool outsideVisualDoesNotHitTerrain = !(bool)overlapsTerrainAtWorldPositionMethod.Invoke(
            null,
            [new Vector2(maxX + 512f, maxY + 512f), 1f])!;
        return insideVisualHitsTerrain && outsideVisualDoesNotHitTerrain;
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
