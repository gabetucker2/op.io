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
-- Color scheme block
-- Seed lock state, render order, and default hex values for every color role.
-----------------------------------------------------------------------
INSERT OR REPLACE INTO BlockColorScheme (RowKey, IsLocked) VALUES ('BlockLock', 1);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('TransparentWindowKey', 1, '#FF69B4FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DefaultFallback', 2, '#FF69B3FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('GameBackground', 3, '#141419FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ScreenBackground', 4, '#121212FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('BlockBackground', 5, '#1A1A1AFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('BlockBorder', 6, '#303030FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('HeaderBackground', 7, '#232323FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('TextPrimary', 8, '#E2E2E2FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('TextMuted', 9, '#A0A0A0FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('Accent', 10, '#6E8EFFFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('AccentSoft', 11, '#6E8EFF46');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('OverlayBackground', 12, '#181818E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DragBarHoverTint', 13, '#1A1A1A78');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ResizeBar', 14, '#3A3A3AD2');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ResizeBarHover', 15, '#6E8EFF96');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ResizeBarActive', 16, '#6E8EFFDC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ButtonNeutral', 17, '#222222E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ButtonNeutralHover', 18, '#343434F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ButtonPrimary', 19, '#3A4E96EB');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ButtonPrimaryHover', 20, '#5674CCF0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('RowHover', 21, '#262626B4');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('RowDragging', 22, '#181818DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DropIndicator', 23, '#6E8EFF5A');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ToggleIdle', 24, '#2626268C');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ToggleHover', 25, '#445CA0C8');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ToggleActive', 26, '#445CA0E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('RebindScrim', 27, '#080808BE');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('Warning', 28, '#F0C440FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ScrollTrack', 29, '#181818DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ScrollThumb', 30, '#5F5F5FFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('ScrollThumbHover', 31, '#888888FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('IndicatorActive', 32, '#48C973FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('IndicatorInactive', 33, '#C0392BFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerBackground', 34, '#501414DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerHoverBackground', 35, '#8C2020F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerBorder', 36, '#A02828FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerHoverBorder', 37, '#DC4848FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerOverlayBackground', 38, '#401818F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerOverlayHoverBackground', 39, '#5A2424F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('DangerOverlayBorder', 40, '#962828FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('LockLockedFill', 41, '#262626DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('LockLockedHoverFill', 42, '#262626F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('LockUnlockedFill', 43, '#445CA0E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('LockUnlockedHoverFill', 44, '#445CA0FA');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('CloseGlyph', 45, '#FF4500FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RenderOrder, RowData) VALUES ('CloseGlyphHover', 46, '#FFFFFFFF');

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
