using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

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
                // Deserialize the basic properties from the database row
                int id = Convert.ToInt32(row["ID"]);
                string name = row["Name"].ToString();
                string type = row["Type"].ToString();
                Vector2 position = new(Convert.ToSingle(row["PositionX"]), Convert.ToSingle(row["PositionY"]));
                float rotation = Convert.ToSingle(row["Rotation"]);
                float mass = Convert.ToSingle(row["Mass"]);
                bool isDestructible = Convert.ToBoolean(row["IsDestructible"]);
                bool isCollidable = Convert.ToBoolean(row["IsCollidable"]);
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

                string shapeType = row["Shape"]?.ToString() ?? "Rectangle"; // Default to rectangle if no shape is specified

                // Deserialize the shape
                Shape shape = new(shapeType, width, height, sides, fillColor, outlineColor, outlineWidth);

                // Deserialize agent-specific data
                float baseSpeed = Convert.ToSingle(row["BaseSpeed"]);
                bool isPlayer = Convert.ToBoolean(row["IsPlayer"]);

                // Return the Agent with all necessary properties
                return new Agent(
                    id,
                    name,
                    type,
                    position,
                    rotation,
                    mass,
                    isDestructible,
                    isCollidable,
                    staticPhysics,
                    shape,
                    baseSpeed,
                    isPlayer,
                    fillColor,
                    outlineColor,
                    outlineWidth
                );
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error deserializing agent: {ex.Message}");
                return null;
            }
        }
    }
}
