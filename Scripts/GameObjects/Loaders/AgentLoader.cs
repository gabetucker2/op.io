using System;
using System.Collections.Generic;

namespace op.io
{
    public static class AgentLoader
    {
        /// <summary>
        /// Loads a single agent by ID, joining with GameObjects.
        /// </summary>
        public static Agent LoadAgent(int agentId)
        {
            try
            {
                DebugLogger.PrintGO($"Loading Agent with ID: {agentId} from database...");

                string query = GameObjectManager.BuildJoinQuery(
                    secondaryTable: "Agents",
                    whereClause: "g.ID = @ID",
                    extraColumns: "s.IsPlayer, s.TriggerCooldown, s.SwitchCooldown, s.BaseSpeed, " +
                                  "COALESCE(s.Mass, g.Mass) AS BodyMass, " +
                                  "s.HealthRegen, s.HealthRegenDelay, s.HealthArmor, " +
                                  "s.MaxShield, s.ShieldRegen, s.ShieldRegenDelay, s.ShieldArmor, " +
                                  "s.BodyPenetration, s.BodyCollisionDamage, " +
                                  "s.CollisionDamageResistance, s.BulletDamageResistance, " +
                                  "COALESCE(s.Speed, 1.0) AS Speed, COALESCE(s.Control, 1.0) AS Control, " +
                                  "COALESCE(s.BodyActionBuff, 0.0) AS BodyActionBuff, " +
                                  "COALESCE(s.MaxXP, 0) AS MaxXP"
                );

                var result = DatabaseQuery.ExecuteQuery(query, new Dictionary<string, object> { { "@ID", agentId } });

                if (result.Count == 0)
                {
                    DebugLogger.PrintError($"No agent found with ID: {agentId}");
                    return null;
                }

                var row = result[0];
                DebugLogger.PrintDatabase($"Row keys: {string.Join(", ", row.Keys)}");

                var agent = DeserializeAgentGO(row);

                if (agent != null)
                {
                    DebugLogger.PrintGO($"Successfully created Agent ID={agent.ID} at {agent.Position}");
                }
                else
                {
                    DebugLogger.PrintWarning($"Deserialization returned null for agent ID {agentId}.");
                }

                return agent;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load agent with ID {agentId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads all agents, joining with GameObjects.
        /// </summary>
        public static List<Agent> LoadAgents()
        {
            var agents = new List<Agent>();

            try
            {
                DebugLogger.PrintGO("Loading all Agents from database...");

                string query = GameObjectManager.BuildJoinQuery(
                    secondaryTable: "Agents",
                    extraColumns: "s.IsPlayer, s.TriggerCooldown, s.SwitchCooldown, s.BaseSpeed, " +
                                  "COALESCE(s.Mass, g.Mass) AS BodyMass, " +
                                  "s.HealthRegen, s.HealthRegenDelay, s.HealthArmor, " +
                                  "s.MaxShield, s.ShieldRegen, s.ShieldRegenDelay, s.ShieldArmor, " +
                                  "s.BodyPenetration, s.BodyCollisionDamage, " +
                                  "s.CollisionDamageResistance, s.BulletDamageResistance, " +
                                  "COALESCE(s.Speed, 1.0) AS Speed, COALESCE(s.Control, 1.0) AS Control, " +
                                  "COALESCE(s.BodyActionBuff, 0.0) AS BodyActionBuff, " +
                                  "COALESCE(s.MaxXP, 0) AS MaxXP"
                );

                var results = DatabaseQuery.ExecuteQuery(query);

                if (results.Count == 0)
                {
                    DebugLogger.PrintWarning("No agents found in database.");
                    return agents;
                }

                DebugLogger.PrintDatabase($"Retrieved {results.Count} rows from Agents + GameObjects.");

                foreach (var row in results)
                {
                    DebugLogger.PrintDatabase($"Row keys: {string.Join(", ", row.Keys)}");

                    var agent = DeserializeAgentGO(row);
                    if (agent != null)
                    {
                        agents.Add(agent);
                        DebugLogger.PrintGO($"Loaded Agent ID={agent.ID}, Pos={agent.Position}, Shape={agent.Shape?.ShapeType ?? "NULL"}");
                    }
                    else
                    {
                        DebugLogger.PrintWarning("Skipped null Agent after deserialization.");
                    }
                }

                DebugLogger.PrintGO($"Total agents loaded: {agents.Count}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load agents: {ex.Message}");
            }

            return agents;
        }

        private static Agent DeserializeAgentGO(Dictionary<string, object> row)
        {
            try
            {
                if (!GameObjectLoader.TryDeserializeSimpleGameObject(row, out SimpleGameObject baseObject))
                {
                    DebugLogger.PrintWarning("Skipped agent row after failing to deserialize base GameObject data.");
                    return null;
                }

                float baseSpeed = Convert.ToSingle(row["BaseSpeed"]);
                bool isPlayer = Convert.ToBoolean(row["IsPlayer"]);

                Attributes_Body bodyAttributes = new()
                {
                    Mass                      = row.ContainsKey("BodyMass") ? Convert.ToSingle(row["BodyMass"]) : Convert.ToSingle(row["Mass"]),
                    HealthRegen               = Convert.ToSingle(row["HealthRegen"]),
                    HealthRegenDelay          = Convert.ToSingle(row["HealthRegenDelay"]),
                    HealthArmor               = Convert.ToSingle(row["HealthArmor"]),
                    MaxShield                 = Convert.ToSingle(row["MaxShield"]),
                    ShieldRegen               = Convert.ToSingle(row["ShieldRegen"]),
                    ShieldRegenDelay          = Convert.ToSingle(row["ShieldRegenDelay"]),
                    ShieldArmor               = Convert.ToSingle(row["ShieldArmor"]),
                    BodyPenetration           = Convert.ToSingle(row["BodyPenetration"]),
                    BodyCollisionDamage       = Convert.ToSingle(row["BodyCollisionDamage"]),
                    CollisionDamageResistance = Convert.ToSingle(row["CollisionDamageResistance"]),
                    BulletDamageResistance    = Convert.ToSingle(row["BulletDamageResistance"]),
                    Speed                     = row.ContainsKey("Speed")   ? Convert.ToSingle(row["Speed"])   : 1.0f,
                    Control                   = row.ContainsKey("Control") ? Convert.ToSingle(row["Control"]) : 1.0f,
                    BodyActionBuff            = row.ContainsKey("BodyActionBuff") ? Convert.ToSingle(row["BodyActionBuff"]) : 0.0f,
                };

                float maxXP = row.ContainsKey("MaxXP") ? Convert.ToSingle(row["MaxXP"]) : 0f;
                PlayerGameObject archetype = new(baseObject, baseSpeed, isPlayer, default, bodyAttributes);
                Agent agent = archetype.ToAgent();
                agent.Mass = bodyAttributes.Mass; // override GO mass with body attribute mass
                agent.MaxXP = maxXP;
                return agent;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error deserializing agent: {ex.Message}");
                return null;
            }
        }
    }
}
