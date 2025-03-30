using System;
using System.Data.SQLite;
using System.Collections.Generic;

namespace op.io
{
    public static class DatabaseManager
    {
        private static readonly string ConnectionString = DatabaseConfig.ConnectionString;

        public static SQLiteConnection OpenConnection()
        {
            DebugLogger.PrintDatabase("Opening database connection...");

            var connection = new SQLiteConnection(ConnectionString);

            try
            {
                connection.Open();
                DebugLogger.PrintDatabase("Database connection opened successfully.");
                DebugLogger.PrintDatabase("Database connection is ready for use.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to open database connection: {ex.Message}");
                connection.Dispose();
                return null;
            }

            return connection; // Return the active connection for use
        }

        public static void CloseConnection(SQLiteConnection connection)
        {
            if (connection == null) return;

            try
            {
                connection.Close();
                connection.Dispose();
                DebugLogger.PrintDatabase("Database connection closed successfully.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to close database connection: {ex.Message}");
            }
        }

        public static void ClearConnectionPool()
        {
            SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            DebugLogger.PrintDatabase("Connection pool cleared.");
        }

        public static T GetSetting<T>(string table, string column, string whereColumn, string whereValue, T defaultValue)
        {
            using (var connection = OpenConnection())
            {
                if (connection == null) return defaultValue;

                try
                {
                    string query = $"SELECT {column} FROM {table} WHERE {whereColumn} = @whereValue LIMIT 1;";
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
                            DebugLogger.PrintWarning($"No result found for query: {query}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintError($"Failed to retrieve setting from table '{table}': {ex.Message}");
                }
                finally
                {
                    CloseConnection(connection);
                }
            }

            return defaultValue;
        }

        public static bool UpdateSetting(string table, string column, string whereColumn, string whereValue, object newValue)
        {
            using (var connection = OpenConnection())
            {
                if (connection == null) return false;

                try
                {
                    string query = $"UPDATE {table} SET {column} = @newValue WHERE {whereColumn} = @whereValue;";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@newValue", newValue);
                        command.Parameters.AddWithValue("@whereValue", whereValue);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            DebugLogger.PrintDatabase($"Successfully updated setting '{whereValue}' in table '{table}'.");
                            return true;
                        }
                        else
                        {
                            DebugLogger.PrintWarning($"No rows were updated for setting '{whereValue}' in table '{table}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintError($"Failed to update setting in table '{table}': {ex.Message}");
                }
                finally
                {
                    CloseConnection(connection);
                }
            }

            return false;
        }
    }
}
