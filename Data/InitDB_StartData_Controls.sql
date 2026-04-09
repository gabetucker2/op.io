-- InitDB_StartData_Controls.sql
-- Keybind tooltips, default control key seed, and control settings.

---------------------------------------------------------------------------------------------------------------------------

-- Controls block — one tooltip per keybind row key
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('MoveUp',                  'Move the player upward.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('MoveDown',                'Move the player downward.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('MoveLeft',                'Move the player to the left.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('MoveRight',               'Move the player to the right.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('MoveTowardsCursor',       'Move the player toward the cursor position.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('MoveAwayFromCursor',      'Move the player away from the cursor position.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Sprint',                  'Hold to move faster. Speed multiplier is set in ControlSettings.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Crouch',                  'Hold to move slower. Speed multiplier is set in ControlSettings.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ReturnCursorToPlayer',    'Snap the cursor back to the player position.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Exit',                    'Quit the game.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BlockMenu',               'Open the block visibility overlay to show or hide UI panels.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DockingMode',             'Toggle docking mode to resize and rearrange UI panels.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DebugMode',               'Toggle debug visuals such as the physics collision circle.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AllowGameInputFreeze',    'Allow the game input freeze toggle. Must be enabled before FreezeGameInputs takes effect.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TransparentTabBlocking',  'When enabled, the transparent block intercepts clicks instead of passing them to the game.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('HoldInputs',              'Toggle hold mode for directional inputs, keeping them active without holding the key.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('UsePreviousConfiguration','Switch to the previous saved control configuration profile.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('UseNextConfiguration',    'Switch to the next saved control configuration profile.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AutoTurnInspectModeOff',  'When enabled, inspect mode turns off automatically after clicking on or away from an object.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('InspectMode',              'Toggle inspect mode to hover and examine game objects.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TabSwitchRequiresBlockMode','When enabled, the tab key only switches panels while Block Menu is open.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Fire',                     'Fire the equipped weapon.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BarrelLeft',               'Rotate barrel selection counter-clockwise.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BarrelRight',              'Rotate barrel selection clockwise.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CombatText',               'Toggle floating damage numbers and XP text during combat.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CameraLockMode',           'Camera follow mode: Free (no follow), Scout (always centered), or Locked (fixed offset).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CameraSnapToPlayer',       'Snap the camera to center on the player. In Locked mode, resets the offset.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Respawn',                  'Respawn the player after death.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ScrollIncrement',          'Scroll wheel units per zoom step (default 120 = one notch).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ShowHiddenAttrs',          'Default visibility of hidden attributes in the Properties block. Per-object overrides are remembered separately.');

-- Backend block
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FreezeGameInputs',   'Gameplay inputs are currently suspended. Keyboard and mouse actions will not affect the game while this is true.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AnyGUIInteracting',  'True when the cursor is pressed inside a UI block (not the game viewport). Gameplay inputs are suppressed while this is active.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('GUIInteractingWith', 'Name of the UI block currently being clicked. Empty or None means no block is being interacted with.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DockingMode',        'Whether block-layout docking mode is active. When true, blocks can be resized and repositioned.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BlockMenuOpen',      'Whether the block visibility overlay is currently open. Use it to show or hide UI panels.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('InputBlocked',       'True when a modal overlay (rebind dialog, block menu, or color editor) is consuming all input.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DraggingLayout',     'True while a UI block or panel is actively being dragged to a new position.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CursorOnGameBlock',  'True when the cursor is hovering over the game viewport and not covered by any overlay block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('HoveredBlock',       'Name of the UI block the cursor is currently over, or None if over the game viewport or outside the window.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('HoveredDragBar',     'Name of the UI block whose drag bar the cursor is currently hovering over, or None.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FocusedBlock',       'The block that last captured keyboard focus. Keyboard shortcuts routed to block content use this. None if no block has focus.');

-- Controls block — XP bar toggle
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPBar', 'Toggle XP bars visible under all units. Configure bar layout in the Bars block.');

-- Specs block
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FPS',            'Frames rendered per second. Higher is smoother.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TargetFPS',      'The frame rate cap configured in GeneralSettings.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FrameTime',      'Time in milliseconds to process and render one frame.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('WindowMode',     'Current window mode: bordered, borderless, or fullscreen.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('VSync',          'Vertical sync state. Locks frame rate to the display refresh rate when enabled.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FixedTime',      'Fixed timestep mode. When enabled, Update runs at a constant rate regardless of rendering speed.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('WindowSize',     'Width and height of the game window in pixels.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Backbuffer',     'Dimensions of the GPU backbuffer used for rendering.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('SurfaceFormat',  'Pixel format of the backbuffer surface.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DepthFormat',    'Bit depth of the depth and stencil buffer.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('GraphicsProfile', 'DirectX feature level used by the graphics device.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Adapter',        'Name of the active GPU adapter.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CPUThreads',     'Number of logical processor threads available to the process.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ProcessMemory',  'Total memory allocated to this process by the OS.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ManagedMemory',  'Memory used by the .NET managed heap.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('OS',             'Operating system name and version.');

---------------------------------------------------------------------------------------------------------------------------

-- Most ControlKey rows are seeded via control configuration saves (see ControlConfigurationManager).
-- Meta controls with explicit defaults are seeded here so their initial state is easy to change.
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, LockMode)
    VALUES ('DebugMode', 'Shift + B', 'SaveSwitch', 0, 1, 13, 0);
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, LockMode)
    VALUES ('CameraLockMode', 'C', 'SaveEnum', 0, 1, 26, 0);

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.1');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CursorFollowDeadzonePixels', '10');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '25');
