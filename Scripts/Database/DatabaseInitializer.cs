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
                EnsureBlockSeedData(existingConnection);
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
            EnsureBlockSeedData(connection);

            DebugLogger.PrintDatabase("Database initialization complete.");
            DatabaseManager.CloseConnection(connection);
            _alreadyInitialized = true;
        }

        private static void LoadStructureScripts(SQLiteConnection connection)
        {
            string structurePathSettings = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_Settings.sql");
            string structurePathGOs      = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_GOs.sql");
            string structurePathBarrels  = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_Barrels.sql");
            string structurePathBodies   = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_Structure_Bodies.sql");

            SQLScriptExecutor.RunSQLScript(connection, structurePathSettings);
            SQLScriptExecutor.RunSQLScript(connection, structurePathGOs);
            SQLScriptExecutor.RunSQLScript(connection, structurePathBarrels);
            SQLScriptExecutor.RunSQLScript(connection, structurePathBodies);

            DebugLogger.PrintDatabase("Database structure scripts loaded successfully.");
        }

        private static void EnsureBlockTables(SQLiteConnection connection)
        {
            try
            {
                BlockDataStore.EnsureTables(
                    connection,
                    DockBlockKind.Properties,
                    DockBlockKind.Controls,
                    DockBlockKind.Notes,
                    DockBlockKind.ControlSetups,
                    DockBlockKind.Backend,
                    DockBlockKind.Specs,
                    DockBlockKind.ColorScheme,
                    DockBlockKind.Ambience,
                    DockBlockKind.Levels,
                    DockBlockKind.DockingSetups,
                    DockBlockKind.DebugLogs,
                    DockBlockKind.Bars,
                    DockBlockKind.Chat,
                    DockBlockKind.Performance,
                    DockBlockKind.Interact);
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
                    ("DisableToolTips",
                        "When enabled, all UI tooltips are suppressed.\n• ON: Tooltips are hidden everywhere.\n• OFF: Tooltips are shown normally.",
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
                    ("Grid",
                        "Toggle the world grid overlay.\n• ON: Draws 1-centifoot grey grid lines with major 5-centifoot coordinate plotting.\n• OFF: Hides the world grid overlay.",
                        "Switch"),
                    (ControlKeyMigrations.OceanZoneDebugKey,
                        "Toggle ocean zone borders and labels.\n• ON: Shows ocean zone borders and labels.\n• OFF: Hides ocean zone borders and labels.",
                        "Switch"),
                    (ControlKeyMigrations.YourBarKey,
                        "Reveal your player's configured bar rows for 5 seconds. In switch mode, ON keeps them visible and OFF lets them fade out.",
                        "Trigger"),
                    // Controls block — SaveEnum (one bullet per value)
                    ("AllowGameInputFreeze",
                        "Set when gameplay inputs should be frozen based on the window state.\n• None: Gameplay inputs are never frozen automatically.\n• Focus: Inputs freeze when the game window loses focus.\n• MouseLeave: Inputs freeze when the cursor leaves the game area.",
                        "Enum"),
                    // Backend block — bool (ON/OFF bullets)
                    ("FreezeGameInputs",
                        "Suspend all gameplay inputs. The game pauses reacting to keyboard and mouse while this is active.\n• ON: All gameplay inputs are suspended.\n• OFF: Gameplay inputs are active.",
                        "bool"),
                    ("ShapeRuntimeContentLoadCount",
                        "How many shape textures were lazily created during draw because a runtime object reached rendering before explicit content loading.",
                        "int"),
                    ("ShapeDrawSkippedMissingTextureCount",
                        "How many shape draw attempts were skipped because no texture and no GraphicsDevice were available for lazy loading.",
                        "int"),
                    ("BulletActiveCount",
                        "Number of active bullets currently managed by BulletManager.",
                        "int"),
                    ("BulletBarrelLockedCount",
                        "Number of active bullets still traveling inside their firing barrels.",
                        "int"),
                    ("BulletCollisionReadyCount",
                        "Number of active bullets that have left the barrel and can collide.",
                        "int"),
                    ("OceanZoneDebugRequested",
                        "Shows whether the OceanZoneDebug save switch is currently requesting ocean zone borders and labels.",
                        "bool"),
                    ("OceanZoneDebugVisible",
                        "Shows whether ocean zone borders and labels are currently rendering. Follows the OceanZoneDebug save switch.",
                        "bool"),
                    ("FogOfWarEnabled",
                        "Shows whether the fog-of-war system has a valid viewing unit to evaluate.",
                        "bool"),
                    ("FogOfWarActive",
                        "Shows whether fog is currently rendering over the world this frame.",
                        "bool"),
                    ("FogVisionSourceCount",
                        "Number of active vision sources currently carving visible territory from fog.",
                        "int"),
                    ("PlayerSightRadius",
                        "Current sight radius used for the player vision territory in world units.",
                        "float"),
                    (AmbienceSettings.FogOfWarRowKey,
                        "Base color currently applied to hidden fog-of-war territory. Edit it live in the Ambience block.",
                        "Color"),
                    (AmbienceSettings.OceanWaterRowKey,
                        "Base color currently driving the ocean water shader. Edit it live in the Ambience block.",
                        "Color"),
                    ("AmbienceOceanWaterLiveColor",
                        "Current ocean water color after zone darkness has been applied.",
                        "Color"),
                    (AmbienceSettings.BackgroundWavesRowKey,
                        "Highlight color for the background wave crests in the ocean ambience. Edit it live in the Ambience block.",
                        "Color"),
                    ("AmbienceBackgroundWavesLiveColor",
                        "Current ocean wave highlight color after zone darkness has been applied.",
                        "Color"),
                    ("GameBlockOceanPlayerZone",
                        "Current ocean zone under the runtime player, or None when the player is on terrain or unavailable.",
                        "string"),
                    ("GameBlockOceanPlayerZoneStatus",
                        "Player ocean-zone probe detail including resolved zone, offshore distance, and water depth.",
                        "string"),
                    ("GameBlockOceanCursorZone",
                        "Current ocean zone under the cursor while it is hovering the Game block.",
                        "string"),
                    ("GameBlockOceanCursorZoneStatus",
                        "Cursor ocean-zone probe detail including hover availability, resolved zone, offshore distance, and water depth.",
                        "string"),
                    ("GameBlockOceanCursorZoneValid",
                        "Shows whether the cursor currently resolves to an ocean zone in the Game block.",
                        "bool"),
                    ("GameBlockOceanCursorZoneDepth",
                        "Water depth resolved under the cursor's Game-block hover position.",
                        "float"),
                    ("GameBlockOceanCursorZoneOffshoreDistance",
                        "Offshore distance resolved under the cursor's Game-block hover position.",
                        "float"),
                    ("GameBlockOceanZoneTransitionBanner",
                        "Current top-center Game overlay text for an ocean-zone transition, or none when inactive.",
                        "string"),
                    (AmbienceSettings.TerrainRowKey,
                        "Fill color for generated terrain, finite map borders, and the world beyond the playable square. Edit it live in the Ambience block.",
                        "Color"),
                    (AmbienceSettings.WorldTintRowKey,
                        "Gameplay object tint follows the live ocean water background color.",
                        "Color"),
                    ("GameLevelActiveName",
                        "Name of the currently loaded game level.",
                        "string"),
                    ("GameLevelActiveKey",
                        "Stable key for the currently loaded game level.",
                        "string"),
                    ("GameLevelCount",
                        "Number of game levels registered with the level manager.",
                        "int"),
                    ("GameLevelReloadCount",
                        "How many times a level has been loaded or reloaded during this run.",
                        "int"),
                    ("GameLevelLoadInProgress",
                        "Shows whether a level load is currently clearing and rebuilding scene objects.",
                        "bool"),
                    ("GameLevelSpawnsPlayer",
                        "Shows whether the active level is required to spawn the runtime player.",
                        "bool"),
                    ("GameLevelPlayerSpawnRelocated",
                        "Shows whether the runtime player spawn was moved away from terrain or collidable scene objects during level load.",
                        "bool"),
                    ("GameLevelPlayerSpawnRelocationDistance",
                        "Distance the runtime player spawn moved from its requested level position to the nearest open position.",
                        "float"),
                    ("GameLevelPlayerSpawnSearchAttempts",
                        "Number of terrain and collidable object probes used while resolving the runtime player spawn.",
                        "int"),
                    ("GameLevelLoadoutSummary",
                        "Scene object groups and world systems loaded by the active level.",
                        "string"),
                    ("GameLevelTerrainConfiguration",
                        "Terrain configuration key requested by the active level.",
                        "string"),
                    ("GameLevelOceanZoneConfiguration",
                        "Ocean-zone configuration key requested by the active level.",
                        "string"),
                    ("GameLevelPrevious",
                        "Load the previous game level in the registered level list.",
                        "Button"),
                    ("GameLevelNext",
                        "Load the next game level in the registered level list.",
                        "Button"),
                    ("GameLevelReload",
                        "Reload the active level from its object, farm, terrain, and ocean-zone configuration.",
                        "Button"),
                    ("GameLevelManual",
                        "Manual level: loads the current authored test scene with scout, farms, walls, zones, terrain, and ocean.",
                        "Button"),
                    ("GameLevelNatural",
                        "Natural level: loads the runtime player with generated terrain and ocean, without ScoutSentry1, farms, walls, or zones.",
                        "Button"),
                    ("FogFrontierBorderThickness",
                        "Reference world-space thickness used to normalize all fog frontier band widths and perturbation amplitudes.",
                        "float"),
                    ("FogFrontierFieldSmoothingRadius",
                        "World-space smoothing radius currently applied to the fog frontier scalar field.",
                        "float"),
                    ("TerrainWorldSeed",
                        "Seed used with chunk coordinates to deterministically regenerate terrain without saving chunk payloads.",
                        "int"),
                    ("TerrainResidentChunkCount",
                        "How many terrain chunks are currently cached in memory, including water-only chunk records.",
                        "int"),
                    ("TerrainResidentChunkMemoryCap",
                        "Maximum resident terrain chunks kept in memory before distant cached chunks are pruned.",
                        "int"),
                    ("TerrainResidentComponentCount",
                        "How many connected terrain landmass render objects are currently resident after stitching loaded chunks together.",
                        "int"),
                    ("TerrainResidentColliderCount",
                        "How many legacy terrain collider proxy bodies are resident; should remain zero because terrain collision no longer creates hidden scene objects.",
                        "int"),
                    ("TerrainResidentVisualTriangleCount",
                        "How many vector terrain fill triangles are currently resident for direct procedural terrain rendering.",
                        "int"),
                    ("TerrainActiveColliderCount",
                        "How many legacy terrain collider proxy bodies are active in the scene; should remain zero.",
                        "int"),
                    ("TerrainDynamicCollisionProbeCount",
                        "How many dynamic object and bullet probes are currently expanding terrain collision-border generation.",
                        "int"),
                    ("TerrainDynamicCollisionObjectProbeCount",
                        "How many dynamic collidable GameObjects are currently expanding terrain collision-border generation.",
                        "int"),
                    ("TerrainDynamicCollisionBulletProbeCount",
                        "How many active bullets are currently expanding terrain collision-border generation.",
                        "int"),
                    ("TerrainSpawnRelocationCount",
                        "How many dynamic startup objects were nudged out of terrain so legacy spawn placement remains valid with collideable land enabled.",
                        "int"),
                    ("TerrainCollisionIntrusionCorrectionCount",
                        "How many dynamic world objects were force-corrected back out of terrain this frame after collision resolution to prevent embeds and freeze-state glitches.",
                        "int"),
                    ("TerrainBulletCollisionCorrectionCount",
                        "How many bullets were reflected out of visible terrain collision loops this frame without using hidden scene proxy colliders.",
                        "int"),
                    ("TerrainPendingChunkCount",
                        "How many terrain chunks are queued, building, or waiting to be promoted by the background worker.",
                        "int"),
                    ("TerrainPendingCriticalChunkCount",
                        "How many camera-or-fog-visible terrain chunks are still building; resident terrain visuals are held stable while this is above zero.",
                        "int"),
                    ("TerrainFullMapChunkWindow",
                        "Inclusive chunk-coordinate window for the finite terrain map cache used by full-map ocean border generation.",
                        "string"),
                    ("TerrainFullMapChunkCount",
                        "How many deterministic terrain chunks make up the full terrain map cache.",
                        "int"),
                    ("TerrainFullMapGeneratedChunkCount",
                        "How many full-map terrain chunks have been generated and retained in memory.",
                        "int"),
                    ("TerrainFullMapPendingChunkCount",
                        "How many full-map terrain chunks are still queued, building, or awaiting promotion.",
                        "int"),
                    ("TerrainFullMapGenerationComplete",
                        "Whether every chunk in the finite terrain map cache has finished generating.",
                        "bool"),
                    ("TerrainFullMapSnapshotReady",
                        "Whether the generated full terrain map has been copied into the immutable mask used by ocean-zone border generation.",
                        "bool"),
                    ("TerrainBackgroundWorkerStatus",
                        "Current low-priority terrain background worker state.",
                        "string"),
                    ("TerrainBackgroundQueuedChunkCount",
                        "How many terrain chunks are queued for gradual background generation.",
                        "int"),
                    ("TerrainBackgroundCompletedChunkQueueCount",
                        "How many generated terrain chunks are waiting for small per-frame main-thread promotion.",
                        "int"),
                    ("TerrainBackgroundActiveChunkBuildCount",
                        "How many terrain chunks are currently being generated by the background worker.",
                        "int"),
                    ("TerrainStartupPhase",
                        "Current terrain startup/streaming stage: nearby synchronous warmup, visible materialization, or background preload.",
                        "string"),
                    ("TerrainStartupSynchronousChunkBuildCount",
                        "How many startup chunks were built synchronously before loading the player into the active world.",
                        "int"),
                    ("TerrainBackgroundQueuedChunkBuildCount",
                        "How many terrain chunks have been queued on the background worker after startup.",
                        "int"),
                    ("TerrainStartupFirstSightTerrainReady",
                        "Whether at least one nearby landform was materialized before or during the first visible startup frame.",
                        "bool"),
                    ("TerrainStartupWarmupChunkCount",
                        "How many nearby chunks were considered for first-sight startup terrain warmup.",
                        "int"),
                    ("TerrainRuntimeFieldCollisionFallbackSuppressedCount",
                        "How many frames skipped expensive runtime terrain field collision fallback while no resident collision loops were available.",
                        "int"),
                    ("TerrainAccessRequestActive",
                        "Shows whether player movement requested terrain streaming around the target area without blocking movement.",
                        "bool"),
                    ("TerrainAccessRequestStatus",
                        "Current non-blocking terrain-access target and radius being queued for generation.",
                        "string"),
                    ("TerrainMovementBlockedUntilReadyCount",
                        "Legacy count of movement attempts blocked by terrain streaming; should remain zero.",
                        "int"),
                    ("TerrainDiscardedStaleMaterializationCount",
                        "How many completed terrain mesh builds were discarded because the camera or vision window changed before they finished.",
                        "int"),
                    ("TerrainChunkBuildsInFlight",
                        "Shows whether any terrain chunk builds are currently in flight.",
                        "bool"),
                    ("TerrainChunkWorldSize",
                        "World-space width and height covered by each deterministic terrain chunk.",
                        "float"),
                    ("TerrainFeatureWorldScaleMultiplier",
                        "Multiplier applied to terrain world-space feature size so islands, coasts, and reefs generate at a larger scale from the same seed.",
                        "float"),
                    ("TerrainArchipelagoMacroCellSize",
                        "Terrain-space size of the regional archipelago cluster mask that gates deep ocean, shelves, protected basins, and island cluster zones.",
                        "float"),
                    ("TerrainArchipelagoSubstrateCellSize",
                        "Legacy terrain-space substrate scale retained for telemetry; current lithology is blended from smooth process fields instead of hard substrate cells.",
                        "float"),
                    ("TerrainArchipelagoEnclosureCellSize",
                        "Terrain-space size of the larger enclosure-producer cells used by reef rings, barrier systems, island rings, and cove/ravine cuts.",
                        "float"),
                    ("TerrainGenerationPipeline",
                        "Current terrain generation order used by the layered archipelago sampler.",
                        "string"),
                    ("TerrainLandformSelectionMode",
                        "Whether terrain is generated from direct archetype placement or from layered geological processes with post-classification.",
                        "string"),
                    ("TerrainOceanZoneDistanceMode",
                        "Current ocean-zone distance source. Nearest-border distance keeps shallow water wrapped around every terrain coastline.",
                        "string"),
                    ("TerrainOceanZoneOrigin",
                        "Distance anchor used by ocean zones. Nearest generated terrain border means every coast starts at shallow water.",
                        "string"),
                    ("TerrainOceanZoneOriginRadius",
                        "Effective shallow-water transition distance from the nearest terrain border.",
                        "float"),
                    ("TerrainOceanDebugWorkerStatus",
                        "Full-map ocean-zone debug border state. Borders build only after the full terrain map cache has finished.",
                        "string"),
                    ("TerrainOceanDebugFullMapReady",
                        "Whether the full-map ocean-zone border cache has finished building from the complete terrain map.",
                        "bool"),
                    ("TerrainOceanDebugFullMapSegmentCount",
                        "How many ocean-zone border segments were generated for the full map cache.",
                        "int"),
                    ("TerrainOceanDebugFullMapBuildMilliseconds",
                        "Milliseconds spent building the full-map ocean-zone border cache on the background task.",
                        "float"),
                    ("TerrainOceanDebugFullMapStatus",
                        "Detailed full-map ocean-zone border generation status and readiness summary.",
                        "string"),
                    ("TerrainOceanDebugSuppressedTinyZoneCount",
                        "How many connected ocean-zone components were removed because no same-zone core circle met the minimum stable radius.",
                        "int"),
                    ("TerrainOceanDebugMinimumStableZoneRadius",
                        "Minimum unobstructed same-zone radius required inside an ocean-zone component before debug borders are allowed to render it.",
                        "float"),
                    ("TerrainOceanDebugTinyZoneViolationSummary",
                        "Summary of any remaining tiny ocean-zone components that failed the minimum stable radius validation.",
                        "string"),
                    ("TerrainOceanDebugTileCacheCount",
                        "Legacy visible-tile ocean contour cache count; full-map border generation keeps this at zero during normal operation.",
                        "int"),
                    ("TerrainOceanDebugQueuedTileCount",
                        "How many visible ocean-zone debug contour tiles are queued for background generation.",
                        "int"),
                    ("TerrainOceanDebugActiveTileBuildCount",
                        "How many ocean-zone debug contour tiles are currently building off the main thread.",
                        "int"),
                    ("TerrainOceanDebugCompletedTileQueueCount",
                        "How many generated ocean-zone debug contour tiles are waiting for bounded main-thread promotion.",
                        "int"),
                    ("TerrainOceanDebugQueuedTileBuildCount",
                        "Total ocean-zone debug contour tile builds queued during this run.",
                        "int"),
                    ("TerrainOceanZoneMinimumTransitionVolumeDistance",
                        "SQL-backed cumulative offshore spread added per water-zone transition before deeper zones begin.",
                        "float"),
                    ("TerrainWaterZoneDistanceScale",
                        "SQL-backed multiplier applied to ocean-zone offshore thresholds before water changes to the next deeper zone.",
                        "float"),
                    ("TerrainWaterShallowDistance",
                        "Maximum offshore distance classified as shallow water around generated landmasses.",
                        "float"),
                    ("TerrainWaterSunlitDistance",
                        "Maximum offshore distance classified as sunlit water after the shallow band.",
                        "float"),
                    ("TerrainWaterTwilightDistance",
                        "Maximum offshore distance classified as twilight water after the sunlit band.",
                        "float"),
                    ("TerrainWaterMidnightDistance",
                        "Maximum offshore distance classified as midnight water before open water becomes abyss.",
                        "float"),
                    ("TerrainLagoonOpeningTarget",
                        "Target pass count used by opening producers so lagoons usually retain one or two navigable breaks instead of sealing shut.",
                        "string"),
                    ("TerrainContourResolutionMultiplier",
                        "Multiplier used when extracting procedural contour geometry from the sampled terrain field before simplifying it into vector terrain polygons and collider shells.",
                        "int"),
                    ("TerrainTargetVisualTextureOversample",
                        "Legacy terrain texture oversample setting. Vector terrain rendering keeps this at 0 because visuals are generated from polygon triangles instead of raster textures.",
                        "int"),
                    ("TerrainPreloadMarginWorldUnits",
                        "Extra world-space margin around the camera-and-fog-visible terrain streaming window that chunk loading prebuilds ahead of view.",
                        "float"),
                    ("TerrainSeedAnchor",
                        "Seed-derived terrain-space anchor applied before chunk sampling so the spawn region opens near generated land instead of empty ocean.",
                        "string"),
                    ("TerrainStreamingFocus",
                        "World position currently anchoring terrain chunk priority. Usually the player position.",
                        "string"),
                    ("TerrainCenterChunk",
                        "Chunk coordinate currently centered under the terrain streaming focus.",
                        "string"),
                    ("TerrainVisibleChunkWindow",
                        "Inclusive chunk-coordinate window currently required by the camera and fog-of-war vision sources before preload margin.",
                        "string"),
                    ("TerrainTargetVisualChunkWindow",
                        "True terrain visual chunk window requested for the next resident terrain materialization.",
                        "string"),
                    ("TerrainTargetMaterializedChunkWindow",
                        "Combined chunk window requested for terrain materialization, including visual and collider preload coverage.",
                        "string"),
                    ("TerrainAppliedVisualChunkWindow",
                        "True terrain visual chunk window currently applied to resident terrain render objects.",
                        "string"),
                    ("TerrainAppliedColliderChunkWindow",
                        "Terrain collision chunk window currently applied to resident visible collision loops.",
                        "string"),
                    ("TerrainColliderChunkWindow",
                        "Target terrain collision chunk window requested for resident visible collision loops.",
                        "string"),
                    ("WorldRenderRegisteredObjectCount",
                        "How many world render game objects are currently registered with the shape renderer before camera culling.",
                        "int"),
                    ("WorldRenderDrawnObjectCount",
                        "How many world render game objects survived camera culling and were actually drawn this frame.",
                        "int"),
                    ("PhysicsBroadPhaseActiveCollidableCount",
                        "How many collidable world objects entered the spatial broad-phase this frame.",
                        "int"),
                    ("PhysicsBroadPhaseCandidatePairCount",
                        "How many collision candidate pairs survived broad-phase filtering and reached narrow-phase SAT tests this frame.",
                        "int"),
                    ("PhysicsStartupOverlapResolvedPairCount",
                        "How many collidable overlap pairs were separated once during startup before the first live gameplay frame.",
                        "int"),
                    ("PhysicsStartupOverlapIterationCount",
                        "How many startup depenetration passes were needed to settle collidable spawn overlaps before gameplay began.",
                        "int"),
                    ("CollisionBounceMomentumTransfer",
                        "Fraction of an object's incoming collision momentum converted into bounce impulse against static or terrain bodies. Lower values make red walls and terrain less springy.",
                        "float"),
                    ("XPPlayerUnstablePreviewClumpCount",
                        "Current unstable-preview clump count being rendered for the player source.",
                        "int"),
                    ("XPPlayerUnstablePreviewHealthRatio",
                        "Player health ratio used by unstable-preview logic (CurrentHealth / MaxHealth).",
                        "float"),
                    ("XPPlayerUnstablePreviewRewardXP",
                        "Drop reward XP currently resolved for the player by XP clump systems.",
                        "float"),
                    ("XPPlayerUnstablePreviewEligible",
                        "Shows whether the player currently qualifies as an unstable-preview source.",
                        "bool"),
                    ("DoubleTapSuppressionSeconds",
                        "Seconds used as the DoubleTapToggle window. Non-DoubleTap switch/toggle bindings that share the same primary input are suppressed during this window after the first tap.",
                        "float"),
                    ("YourBarRevealActive",
                        "True while the YourBar control is actively requesting your player's configured bar rows to stay visible.",
                        "bool"),
                    ("YourBarControlSwitchMode",
                        "True when the YourBar control is currently configured as a switch-style input.",
                        "bool"),
                    ("YourBarRevealRemainingSeconds",
                        "Seconds left in the timed YourBar reveal before the existing bar fade begins.",
                        "float"),
                    ("YourBarRevealSeconds",
                        "Configured duration for timed YourBar reveals.",
                        "float"),
                    ("YourBarVisible",
                        "True while at least one of your player's configured bar rows is currently visible or fading.",
                        "bool"),
                    ("YourBarVisibilityAlpha",
                        "Highest current visibility alpha across your player's configured bar rows.",
                        "float"),
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
            string dataPathBodies     = Path.Combine(DatabaseConfig.DatabaseDirectory, "InitDB_StartData_Bodies.sql");

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
            SQLScriptExecutor.RunSQLScript(connection, dataPathBodies);
        }

        private static void EnsureBlockSeedData(SQLiteConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                EnsureAmbienceBlockSeedData(connection);
                EnsureLevelsBlockSeedData(connection);
                EnsureDockingSetupSeedData(connection);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed ensuring block seed data: {ex.Message}");
            }
        }

        private static void EnsureAmbienceBlockSeedData(SQLiteConnection connection)
        {
            BlockDataStore.EnsureTables(connection, DockBlockKind.Ambience);

            using SQLiteTransaction transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(@"
INSERT OR IGNORE INTO BlockAmbience (RowKey, IsLocked) VALUES ('BlockLock', 1);
INSERT OR IGNORE INTO BlockAmbience (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AmbienceFogOfWarColor', '#B8A684FF', 1, 0);
INSERT OR IGNORE INTO BlockAmbience (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AmbienceOceanWaterColor', '#109CC6FF', 2, 0);
INSERT OR IGNORE INTO BlockAmbience (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AmbienceBackgroundWavesColor', '#FFFFFFFF', 3, 0);
INSERT OR IGNORE INTO BlockAmbience (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AmbienceTerrainColor', '#0C321AFF', 4, 0);
UPDATE BlockAmbience SET RenderOrder = 5 WHERE RowKey = 'AmbienceWorldTintColor' AND RenderOrder IN (3, 4);
INSERT OR IGNORE INTO BlockAmbience (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AmbienceWorldTintColor', '#808080FF', 5, 0);",
                connection,
                transaction);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        private static void EnsureLevelsBlockSeedData(SQLiteConnection connection)
        {
            BlockDataStore.EnsureTables(connection, DockBlockKind.Levels);

            using SQLiteTransaction transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(@"
INSERT OR IGNORE INTO BlockLevels (RowKey, IsLocked) VALUES ('BlockLock', 1);
INSERT OR IGNORE INTO BlockLevels (RowKey, RowData, RenderOrder, IsLocked) VALUES ('LaunchLevelKey', 'natural', 1, 0);
INSERT OR IGNORE INTO BlockLevels (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ActiveLevelKey', 'natural', 2, 0);",
                connection,
                transaction);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        private static void EnsureDockingSetupSeedData(SQLiteConnection connection)
        {
            BlockDataStore.EnsureTables(connection, DockBlockKind.DockingSetups);

            using SQLiteTransaction transaction = connection.BeginTransaction();

            using (var insertDefault = new SQLiteCommand(@"
INSERT OR IGNORE INTO BlockDockingSetups (RowKey, IsLocked) VALUES ('BlockLock', 1);
INSERT OR IGNORE INTO BlockDockingSetups (RowKey, RowData) VALUES ('Default', @defaultPayload);",
                connection,
                transaction))
            {
                insertDefault.Parameters.AddWithValue("@defaultPayload", GetDefaultDockingSetupPayload());
                insertDefault.ExecuteNonQuery();
            }

            string activeSetupName = null;
            using (var selectActive = new SQLiteCommand(
                "SELECT RowData FROM BlockDockingSetups WHERE RowKey = '__ActiveSetup' LIMIT 1;",
                connection,
                transaction))
            {
                object active = selectActive.ExecuteScalar();
                activeSetupName = active?.ToString()?.Trim();
            }

            if (string.IsNullOrWhiteSpace(activeSetupName))
            {
                using var selectFallback = new SQLiteCommand(@"
SELECT RowKey
FROM BlockDockingSetups
WHERE RowKey <> 'BlockLock' AND RowKey <> '__ActiveSetup'
ORDER BY CASE WHEN RowKey = 'Default' THEN 0 ELSE 1 END, RowKey ASC
LIMIT 1;",
                    connection,
                    transaction);
                activeSetupName = selectFallback.ExecuteScalar()?.ToString()?.Trim();
            }

            if (string.IsNullOrWhiteSpace(activeSetupName))
            {
                activeSetupName = "Default";
            }

            using var upsertActive = new SQLiteCommand(@"
INSERT INTO BlockDockingSetups (RowKey, RowData)
VALUES ('__ActiveSetup', @activeName)
ON CONFLICT(RowKey) DO UPDATE SET RowData = CASE
    WHEN TRIM(COALESCE(BlockDockingSetups.RowData, '')) = '' THEN excluded.RowData
    ELSE BlockDockingSetups.RowData
END;",
                connection,
                transaction);
            upsertActive.Parameters.AddWithValue("@activeName", activeSetupName);
            upsertActive.ExecuteNonQuery();

            transaction.Commit();
        }

        private static string GetDefaultDockingSetupPayload()
        {
            return """
{
  "version": 3,
  "menu": [
    { "kind": "Blank", "mode": "Count", "count": 0, "visible": false },
    { "kind": "Game", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Properties", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "ColorScheme", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "Ambience", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Levels", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Controls", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Notes", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "ControlSetups", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "DockingSetups", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "Backend", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "Specs", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "DebugLogs", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "Chat", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "Performance", "mode": "Toggle", "count": 0, "visible": false },
    { "kind": "Bars", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Interact", "mode": "Toggle", "count": 0, "visible": true }
  ],
  "panels": [
    { "id": "colors", "active": "colors", "blocks": ["colors", "notes", "dockingsetups"] },
    { "id": "game", "active": "game", "blocks": ["game"] },
    { "id": "controls", "active": "ambience", "blocks": ["interact", "controls", "ambience", "levels"] },
    { "id": "backend", "active": "backend", "blocks": ["backend", "debuglogs", "chat", "specs"] },
    { "id": "blank", "active": "blank", "blocks": ["blank"] },
    { "id": "properties", "active": "properties", "blocks": ["properties", "bars"] }
  ],
  "layout": {
    "type": "Split",
    "orientation": "Vertical",
    "ratio": 0.67,
    "first": {
      "type": "Split",
      "orientation": "Horizontal",
      "ratio": 0.36,
      "first": { "type": "Panel", "panel": "game" }
    },
    "second": {
      "type": "Split",
      "orientation": "Horizontal",
      "ratio": 0.7,
      "first": {
        "type": "Split",
        "orientation": "Horizontal",
        "ratio": 0.26,
        "first": { "type": "Panel", "panel": "properties" },
        "second": { "type": "Panel", "panel": "controls" }
      },
      "second": {
        "type": "Split",
        "orientation": "Horizontal",
        "ratio": 0.42,
        "first": { "type": "Panel", "panel": "colors" },
        "second": { "type": "Panel", "panel": "backend" }
      }
    }
  },
  "overlays": [],
  "blockOpacities": {}
}
""";
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
