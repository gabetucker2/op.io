using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using CollisionMode = op.io.Attributes.CollisionMode;
using DestructionMode = op.io.Attributes.DestructionMode;
using PhysicsAttributes = op.io.Attributes.Physics;
using PhysicsMotion = op.io.Attributes.PhysicsMotion;
using ShapeAttributes = op.io.Attributes.Shape;

namespace op.io
{
    public static class GameObjectLoader
    {
        public static List<GameObject> LoadGameObjects(string tableName)
        {
            var gameObjects = new List<GameObject>();

            try
            {
                DebugLogger.PrintDatabase($"Loading GameObjects from table: {tableName}...");
                string query = $"SELECT * FROM {tableName};";
                var result = DatabaseQuery.ExecuteQuery(query);

                if (result.Count == 0)
                {
                    DebugLogger.PrintWarning($"No GameObjects found in table: {tableName}.");
                    return gameObjects;
                }

                DebugLogger.PrintDatabase($"Retrieved {result.Count} rows from {tableName}.");

                foreach (var row in result)
                {
                    DebugLogger.PrintDatabase($"Row keys: {string.Join(", ", row.Keys)}");

                    var gameObject = DeserializeGameObject(row);
                    if (gameObject != null)
                    {
                        gameObjects.Add(gameObject);
                        DebugLogger.PrintGO($"Loaded GameObject with ID: {gameObject.ID}, Type: {gameObject.GetType().Name}, Pos: {gameObject.Position}");
                    }
                    else
                    {
                        DebugLogger.PrintWarning("Skipped null GameObject after deserialization.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load GameObjects from {tableName}: {ex.Message}");
            }

            return gameObjects;
        }

        public static List<GameObject> LoadGameObjectsFromJoin(string joinTable, string typeFilter)
        {
            var gameObjects = new List<GameObject>();

            try
            {
                DebugLogger.PrintDatabase($"Loading GameObjects from join: {joinTable} + GameObjects where Type = {typeFilter}...");

                string query = GameObjectManager.BuildJoinQuery(
                    secondaryTable: joinTable,
                    whereClause: "g.Type = @Type"
                );

                var result = DatabaseQuery.ExecuteQuery(query, new Dictionary<string, object>
                {
                    { "@Type", typeFilter }
                });

                if (result.Count == 0)
                {
                    DebugLogger.PrintWarning($"No joined GameObjects found in {joinTable} for Type = {typeFilter}.");
                    return gameObjects;
                }

                DebugLogger.PrintDatabase($"Retrieved {result.Count} rows from join.");

                foreach (var row in result)
                {
                    DebugLogger.PrintDatabase($"Row keys: {string.Join(", ", row.Keys)}");

                    var gameObject = DeserializeGameObject(row);
                    if (gameObject != null)
                    {
                        gameObjects.Add(gameObject);
                        DebugLogger.PrintGO($"Loaded GameObject with ID: {gameObject.ID}, Type: {gameObject.GetType().Name}, Pos: {gameObject.Position}");
                    }
                    else
                    {
                        DebugLogger.PrintWarning("Skipped null GameObject after deserialization.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load GameObjects from join {joinTable}: {ex.Message}");
            }

            return gameObjects;
        }

        public static bool TryDeserializeSimpleGameObject(Dictionary<string, object> row, out SimpleGameObject archetype)
        {
            archetype = default;

            try
            {
                int id = Convert.ToInt32(row["ID"]);
                string name = row["Name"]?.ToString() ?? "Unknown";
                string type = row["Type"]?.ToString() ?? "Unknown";
                Vector2 position = new(Convert.ToSingle(row["PositionX"]), Convert.ToSingle(row["PositionY"]));
                float rotation = row.ContainsKey("Rotation") ? Convert.ToSingle(row["Rotation"]) : 0f;
                float mass = Convert.ToSingle(row["Mass"]);
                bool isCollidable = Convert.ToBoolean(row["IsCollidable"]);
                bool isDestructible = Convert.ToBoolean(row["IsDestructible"]);
                bool staticPhysics = Convert.ToBoolean(row["StaticPhysics"]);

                Color fillColor = new(
                    Convert.ToInt32(row["FillR"]),
                    Convert.ToInt32(row["FillG"]),
                    Convert.ToInt32(row["FillB"]),
                    Convert.ToInt32(row["FillA"])
                );

                Color outlineColor = new(
                    Convert.ToInt32(row["OutlineR"]),
                    Convert.ToInt32(row["OutlineG"]),
                    Convert.ToInt32(row["OutlineB"]),
                    Convert.ToInt32(row["OutlineA"])
                );

                int width = Convert.ToInt32(row["Width"]);
                int height = Convert.ToInt32(row["Height"]);
                int sides = Convert.ToInt32(row["Sides"]);
                int outlineWidth = Convert.ToInt32(row["OutlineWidth"]);

                string shapeType = row.ContainsKey("Shape")
                    ? row["Shape"]?.ToString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(shapeType))
                {
                    shapeType = sides switch
                    {
                        0 => "Circle",
                        4 => "Rectangle",
                        >= 3 => "Polygon",
                        _ => "INVALID"
                    };
                }

                Attributes.Identity identity = new(id, name, type);
                Attributes.Transform transform = new(position, rotation);
                PhysicsAttributes physics = new(
                    staticPhysics ? PhysicsMotion.Static : PhysicsMotion.Dynamic,
                    isCollidable ? CollisionMode.Collidable : CollisionMode.NonCollidable,
                    isDestructible ? DestructionMode.Destructible : DestructionMode.Indestructible,
                    mass);

                ShapeAttributes shapeAttributes = new(sides);
                Attributes.Geometry geometry = new(shapeType, width, height, shapeAttributes);
                Attributes.Appearance appearance = new(fillColor, outlineColor, outlineWidth);

                archetype = new SimpleGameObject(
                    identity,
                    transform,
                    physics,
                    geometry,
                    appearance);

                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error deserializing GameObject archetype: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts a DB row to a GameObject using the SimpleGameObject archetype.
        /// </summary>
        public static GameObject DeserializeGameObject(Dictionary<string, object> row, bool isPrototype = false)
        {
            if (!TryDeserializeSimpleGameObject(row, out SimpleGameObject archetype))
            {
                return null;
            }

            return archetype.ToGameObject(isPrototype);
        }

    }
}
