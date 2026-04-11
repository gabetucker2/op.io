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

            // Map objects are GameObjects with a Destructibles entry that are
            // not Agents and not FarmData prototypes.
            const string query = @"
                SELECT
                    g.ID, g.Name,
                    g.PositionX, g.PositionY, g.Rotation,
                    g.Width, g.Height, g.Sides,
                    g.FillR, g.FillG, g.FillB, g.FillA,
                    g.OutlineR, g.OutlineG, g.OutlineB, g.OutlineA, g.OutlineWidth,
                    g.IsCollidable, g.IsDestructible, g.Mass, g.StaticPhysics,
                    g.Shape, g.RotationSpeed,
                    d.MaxHealth, d.HealthRegen, d.HealthRegenDelay, d.HealthArmor,
                    d.MaxShield, d.ShieldRegen, d.ShieldRegenDelay, d.ShieldArmor,
                    d.BodyPenetration, d.BodyCollisionDamage,
                    d.CollisionDamageResistance, d.BulletDamageResistance,
                    d.DeathPointReward
                FROM Destructibles d
                INNER JOIN GameObjects g ON g.ID = d.ID
                WHERE g.ID NOT IN (SELECT ID FROM Agents)
                AND   g.ID NOT IN (SELECT ID FROM FarmData)";

            var results = DatabaseQuery.ExecuteQuery(query);

            if (results.Count == 0)
            {
                DebugLogger.PrintWarning("No map objects found in database.");
                return mapObjects;
            }

            DebugLogger.PrintGO($"Loaded {results.Count} map objects.");

            float SafeFloat(Dictionary<string, object> row, string key, float fallback = 0f) =>
                row.TryGetValue(key, out object v) && v != null && v != DBNull.Value
                    ? Convert.ToSingle(v) : fallback;

            foreach (var row in results)
            {
                try
                {
                    GameObject mapObject = GameObjectLoader.DeserializeGameObject(row);

                    if (mapObject != null)
                    {
                        float maxHealth = SafeFloat(row, "MaxHealth");
                        mapObject.MaxHealth         = maxHealth;
                        mapObject.CurrentHealth     = maxHealth;
                        mapObject.DeathPointReward  = SafeFloat(row, "DeathPointReward");
                        mapObject.RotationSpeed     = SafeFloat(row, "RotationSpeed");

                        mapObject.HealthRegen       = SafeFloat(row, "HealthRegen");
                        mapObject.HealthRegenDelay  = SafeFloat(row, "HealthRegenDelay", 5f);
                        mapObject.HealthArmor       = SafeFloat(row, "HealthArmor");

                        float maxShield             = SafeFloat(row, "MaxShield");
                        mapObject.MaxShield         = maxShield;
                        mapObject.CurrentShield     = maxShield;
                        mapObject.ShieldRegen       = SafeFloat(row, "ShieldRegen");
                        mapObject.ShieldRegenDelay  = SafeFloat(row, "ShieldRegenDelay", 5f);
                        mapObject.ShieldArmor       = SafeFloat(row, "ShieldArmor");

                        mapObject.BodyPenetration           = SafeFloat(row, "BodyPenetration");
                        mapObject.BodyCollisionDamage       = SafeFloat(row, "BodyCollisionDamage");
                        mapObject.CollisionDamageResistance = SafeFloat(row, "CollisionDamageResistance");
                        mapObject.BulletDamageResistance    = SafeFloat(row, "BulletDamageResistance");

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

        return mapObjects;
    }
}
