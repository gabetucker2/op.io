using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameObjectLoader
    {
        public static GameObject LoadGameObject(string tableName, int objectId)
        {
            string query = $"SELECT * FROM {tableName} WHERE ID = @ObjectId LIMIT 1;";
            var result = DatabaseQuery.ExecuteQuery(query, new Dictionary<string, object> { { "@ObjectId", objectId } });

            if (result.Count > 0)
            {
                return DeserializeGameObject(result[0]);
            }

            DebugLogger.PrintError($"Failed to load GameObject: {objectId} from {tableName}.");
            return null;
        }

        public static List<GameObject> LoadGameObjects(string tableName)
        {
            List<GameObject> objects = new List<GameObject>();
            string query = $"SELECT * FROM {tableName};";

            var result = DatabaseQuery.ExecuteQuery(query);

            foreach (var row in result)
            {
                GameObject obj = DeserializeGameObject(row);
                if (obj != null)
                {
                    objects.Add(obj);
                }
            }

            DebugLogger.PrintInfo($"Loaded {objects.Count} GameObjects from {tableName}.");
            return objects;
        }

        private static GameObject DeserializeGameObject(Dictionary<string, object> row)
        {
            if (!row.ContainsKey("Width") || !row.ContainsKey("Height"))
            {
                DebugLogger.PrintError("SQL row missing Width or Height keys.");
                return null;
            }

            try
            {
                Color fillColor = new Color(
                    Convert.ToInt32(row["FillR"]),
                    Convert.ToInt32(row["FillG"]),
                    Convert.ToInt32(row["FillB"]),
                    Convert.ToInt32(row["FillA"])
                );

                Color outlineColor = new Color(
                    Convert.ToInt32(row["OutlineR"]),
                    Convert.ToInt32(row["OutlineG"]),
                    Convert.ToInt32(row["OutlineB"]),
                    Convert.ToInt32(row["OutlineA"])
                );

                int width = Convert.ToInt32(row["Width"]);
                int height = Convert.ToInt32(row["Height"]);

                Vector2 position = new Vector2(Convert.ToSingle(row["PositionX"]), Convert.ToSingle(row["PositionY"]));
                float mass = Convert.ToSingle(row["Mass"]);
                bool isPlayer = Convert.ToBoolean(row["IsPlayer"]);
                bool isDestructible = Convert.ToBoolean(row["IsDestructible"]);
                bool isCollidable = Convert.ToBoolean(row["IsCollidable"]);
                bool staticPhysics = row.ContainsKey("StaticPhysics") && Convert.ToBoolean(row["StaticPhysics"]); // <- added
                int sides = Convert.ToInt32(row["Sides"]);
                int outlineWidth = Convert.ToInt32(row["OutlineWidth"]);
                string shapeType = row["Type"].ToString();

                int count = row.ContainsKey("Count") && int.TryParse(row["Count"].ToString(), out int parsedCount) ? parsedCount : 1;

                DebugLogger.PrintDebug($"Deserializing GameObject with Width={width}, Height={height} from table data.");

                if (width <= 0 || height <= 0)
                {
                    DebugLogger.PrintError($"Invalid dimensions found: Width={width}, Height={height}");
                }

                if (isPlayer)
                {
                    float speed = Convert.ToSingle(row["Speed"]);
                    int radius = Convert.ToInt32(row["Radius"]);

                    return new Player(position, radius, speed, fillColor, outlineColor, outlineWidth);
                }

                return new GameObject
                {
                    Position = position,
                    Mass = mass,
                    IsPlayer = isPlayer,
                    IsDestructible = isDestructible,
                    IsCollidable = isCollidable,
                    StaticPhysics = staticPhysics, // <- crucial addition here
                    Shape = new Shape(position, shapeType, width, height, sides, fillColor, outlineColor, outlineWidth),
                    Count = count
                };
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"SQL GameObject deserialization: {ex.Message}");
                return null;
            }
        }
    }
}
