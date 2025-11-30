-- InitDB_StartData_Blocks.sql
-- Default lock states and render orders for block tables managed by BlockDataStore.

-----------------------------------------------------------------------
-- Controls block
-----------------------------------------------------------------------
INSERT OR REPLACE INTO BlockControls (RowKey, IsLocked) VALUES ('BlockLock', 1); -- default locked state stored at block-level
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('MoveUp', 1);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('MoveDown', 2);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('MoveLeft', 3);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('MoveRight', 4);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('MoveTowardsCursor', 5);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('MoveAwayFromCursor', 6);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('Sprint', 7);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('Crouch', 8);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('ReturnCursorToPlayer', 9);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('Exit', 10);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('BlockMenu', 11);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('DockingMode', 12);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('DebugMode', 13);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('AllowGameInputFreeze', 14);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('TransparentTabBlocking', 15);

-----------------------------------------------------------------------
-- Backend block
-- Add additional tracked variable keys here as they are introduced.
-----------------------------------------------------------------------
INSERT OR REPLACE INTO BlockBackend (RowKey, IsLocked) VALUES ('BlockLock', 1); -- default locked state stored at block-level
INSERT OR REPLACE INTO BlockBackend (RowKey, RenderOrder) VALUES ('FreezeGameInputs', 1);

-----------------------------------------------------------------------
-- Specs block
-- These keys mirror SystemSpecsProvider.GetSpecs() defaults.
-----------------------------------------------------------------------
INSERT OR REPLACE INTO BlockSpecs (RowKey, IsLocked) VALUES ('BlockLock', 1); -- default locked state stored at block-level
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('FPS', 1);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('TargetFPS', 2);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('FrameTime', 3);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('WindowMode', 4);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('VSync', 5);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('FixedTime', 6);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('WindowSize', 7);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('Backbuffer', 8);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('SurfaceFormat', 9);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('DepthFormat', 10);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('GraphicsProfile', 11);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('Adapter', 12);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('CPUThreads', 13);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('ProcessMemory', 14);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('ManagedMemory', 15);
INSERT OR REPLACE INTO BlockSpecs (RowKey, RenderOrder) VALUES ('OS', 16);
