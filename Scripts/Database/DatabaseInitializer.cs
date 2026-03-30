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
            string structurePathGOs      = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_GOs.sql");
            string structurePathBarrels  = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_Barrels.sql");

            SQLScriptExecutor.RunSQLScript(connection, structurePathSettings);
            SQLScriptExecutor.RunSQLScript(connection, structurePathGOs);
            SQLScriptExecutor.RunSQLScript(connection, structurePathBarrels);

            DebugLogger.PrintDatabase("Database structure scripts loaded successfully.");
        }

        private static void EnsureBlockTables(SQLiteConnection connection)
        {
            try
            {
                BlockDataStore.EnsureTables(
                    connection,
                    DockBlockKind.Controls,
                    DockBlockKind.ControlSetups,
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

            EnsureTooltipsTable(connection);
        }

        private static void EnsureTooltipsTable(SQLiteConnection connection)
        {
            try
            {
                using var create = new SQLiteCommand(
                    "CREATE TABLE IF NOT EXISTS UITooltips (RowKey TEXT PRIMARY KEY, TooltipText TEXT NOT NULL);",
                    connection);
                create.ExecuteNonQuery();

                (string kind, string text)[] defaults =
                [
                    // Controls block
                    ("MoveUp",                  "Move the player upward."),
                    ("MoveDown",                "Move the player downward."),
                    ("MoveLeft",                "Move the player to the left."),
                    ("MoveRight",               "Move the player to the right."),
                    ("MoveTowardsCursor",        "Move the player toward the cursor position."),
                    ("MoveAwayFromCursor",       "Move the player away from the cursor position."),
                    ("Sprint",                  "Hold to move faster. Speed multiplier is set in ControlSettings."),
                    ("Crouch",                  "Hold to move slower. Speed multiplier is set in ControlSettings."),
                    ("ReturnCursorToPlayer",     "Snap the cursor back to the player position."),
                    ("Exit",                    "Quit the game."),
                    ("BlockMenu",               "Open the block visibility overlay to show or hide UI panels."),
                    ("DockingMode",             "Toggle docking mode to resize and rearrange UI panels."),
                    ("DebugMode",               "Toggle debug visuals such as the physics collision circle."),
                    ("AllowGameInputFreeze",     "Allow the game input freeze toggle. Must be enabled before FreezeGameInputs takes effect."),
                    ("TransparentTabBlocking",   "When enabled, the transparent block intercepts clicks instead of passing them to the game."),
                    ("HoldInputs",              "Toggle hold mode for directional inputs, keeping them active without holding the key."),
                    ("UsePreviousConfiguration", "Switch to the previous saved control configuration profile."),
                    ("UseNextConfiguration",     "Switch to the next saved control configuration profile."),
                    // Backend block
                    ("FreezeGameInputs",  "Suspend all gameplay inputs. The game pauses reacting to keyboard and mouse while this is active."),
                    // Specs block
                    ("FPS",           "Frames rendered per second. Higher is smoother."),
                    ("TargetFPS",     "The frame rate cap configured in GeneralSettings."),
                    ("FrameTime",     "Time in milliseconds to process and render one frame."),
                    ("WindowMode",    "Current window mode: bordered, borderless, or fullscreen."),
                    ("VSync",         "Vertical sync state. Locks frame rate to the display refresh rate when enabled."),
                    ("FixedTime",     "Fixed timestep mode. When enabled, Update runs at a constant rate regardless of rendering speed."),
                    ("WindowSize",    "Width and height of the game window in pixels."),
                    ("Backbuffer",    "Dimensions of the GPU backbuffer used for rendering."),
                    ("SurfaceFormat", "Pixel format of the backbuffer surface."),
                    ("DepthFormat",   "Bit depth of the depth and stencil buffer."),
                    ("GraphicsProfile","DirectX feature level used by the graphics device."),
                    ("Adapter",       "Name of the active GPU adapter."),
                    ("CPUThreads",    "Number of logical processor threads available to the process."),
                    ("ProcessMemory", "Total memory allocated to this process by the OS."),
                    ("ManagedMemory", "Memory used by the .NET managed heap."),
                    ("OS",            "Operating system name and version."),
                ];

                foreach ((string kind, string text) in defaults)
                {
                    using var insert = new SQLiteCommand(
                        "INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES (@kind, @text);",
                        connection);
                    insert.Parameters.AddWithValue("@kind", kind);
                    insert.Parameters.AddWithValue("@text", text);
                    insert.ExecuteNonQuery();
                }

                DebugLogger.PrintDatabase("Ensured UITooltips table and default tooltip data.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to ensure UITooltips table: {ex.Message}");
            }
        }

        private static void LoadStartData(SQLiteConnection connection)
        {
            string dataPathSettings = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Settings.sql");
            string dataPathBlocks   = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Blocks.sql");
            string dataPathGOs      = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_GOs.sql");
            string dataPathBarrels  = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Barrels.sql");

            SQLScriptExecutor.RunSQLScript(connection, dataPathSettings);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBlocks);
            SQLScriptExecutor.RunSQLScript(connection, dataPathGOs);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBarrels);
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
