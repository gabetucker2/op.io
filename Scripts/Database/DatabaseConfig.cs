using System;
using System.Collections.Generic;
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
            DebugLogger.PrintDatabase("DatabaseConfig static constructor called.");

            if (!Directory.Exists(DatabaseDirectory))
            {
                Directory.CreateDirectory(DatabaseDirectory);
                DebugLogger.PrintDatabase($"Created missing database directory at: {DatabaseDirectory}");
            }

            DebugLogger.PrintDatabase($"Using database file path: {DatabaseFilePath}");
            DebugLogger.PrintDatabase($"Initial value of IsConfigured: {IsConfigured}");
        }

        // Method to read a setting from the DebugSettings table
        public static T GetSetting<T>(string tableName, string columnName, string settingKey)
        {
            try
            {
                DebugLogger.PrintDatabase($"Attempting to open database connection to: {DatabaseFilePath}");

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    DebugLogger.PrintDatabase($"Database connection opened successfully to: {DatabaseFilePath}");

                    ConfigureDatabase(connection); // Ensure PRAGMA settings are applied

                    string query = $"SELECT {columnName} FROM {tableName} WHERE Setting = @settingKey;";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@settingKey", settingKey);

                        var result = command.ExecuteScalar();

                        if (result == DBNull.Value || result == null)
                        {
                            DebugLogger.PrintWarning($"No result found for {columnName} in {tableName} where Setting = '{settingKey}'.");
                            throw new KeyNotFoundException($"Setting '{settingKey}' not found in {tableName}.");
                        }

                        return (T)Convert.ChangeType(result, typeof(T));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to retrieve setting '{settingKey}' from {tableName}: {ex.Message}");
                throw; // Rethrow the exception to indicate a failure to retrieve the setting
            }
        }

        // Method to update a setting in the DebugSettings table
        public static void UpdateSetting(string tableName, string columnName, string settingKey, int newValue)
        {
            try
            {
                DebugLogger.PrintDatabase($"Attempting to open database connection to: {DatabaseFilePath}");

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    DebugLogger.PrintDatabase($"Database connection opened successfully to: {DatabaseFilePath}");

                    ConfigureDatabase(connection); // Ensure PRAGMA settings are applied

                    // Corrected query to match your table structure (No 'Group' column)
                    string query = $"UPDATE {tableName} SET {columnName} = @newValue WHERE Setting = @settingKey;";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@newValue", newValue);
                        command.Parameters.AddWithValue("@settingKey", settingKey);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            DebugLogger.PrintDatabase($"Successfully updated '{settingKey}' in {tableName}.");
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
            DebugLogger.PrintDatabase($"ConfigureDatabase called by: {caller}, Thread ID: {Environment.CurrentManagedThreadId}, IsConfigured: {IsConfigured}");

            if (IsConfigured)
            {
                DebugLogger.PrintDatabase("Skipping redundant PRAGMA configuration (Already configured).");
                return;
            }

            try
            {
                DebugLogger.PrintDatabase($"Attempting to configure database with PRAGMA settings on connection: {connection.ConnectionString}");

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = "PRAGMA foreign_keys = ON;";
                    command.ExecuteNonQuery();
                    DebugLogger.PrintDatabase("Applied: PRAGMA foreign_keys = ON;");

                    command.CommandText = "PRAGMA synchronous = NORMAL;";
                    command.ExecuteNonQuery();
                    DebugLogger.PrintDatabase("Applied: PRAGMA synchronous = NORMAL;");

                    command.CommandText = "PRAGMA journal_mode = WAL;";
                    command.ExecuteNonQuery();
                    DebugLogger.PrintDatabase("Applied: PRAGMA journal_mode = WAL;");
                }

                IsConfigured = true;
                DebugLogger.PrintDatabase($"IsConfigured set to TRUE - Thread ID: {Environment.CurrentManagedThreadId}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to configure database with PRAGMA settings: {ex.Message}");
            }
        }

        public static int LoadDebugSettings()
        {
            DebugLogger.PrintDatabase("Attempting to load DebugSettings...");
            int result = GetSetting<int>("DebugSettings", "Enabled", "General");
            DebugLogger.PrintDatabase($"Loaded DebugSettings value: {result}");
            return result;
        }

        public static void ToggleDebugMode(int newState)
        {
            DebugLogger.PrintDebug("Attempting to toggle debug mode...");
            UpdateSetting("DebugSettings", "Enabled", "General", newState);
            DebugLogger.PrintDebug($"Toggled debug mode to: {newState}");
        }
    }
}
