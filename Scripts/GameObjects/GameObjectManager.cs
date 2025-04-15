using op.io;
using System.Collections.Generic;

public static class GameObjectManager
{
    private static int _nextAvailableID = 1;

    public static List<GameObject> GameObjects { get; private set; } = new();
    public static List<GameObject> StaticObjects { get; private set; } = new();

    public static void InitializeObjects(Core game)
    {
        _nextAvailableID = 1;
        GameObjects.Clear();
        StaticObjects.Clear();

        // Load agents, farms, and map objects
        LoadAgents();

        // Load farm prototypes (no need to call LoadFarmData here)
        var farmProtos = FarmProtoLoader.LoadFarmPrototypes();  // Load farm prototypes here

        // Directly load farm data (this now calls the FarmDataLoader)
        LoadFarmObjects(farmProtos);  // Handle farm objects instantiation based on loaded farm data

        LoadMapObjects();
    }

    public static int GetNextID()
    {
        return _nextAvailableID++;
    }

    private static void LoadAgents()
    {
        // Load agents from the database
        var agents = AgentLoader.LoadAgents();
        GameObjects.AddRange(agents);
        DebugLogger.PrintGO($"Agents initialized with {agents.Count} objects.");
    }

    private static void LoadFarmObjects(List<GameObject> farmProtos)
    {
        // Load farm data (count information) from the database using FarmDataLoader
        var farmData = FarmDataLoader.GetFarmData();

        if (farmData.Count != 0)
        {
            // Instantiate farms from farm prototypes and data using the new FarmObjectLoader
            var farms = FarmObjectLoader.LoadFarmObjects(farmProtos, farmData);
            GameObjects.AddRange(farms);
            DebugLogger.PrintGO($"Farms initialized with {farms.Count} objects.");
        }
        else
        {
            DebugLogger.PrintWarning("No farm data loaded.");
        }
    }

    private static void LoadMapObjects()
    {
        // Load map-related objects from the database
        var mapObjects = MapObjectLoader.LoadMapObjects();
        StaticObjects.AddRange(mapObjects);
        GameObjects.AddRange(mapObjects);
        DebugLogger.PrintGO($"Map initialized with {mapObjects.Count} objects.");
    }

    public static void Update()
    {
        // Update all game objects in the scene
        foreach (var obj in GameObjects)
        {
            obj.Update();
        }
    }

    // Build the SQL query to join the GameObjects with the secondary table
    public static string BuildJoinQuery(string secondaryTable, string whereClause = null, string extraColumns = "")
    {
        // Ensure we select all necessary columns from the GameObjects table
        string baseColumns = @"
            g.ID,
            g.Name,
            g.Type,
            g.PositionX,
            g.PositionY,
            g.Rotation,        -- Ensure this is included for rotation
            g.Width,
            g.Height,
            g.Sides,
            g.FillR, g.FillG, g.FillB, g.FillA,
            g.OutlineR, g.OutlineG, g.OutlineB, g.OutlineA,
            g.OutlineWidth,
            g.IsCollidable,
            g.IsDestructible,
            g.Mass,
            g.StaticPhysics,
            g.Shape";

        // Add extra columns if needed (like Agent-specific data)
        if (!string.IsNullOrWhiteSpace(extraColumns))
            baseColumns += ", " + extraColumns;

        // Build the query string with the necessary join
        string query = $@"
            SELECT {baseColumns}
            FROM {secondaryTable} s
            INNER JOIN GameObjects g ON s.ID = g.ID";

        // Add any where clause if provided (useful for filtering)
        if (!string.IsNullOrWhiteSpace(whereClause))
            query += $" WHERE {whereClause}";

        return query + ";";
    }
}
