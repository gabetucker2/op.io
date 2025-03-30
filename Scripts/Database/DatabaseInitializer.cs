using System;
using System.IO;
using System.Data.SQLite;

namespace op.io
{
    public static class DatabaseInitializer
    {
        private static bool _alreadyInitialized = false;

        public static void InitializeDatabase()
        {
            if (_alreadyInitialized)
            {
                DebugLogger.PrintWarning("InitializeDatabase() called more than once. Skipping reinitialization.");
                return;
            }

            _alreadyInitialized = true;

            // Use the DatabaseConfig for consistent path resolution
            string fullPath = DatabaseConfig.DatabaseFilePath;
            DebugLogger.PrintDatabase($"Using database file path: {fullPath}");

            DeleteDatabaseIfExists(fullPath);
            CreateDatabaseIfNotExists(fullPath);

            string structurePath = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDatabaseStructure.sql");
            string dataPath = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDatabaseStartData.sql");

            using (var connection = DatabaseManager.OpenConnection())
            {
                if (connection == null)
                {
                    DebugLogger.PrintError("Failed to open database connection. Initialization aborted.");
                    return;
                }

                DatabaseConfig.ConfigureDatabase(connection);

                bool structureLoaded = SQLScriptExecutor.RunSQLScript(connection, structurePath);
                bool dataLoaded = SQLScriptExecutor.RunSQLScript(connection, dataPath);

                if (structureLoaded && dataLoaded)
                {
                    DebugLogger.PrintDatabase("Database structure and data scripts loaded successfully.");
                    VerifyDebugSettingsTable(connection);
                    WarnIfDuplicateSettings(connection);

                    DebugLogger.PrintDatabase("Database initialization complete.");
                }
                else
                {
                    DebugLogger.PrintError("Database initialization incomplete. Check your SQL scripts or paths.");
                }

                DatabaseManager.CloseConnection(connection);
            }
        }

        private static void DeleteDatabaseIfExists(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                DebugLogger.PrintWarning($"Database file not found at: {fullPath}");
                return;
            }

            try
            {
                DebugLogger.PrintDatabase("Attempting to rxisting database...");

                SQLiteConnection.ClearAllPools(); // Clear any connection pool
                File.Delete(fullPath);
                DebugLogger.PrintDatabase($"Successfully deleted existing database file at: {fullPath}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error deleting database file. Reason: {ex.Message}");
            }
        }

        private static void CreateDatabaseIfNotExists(string fullPath)
        {
            if (File.Exists(fullPath)) return;

            try
            {
                DebugLogger.PrintDatabase("Creating new database file...");
                SQLiteConnection.CreateFile(fullPath);
                DebugLogger.PrintDatabase($"Created new database file at: {fullPath}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to create a new database file at {fullPath}: {ex.Message}");
            }
        }

        private static void VerifyDebugSettingsTable(SQLiteConnection connection)
        {
            try
            {
                using (var command = new SQLiteCommand("SELECT Setting, Enabled, MaxRepeats FROM DebugSettings;", connection))
                using (var reader = command.ExecuteReader())
                {
                    DebugLogger.PrintDatabase("DebugSettings table contents after initialization:");

                    while (reader.Read())
                    {
                        string setting = reader.GetString(0);
                        bool enabled = reader.GetBoolean(1);
                        int maxRepeats = reader.GetInt32(2);

                        DebugLogger.PrintDatabase($"Retrieved entry -> Setting: {setting}, Enabled: {enabled}, MaxRepeats: {maxRepeats}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to read DebugSettings table: {ex.Message}");
            }
        }

        private static void WarnIfDuplicateSettings(SQLiteConnection connection)
        {
            try
            {
                using var command = new SQLiteCommand(@"
                    SELECT SettingKey
                    FROM GeneralSettings
                    GROUP BY SettingKey
                    HAVING COUNT(*) > 1;", connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string key = reader.GetString(0);
                    DebugLogger.PrintWarning($"Duplicate setting '{key}' detected in GeneralSettings. Consider checking your initialization flow.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to check for duplicate settings: {ex.Message}");
            }
        }
    }
}
