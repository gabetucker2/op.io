using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameObjectInitializer
    {
        public static void Initialize()
        {
            DebugLogger.PrintGO("Initializing GameObjects...");

            Core.Instance.GameObjects = new List<GameObject>();
            Core.Instance.StaticObjects = new List<GameObject>();

            InitializeMapObjects();
            InitializeFarms();
            InitializeBlocks();
            InitializeAgents();

            DebugLogger.PrintGO($"Initialization complete. Total GameObjects: {Core.Instance.GameObjects.Count}, StaticObjects: {Core.Instance.StaticObjects.Count}");
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
                    DebugLogger.PrintGO($"Farm Prototype: ID={proto.ID}, Name={proto.Name}, Type={proto.Type}");
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
                    var farm = CloneFarmWithRandomPosition(farmArchetype, random, viewportWidth, viewportHeight);
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
            // Random position generation for the farm
            SimpleGameObject baseObject = archetype.BaseObject;
            int maxX = Math.Max(1, viewportWidth - baseObject.Geometry.Width);
            int maxY = Math.Max(1, viewportHeight - baseObject.Geometry.Height);

            Vector2 newPosition = new Vector2(random.Next(0, maxX), random.Next(0, maxY));
            float newRotation = (float)(random.NextDouble() * MathF.Tau);

            SimpleGameObject instance = archetype.CreateInstance(GameObjectManager.GetNextID(), newPosition, newRotation);
            return instance.ToGameObject();
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

        private static void InitializeAgents()
        {
            try
            {
                DebugLogger.PrintGO("Initializing Agents...");

                var agents = AgentLoader.LoadAgents();

                if (agents.Count != 0)
                {
                    Core.Instance.GameObjects.AddRange(agents);
                    Core.Instance.Player = agents.FirstOrDefault(a => a.IsPlayer);

                    if (Core.Instance.Player == null)
                        DebugLogger.PrintError("No Agent with IsPlayer=1 found. Core.Instance.Player is null.");
                    else
                        DebugLogger.PrintGO($"Core player set: ID={Core.Instance.Player.ID}, Pos={Core.Instance.Player.Position}, Shape={Core.Instance.Player.Shape?.ShapeType}");

                    DebugLogger.PrintGO($"Agents loaded: {agents.Count} total");
                }
                else
                {
                    DebugLogger.PrintWarning("No agents were loaded.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializeAgents: {ex.Message}");
            }
        }
    }
}
