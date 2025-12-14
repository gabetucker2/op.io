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
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('HoldInputs', 16);

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

-----------------------------------------------------------------------
-- Color scheme block
-- Seeds all palette entries with defaults, render order, and lock flags.
-----------------------------------------------------------------------
INSERT OR REPLACE INTO BlockColorScheme (RowKey, IsLocked) VALUES ('BlockLock', 1); -- default locked state stored at block-level
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('TransparentWindowKey', '#FF69B4FF', 1, 1);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('DefaultFallback', '#FF69B3FF', 2, 1);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('GameBackground', '#141419FF', 3, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ScreenBackground', '#121212FF', 4, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('BlockBackground', '#1A1A1AFF', 5, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('BlockBorder', '#303030FF', 6, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('HeaderBackground', '#232323FF', 7, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('TextPrimary', '#E2E2E2FF', 8, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('TextMuted', '#A0A0A0FF', 9, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('Accent', '#6E8EFFFF', 10, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AccentSoft', '#6E8EFF46', 11, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('OverlayBackground', '#181818E6', 12, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('DragBarHoverTint', '#1A1A1A78', 13, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ResizeBar', '#3A3A3AD2', 14, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ResizeBarHover', '#6E8EFF96', 15, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ResizeBarActive', '#6E8EFFDC', 16, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ButtonNeutral', '#222222E6', 17, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ButtonNeutralHover', '#343434F0', 18, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ButtonPrimary', '#3A4E96EB', 19, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ButtonPrimaryHover', '#5674CCF0', 20, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('RowHover', '#262626B4', 21, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('RowDragging', '#181818DC', 22, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('DropIndicator', '#6E8EFF5A', 23, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ToggleIdle', '#2626268C', 24, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ToggleHover', '#445CA0C8', 25, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ToggleActive', '#445CA0E6', 26, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('RebindScrim', '#080808BE', 27, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('Warning', '#F0C440FF', 28, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ScrollTrack', '#181818DC', 29, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ScrollThumb', '#5F5F5FFF', 30, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ScrollThumbHover', '#888888FF', 31, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('IndicatorActive', '#48C973FF', 32, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('IndicatorInactive', '#C0392BFF', 33, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseBackground', '#501414DC', 34, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseHoverBackground', '#8C2020F0', 35, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseBorder', '#A02828FF', 36, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseHoverBorder', '#DC4848FF', 37, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseOverlayBackground', '#401818F0', 38, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseOverlayHoverBackground', '#5A2424F0', 39, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseOverlayBorder', '#962828FF', 40, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('LockLockedFill', '#262626DC', 41, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('LockLockedHoverFill', '#262626F0', 42, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('LockUnlockedFill', '#445CA0E6', 43, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('LockUnlockedHoverFill', '#445CA0FA', 44, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseGlyph', '#FF4500FF', 45, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('CloseGlyphHover', '#FFFFFFFF', 46, 0);
