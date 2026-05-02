using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameObjectInitializer
    {
        private const int FarmSpawnPlacementMaxAttempts = 48;
        private const float FarmSpawnClearancePaddingWorldUnits = 10f;
        private const float PlayerSpawnClearancePaddingWorldUnits = 8f;
        private const float PlayerSpawnMaxSearchDistanceWorldUnits = 1536f;
        private const int PlayerSpawnMaxRingSamples = 192;

        private static bool _playerSpawnRelocated;
        private static float _playerSpawnRelocationDistance;
        private static int _playerSpawnSearchAttempts;

        public static bool PlayerSpawnRelocated => _playerSpawnRelocated;
        public static float PlayerSpawnRelocationDistance => _playerSpawnRelocationDistance;
        public static int PlayerSpawnSearchAttempts => _playerSpawnSearchAttempts;

        public static void Initialize()
        {
            DebugLogger.PrintGO("Initializing GameObjects...");

            GameObjectRegister.ClearAllGameObjects();
            Core.Instance.GameObjects = new List<GameObject>();
            Core.Instance.StaticObjects = new List<GameObject>();
            InitializeActiveLevel(loadContent: false);

            DebugLogger.PrintGO($"Initialization complete. Total GameObjects: {Core.Instance.GameObjects.Count}, StaticObjects: {Core.Instance.StaticObjects.Count}");
        }

        public static void ReloadActiveLevel()
        {
            try
            {
                DebugLogger.PrintGO($"Reloading active level: {GameLevelManager.ActiveLevelName}");

                GameBlockTerrainBackground.ResetRuntimeTerrainObjectsForLevelLoad();
                List<GameObject> preservedRuntimeObjects = CollectPreservedRuntimeObjects();
                DisposeLevelManagedObjects(preservedRuntimeObjects);

                Core.Instance.GameObjects = new List<GameObject>(preservedRuntimeObjects);
                Core.Instance.StaticObjects = preservedRuntimeObjects
                    .Where(IsPreservedRuntimeStaticObject)
                    .ToList();
                Core.Instance.Player = null;

                GameUpdater.ResetSceneTransientState();
                BulletManager.Clear();
                DamageNumberManager.Clear();
                HealthBarManager.Clear();
                ZoneBlockDetector.Reset();
                InspectModeState.ClearTargets();
                XPClumpManager.Reset();

                InitializeActiveLevel(loadContent: true);

                DebugLogger.PrintGO($"Level reload complete. Active={GameLevelManager.ActiveLevelName}, GameObjects={Core.Instance.GameObjects.Count}, StaticObjects={Core.Instance.StaticObjects.Count}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in ReloadActiveLevel: {ex.Message}");
            }
        }

        private static void InitializeActiveLevel(bool loadContent)
        {
            GameLevelDefinition level = GameLevelManager.ActiveLevel;
            DebugLogger.PrintGO($"Initializing level '{level.DisplayName}' with loadout: {GameLevelManager.BuildLevelLoadoutSummary(level)}");

            ResetPlayerSpawnTelemetry();
            XPClumpManager.Reset();
            GameObjectManager.SeedNextID();

            if (level.LoadMapObjects)
            {
                InitializeMapObjects();
            }
            else
            {
                DebugLogger.PrintGO("Level skips map objects.");
            }

            if (level.LoadFarms)
            {
                InitializeFarms();
            }
            else
            {
                DebugLogger.PrintGO("Level skips farms.");
            }

            InitializeBlocks();

            if (level.SpawnPlayer || level.LoadAgents || level.LoadsSelectedAgents)
            {
                InitializeAgents(
                    includeNonPlayerAgents: level.LoadAgents,
                    requirePlayer: level.SpawnPlayer,
                    includedAgentNames: level.IncludedAgentNames);
            }
            else
            {
                Core.Instance.Player = null;
                GameBlockTerrainBackground.PrepareStartupTerrainAroundWorldPosition(Vector2.Zero);
                DebugLogger.PrintGO("Level skips agents.");
            }

            if (level.LoadZoneBlocks)
            {
                InitializeZoneBlocks();
            }
            else
            {
                DebugLogger.PrintGO("Level skips zone blocks.");
            }

            ResolveTerrainSpawnOverlaps();
            ResolveStartupCollidableOverlaps();

            if (loadContent)
            {
                LoadSceneContent();
            }
        }

        private static List<GameObject> CollectPreservedRuntimeObjects()
        {
            List<GameObject> preserved = new();
            if (Core.Instance?.GameObjects == null)
            {
                return preserved;
            }

            foreach (GameObject gameObject in Core.Instance.GameObjects)
            {
                if (IsPreservedRuntimeObject(gameObject) && !preserved.Contains(gameObject))
                {
                    preserved.Add(gameObject);
                }
            }

            return preserved;
        }

        private static bool IsPreservedRuntimeObject(GameObject gameObject)
        {
            return false;
        }

        private static bool IsPreservedRuntimeStaticObject(GameObject gameObject)
        {
            return IsPreservedRuntimeObject(gameObject) && !gameObject.DynamicPhysics;
        }

        private static void DisposeLevelManagedObjects(IReadOnlyCollection<GameObject> preservedRuntimeObjects)
        {
            HashSet<GameObject> preserved = preservedRuntimeObjects != null
                ? new HashSet<GameObject>(preservedRuntimeObjects)
                : new HashSet<GameObject>();
            HashSet<GameObject> disposed = new();

            DisposeObjects(Core.Instance?.GameObjects, preserved, disposed);
            DisposeObjects(Core.Instance?.StaticObjects, preserved, disposed);
        }

        private static void DisposeObjects(IEnumerable<GameObject> objects, HashSet<GameObject> preserved, HashSet<GameObject> disposed)
        {
            if (objects == null)
            {
                return;
            }

            foreach (GameObject gameObject in objects)
            {
                if (gameObject == null || preserved.Contains(gameObject) || !disposed.Add(gameObject))
                {
                    continue;
                }

                gameObject.Dispose();
            }
        }

        private static void LoadSceneContent()
        {
            if (Core.Instance?.GraphicsDevice == null || Core.Instance.GameObjects == null)
            {
                return;
            }

            foreach (GameObject gameObject in Core.Instance.GameObjects)
            {
                gameObject?.LoadContent(Core.Instance.GraphicsDevice);
                if (gameObject is Agent agent)
                {
                    foreach (var slot in agent.Barrels)
                    {
                        slot.FullShape?.LoadContent(Core.Instance.GraphicsDevice);
                    }
                }
            }
        }

        private static void InitializeMapObjects()
        {
            try
            {
                DebugLogger.PrintGO("Initializing MapObjects...");

                var mapObjects = MapObjectLoader.LoadMapObjects();

                if (mapObjects.Count > 0)
                {
                    Core.Instance.StaticObjects.AddRange(mapObjects);
                    Core.Instance.GameObjects.AddRange(mapObjects);
                    DebugLogger.PrintGO($"MapObjects loaded: {mapObjects.Count} total");
                }
                else
                {
                    DebugLogger.PrintWarning("No map objects were loaded.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializeMapObjects: {ex.Message}");
            }
        }

        private static void InitializeFarms()
        {
            try
            {
                DebugLogger.PrintGO("Initializing Farms...");

                // Load farm prototypes (GameObjects) and farm data (Count values)
                var farmProtos = FarmProtoLoader.LoadFarmPrototypes();
                var farmData = FarmDataLoader.GetFarmData();

                // Log the number of farm prototypes loaded
                DebugLogger.PrintGO($"Farm Prototypes loaded: {farmProtos.Count}");

                // Log the number of farm data entries loaded
                DebugLogger.PrintGO($"Farm Data loaded: {farmData.Count}");

                // Log details of each farm prototype
                foreach (var proto in farmProtos)
                {
                    DebugLogger.PrintGO($"Farm Prototype: ID={proto.ID}, Name={proto.Name}");
                }

                // Instantiate the farms based on the farm data
                var farms = InstantiateFarms(farmProtos, farmData, Core.Instance.ViewportWidth, Core.Instance.ViewportHeight);

                // Log the number of farms instantiated
                DebugLogger.PrintGO($"Farms instantiated: {farms.Count}");

                if (farms.Count != 0)
                {
                    Core.Instance.GameObjects.AddRange(farms);
                    DebugLogger.PrintGO($"Farms loaded into game: {farms.Count} total");
                }
                else
                {
                    DebugLogger.PrintWarning("No farms loaded.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializeFarms: {ex.Message}");
            }
        }

        private static List<GameObject> InstantiateFarms(List<GameObject> farmProtos, List<FarmData> farmData, int viewportWidth, int viewportHeight)
        {
            var farms = new List<GameObject>();
            Random random = new();

            // Track total iterations and total farms created
            int totalIterations = 0;

            foreach (var proto in farmProtos)
            {
                var matchingData = farmData.FirstOrDefault(f => f.ID == proto.ID);
                if (matchingData == null) continue;

                if (!SimpleGameObject.TryFromGameObject(proto, out SimpleGameObject baseArchetype))
                {
                    DebugLogger.PrintWarning($"Farm prototype ID={proto?.ID} is missing shape data. Skipping.");
                    continue;
                }

                FarmGameObject farmArchetype = new(baseArchetype, matchingData);

                // Debugging meta info before instantiating
                DebugLogger.PrintGO($"Processing prototype: {proto.Name}, ID: {proto.ID}. Farms to instantiate: {matchingData.Count}");

                // Track the total iterations per prototype
                totalIterations += matchingData.Count;

                // Check farm data consistency
                if (matchingData.Count > 0)
                {
                    DebugLogger.PrintGO($"Instantiating {matchingData.Count} farms for prototype {proto.Name} (ID={proto.ID})");
                }

                // Debugging each farm being instantiated
                for (int i = 0; i < matchingData.Count; i++)
                {
                    GameObject farm = matchingData.IsManual
                        ? CloneFarmAtPosition(farmArchetype, new Vector2(matchingData.ManualX, matchingData.ManualY), 0f, random)
                        : CloneFarmWithRandomPosition(farmArchetype, random, viewportWidth, viewportHeight);

                    ResolveFarmSpawnPosition(farm, farms, random, viewportWidth, viewportHeight);
                    farms.Add(farm);

                    // Log every 50th farm instantiation for debugging
                    if (i % 50 == 0)
                    {
                        DebugLogger.PrintGO($"Instantiating farm #{i} for prototype: {proto.Name}, ID: {proto.ID}");
                    }
                }
            }

            // After the loop, print the total number of iterations and farm objects created
            DebugLogger.PrintGO($"Farm instantiation completed. Total iterations across all prototypes: {totalIterations}, Total farms created: {farms.Count}");

            return farms;
        }

        private static GameObject CloneFarmWithRandomPosition(FarmGameObject archetype, Random random, int viewportWidth, int viewportHeight)
        {
            SimpleGameObject baseObject = archetype.BaseObject;
            Vector2 newPosition = BuildRandomFarmSpawnCandidate(
                baseObject.Geometry.Width,
                baseObject.Geometry.Height,
                viewportWidth,
                viewportHeight,
                random);
            float newRotation   = (float)(random.NextDouble() * MathF.Tau);

            SimpleGameObject instance = archetype.CreateInstance(GameObjectManager.GetNextID(), newPosition, newRotation);
            GameObject farmObject = instance.ToGameObject();

            ApplyFarmStats(farmObject, archetype.FarmData);
            if (archetype.FarmData.FloatAmplitude > 0f)
            {
                farmObject.FarmAttributes = new FarmAttributes
                {
                    FloatAmplitude = archetype.FarmData.FloatAmplitude,
                    FloatSpeed     = archetype.FarmData.FloatSpeed
                };
                farmObject.FarmFloatBase  = newRotation;
                farmObject.FarmFloatPhase = (float)(random.NextDouble() * MathF.Tau);
            }

            return farmObject;
        }

        private static GameObject CloneFarmAtPosition(FarmGameObject archetype, Vector2 position, float rotation, Random random)
        {
            SimpleGameObject instance = archetype.CreateInstance(GameObjectManager.GetNextID(), position, rotation);
            GameObject farmObject = instance.ToGameObject();

            ApplyFarmStats(farmObject, archetype.FarmData);
            if (archetype.FarmData.FloatAmplitude > 0f)
            {
                farmObject.FarmAttributes = new FarmAttributes
                {
                    FloatAmplitude = archetype.FarmData.FloatAmplitude,
                    FloatSpeed     = archetype.FarmData.FloatSpeed
                };
                farmObject.FarmFloatBase  = rotation;
                farmObject.FarmFloatPhase = (float)(random.NextDouble() * MathF.Tau);
            }

            return farmObject;
        }

        private static void ResolveFarmSpawnPosition(
            GameObject farmObject,
            IReadOnlyList<GameObject> pendingFarms,
            Random random,
            int viewportWidth,
            int viewportHeight)
        {
            if (farmObject == null || farmObject.Shape == null)
            {
                return;
            }

            float clearanceRadius = MathF.Max(12f, farmObject.BoundingRadius + FarmSpawnClearancePaddingWorldUnits);
            Vector2 initialPosition = farmObject.Position;

            if (TryApplyFarmSpawnCandidate(farmObject, pendingFarms, initialPosition, clearanceRadius))
            {
                return;
            }

            for (int attempt = 0; attempt < FarmSpawnPlacementMaxAttempts; attempt++)
            {
                Vector2 candidate = BuildRandomFarmSpawnCandidate(farmObject.Shape.Width, farmObject.Shape.Height, viewportWidth, viewportHeight, random);
                if (TryApplyFarmSpawnCandidate(farmObject, pendingFarms, candidate, clearanceRadius))
                {
                    return;
                }
            }

            ApplyFarmSpawnState(farmObject, initialPosition);
        }

        private static Vector2 BuildRandomFarmSpawnCandidate(int width, int height, int viewportWidth, int viewportHeight, Random random)
        {
            int maxX = Math.Max(1, viewportWidth - width);
            int maxY = Math.Max(1, viewportHeight - height);
            return new Vector2(random.Next(0, maxX), random.Next(0, maxY));
        }

        private static bool TryApplyFarmSpawnCandidate(
            GameObject farmObject,
            IReadOnlyList<GameObject> pendingFarms,
            Vector2 candidatePosition,
            float clearanceRadius)
        {
            Vector2 terrainSafePosition = GameBlockTerrainBackground.ResolveNearestTerrainFreeWorldPosition(
                candidatePosition,
                clearanceRadius);
            if (DoesFarmSpawnOverlap(farmObject, terrainSafePosition, pendingFarms))
            {
                return false;
            }

            ApplyFarmSpawnState(farmObject, terrainSafePosition);
            return true;
        }

        private static void ApplyFarmSpawnState(GameObject farmObject, Vector2 position)
        {
            farmObject.Position = position;
            farmObject.PreviousPosition = position;
            farmObject.PhysicsVelocity = Vector2.Zero;
        }

        private static bool DoesFarmSpawnOverlap(GameObject farmObject, Vector2 candidatePosition, IReadOnlyList<GameObject> pendingFarms)
        {
            if (farmObject == null || farmObject.Shape == null)
            {
                return false;
            }

            foreach (GameObject existing in Core.Instance.GameObjects)
            {
                if (WouldFarmOverlapExisting(farmObject, candidatePosition, existing))
                {
                    return true;
                }
            }

            if (pendingFarms == null)
            {
                return false;
            }

            for (int i = 0; i < pendingFarms.Count; i++)
            {
                if (WouldFarmOverlapExisting(farmObject, candidatePosition, pendingFarms[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool WouldFarmOverlapExisting(GameObject farmObject, Vector2 candidatePosition, GameObject existingObject)
        {
            if (existingObject == null ||
                existingObject == farmObject ||
                !existingObject.IsCollidable ||
                existingObject.Shape == null)
            {
                return false;
            }

            float combinedRadius = farmObject.BoundingRadius + existingObject.BoundingRadius + FarmSpawnClearancePaddingWorldUnits;
            if (Vector2.DistanceSquared(candidatePosition, existingObject.Position) > combinedRadius * combinedRadius)
            {
                return false;
            }

            Vector2[] farmVertices = farmObject.Shape.GetTransformedVertices(candidatePosition, farmObject.Rotation);
            Vector2[] existingVertices = existingObject.Shape.GetTransformedVertices(existingObject.Position, existingObject.Rotation);
            return SATCollisionUtil.TryGetCollision(farmVertices, existingVertices, out _);
        }

        private static void ApplyFarmStats(GameObject farmObject, FarmData data)
        {
            farmObject.IsFarmObject               = true;
            farmObject.MaxHealth                  = data.MaxHealth;
            farmObject.CurrentHealth              = data.MaxHealth;
            farmObject.HealthRegen                = data.HealthRegen;
            farmObject.HealthRegenDelay           = data.HealthRegenDelay;
            farmObject.HealthArmor                = data.HealthArmor;
            farmObject.MaxShield                  = data.MaxShield;
            farmObject.CurrentShield              = data.MaxShield;
            farmObject.ShieldRegen                = data.ShieldRegen;
            farmObject.ShieldRegenDelay           = data.ShieldRegenDelay;
            farmObject.ShieldArmor                = data.ShieldArmor;
            farmObject.BodyPenetration            = data.BodyPenetration;
            farmObject.BodyCollisionDamage        = data.BodyCollisionDamage;
            farmObject.CollisionDamageResistance  = data.CollisionDamageResistance;
            farmObject.BulletDamageResistance     = data.BulletDamageResistance;
            farmObject.DeathPointReward           = data.DeathPointReward;
            farmObject.RotationSpeed              = data.RotationSpeed;
        }

        private static void InitializeBlocks()
        {
            try
            {
                DebugLogger.PrintGO("Initializing Block GameObjects...");

                var blockArchetypes = BlockObjectLoader.LoadBlockArchetypes();
                if (blockArchetypes.Count > 0)
                {
                    DebugLogger.PrintWarning("Block GameObjects are defined but instantiation is disabled.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializeBlocks: {ex.Message}");
            }
        }

        private static void InitializeAgents(
            bool includeNonPlayerAgents,
            bool requirePlayer,
            IReadOnlyCollection<string> includedAgentNames = null)
        {
            try
            {
                HashSet<string> selectedAgentNames = includedAgentNames != null
                    ? new HashSet<string>(includedAgentNames.Where(name => !string.IsNullOrWhiteSpace(name)), StringComparer.OrdinalIgnoreCase)
                    : [];
                DebugLogger.PrintGO(includeNonPlayerAgents
                    ? "Initializing player and level agents..."
                    : selectedAgentNames.Count > 0
                        ? $"Initializing player and selected level agents: {string.Join(", ", selectedAgentNames)}"
                        : "Initializing player spawn...");

                var agents = AgentLoader.LoadAgents();

                if (agents.Count != 0)
                {
                    Agent player = agents.FirstOrDefault(a => a.IsPlayer);
                    List<Agent> activeAgents = includeNonPlayerAgents
                        ? agents
                        : agents
                            .Where(a =>
                                (requirePlayer && ReferenceEquals(a, player)) ||
                                selectedAgentNames.Contains(a.Name))
                            .ToList();
                    DisposeSkippedLevelAgents(agents, activeAgents);

                    if (selectedAgentNames.Count > 0)
                    {
                        foreach (string requestedName in selectedAgentNames)
                        {
                            if (!activeAgents.Any(agent => string.Equals(agent.Name, requestedName, StringComparison.OrdinalIgnoreCase)))
                            {
                                DebugLogger.PrintWarning($"Selected level agent '{requestedName}' was requested but not found.");
                            }
                        }
                    }

                    foreach (var agent in activeAgents)
                    {
                        var barrels = BarrelLoader.LoadBarrelsForAgent(agent.ID);
                        if (barrels.Count > 0)
                        {
                            // Clear the constructor-seeded placeholder before applying DB barrels.
                            agent.ClearBarrels();
                            foreach (var barrel in barrels)
                                agent.AddBarrel(barrel);
                        }

                        var bodies = BodyLoader.LoadBodiesForAgent(agent.ID);
                        if (bodies.Count > 0)
                        {
                            agent.ClearBodies();
                            for (int bi = 0; bi < bodies.Count; bi++)
                            {
                                agent.AddBody(
                                    bodies[bi].Attrs,
                                    bodies[bi].FillColor,
                                    bodies[bi].OutlineColor,
                                    bodies[bi].OutlineWidth);
                                if (!string.IsNullOrEmpty(bodies[bi].Name))
                                    agent.Bodies[bi].Name = bodies[bi].Name;
                            }
                        }
                    }

                    if (player != null)
                    {
                        PreparePlayerTerrainAndSpawnPosition(player, activeAgents);
                    }

                    Core.Instance.GameObjects.AddRange(activeAgents);
                    Core.Instance.Player = activeAgents.Contains(player) ? player : null;

                    if (Core.Instance.PlayerOrNull == null)
                    {
                        GameBlockTerrainBackground.PrepareStartupTerrainAroundWorldPosition(Vector2.Zero);
                        DebugLogger.PrintWarning(requirePlayer
                            ? "Required player Agent with IsPlayer=1 was not found. Core.Instance.Player is null."
                            : "No Agent with IsPlayer=1 found. Core.Instance.Player is null.");
                    }
                    else
                    {
                        DebugLogger.PrintGO($"Core player set: ID={Core.Instance.PlayerOrNull.ID}, Pos={Core.Instance.PlayerOrNull.Position}, Shape={Core.Instance.PlayerOrNull.Shape?.ShapeType}");
                    }

                    DebugLogger.PrintGO(includeNonPlayerAgents
                        ? $"Agents loaded: {activeAgents.Count} active of {agents.Count} total"
                        : selectedAgentNames.Count > 0
                            ? $"Player and selected agents loaded: {activeAgents.Count} active, {Math.Max(0, agents.Count - activeAgents.Count)} agents skipped"
                            : $"Player spawn loaded: {activeAgents.Count} active, {Math.Max(0, agents.Count - activeAgents.Count)} level agents skipped");
                }
                else
                {
                    Core.Instance.Player = null;
                    GameBlockTerrainBackground.PrepareStartupTerrainAroundWorldPosition(Vector2.Zero);
                    DebugLogger.PrintWarning(requirePlayer
                        ? "No agents were loaded, so the required player spawn is missing."
                        : "No agents were loaded.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializeAgents: {ex.Message}");
            }
        }

        private static void DisposeSkippedLevelAgents(IReadOnlyList<Agent> loadedAgents, IReadOnlyCollection<Agent> activeAgents)
        {
            if (loadedAgents == null || loadedAgents.Count == 0)
            {
                return;
            }

            HashSet<Agent> active = activeAgents != null
                ? new HashSet<Agent>(activeAgents)
                : [];
            foreach (Agent agent in loadedAgents)
            {
                if (agent == null || active.Contains(agent))
                {
                    continue;
                }

                agent.Dispose();
            }
        }

        private static void PreparePlayerTerrainAndSpawnPosition(Agent player, IReadOnlyList<Agent> activeAgents)
        {
            Vector2 requestedPosition = player?.Position ?? Vector2.Zero;
            ResetPlayerSpawnTelemetry();

            if (player == null)
            {
                GameBlockTerrainBackground.PrepareStartupTerrainAroundWorldPosition(Vector2.Zero);
                return;
            }

            GameBlockTerrainBackground.PrepareStartupTerrainAroundWorldPosition(requestedPosition);

            Vector2 resolvedPosition = ResolveNearestUnoccupiedPlayerSpawnPosition(
                player,
                requestedPosition,
                activeAgents);
            ApplyPlayerSpawnState(player, resolvedPosition);

            float relocationDistance = Vector2.Distance(requestedPosition, resolvedPosition);
            _playerSpawnRelocated = relocationDistance > 0.5f;
            _playerSpawnRelocationDistance = _playerSpawnRelocated ? relocationDistance : 0f;

            if (_playerSpawnRelocated)
            {
                GameBlockTerrainBackground.PrepareStartupTerrainAroundWorldPosition(resolvedPosition);
                DebugLogger.PrintGO(
                    $"Player spawn relocated from {requestedPosition} to {resolvedPosition} " +
                    $"after {_playerSpawnSearchAttempts} probes; distance={relocationDistance:0.##}.");
            }
        }

        private static Vector2 ResolveNearestUnoccupiedPlayerSpawnPosition(
            Agent player,
            Vector2 requestedPosition,
            IReadOnlyList<Agent> activeAgents)
        {
            if (player == null || player.Shape == null)
            {
                return requestedPosition;
            }

            float clearanceRadius = MathF.Max(12f, player.BoundingRadius + PlayerSpawnClearancePaddingWorldUnits);
            Vector2 terrainResolvedPosition = GameBlockTerrainBackground.ResolveNearestTerrainFreeWorldPosition(
                requestedPosition,
                clearanceRadius,
                PlayerSpawnMaxSearchDistanceWorldUnits);
            if (IsPlayerSpawnCandidateOpen(player, terrainResolvedPosition, activeAgents, clearanceRadius))
            {
                return terrainResolvedPosition;
            }

            float radialStep = MathF.Max(18f, clearanceRadius * 0.45f);
            for (float distance = radialStep; distance <= PlayerSpawnMaxSearchDistanceWorldUnits; distance += radialStep)
            {
                int sampleCount = Math.Min(
                    PlayerSpawnMaxRingSamples,
                    Math.Max(16, (int)MathF.Ceiling(MathF.Tau * distance / radialStep)));
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float angle = (sampleIndex / (float)sampleCount) * MathF.Tau;
                    Vector2 candidate = new(
                        requestedPosition.X + (MathF.Cos(angle) * distance),
                        requestedPosition.Y + (MathF.Sin(angle) * distance));
                    if (IsPlayerSpawnCandidateOpen(player, candidate, activeAgents, clearanceRadius))
                    {
                        return candidate;
                    }
                }
            }

            DebugLogger.PrintWarning(
                $"Player spawn remained at terrain-resolved position {terrainResolvedPosition}; no fully open spawn found within {PlayerSpawnMaxSearchDistanceWorldUnits:0.##} world units.");
            return terrainResolvedPosition;
        }

        private static bool IsPlayerSpawnCandidateOpen(
            Agent player,
            Vector2 candidatePosition,
            IReadOnlyList<Agent> activeAgents,
            float clearanceRadius)
        {
            _playerSpawnSearchAttempts++;
            return GameBlockTerrainBackground.IsTerrainFreeWorldPosition(candidatePosition, clearanceRadius) &&
                !PlayerSpawnOverlapsAnyCollidable(player, candidatePosition, activeAgents);
        }

        private static bool PlayerSpawnOverlapsAnyCollidable(
            Agent player,
            Vector2 candidatePosition,
            IReadOnlyList<Agent> activeAgents)
        {
            if (Core.Instance?.GameObjects != null)
            {
                for (int i = 0; i < Core.Instance.GameObjects.Count; i++)
                {
                    if (WouldPlayerSpawnOverlapObject(player, candidatePosition, Core.Instance.GameObjects[i]))
                    {
                        return true;
                    }
                }
            }

            if (activeAgents != null)
            {
                for (int i = 0; i < activeAgents.Count; i++)
                {
                    if (WouldPlayerSpawnOverlapObject(player, candidatePosition, activeAgents[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool WouldPlayerSpawnOverlapObject(
            Agent player,
            Vector2 candidatePosition,
            GameObject existingObject)
        {
            if (player == null ||
                existingObject == null ||
                ReferenceEquals(existingObject, player) ||
                !existingObject.IsCollidable ||
                existingObject.Shape == null ||
                player.Shape == null)
            {
                return false;
            }

            float combinedRadius = player.BoundingRadius + existingObject.BoundingRadius + PlayerSpawnClearancePaddingWorldUnits;
            if (Vector2.DistanceSquared(candidatePosition, existingObject.Position) > combinedRadius * combinedRadius)
            {
                return false;
            }

            try
            {
                Vector2[] playerVertices = player.Shape.GetTransformedVertices(candidatePosition, player.Rotation);
                Vector2[] existingVertices = existingObject.Shape.GetTransformedVertices(existingObject.Position, existingObject.Rotation);
                return SATCollisionUtil.TryGetCollision(playerVertices, existingVertices, out _);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning(
                    $"Player spawn overlap probe failed against ID={existingObject.ID}, Name={existingObject.Name}: {ex.Message}");
                return true;
            }
        }

        private static void ApplyPlayerSpawnState(Agent player, Vector2 position)
        {
            if (player == null)
            {
                return;
            }

            player.Position = position;
            player.PreviousPosition = position;
            player.PhysicsVelocity = Vector2.Zero;
            player.MovementVelocity = Vector2.Zero;
        }

        private static void ResetPlayerSpawnTelemetry()
        {
            _playerSpawnRelocated = false;
            _playerSpawnRelocationDistance = 0f;
            _playerSpawnSearchAttempts = 0;
        }

        private static void InitializeZoneBlocks()
        {
            try
            {
                DebugLogger.PrintGO("Initializing ZoneBlocks...");

                // Large orange square placed to the left of the farm generation zone.
                // Farms spawn within [0, viewportWidth), so negative X is "off to the left".
                int zoneSize = 300;
                Vector2 position = new(-zoneSize, Core.Instance.ViewportHeight / 2f);

                Color fill = new(255, 165, 0, 100);       // orange, semi-transparent
                Color outline = new(255, 140, 0, 180);
                Shape shape = new("Rectangle", zoneSize, zoneSize, 4, fill, outline, 3);

                GameObject zone = new(
                    id: GameObjectManager.GetNextID(),
                    name: "PlayerPreviewZone",
                    position: position,
                    rotation: 0f,
                    mass: 0f,
                    isDestructible: false,
                    isCollidable: false,
                    dynamicPhysics: false,
                    shape: shape,
                    fillColor: fill,
                    outlineColor: outline,
                    outlineWidth: 3
                );
                zone.IsInteract = true;
                zone.IsZoneBlock = true;
                zone.ZoneBlockDynamicKey = "PlayerPreview";

                zone.Shape.LoadContent(Core.Instance.GraphicsDevice);

                Core.Instance.StaticObjects.Add(zone);
                Core.Instance.GameObjects.Add(zone);

                DebugLogger.PrintGO($"ZoneBlock created: ID={zone.ID}, Pos={zone.Position}, Size={zoneSize}x{zoneSize}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializeZoneBlocks: {ex.Message}");
            }
        }

        private static void ResolveTerrainSpawnOverlaps()
        {
            try
            {
                if (Core.Instance?.GameObjects == null || Core.Instance.GameObjects.Count == 0)
                {
                    GameBlockTerrainBackground.SetTerrainSpawnRelocationCount(0);
                    return;
                }

                int relocationCount = 0;
                foreach (GameObject gameObject in Core.Instance.GameObjects)
                {
                    if (gameObject == null ||
                        gameObject.Shape == null ||
                        !gameObject.DynamicPhysics ||
                        !gameObject.IsCollidable)
                    {
                        continue;
                    }

                    float clearanceRadius = MathF.Max(12f, gameObject.BoundingRadius + 6f);
                    Vector2 originalPosition = gameObject.Position;
                    Vector2 resolvedPosition = GameBlockTerrainBackground.ResolveNearestTerrainFreeWorldPosition(
                        originalPosition,
                        clearanceRadius);
                    if (Vector2.DistanceSquared(resolvedPosition, originalPosition) <= 0.25f)
                    {
                        continue;
                    }

                    gameObject.Position = resolvedPosition;
                    gameObject.PreviousPosition = resolvedPosition;
                    gameObject.PhysicsVelocity = Vector2.Zero;
                    if (gameObject is Agent agent)
                    {
                        agent.MovementVelocity = Vector2.Zero;
                    }

                    relocationCount++;
                    DebugLogger.PrintGO(
                        $"Terrain spawn relocation: ID={gameObject.ID}, Name={gameObject.Name}, From={originalPosition}, To={resolvedPosition}, Clearance={clearanceRadius:0.##}");
                }

                GameBlockTerrainBackground.SetTerrainSpawnRelocationCount(relocationCount);
                if (relocationCount > 0)
                {
                    DebugLogger.PrintGO($"Terrain spawn overlap resolution relocated {relocationCount} dynamic objects.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in ResolveTerrainSpawnOverlaps: {ex.Message}");
            }
        }

        private static void ResolveStartupCollidableOverlaps()
        {
            try
            {
                if (Core.Instance?.GameObjects == null || Core.Instance.GameObjects.Count == 0)
                {
                    return;
                }

                CollisionResolver.ResolveStartupOverlaps(Core.Instance.GameObjects);
                if (CollisionResolver.StartupOverlapResolvedPairCount > 0)
                {
                    DebugLogger.PrintGO(
                        $"Startup collidable overlap resolution separated {CollisionResolver.StartupOverlapResolvedPairCount} pairs " +
                        $"across {CollisionResolver.StartupOverlapIterationCount} iterations.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in ResolveStartupCollidableOverlaps: {ex.Message}");
            }
        }
    }
}
