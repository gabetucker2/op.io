using System;
using System.IO;
using System.Data.SQLite;
using System.Collections.Generic;

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

            string fullPath = DatabaseConfig.DatabaseFilePath;
            DebugLogger.PrintDatabase($"Using database file path: {fullPath}");

            DeleteDatabaseIfExists(fullPath);
            CreateDatabaseIfNotExists(fullPath);

            using var connection = DatabaseManager.OpenConnection();
            if (connection == null)
            {
                DebugLogger.PrintError("Failed to open database connection. Initialization aborted.");
                return;
            }

            DatabaseConfig.ConfigureDatabase(connection);

            // Load structure scripts FIRST
            LoadStructureScripts(connection);

            // Verify tables exist BEFORE inserting data
            VerifyTablesExistence(connection);

            // Insert Data
            LoadStartData(connection);

            DebugLogger.PrintDatabase("Database initialization complete.");
            DatabaseManager.CloseConnection(connection);
        }

        private static void LoadStructureScripts(SQLiteConnection connection)
        {
            string structurePathSettings = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_Settings.sql");
            string structurePathGOs = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_GOs.sql");

            SQLScriptExecutor.RunSQLScript(connection, structurePathSettings);
            SQLScriptExecutor.RunSQLScript(connection, structurePathGOs);

            DebugLogger.PrintDatabase("Database structure scripts loaded successfully.");
        }

        private static void LoadStartData(SQLiteConnection connection)
        {
            string dataPathSettings = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Settings.sql");
            string dataPathGOs = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_GOs.sql");

            SQLScriptExecutor.RunSQLScript(connection, dataPathSettings);
            SQLScriptExecutor.RunSQLScript(connection, dataPathGOs);
        }

        private static void VerifyTablesExistence(SQLiteConnection connection)
        {
            try
            {
                string[] requiredTables = { "GameObjects", "Agents", "FarmData", "MapData" };
                foreach (string table in requiredTables)
                {
                    using var command = new SQLiteCommand($"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}';", connection);
                    var result = command.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                    {
                        DebugLogger.PrintError($"Required table '{table}' is missing. Ensure your structure scripts are correct.");
                    }
                    else
                    {
                        DebugLogger.PrintDatabase($"Verified table exists: {table}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error verifying tables: {ex.Message}");
            }
        }

        private static void DeleteDatabaseIfExists(string fullPath)
        {
            if (!File.Exists(fullPath)) return;

            try
            {
                SQLiteConnection.ClearAllPools();
                File.Delete(fullPath);
                DebugLogger.PrintDatabase($"Successfully deleted existing database file at: {fullPath}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Error deleting database file: {ex.Message}");
            }
        }

        private static void CreateDatabaseIfNotExists(string fullPath)
        {
            if (File.Exists(fullPath)) return;

            try
            {
                SQLiteConnection.CreateFile(fullPath);
                DebugLogger.PrintDatabase($"Created new database file at: {fullPath}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to create database file at {fullPath}: {ex.Message}");
            }
        }
    }
}
