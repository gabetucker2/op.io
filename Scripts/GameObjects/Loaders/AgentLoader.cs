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
                    extraColumns: "s.IsPlayer, s.TriggerCooldown, s.SwitchCooldown, s.BaseSpeed"
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
                    extraColumns: "s.IsPlayer, s.TriggerCooldown, s.SwitchCooldown, s.BaseSpeed"
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

                PlayerGameObject archetype = new(baseObject, baseSpeed, isPlayer, default, default);
                return archetype.ToAgent();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error deserializing agent: {ex.Message}");
                return null;
            }
        }
    }
}
