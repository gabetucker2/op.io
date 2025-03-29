using System;
using System.IO;
using System.Data.SQLite;
using System.Threading;

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

            string fullPath = Path.GetFullPath(DatabaseConfig.DatabaseFilePath);
            DebugLogger.PrintMeta($"Full database path being accessed: {fullPath}");

            // Ensure database deletion only happens if debug mode is enabled.
            if (DebugModeHandler.IsDebugEnabled())
            {
                DeleteDatabaseIfExists(fullPath);
            }

            CreateDatabaseIfNotExists(fullPath);

            string structurePath = Path.Combine("Data", "InitDatabaseStructure.sql");
            string dataPath = Path.Combine("Data", "InitDatabaseStartData.sql");

            using (var connection = DatabaseManager.OpenConnection())
            {
                if (connection == null)
                {
                    DebugLogger.PrintError("Failed to open database connection. Initialization aborted.");
                    return;
                }

                DatabaseManager.ConfigureDatabase(connection);

                bool structureLoaded = SQLScriptExecutor.RunSQLScript(connection, structurePath);
                bool dataLoaded = SQLScriptExecutor.RunSQLScript(connection, dataPath);

                if (structureLoaded && dataLoaded)
                {
                    DebugLogger.PrintMeta("Database structure and data scripts loaded successfully.");
                    VerifyDebugSettingsTable(connection);
                    WarnIfDuplicateSettings(connection);

                    DebugLogger.PrintMeta("Database initialization complete.");
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

            bool deleted = false;
            int maxRetries = 5;
            int retryDelay = 200; // Milliseconds

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                DebugLogger.PrintDebug($"Attempting deletion - Attempt {attempt}/{maxRetries}");

                try
                {
                    DatabaseManager.ClearConnectionPool();

                    File.Delete(fullPath);
                    DebugLogger.PrintMeta($"Successfully deleted existing database file at: {fullPath} on attempt {attempt}.");
                    deleted = true;
                    break;
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintError($"Attempt {attempt}/{maxRetries}: Error while deleting database file. Reason: {ex.Message}");
                    Thread.Sleep(retryDelay);
                }
            }

            if (!deleted)
            {
                DebugLogger.PrintError("Failed to delete the database file after maximum retry attempts. Proceeding anyway.");
            }
        }

        private static void CreateDatabaseIfNotExists(string fullPath)
        {
            if (File.Exists(fullPath)) return;

            try
            {
                DebugLogger.PrintDebug("Creating new database file...");
                SQLiteConnection.CreateFile(fullPath);
                DebugLogger.PrintMeta($"Created new database file at: {fullPath}");
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
                    DebugLogger.PrintMeta("DebugSettings table contents after initialization:");

                    while (reader.Read())
                    {
                        string setting = reader.GetString(0);
                        bool enabled = reader.GetBoolean(1);
                        int maxRepeats = reader.GetInt32(2);

                        DebugLogger.PrintMeta($"Retrieved entry -> Setting: {setting}, Enabled: {enabled}, MaxRepeats: {maxRepeats}");
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
