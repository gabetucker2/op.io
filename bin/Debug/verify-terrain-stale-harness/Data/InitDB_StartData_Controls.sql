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
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DisableToolTips',         'When enabled, tooltips do not appear anywhere in the UI.');
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
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyLeft',                 'Switch to the previous body configuration.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyRight',                'Switch to the next body configuration.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CombatText',               'Toggle floating damage numbers and XP text during combat.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CameraLockMode',           'Camera follow mode: Free (no follow), Scout (always centered), or Locked (fixed offset).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CameraSnapToPlayer',       'Snap the camera to center on the player. In Locked mode, resets the offset.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Respawn',                  'Respawn the player after death.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ScrollIncrement',          'Scroll wheel units per zoom step (default 120 = one notch).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ShowHiddenAttrs',          'Default visibility of hidden attributes in the Properties block. Per-object overrides are remembered separately.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Grid',                     'Toggle the world grid overlay. Draws 1-centifoot grey grid lines with major 5-centifoot coordinate plotting.');

-- Ambience block
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AmbienceFogOfWarColor',   'Base color currently applied to hidden fog-of-war territory. Edit it live in the Ambience block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AmbienceOceanWaterColor', 'Base color currently driving the ocean water shader. Edit it live in the Ambience block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AmbienceBackgroundWavesColor', 'Highlight color for the background wave crests in the ocean ambience. Edit it live in the Ambience block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AmbienceTerrainColor', 'Fill color for generated terrain, finite map borders, and the world beyond the playable square. Edit it live in the Ambience block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AmbienceWorldTintColor',  'Gameplay object tint color. Mid-gray is neutral; warmer or cooler colors shift object colors already drawn in the world.');

