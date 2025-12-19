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
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('UsePreviousConfiguration', 17);
INSERT OR REPLACE INTO BlockControls (RowKey, RenderOrder) VALUES ('UseNextConfiguration', 18);

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
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('DragBarBackground', '#232323FF', 7, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('TextPrimary', '#E2E2E2FF', 8, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('TextMuted', '#A0A0A0FF', 9, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('Accent', '#6E8EFFFF', 10, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('AccentSoft', '#6E8EFF46', 11, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('OverlayBackground', '#181818E6', 12, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('DragBarHoverTint', '#1A1A1A78', 13, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ResizeEdge', '#3A3A3AD2', 14, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ResizeEdgeHover', '#6E8EFF96', 15, 0);
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData, RenderOrder, IsLocked) VALUES ('ResizeEdgeActive', '#6E8EFFDC', 16, 0);
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

-- Named color schemes (stored in RowData only; RenderOrder is left null)
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('__ActiveScheme', 'DarkMode');

-- Default (dark) palette snapshot
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::TransparentWindowKey', '#FF69B4FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::DefaultFallback', '#FF69B3FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::GameBackground', '#141419FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ScreenBackground', '#121212FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::BlockBackground', '#1A1A1AFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::BlockBorder', '#303030FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::DragBarBackground', '#232323FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::TextPrimary', '#E2E2E2FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::TextMuted', '#A0A0A0FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::Accent', '#6E8EFFFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::AccentSoft', '#6E8EFF46');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::OverlayBackground', '#181818E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::DragBarHoverTint', '#1A1A1A78');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ResizeEdge', '#3A3A3AD2');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ResizeEdgeHover', '#6E8EFF96');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ResizeEdgeActive', '#6E8EFFDC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ButtonNeutral', '#222222E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ButtonNeutralHover', '#343434F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ButtonPrimary', '#3A4E96EB');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ButtonPrimaryHover', '#5674CCF0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::RowHover', '#262626B4');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::RowDragging', '#181818DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::DropIndicator', '#6E8EFF5A');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ToggleIdle', '#2626268C');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ToggleHover', '#445CA0C8');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ToggleActive', '#445CA0E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::RebindScrim', '#080808BE');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::Warning', '#F0C440FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ScrollTrack', '#181818DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ScrollThumb', '#5F5F5FFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::ScrollThumbHover', '#888888FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::IndicatorActive', '#48C973FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::IndicatorInactive', '#C0392BFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseBackground', '#501414DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseHoverBackground', '#8C2020F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseBorder', '#A02828FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseHoverBorder', '#DC4848FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseOverlayBackground', '#401818F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseOverlayHoverBackground', '#5A2424F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseOverlayBorder', '#962828FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::LockLockedFill', '#262626DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::LockLockedHoverFill', '#262626F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::LockUnlockedFill', '#445CA0E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::LockUnlockedHoverFill', '#445CA0FA');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseGlyph', '#FF4500FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:DarkMode::CloseGlyphHover', '#FFFFFFFF');

-- Light mode palette snapshot
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::TransparentWindowKey', '#FF69B4FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::DefaultFallback', '#FF69B3FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::GameBackground', '#F5F5F8FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ScreenBackground', '#FAFAFCFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::BlockBackground', '#FFFFFFFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::BlockBorder', '#D7DAE2FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::DragBarBackground', '#F0F2F8FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::TextPrimary', '#202430FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::TextMuted', '#606880FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::Accent', '#4A6CD2FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::AccentSoft', '#4A6CD23C');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::OverlayBackground', '#F0F0F5E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::DragBarHoverTint', '#E2E6F0A0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ResizeEdge', '#D2D6DED2');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ResizeEdgeHover', '#4A6CD296');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ResizeEdgeActive', '#4A6CD2DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ButtonNeutral', '#F6F7FAFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ButtonNeutralHover', '#E8ECF4FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ButtonPrimary', '#4A6CD2EB');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ButtonPrimaryHover', '#5C7EE6F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::RowHover', '#ECF0F8C8');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::RowDragging', '#E2E6F0F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::DropIndicator', '#4A6CD26E');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ToggleIdle', '#E8ECF4BE');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ToggleHover', '#4A6CD2C8');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ToggleActive', '#4A6CD2E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::RebindScrim', '#F0F0F5BE');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::Warning', '#C8A020FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ScrollTrack', '#E8ECF4DC');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ScrollThumb', '#B4B8C3FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::ScrollThumbHover', '#A0A8B6FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::IndicatorActive', '#26A04BFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::IndicatorInactive', '#D24638FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseBackground', '#FFE8E8F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseHoverBackground', '#FFDCDCFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseBorder', '#DC8C8CFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseHoverBorder', '#F07878FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseOverlayBackground', '#FFECECF5');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseOverlayHoverBackground', '#FADEDEF5');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseOverlayBorder', '#DC8C8CFF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::LockLockedFill', '#E6E6EBE6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::LockLockedHoverFill', '#E2E2E8F0');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::LockUnlockedFill', '#4A6CD2E6');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::LockUnlockedHoverFill', '#4A6CD2F5');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseGlyph', '#C84634FF');
INSERT OR REPLACE INTO BlockColorScheme (RowKey, RowData) VALUES ('Scheme:LightMode::CloseGlyphHover', '#FFFFFFFF');

-----------------------------------------------------------------------
-- Docking setups block
-----------------------------------------------------------------------
INSERT OR REPLACE INTO BlockDockingSetups (RowKey, IsLocked) VALUES ('BlockLock', 1); -- default locked state stored at block-level
INSERT OR REPLACE INTO BlockDockingSetups (RowKey, RowData) VALUES ('__ActiveSetup', 'Default');
-- Make a group: { "id": "MyBlockGroup", ..., "blocks": ["block1", "block2", "block3"] },
--  then end in: "first": { "type": "Panel", "panel": "MyBlockGroup" },
--  so the group references the first block in the array
INSERT OR REPLACE INTO BlockDockingSetups (RowKey, RowData) VALUES ('Default', '{
  "version": 3,
  "menu": [
    { "kind": "Blank", "mode": "Count", "count": 0, "visible": false },
    { "kind": "Transparent", "mode": "Count", "count": 0, "visible": false },
    { "kind": "Game", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Properties", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "ColorScheme", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Controls", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Notes", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "DockingSetups", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Backend", "mode": "Toggle", "count": 0, "visible": true },
    { "kind": "Specs", "mode": "Toggle", "count": 0, "visible": true }
  ],
  "panels": [
    { "id": "colors", "active": "colors", "blocks": ["colors", "notes", "dockingsetups"] },
    { "id": "game", "active": "game", "blocks": ["game"] },
    { "id": "controls", "active": "controls", "blocks": ["controls"] },
    { "id": "backend", "active": "backend", "blocks": ["backend", "specs"] },
    { "id": "blank", "active": "blank", "blocks": ["blank"] },
    { "id": "transparent", "active": "transparent", "blocks": ["transparent"] },
    { "id": "properties", "active": "properties", "blocks": ["properties"] }
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
      "ratio": 0.28,
      "first": { "type": "Panel", "panel": "properties" },
      "second": {
        "type": "Split",
        "orientation": "Horizontal",
        "ratio": 0.58,
        "first": { "type": "Panel", "panel": "controls" },
        "second": {
          "type": "Split",
          "orientation": "Horizontal",
          "ratio": 0.45,
          "first": { "type": "Panel", "panel": "colors" },
          "second": { "type": "Panel", "panel": "backend" }
        }
      }
    }
  }
}');
