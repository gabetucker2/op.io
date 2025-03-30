using System;
using System.Data.SQLite;
using System.IO;

namespace op.io
{
    public static class DatabaseConfig
    {
        // Ensure consistent path resolution by using AppContext.BaseDirectory
        private static readonly string ProjectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName; // Ensure we're at the project root
        public static readonly string DatabaseDirectory = Path.Combine(ProjectRoot, "Data");
        private static readonly string DatabaseFileName = "op.io.db";

        public static string DatabaseFilePath => Path.Combine(DatabaseDirectory, DatabaseFileName);
        public static string ConnectionString => $"Data Source={DatabaseFilePath};Version=3;";

        // Prevent redundant PRAGMA configuration
        private static bool IsConfigured = false;

        // Ensure the directory exists before trying to access the database file
        static DatabaseConfig()
        {
            DebugLogger.PrintMeta("DatabaseConfig static constructor called.");

            if (!Directory.Exists(DatabaseDirectory))
            {
                Directory.CreateDirectory(DatabaseDirectory);
                DebugLogger.PrintMeta($"Created missing database directory at: {DatabaseDirectory}");
            }

            DebugLogger.PrintMeta($"Using database file path: {DatabaseFilePath}");
            DebugLogger.PrintMeta($"Initial value of IsConfigured: {IsConfigured}");
        }

        // Method to read a setting from the DebugSettings table
        public static T GetSetting<T>(string tableName, string columnName, string settingKey, T defaultValue)
        {
            try
            {
                DebugLogger.PrintMeta($"Attempting to open database connection to: {DatabaseFilePath}");

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    DebugLogger.PrintMeta($"Database connection opened successfully to: {DatabaseFilePath}");

                    ConfigureDatabase(connection); // Ensure PRAGMA settings are applied

                    string query = $"SELECT {columnName} FROM {tableName} WHERE Setting = @settingKey;";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@settingKey", settingKey);

                        var result = command.ExecuteScalar();

                        if (result == DBNull.Value || result == null)
                        {
                            DebugLogger.PrintWarning($"No result found for {columnName} in {tableName} where Setting = '{settingKey}'. Returning default value: {defaultValue}");
                            return defaultValue;
                        }

                        return (T)Convert.ChangeType(result, typeof(T));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to retrieve setting '{settingKey}' from {tableName}: {ex.Message}");
                return defaultValue;
            }
        }

        // Method to update a setting in the DebugSettings table
        public static void UpdateSetting(string tableName, string columnName, string settingKey, string group, int newValue)
        {
            try
            {
                DebugLogger.PrintMeta($"Attempting to open database connection to: {DatabaseFilePath}");

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    DebugLogger.PrintMeta($"Database connection opened successfully to: {DatabaseFilePath}");

                    ConfigureDatabase(connection); // Ensure PRAGMA settings are applied

                    string query = $"UPDATE {tableName} SET {columnName} = @newValue WHERE Setting = @settingKey AND Group = @group;";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@newValue", newValue);
                        command.Parameters.AddWithValue("@settingKey", settingKey);
                        command.Parameters.AddWithValue("@group", group);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            DebugLogger.PrintMeta($"Successfully updated '{settingKey}' in {tableName}.");
                        }
                        else
                        {
                            DebugLogger.PrintWarning($"No rows updated for '{settingKey}' in {tableName}. Check the provided parameters.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to update setting '{settingKey}' in {tableName}: {ex.Message}");
            }
        }

        public static void ConfigureDatabase(SQLiteConnection connection)
        {
            var caller = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name;
            DebugLogger.PrintMeta($"ConfigureDatabase called by: {caller}, Thread ID: {Environment.CurrentManagedThreadId}, IsConfigured: {IsConfigured}");

            if (IsConfigured)
            {
                DebugLogger.PrintMeta("Skipping redundant PRAGMA configuration (Already configured).");
                return;
            }

            try
            {
                DebugLogger.PrintMeta($"Attempting to configure database with PRAGMA settings on connection: {connection.ConnectionString}");

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = "PRAGMA foreign_keys = ON;";
                    command.ExecuteNonQuery();
                    DebugLogger.PrintMeta("Applied: PRAGMA foreign_keys = ON;");

                    command.CommandText = "PRAGMA synchronous = NORMAL;";
                    command.ExecuteNonQuery();
                    DebugLogger.PrintMeta("Applied: PRAGMA synchronous = NORMAL;");

                    command.CommandText = "PRAGMA journal_mode = WAL;";
                    command.ExecuteNonQuery();
                    DebugLogger.PrintMeta("Applied: PRAGMA journal_mode = WAL;");
                }

                IsConfigured = true;
                DebugLogger.PrintMeta($"IsConfigured set to TRUE - Thread ID: {Environment.CurrentManagedThreadId}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to configure database with PRAGMA settings: {ex.Message}");
            }
        }

        public static int LoadDebugSettings()
        {
            DebugLogger.PrintMeta("Attempting to load DebugSettings...");
            int result = GetSetting<int>("DebugSettings", "Enabled", "General", 0);
            DebugLogger.PrintMeta($"Loaded DebugSettings value: {result}");
            return result;
        }

        public static void ToggleDebugMode(int newState)
        {
            DebugLogger.PrintMeta("Attempting to toggle debug mode...");
            UpdateSetting("DebugSettings", "Enabled", "General", "General", newState);
            DebugLogger.PrintMeta($"Toggled debug mode to: {newState}");
        }
    }
}