-- Backend block
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FreezeGameInputs',   'Gameplay inputs are currently suspended. Keyboard and mouse actions will not affect the game while this is true.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AnyGUIInteracting',  'True when the cursor is pressed inside a UI block (not the game viewport). Gameplay inputs are suppressed while this is active.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('GUIInteractingWith', 'Name of the UI block currently being clicked. Empty or None means no block is being interacted with.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DockingMode',        'Whether block-layout docking mode is active. When true, blocks can be resized and repositioned.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('NativeWindowResizeEdges', 'True when borderless window mode is exposing native Windows outer-edge resize hit targets.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BlockMenuOpen',      'Whether the block visibility overlay is currently open. Use it to show or hide UI panels.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('InputBlocked',       'True when a modal overlay (rebind dialog, block menu, or color editor) is consuming all input.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DraggingLayout',     'True while a UI block or panel is actively being dragged to a new position.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CursorOnGameBlock',  'True when the cursor is hovering over the game viewport and not covered by any overlay block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('HoveredBlock',       'Name of the UI block the cursor is currently over, or None if over the game viewport or outside the window.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('HoveredDragBar',     'Name of the UI block whose drag bar the cursor is currently hovering over, or None.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FocusedBlock',       'The block that last captured keyboard focus. Keyboard shortcuts routed to block content use this. None if no block has focus.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BlockType',          'Category of the focused block: Standard, Overlay, or Dynamic.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DoubleTapSuppressionSeconds', 'Seconds used as the DoubleTapToggle window. Non-DoubleTap switch/toggle bindings that share the same primary input are suppressed during this window after the first tap.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPClumpCount',       'Number of active farm XP clumps currently in the world.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('PendingFarmXPDrops', 'Number of farms currently fading out that still have queued XP clumps to spawn.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPClumpsAbsorbedThisSecond', 'How many XP clumps have been absorbed across all units during the current one-second pickup window.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPClumpPickupPerSecond', 'Maximum number of XP clumps a single unit may absorb per second.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPClumpDeadZoneRadius', 'Distance from a clump where units are considered outside active pickup influence, shown in centifoots.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPClumpPullZoneRadius', 'Distance from a clump where units begin applying pull-zone attraction, shown in centifoots.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('XPClumpAbsorbZoneRadius', 'Distance from a clump where orbit-lock and absorption behavior begins, shown in centifoots.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CentifootWorldUnits', 'Copied baseline conversion: 1 centifoot = this many world units.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DistanceUnit', 'Name of the active distance unit used by backend distance displays.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainWorldSeed', 'Seed used with chunk coordinates to deterministically regenerate terrain without saving chunk payloads.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainResidentChunkCount', 'How many terrain chunks are currently cached in memory, including water-only chunk records.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainPendingChunkCount', 'How many terrain chunks are currently queued or building in the background.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainPendingCriticalChunkCount', 'How many camera-or-fog-visible terrain chunks are still building; resident terrain visuals are held stable while this is above zero.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainDiscardedStaleMaterializationCount', 'How many completed terrain mesh builds were discarded because the camera or vision window changed before they finished.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainChunkBuildsInFlight', 'Shows whether any terrain chunk builds are currently in flight.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainChunkWorldSize', 'World-space width and height covered by each deterministic terrain chunk.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainPreloadMarginWorldUnits', 'Extra world-space margin around the camera-and-fog-visible terrain streaming window that chunk loading prebuilds ahead of view.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainSeedAnchor', 'Seed-derived terrain-space anchor applied before chunk sampling so the spawn region opens near generated land instead of empty ocean.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainCenterChunk', 'Chunk coordinate currently centered under the terrain streaming focus.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('TerrainVisibleChunkWindow', 'Inclusive chunk-coordinate window currently required by the camera and fog-of-war vision sources before preload margin.');

-- Backend block — physics & bullet constants
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AirResistanceScalar',   'Drag scalar applied to bullets: drag = AirResistanceScalar × bulletVolume / bulletDragFactor.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BounceVelocityLoss',    'Fraction of bullet speed lost per bounce off static objects, scaled by mass ratio.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('HitVelocityLoss',       'Fraction of bullet speed lost when penetrating non-static objects, scaled by mass ratio.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('PenetrationSpring',     'Spring stiffness governing how deeply bullets embed in objects. Higher = shallower penetration.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('PenetrationDamping',    'Damping on embedded bullets. 0 = elastic bounce; higher = stickier impact.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DefaultBulletSpeed',    'Fallback bullet speed (centifoot/s) when a barrel does not specify one.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DefaultBulletLifespan', 'Fallback bullet lifespan in seconds before automatic despawn.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DefaultDragFactor',     'Fallback bullet drag factor used in air-resistance calculations.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DefaultBulletMass',     'Fallback bullet mass, affects recoil, knockback, and drag.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DefaultBulletDamage',   'Fallback flat damage dealt by a bullet on hit.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('DefaultBulletPenHP',    'Fallback penetration HP; consumed as a bullet passes through objects.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('PhysicsFrictionRate',   'Velocity decay rate applied to game objects each frame: vel *= clamp(1 − rate × dt, 0, 1).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('CollisionBounceMomentumTransfer', 'Fraction of incoming collision momentum converted into bounce impulse against static or terrain bodies.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('AngularAccelFactor',        'Multiplier on angular acceleration when rotating toward the cursor.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BarrelSwitchSpeed',         'Speed at which the active barrel index rotates between barrels (units/s).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ActiveBodyIndex',           'Zero-based index of the currently active body slot.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('ActiveBodyName',            'Display name of the currently active body.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyTransitionAnimating',   'True while the player body is actively blending from the previous slot into the newly selected slot.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyTransitionProgress',    'Normalized progress through the active body transition lerp. 0 = just started, 1 = fully arrived.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyTransitionCooldownRemaining', 'Seconds remaining before another body change is allowed after the current transition finishes.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyTransitionDurationSeconds', 'Configured SQL duration for a full body transition lerp.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyTransitionBufferSeconds', 'Configured SQL lockout that begins after a body transition finishes.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BodyRadiusScalar',           'Circle body radius = sqrt(mass) × BodyRadiusScalar.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BulletRadiusScalar',        'Bullet visual radius = sqrt(mass) × BulletRadiusScalar.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BarrelHeightScalar',        'Barrel length = max(4, bulletSpeed × BarrelHeightScalar).');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BulletKnockbackScalar',     'Knockback multiplier: knockback = bulletPenetration × BulletKnockbackScalar.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BulletRecoilScalar',        'Recoil multiplier: recoil = bulletMass × (1 + knockback) × BulletRecoilScalar.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('BulletFarmKnockbackScalar', 'Knockback attenuation for farm objects hit by bullets.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('OwnerImmunityDuration',    'Seconds after spawn during which a bullet cannot collide with or damage its owner. Bullet fades in over this period.');

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

-- Drag bar button tooltips
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_Close',         'Close this tab. If it is the last tab in the panel, the panel is removed from the layout.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_PanelLock',     'Toggle panel lock for drag-bar layout actions.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_TabLock',       'Toggle lock for this tab''s block.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_TabRelease',    'Move this overlay tab back into its parent panel.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_OverlayMerge',  'Move overlay tab(s) from this panel back to the parent panel.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_Lock',          'Toggle panel lock for drag-bar layout actions.');

-- Color scheme block button tooltips
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SchemeSave',    'Save the current color values to the active scheme.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SchemeNew',     'Create a new color scheme copied from the current values.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SchemeRename',  'Rename the active scheme. Built-in schemes cannot be renamed.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SchemeDelete',  'Delete the active scheme. Built-in schemes cannot be deleted.');

-- Setup block button tooltips (shared by Control Setups, Docking Setups, and Notes)
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SetupSave',     'Save current settings to the selected configuration.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SetupNew',      'Create a new configuration from the current settings.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SetupRename',   'Rename the selected configuration.');
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('Btn_SetupDelete',   'Delete the selected configuration.');

---------------------------------------------------------------------------------------------------------------------------

-- Most ControlKey rows are seeded via control configuration saves (see ControlConfigurationManager).
-- Meta controls with explicit defaults are seeded here so their initial state is easy to change.
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, RenderCategory, RenderCategoryOrder, LockMode)
    VALUES ('DebugMode', 'Shift + B', 'SaveSwitch', 0, 1, 0, 'System', 4, 0);
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, RenderCategory, RenderCategoryOrder, LockMode)
    VALUES ('DisableToolTips', 'Ctrl + T', 'SaveSwitch', 0, 1, 0, 'Interface', 3, 0);
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, RenderCategory, RenderCategoryOrder, LockMode)
    VALUES ('CameraLockMode', 'C', 'SaveEnum', 0, 1, 0, 'Camera', 2, 0);
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, RenderCategory, RenderCategoryOrder, LockMode)
    VALUES ('Grid', 'G', 'SaveSwitch', 0, 0, 0, 'Interface', 3, 0);

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.1');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DoubleTapSuppressionSeconds', '0.25');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CursorFollowDeadzonePixels', '10');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '25');
