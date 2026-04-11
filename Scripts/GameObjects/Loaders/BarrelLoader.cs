using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class BarrelLoader
    {
        /// <summary>
        /// Loads all barrel prototypes assigned to the given agent, ordered by SlotIndex.
        /// Returns an empty list if the agent has no barrel rows; the caller should then
        /// fall back to AddBarrel(default) so firing still works.
        /// </summary>
        public static List<Attributes_Barrel> LoadBarrelsForAgent(int agentId)
        {
            var barrels = new List<Attributes_Barrel>();

            try
            {
                string query =
                    "SELECT bp.BulletDamage, bp.BulletPenetration, bp.BulletSpeed, " +
                    "       bp.ReloadSpeed, bp.BulletMaxLifespan, bp.BulletMass, bp.BulletHealth, " +
                    "       bp.BulletControl, " +
                    "       bp.BulletFillR, bp.BulletFillG, bp.BulletFillB, bp.BulletFillA, " +
                    "       bp.BulletOutlineR, bp.BulletOutlineG, bp.BulletOutlineB, bp.BulletOutlineA, " +
                    "       bp.BulletOutlineWidth " +
                    "FROM AgentBarrels ab " +
                    "JOIN BarrelPrototypes bp ON ab.BarrelPrototypeID = bp.ID " +
                    "WHERE ab.AgentID = @AgentID " +
                    "ORDER BY ab.SlotIndex;";

                var results = DatabaseQuery.ExecuteQuery(query,
                    new Dictionary<string, object> { { "@AgentID", agentId } });

                foreach (var row in results)
                {
                    barrels.Add(new Attributes_Barrel
                    {
                        BulletDamage      = Convert.ToSingle(row["BulletDamage"]),
                        BulletPenetration = Convert.ToSingle(row["BulletPenetration"]),
                        BulletSpeed       = Convert.ToSingle(row["BulletSpeed"]),
                        ReloadSpeed       = Convert.ToSingle(row["ReloadSpeed"]),
                        BulletMaxLifespan = Convert.ToSingle(row["BulletMaxLifespan"]),
                        BulletMass        = Convert.ToSingle(row["BulletMass"]),
                        BulletHealth      = Convert.ToSingle(row["BulletHealth"]),
                        BulletControl                  = Convert.ToSingle(row["BulletControl"]),
                        BulletFillAlphaRaw    = Convert.ToInt32(row["BulletFillA"]),
                        BulletOutlineAlphaRaw = Convert.ToInt32(row["BulletOutlineA"]),
                        BulletFillColor   = new Color(
                            Convert.ToInt32(row["BulletFillR"]),
                            Convert.ToInt32(row["BulletFillG"]),
                            Convert.ToInt32(row["BulletFillB"]),
                            Convert.ToInt32(row["BulletFillA"])),
                        BulletOutlineColor = new Color(
                            Convert.ToInt32(row["BulletOutlineR"]),
                            Convert.ToInt32(row["BulletOutlineG"]),
                            Convert.ToInt32(row["BulletOutlineB"]),
                            Convert.ToInt32(row["BulletOutlineA"])),
                        BulletOutlineWidth = Convert.ToInt32(row["BulletOutlineWidth"]),
                    });
                }

                DebugLogger.PrintGO($"Loaded {barrels.Count} barrel(s) for agent ID={agentId}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"BarrelLoader.LoadBarrelsForAgent({agentId}) failed: {ex.Message}");
            }

            return barrels;
        }
    }
}
