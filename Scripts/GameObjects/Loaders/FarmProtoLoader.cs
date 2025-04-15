using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class FarmProtoLoader
    {
        public static List<GameObject> LoadFarmPrototypes()
        {
            List<GameObject> farmPrototypes = new List<GameObject>();

            try
            {
                DebugLogger.PrintGO("Loading farm prototypes from database...");

                // Actual SQL query to load farm prototypes
                string query = @"
                    SELECT
                        g.ID,
                        g.Name,
                        g.Type,
                        g.PositionX,
                        g.PositionY,
                        g.Rotation,
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
                        g.Shape,
                        f.Count
                    FROM GameObjects g
                    INNER JOIN FarmData f ON g.ID = f.ID
                    WHERE g.Type = 'Prototype';";

                var results = DatabaseQuery.ExecuteQuery(query);

                if (results.Count == 0)
                {
                    DebugLogger.PrintWarning("No farm prototypes found in database.");
                    return farmPrototypes;
                }

                DebugLogger.PrintGO($"Loaded {results.Count} farm prototypes.");

                foreach (var row in results)
                {
                    try
                    {
                        // Deserialize the row into a GameObject for the prototype
                        GameObject proto = GameObjectLoader.DeserializeGameObject(row, isPrototype: true);
                        if (proto == null)
                        {
                            DebugLogger.PrintError("Failed to deserialize prototype. Skipping.");
                            continue;
                        }

                        // Print debugging information about the prototype
                        DebugLogger.PrintGO($"Farm Prototype: ID={proto.ID}, Name={proto.Name}, Type={proto.Type}");
                        farmPrototypes.Add(proto);
                    }
                    catch (Exception exRow)
                    {
                        DebugLogger.PrintError($"Error parsing farm prototype row: {exRow.Message}");
                    }
                }

                DebugLogger.PrintGO($"Farm Prototypes loaded: {farmPrototypes.Count}");

            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load farm prototypes: {ex.Message}");
            }

            return farmPrototypes;
        }
    }
}
