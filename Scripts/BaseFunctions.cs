using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using SDL2;
using System.Data.SQLite;
using System.Linq;

namespace op.io
{
    public static class BaseFunctions
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        public static T GetValue<T>(
            string tableName,
            string column,
            string conditionColumn,
            object conditionValue,
            bool suppressLog = false)
        {
            string query = $"SELECT {column} FROM {tableName} WHERE {conditionColumn} = @value LIMIT 1;";
            var result = DatabaseQuery.ExecuteQuery(query, new Dictionary<string, object> { { "@value", conditionValue } });

            if (result.Count > 0 && result[0].ContainsKey(column))
            {
                return (T)Convert.ChangeType(result[0][column], typeof(T));
            }

            DebugLogger.PrintError($"No data collected in {column} from {tableName}");

            return default;
        }

        /// <summary>
        /// Executes a SQL query and returns a single value of the specified type.
        /// </summary>
        public static T GetSingleValue<T>(string query, params (string, object)[] parameters)
        {
            // Open a connection to the database
            using (var connection = DatabaseManager.OpenConnection())
            {
                if (connection == null)
                {
                    DebugLogger.PrintError("Database connection failed. Returning default value.");
                    return default; // Return default if the connection could not be opened
                }

                try
                {
                    // Construct the SQLite command with the provided query and parameters
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        // Add parameters to the command
                        foreach (var (key, value) in parameters)
                        {
                            command.Parameters.AddWithValue(key, value);
                        }

                        // Execute the query and get the result
                        object result = command.ExecuteScalar();

                        // Check if the result is valid and return it
                        if (result != null && result != DBNull.Value)
                        {
                            DebugLogger.PrintDatabase($"Successfully retrieved value: {result} for query: {query}");
                            return (T)Convert.ChangeType(result, typeof(T));
                        }
                        else
                        {
                            // If result is null or DBNull, log a warning and return the default value
                            DebugLogger.PrintWarning($"No result found for query: {query} with parameters: {string.Join(", ", parameters.Select(p => $"{p.Item1}: {p.Item2}"))}");
                            return default; // Return default value if no result is found
                        }
                    }
                }
                catch (SQLiteException sqlEx)
                {
                    // Log any specific SQLite errors
                    DebugLogger.PrintError($"SQLite error while executing query: {query} - {sqlEx.Message}");
                }
                catch (Exception ex)
                {
                    // Catch any other exceptions
                    DebugLogger.PrintError($"Failed to execute query: {query} - {ex.Message}");
                }
            }

            return default; // Return default if an error occurred
        }

        /// <summary>
        /// Retrieves a color value from the database.
        /// </summary>
        public static Color GetColor(string tableName, string conditionColumn, object conditionValue, Color defaultColor)
        {
            try
            {
                int r = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_R");
                int g = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_G");
                int b = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_B");
                int a = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_A");

                Color color = new Color(r, g, b, a);
                DebugLogger.Print($"Retrieved color from {tableName} -> {color}");
                return color;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to retrieve color from {tableName}: {ex.Message}");
                return defaultColor;
            }
        }

        /// <summary>
        /// Parses a color from a JSON document.
        /// </summary>
        public static Color ParseColor(JsonElement element)
        {
            try
            {
                int r = element.GetProperty("R").GetInt32();
                int g = element.GetProperty("G").GetInt32();
                int b = element.GetProperty("B").GetInt32();
                int a = element.GetProperty("A").GetInt32();

                Color color = new Color(r, g, b, a);
                DebugLogger.Print($"Parsed color from JSON -> {color}");
                return color;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to parse color from JSON: {ex.Message}");
                return Color.White; // Default fallback
            }
        }

        public static Vector2 GetGOLocalScreenPosition(GameObject gameObject)
        {
            var player = Core.Instance.Player;

            if (player == null)
            {
                DebugLogger.PrintError("Player instance is null. Make sure the player is properly initialized.");
                return new Vector2(0,0);
            }

            // Assuming Player's position is stored as a Vector2
            Vector2 playerPosition = new(player.Position.X, player.Position.Y);

            // Convert player's position to screen coordinates
            Vector2 screenPosition = new((int)playerPosition.X, (int)playerPosition.Y);

            DebugLogger.PrintUI($"Player screen position calculated: {screenPosition}");

            return screenPosition;
        }

        public static Vector2 GetGOGlobalScreenPosition(GameObject gameObject)
        {
            var player = Core.Instance.Player;

            if (player == null)
            {
                DebugLogger.PrintError("Player instance is null. Make sure the player is properly initialized.");
                return new Vector2(0, 0);
            }

            // Get the local screen position of the player
            Vector2 localScreenPosition = GetGOLocalScreenPosition(gameObject);
            DebugLogger.PrintUI($"Local screen position of player: {localScreenPosition}");

            IntPtr windowHandle = Core.Instance?.Window?.Handle ?? IntPtr.Zero;

            if (windowHandle == IntPtr.Zero)
            {
                DebugLogger.PrintError("Failed to retrieve valid game window handle.");
                return localScreenPosition;
            }

            DebugLogger.PrintUI($"Window Handle Retrieved: {windowHandle}");

            // SDL requires window handle from SDL2 functions
            int windowX = 0, windowY = 0;
            SDL.SDL_GetWindowPosition(windowHandle, out windowX, out windowY);

            if (windowX == 0 && windowY == 0)
            {
                DebugLogger.PrintError("Failed to retrieve SDL window position. Ensure SDL2 is correctly integrated.");
                return localScreenPosition;
            }

            DebugLogger.PrintUI($"SDL Window Position Retrieved: X={windowX}, Y={windowY}");

            // Calculate global screen position
            Vector2 globalScreenPosition = new(
                localScreenPosition.X + windowX,
                localScreenPosition.Y + windowY
            );

            DebugLogger.PrintUI($"Player global screen position calculated: {globalScreenPosition}");

            return globalScreenPosition;
        }

        public static System.Drawing.Point Vector2ToPoint(Vector2 vector)
        {
            return new System.Drawing.Point((int)vector.X, (int)vector.Y);
        }

        public static bool IntToBool(int value)
        {
            return value == 1;
        }

        public static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

    }
}
