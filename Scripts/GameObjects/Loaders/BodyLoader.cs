using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class BodyLoader
    {
        /// <summary>
        /// Loads all body prototypes assigned to the given agent, ordered by SlotIndex.
        /// Returns (Name, Attributes) tuples. Empty list if the agent has no body rows;
        /// the caller should then fall back to the body attributes on the Agent from the Agents table.
        /// </summary>
        public static List<(string Name, Attributes_Body Attrs, Color? FillColor, Color? OutlineColor, int? OutlineWidth)> LoadBodiesForAgent(int agentId)
        {
            var bodies = new List<(string, Attributes_Body, Color?, Color?, int?)>();

            try
            {
                string query =
                    "SELECT bp.Name, bp.Mass, " +
                    "       bp.HealthRegen, bp.HealthRegenDelay, bp.HealthArmor, " +
                    "       bp.MaxShield, bp.ShieldRegen, bp.ShieldRegenDelay, bp.ShieldArmor, " +
                    "       bp.BodyCollisionDamage, bp.BodyPenetration, " +
                    "       bp.CollisionDamageResistance, bp.BulletDamageResistance, " +
                    "       bp.Speed, bp.Control, bp.Sight, bp.BodyActionBuff, " +
                    "       bp.FillR, bp.FillG, bp.FillB, bp.FillA, " +
                    "       bp.OutlineR, bp.OutlineG, bp.OutlineB, bp.OutlineA, bp.OutlineWidth " +
                    "FROM AgentBodies ab " +
                    "JOIN BodyPrototypes bp ON ab.BodyPrototypeID = bp.ID " +
                    "WHERE ab.AgentID = @AgentID " +
                    "ORDER BY ab.SlotIndex;";

                var results = DatabaseQuery.ExecuteQuery(query,
                    new Dictionary<string, object> { { "@AgentID", agentId } });

                foreach (var row in results)
                {
                    string name = row.ContainsKey("Name") ? row["Name"]?.ToString() : null;
                    var attrs = new Attributes_Body
                    {
                        Mass                      = Convert.ToSingle(row["Mass"]),
                        HealthRegen               = Convert.ToSingle(row["HealthRegen"]),
                        HealthRegenDelay          = Convert.ToSingle(row["HealthRegenDelay"]),
                        HealthArmor               = Convert.ToSingle(row["HealthArmor"]),
                        MaxShield                 = Convert.ToSingle(row["MaxShield"]),
                        ShieldRegen               = Convert.ToSingle(row["ShieldRegen"]),
                        ShieldRegenDelay          = Convert.ToSingle(row["ShieldRegenDelay"]),
                        ShieldArmor               = Convert.ToSingle(row["ShieldArmor"]),
                        BodyCollisionDamage       = Convert.ToSingle(row["BodyCollisionDamage"]),
                        BodyPenetration           = Convert.ToSingle(row["BodyPenetration"]),
                        CollisionDamageResistance = Convert.ToSingle(row["CollisionDamageResistance"]),
                        BulletDamageResistance    = Convert.ToSingle(row["BulletDamageResistance"]),
                        Speed                     = Convert.ToSingle(row["Speed"]),
                        Control                   = Convert.ToSingle(row["Control"]),
                        Sight                     = row.ContainsKey("Sight") ? Convert.ToSingle(row["Sight"]) : 0.0f,
                        BodyActionBuff            = Convert.ToSingle(row["BodyActionBuff"]),
                    };
                    Color? fillColor = TryReadOptionalColor(row, "Fill");
                    Color? outlineColor = TryReadOptionalColor(row, "Outline");
                    int? outlineWidth = TryReadOptionalInt(row, "OutlineWidth");

                    bodies.Add((name, attrs, fillColor, outlineColor, outlineWidth));
                }

                DebugLogger.PrintGO($"Loaded {bodies.Count} body/bodies for agent ID={agentId}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"BodyLoader.LoadBodiesForAgent({agentId}) failed: {ex.Message}");
            }

            return bodies;
        }

        private static Color? TryReadOptionalColor(IReadOnlyDictionary<string, object> row, string prefix)
        {
            int? r = TryReadOptionalInt(row, $"{prefix}R");
            int? g = TryReadOptionalInt(row, $"{prefix}G");
            int? b = TryReadOptionalInt(row, $"{prefix}B");
            int? a = TryReadOptionalInt(row, $"{prefix}A");

            if (!r.HasValue || !g.HasValue || !b.HasValue || !a.HasValue)
            {
                return null;
            }

            return new Color(r.Value, g.Value, b.Value, a.Value);
        }

        private static int? TryReadOptionalInt(IReadOnlyDictionary<string, object> row, string key)
        {
            if (!row.TryGetValue(key, out object value) || value == null || value == DBNull.Value)
            {
                return null;
            }

            int resolved = Convert.ToInt32(value);
            return resolved >= 0 ? resolved : null;
        }
    }
}
