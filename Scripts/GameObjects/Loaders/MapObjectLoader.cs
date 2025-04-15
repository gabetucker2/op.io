using op.io;
using System.Collections.Generic;
using System;

public static class MapObjectLoader
{
    public static List<GameObject> LoadMapObjects()
    {
        List<GameObject> mapObjects = new List<GameObject>();

        try
        {
            DebugLogger.PrintGO("Loading map objects from database...");

            var results = DatabaseQuery.ExecuteQuery("SELECT g.ID, g.Name, g.Type, g.PositionX, g.PositionY, g.Rotation, g.Width, g.Height, g.Sides, g.FillR, g.FillG, g.FillB, g.FillA, g.OutlineR, g.OutlineG, g.OutlineB, g.OutlineA, g.OutlineWidth, g.IsCollidable, g.IsDestructible, g.Mass, g.StaticPhysics, g.Shape FROM MapData s INNER JOIN GameObjects g ON s.ID = g.ID");

            if (results.Count == 0)
            {
                DebugLogger.PrintWarning("No map objects found in database.");
                return mapObjects;
            }

            DebugLogger.PrintGO($"Loaded {results.Count} map objects.");

            foreach (var row in results)
            {
                try
                {
                    GameObject mapObject = GameObjectLoader.DeserializeGameObject(row);

                    if (mapObject != null)
                    {
                        mapObjects.Add(mapObject);
                        DebugLogger.PrintDebug($"Loaded map object ID={mapObject.ID}.");
                    }
                    else
                    {
                        DebugLogger.PrintError("Failed to deserialize a map object.");
                    }
                }
                catch (Exception exRow)
                {
                    DebugLogger.PrintError($"Error parsing map object row: {exRow.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.PrintError($"Failed to load map objects: {ex.Message}");
        }

        return mapObjects; // Ensure a list is returned
    }

}
