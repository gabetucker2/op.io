using System;
using System.IO;
using System.Data.SQLite;

namespace op.io
{
    public static class DatabaseInitializer
    {
        private static readonly string ConnectionString = DatabaseConfig.ConnectionString;
        private static bool _alreadyInitialized = false;

        public static void InitializeDatabase()
        {
            if (_alreadyInitialized)
            {
                DebugManager.PrintWarning("InitializeDatabase() called more than once. Skipping reinitialization.");
                return;
            }

            _alreadyInitialized = true;

            if (DebugManager.IsDebugEnabled() && File.Exists(DatabaseConfig.DatabaseFilePath))
            {
                File.Delete(DatabaseConfig.DatabaseFilePath);
                DebugManager.PrintWarning("Deleted existing database file for clean reinitialization.");
            }
            else if (!File.Exists(DatabaseConfig.DatabaseFilePath))
            {
                DebugManager.PrintMeta($"Database file not found at '{DatabaseConfig.DatabaseFilePath}'. A new one will be created.");
            }

            string structurePath = Path.Combine("Data", "InitDatabaseStructure.sql");
            string dataPath = Path.Combine("Data", "InitDatabaseStartData.sql");

            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();

                foreach (var scriptPath in new[] { structurePath, dataPath })
                {
                    try
                    {
                        RunSQLScript(connection, scriptPath);

                        // Only check for duplicates after the data insert script runs
                        if (Path.GetFileName(scriptPath).Equals("InitDatabaseStartData.sql", StringComparison.OrdinalIgnoreCase))
                        {
                            WarnIfDuplicateSettings(connection);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugManager.PrintError($"Failed executing {Path.GetFileName(scriptPath)}: {ex.Message}");
                    }
                }
            }
        }

        private static void RunSQLScript(SQLiteConnection connection, string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                DebugManager.PrintError($"SQL script not found: {scriptPath}");
                return;
            }

            string script = File.ReadAllText(scriptPath);
            using (var command = new SQLiteCommand(script, connection))
            {
                command.ExecuteNonQuery();
            }

            DebugManager.PrintMeta($"Executed: {Path.GetFileName(scriptPath)}");
        }

        private static void WarnIfDuplicateSettings(SQLiteConnection connection)
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
                DebugManager.PrintWarning($"Duplicate setting '{key}' detected in GeneralSettings. Consider checking your initialization flow.");
            }
        }
    }
}
