using System;
using System.Collections.Generic;

namespace op.io
{
    public static class FarmDataLoader
    {
        // Cache to store the loaded farm data
        private static List<FarmData> _cachedFarmData = null;

        // Function to get the farm data (cached if available)
        public static List<FarmData> GetFarmData()
        {
            // If the data has been cached already, return it
            if (_cachedFarmData != null)
            {
                return _cachedFarmData;
            }

            // If not cached, load it from the database and cache it
            return LoadFarmData();
        }

        // Load farm data from the database and cache it
        private static List<FarmData> LoadFarmData()
        {
            List<FarmData> farmDataList = [];

            try
            {
                DebugLogger.PrintDatabase("Loading farm data from database...");

                // FarmData holds spawn/animation config; Destructibles holds health/combat/reward.
                const string query = @"
                    SELECT
                        f.ID, f.Count, f.RotationSpeed,
                        f.IsManual, f.ManualX, f.ManualY,
                        f.FloatAmplitude, f.FloatSpeed,
                        d.MaxHealth, d.HealthRegen, d.HealthRegenDelay, d.HealthArmor,
                        d.MaxShield, d.ShieldRegen, d.ShieldRegenDelay, d.ShieldArmor,
                        d.BodyPenetration, d.BodyCollisionDamage,
                        d.CollisionDamageResistance, d.BulletDamageResistance,
                        d.DeathPointReward
                    FROM FarmData f
                    INNER JOIN Destructibles d ON f.ID = d.ID";

                var results = DatabaseQuery.ExecuteQuery(query);

                if (results.Count == 0)
                {
                    DebugLogger.PrintWarning("No farm data found in database.");
                    return farmDataList;
                }

                DebugLogger.PrintDatabase($"Loaded {results.Count} farm data entries.");

                foreach (var row in results)
                {
                    try
                    {
                        int id    = Convert.ToInt32(row["ID"]);
                        int count = Convert.ToInt32(row["Count"]);

                        if (count <= 0)
                        {
                            DebugLogger.PrintWarning($"Farm with ID {id} has non-positive count ({count}). Skipping.");
                            continue;
                        }

                        float SafeFloat(string key, float fallback = 0f) =>
                            row.TryGetValue(key, out object v) && v != null && v != DBNull.Value
                                ? Convert.ToSingle(v) : fallback;

                        bool SafeBool(string key) =>
                            row.TryGetValue(key, out object bv) && bv != null && bv != DBNull.Value
                                && Convert.ToInt32(bv) != 0;

                        farmDataList.Add(new FarmData
                        {
                            ID                         = id,
                            Count                      = count,
                            RotationSpeed              = SafeFloat("RotationSpeed"),
                            IsManual                   = SafeBool("IsManual"),
                            ManualX                    = SafeFloat("ManualX"),
                            ManualY                    = SafeFloat("ManualY"),
                            FloatAmplitude             = SafeFloat("FloatAmplitude"),
                            FloatSpeed                 = SafeFloat("FloatSpeed"),
                            // Destructible attributes (loaded from Destructibles JOIN)
                            MaxHealth                  = SafeFloat("MaxHealth"),
                            HealthRegen                = SafeFloat("HealthRegen"),
                            HealthRegenDelay           = SafeFloat("HealthRegenDelay", 5f),
                            HealthArmor                = SafeFloat("HealthArmor"),
                            MaxShield                  = SafeFloat("MaxShield"),
                            ShieldRegen                = SafeFloat("ShieldRegen"),
                            ShieldRegenDelay           = SafeFloat("ShieldRegenDelay", 5f),
                            ShieldArmor                = SafeFloat("ShieldArmor"),
                            BodyPenetration            = SafeFloat("BodyPenetration"),
                            BodyCollisionDamage        = SafeFloat("BodyCollisionDamage"),
                            CollisionDamageResistance  = SafeFloat("CollisionDamageResistance"),
                            BulletDamageResistance     = SafeFloat("BulletDamageResistance"),
                            DeathPointReward           = SafeFloat("DeathPointReward"),
                        });
                    }
                    catch (Exception exRow)
                    {
                        DebugLogger.PrintError($"Error parsing farm data row: {exRow.Message}");
                    }
                }

                // Cache the data for future use
                _cachedFarmData = farmDataList;

            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load farm data: {ex.Message}");
            }

            return farmDataList;
        }
    }
}
