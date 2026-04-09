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

                // Add DataType column if it doesn't exist yet (idempotent migration).
                try
                {
                    using var addCol = new SQLiteCommand(
                        "ALTER TABLE UITooltips ADD COLUMN DataType TEXT DEFAULT '';",
                        connection);
                    addCol.ExecuteNonQuery();
                }
                catch { /* Column already exists — safe to ignore. */ }

                // key, text, dataType
                // Bullet lines use "• Label: description." format, separated by \n.
                (string key, string text, string dataType)[] defaults =
                [
                    // Controls block — Hold (no bullets)
                    ("MoveUp",              "Move the player upward.",                                                              "Hold"),
                    ("MoveDown",            "Move the player downward.",                                                            "Hold"),
                    ("MoveLeft",            "Move the player to the left.",                                                         "Hold"),
                    ("MoveRight",           "Move the player to the right.",                                                        "Hold"),
                    ("MoveTowardsCursor",   "Move the player toward the cursor position.",                                          "Hold"),
                    ("MoveAwayFromCursor",  "Move the player away from the cursor position.",                                       "Hold"),
                    ("Sprint",              "Hold to move faster. Speed multiplier is set in ControlSettings.",                     "Hold"),
                    // Controls block — NoSaveSwitch (ON/OFF bullets)
                    ("Crouch",
                        "Hold to move slower. Speed multiplier is set in ControlSettings.\n• ON: Player moves at reduced speed.\n• OFF: Player moves at normal speed.",
                        "Switch"),
                    ("HoldInputs",
                        "Toggle hold mode for directional inputs, keeping them active without holding the key.\n• ON: Directional inputs stay active without holding the key.\n• OFF: Directional inputs only activate while the key is held.",
                        "Switch"),
                    // Controls block — Trigger (no bullets)
                    ("ReturnCursorToPlayer",     "Snap the cursor back to the player position.",            "Trigger"),
                    ("Exit",                     "Quit the game.",                                          "Trigger"),
                    ("UsePreviousConfiguration", "Switch to the previous saved control configuration.",     "Trigger"),
                    ("UseNextConfiguration",     "Switch to the next saved control configuration.",         "Trigger"),
                    // Controls block — SaveSwitch (ON/OFF bullets)
                    ("BlockMenu",
                        "Open the block visibility overlay to show or hide UI panels.\n• ON: Block visibility overlay is shown.\n• OFF: Block visibility overlay is hidden.",
                        "Switch"),
                    ("DockingMode",
                        "Toggle docking mode to resize and rearrange UI panels.\n• ON: UI panels can be resized and repositioned.\n• OFF: UI panels are locked in place.",
                        "Switch"),
                    ("DebugMode",
                        "Toggle debug visuals such as the physics collision circle.\n• ON: Debug visuals are shown.\n• OFF: Debug visuals are hidden.",
                        "Switch"),
                    ("TransparentTabBlocking",
                        "When enabled, the transparent block intercepts clicks instead of passing them to the game.\n• ON: Transparent blocks intercept mouse clicks.\n• OFF: Clicks pass through transparent blocks to the game.",
                        "Switch"),
                    ("AutoTurnInspectModeOff",
                        "When enabled, inspect mode turns off automatically after clicking on or away from an object.\n• ON: Inspect mode turns off after each interaction.\n• OFF: Inspect mode stays active until manually toggled.",
                        "Switch"),
                    ("InspectMode",
                        "Toggle inspect mode to examine game object properties.\n• ON: Click on objects to inspect their properties.\n• OFF: Clicking objects has no inspect effect.",
                        "Switch"),
                    // Controls block — SaveEnum (one bullet per value)
                    ("AllowGameInputFreeze",
                        "Set when gameplay inputs should be frozen based on the window state.\n• None: Gameplay inputs are never frozen automatically.\n• Focus: Inputs freeze when the game window loses focus.\n• MouseLeave: Inputs freeze when the cursor leaves the game area.",
                        "Enum"),
                    // Backend block — bool (ON/OFF bullets)
                    ("FreezeGameInputs",
                        "Suspend all gameplay inputs. The game pauses reacting to keyboard and mouse while this is active.\n• ON: All gameplay inputs are suspended.\n• OFF: Gameplay inputs are active.",
                        "bool"),
                    // Specs block — plain values
                    ("FPS",           "Frames rendered per second. Higher is smoother.",                    "float"),
                    ("TargetFPS",     "The frame rate cap configured in GeneralSettings.",                  "int"),
                    ("FrameTime",     "Time in milliseconds to process and render one frame.",              "float"),
                    ("WindowSize",    "Width and height of the game window in pixels.",                     "string"),
                    ("Backbuffer",    "Dimensions of the GPU backbuffer used for rendering.",               "string"),
                    ("SurfaceFormat", "Pixel format of the backbuffer surface.",                            "string"),
                    ("DepthFormat",   "Bit depth of the depth and stencil buffer.",                         "string"),
                    ("GraphicsProfile","DirectX feature level used by the graphics device.",                "string"),
                    ("Adapter",       "Name of the active GPU adapter.",                                    "string"),
                    ("CPUThreads",    "Number of logical processor threads available to the process.",      "int"),
                    ("ProcessMemory", "Total memory allocated to this process by the OS.",                  "string"),
                    ("ManagedMemory", "Memory used by the .NET managed heap.",                              "string"),
                    ("OS",            "Operating system name and version.",                                 "string"),
                    // Specs block — bool (ON/OFF bullets)
                    ("VSync",
                        "Vertical sync state. Locks frame rate to the display refresh rate when enabled.\n• ON: Frame rate is locked to the monitor refresh rate.\n• OFF: Frame rate is not constrained by vertical sync.",
                        "bool"),
                    ("FixedTime",
                        "Fixed timestep mode. When enabled, Update runs at a constant rate regardless of rendering speed.\n• ON: Game logic updates at a constant rate (60 Hz).\n• OFF: Game logic updates at variable rate matching rendering.",
                        "bool"),
                    // Specs block — enum-like string (one bullet per mode)
                    ("WindowMode",
                        "Current window display mode.\n• Bordered: Standard windowed mode with window chrome.\n• Borderless: Fullscreen-like window without chrome.\n• Fullscreen: Exclusive fullscreen mode.",
                        "string"),
                ];

                foreach ((string key, string text, string dataType) in defaults)
                {
                    using var insert = new SQLiteCommand(
                        "INSERT INTO UITooltips (RowKey, TooltipText, DataType) VALUES (@key, @text, @dt) " +
                        "ON CONFLICT(RowKey) DO UPDATE SET TooltipText = excluded.TooltipText, DataType = excluded.DataType;",
                        connection);
                    insert.Parameters.AddWithValue("@key", key);
                    insert.Parameters.AddWithValue("@text", text);
                    insert.Parameters.AddWithValue("@dt", dataType);
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
            string dataPathSettings   = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Settings.sql");
            string dataPathControls   = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Controls.sql");
            string dataPathBullets    = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Bullets.sql");
            string dataPathBars       = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Bars.sql");
            string dataPathFX         = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_FX.sql");
            string dataPathBlocks     = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Blocks.sql");
            string dataPathPlayer     = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Player.sql");
            string dataPathMapObjects = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_MapObjects.sql");
            string dataPathFarms      = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Farms.sql");
            string dataPathBarrels    = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Barrels.sql");

            SQLScriptExecutor.RunSQLScript(connection, dataPathSettings);
            SQLScriptExecutor.RunSQLScript(connection, dataPathControls);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBullets);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBars);
            SQLScriptExecutor.RunSQLScript(connection, dataPathFX);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBlocks);
            SQLScriptExecutor.RunSQLScript(connection, dataPathPlayer);
            SQLScriptExecutor.RunSQLScript(connection, dataPathMapObjects);
            SQLScriptExecutor.RunSQLScript(connection, dataPathFarms);
            SQLScriptExecutor.RunSQLScript(connection, dataPathBarrels);
        }

        private static void VerifyTablesExistence(SQLiteConnection connection)
        {
            try
            {
                string[] requiredTables = { "GameObjects", "Agents", "FarmData", "Destructibles" };
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
