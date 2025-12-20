using System;
using System.IO;
using System.Data.SQLite;
using System.Collections.Generic;
using op.io.UI.BlockScripts.BlockUtilities;

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

            string primaryPath = DatabaseConfig.DatabaseFilePath;
            DebugLogger.PrintDatabase($"Using database file path: {primaryPath}");

            bool databaseExists = File.Exists(primaryPath);
            bool shouldReset = Core.RestartDB || !databaseExists;

            if (shouldReset)
            {
                BlockDataStore.ResetCache();
                NotesFileSystem.ResetToDefaultNote();
            }

            if (!shouldReset && databaseExists)
            {
                DebugLogger.PrintDatabase("RestartDB disabled and database already exists. Skipping database reset.");

                using var existingConnection = DatabaseManager.OpenConnection();
                if (existingConnection == null)
                {
                    DebugLogger.PrintError("Failed to open database connection while skipping reset.");
                    return;
                }

                DatabaseConfig.ConfigureDatabase(existingConnection);
                EnsureBlockTables(existingConnection);
                DatabaseManager.CloseConnection(existingConnection);
                _alreadyInitialized = true;
                return;
            }

            DeleteAllDatabaseCopies();
            CreateDatabaseIfNotExists(primaryPath);

            using var connection = DatabaseManager.OpenConnection();
            if (connection == null)
            {
                DebugLogger.PrintError("Failed to open database connection. Initialization aborted.");
                return;
            }

            DatabaseConfig.ConfigureDatabase(connection);

            // Load structure scripts FIRST
            LoadStructureScripts(connection);
            EnsureBlockTables(connection);

            // Verify tables exist BEFORE inserting data
            VerifyTablesExistence(connection);

            // Insert Data
            LoadStartData(connection);

            DebugLogger.PrintDatabase("Database initialization complete.");
            DatabaseManager.CloseConnection(connection);
            _alreadyInitialized = true;
        }

        private static void LoadStructureScripts(SQLiteConnection connection)
        {
            string structurePathSettings = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_Settings.sql");
            string structurePathGOs = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_GOs.sql");

            SQLScriptExecutor.RunSQLScript(connection, structurePathSettings);
            SQLScriptExecutor.RunSQLScript(connection, structurePathGOs);

            DebugLogger.PrintDatabase("Database structure scripts loaded successfully.");
        }

        private static void EnsureBlockTables(SQLiteConnection connection)
        {
            try
            {
                BlockDataStore.EnsureTables(
                    connection,
                    DockBlockKind.Controls,
                    DockBlockKind.Backend,
                    DockBlockKind.Specs,
                    DockBlockKind.ColorScheme,
                    DockBlockKind.DockingSetups,
                    DockBlockKind.DebugLogs);
                DebugLogger.PrintDatabase("Ensured block tables for lock/order persistence.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to ensure block tables: {ex.Message}");
            }
        }

        private static void LoadStartData(SQLiteConnection connection)
        {
            string dataPathSettings = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Settings.sql");
            string dataPathBlocks = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Blocks.sql");
            string dataPathGOs = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_GOs.sql");

            SQLScriptExecutor.RunSQLScript(connection, dataPathSettings);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBlocks);
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

        private static void DeleteAllDatabaseCopies()
        {
            foreach (string path in GetDatabasePathsToReset())
            {
                DeleteDatabaseIfExists(path);
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

        private static IEnumerable<string> GetDatabasePathsToReset()
        {
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

            void TryAdd(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                try
                {
                    string normalized = Path.GetFullPath(path);
                    paths.Add(normalized);
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"Failed to normalize database path '{path}': {ex.Message}");
                }
            }

            TryAdd(DatabaseConfig.DatabaseFilePath);
            TryAdd(DatabaseConfig.OutputDatabaseFilePath);

            string projectRoot = DatabaseConfig.ProjectRootPath;
            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                TryAddBuildOutputDatabases(Path.Combine(projectRoot, "bin"));
                TryAddBuildOutputDatabases(Path.Combine(projectRoot, "obj"));
            }

            return paths;

            void TryAddBuildOutputDatabases(string rootDirectory)
            {
                if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
                {
                    return;
                }

                try
                {
                    foreach (string configDir in Directory.EnumerateDirectories(rootDirectory))
                    {
                        foreach (string frameworkDir in Directory.EnumerateDirectories(configDir))
                        {
                            string dataDir = Path.Combine(frameworkDir, "Data");
                            if (!Directory.Exists(dataDir))
                            {
                                continue;
                            }

                            TryAdd(Path.Combine(dataDir, DatabaseConfig.DatabaseFileName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"Failed to enumerate build output databases in '{rootDirectory}': {ex.Message}");
                }
            }
        }
    }
}
