using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace op.io
{
    public static class DatabaseQuery
    {
        private static readonly string ConnectionString = DatabaseConfig.ConnectionString;

        public static List<Dictionary<string, object>> ExecuteQuery(string query, Dictionary<string, object> parameters = null)
        {
            List<Dictionary<string, object>> results = new();

            // Debugging: Log the SQL query and parameters (if any)
            DebugLogger.PrintDatabase($"Executing query: {query}");
            if (parameters != null && parameters.Count > 0)
            {
                DebugLogger.PrintDatabase("With parameters:");
                foreach (var param in parameters)
                {
                    DebugLogger.PrintDatabase($"  {param.Key}: {param.Value}");
                }
            }
            else
            {
                DebugLogger.PrintDatabase("No parameters provided.");
            }

            try
            {
                using var connection = new SQLiteConnection(ConnectionString);
                DebugLogger.PrintDatabase("Opening database connection...");
                connection.Open();
                DebugLogger.PrintDatabase("Database connection opened successfully.");

                using var command = new SQLiteCommand(query, connection);
                // Adding parameters to command
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value);
                    }
                }

                // Execute the query and read the data
                using var reader = command.ExecuteReader();
                DebugLogger.PrintDatabase("Executing query and reading results...");

                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.GetValue(i);
                    }
                    results.Add(row);
                }

                DebugLogger.PrintDatabase($"Query executed successfully. Retrieved {results.Count} rows.");
            }
            catch (SQLiteException ex)
            {
                // Log SQL specific errors
                DebugLogger.PrintError($"SQLite error while executing query: {ex.Message}");
            }
            catch (Exception ex)
            {
                // General exception handler
                DebugLogger.PrintError($"General error while executing query: {ex.Message}");
            }

            return results;
        }

        public static void ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
        {
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            using var command = new SQLiteCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }
            command.ExecuteNonQuery();
        }
    }
}
