using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using System.Data.SQLite;
using System.Linq;

namespace op.io // TODO: Migrate this script elsewhere
{
    public static class DatabaseFetch
    {
        public static T GetValue<T>(
            string tableName,
            string column,
            string conditionColumn,
            object conditionValue)
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

        public static T GetSetting<T>(string table, string column, string whereColumn, string whereValue, T defaultValue)
        {
            using (var connection = DatabaseManager.OpenConnection())
            {
                if (connection == null) return defaultValue;

                try
                {
                    string query = $"SELECT {column} FROM {table} WHERE {whereColumn} = @whereValue LIMIT 1;";
                    DebugLogger.PrintDatabase($"Executing query: {query} with whereValue: {whereValue}");

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@whereValue", whereValue);

                        object result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            DebugLogger.PrintDatabase($"Successfully retrieved setting '{whereValue}' from '{table}'.");
                            return (T)Convert.ChangeType(result, typeof(T));
                        }
                        else
                        {
                            DebugLogger.PrintWarning($"No result found for query: {query}. Check if the data exists in the database.");
                        }
                    }
                }
                catch (SQLiteException sqlEx)
                {
                    // Log any SQL specific errors
                    DebugLogger.PrintError($"SQLite error while executing query on table '{table}': {sqlEx.Message}");
                }
                catch (Exception ex)
                {
                    // Log any other general exceptions
                    DebugLogger.PrintError($"Failed to retrieve setting from table '{table}': {ex.Message}");
                }
                finally
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }

            return defaultValue;
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
        /// Loads all rows from UITooltips and returns them as a RowKey-keyed dictionary of (Text, DataType).
        /// </summary>
        public static Dictionary<string, (string Text, string DataType)> LoadBlockTooltips()
        {
            var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

            using var connection = DatabaseManager.OpenConnection();
            if (connection == null)
            {
                return result;
            }

            try
            {
                using var command = new SQLiteCommand("SELECT RowKey, TooltipText, DataType FROM UITooltips;", connection);
                using SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string rowKey = reader.GetString(0);
                    string text = reader.GetString(1);
                    string dataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    result[rowKey] = (text, dataType);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load block tooltips: {ex.Message}");
            }
            finally
            {
                DatabaseManager.CloseConnection(connection);
            }

            return result;
        }

        /// <summary>
        /// Retrieves a FadeIn|Hold|FadeOut animation triplet stored as "x|y|z" in the given settings table.
        /// </summary>
        public static (float FadeIn, float Hold, float FadeOut) GetAnimSetting(
            string key, float defaultFadeIn = 0f, float defaultHold = 0f, float defaultFadeOut = 0f,
            string tableName = "FXSettings")
        {
            string raw = GetSetting<string>(tableName, "Value", "SettingKey", key, null);
            if (raw != null)
            {
                var parts = raw.Split('|');
                if (parts.Length == 3 &&
                    float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fi) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fo))
                    return (fi, h, fo);
            }
            return (defaultFadeIn, defaultHold, defaultFadeOut);
        }

        /// <summary>
        /// Retrieves a color value from the database.
        /// </summary>
        public static Color GetColor(string tableName, string conditionColumn, object conditionValue)
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
                return Core.DefaultColor;
            }
        }

    }
}
