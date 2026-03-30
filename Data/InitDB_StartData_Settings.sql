-- InitDB_StartData_Settings.sql

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_R', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_G', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_B', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_A', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleRadius', '3');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_R', '20');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_G', '20');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_B', '25');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_A', '255');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportWidth', '1200');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportHeight', '1200');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('WindowMode', 'BorderedWindowed');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('VSync', 'false'); -- G-Sync will override this
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('FixedTimeStep', 'false');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('TargetFrameRate', '240');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('NumLogFiles', '5');

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

-- Backend block
INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES ('FreezeGameInputs', 'Suspend all gameplay inputs. The game pauses reacting to keyboard and mouse while this is active.');

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
-- DebugMode is seeded here so its default state is explicit and easy to change.
INSERT OR IGNORE INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl, RenderOrder, LockMode)
    VALUES ('DebugMode', 'Shift + B', 'SaveSwitch', 0, 1, 13, 0);

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.1');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CursorFollowDeadzonePixels', '10');

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '25');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('AirResistanceScalar', '2.12');   -- drag scalar: drag = AirResistanceScalar * bulletVolume / bulletDragFactor
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('BounceVelocityLoss', '0.2'); -- fraction of bullet speed lost per bounce off static objects (scaled by object mass ratio)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('HitVelocityLoss', '0.2');   -- fraction of bullet speed lost per hit through non-static objects (scaled by object mass ratio)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletSpeed', '400');    -- fallback bullet speed (px/s) when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletLifespan', '3');   -- fallback bullet lifespan (seconds) when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletDragFactor', '800');  -- fallback bullet drag factor when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletMass', '1');       -- fallback bullet mass when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletDamage', '10');    -- fallback bullet damage when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletHealth', '10');    -- fallback bullet penetration HP when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('PenetrationSpringCoeff', '500'); -- spring stiffness: higher = shallower embed; needs ≥ mv²/R² to stop bullet in-object
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('PenetrationDamping', '10');      -- damping: 0 = elastic bounce; ~89 = critical (no overshoot); higher = inelastic/sticky
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('BulletRadiusScalar', '6');       -- bullet radius scalar: radius = sqrt(mass) * BulletRadiusScalar
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('HitFlashAnim',  '0.05|0.0|0.2');  -- hit-flash:  fadeIn | hold | fadeOut (seconds)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DespawnAnim',   '0.0|0.0|0.2');   -- despawn:    fadeIn | hold | fadeOut (seconds)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumAnim', '0.12|0.8|0.8');  -- damage num: fadeIn | hold | fadeOut (seconds)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumDriftSpeed',    '28');   -- upward drift speed (px/s)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumDriftSpread',   '22');   -- horizontal random spread (px/s)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumScaleStart',    '0.5');  -- initial scale at spawn
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumScalePeak',     '1.4');  -- peak scale at end of fade-in
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumSpawnCooldown', '0.15'); -- min time between damage numbers per object (seconds)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DamageNumLifetimeExtension', '0.15'); -- lifetime added per stacked hit (seconds)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('HealthBarAnim', '0.15|0.0|0.40'); -- health bar: fadeIn | hold | fadeOut (seconds)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('HealthBarHeight',  '4');    -- health bar height in pixels
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('HealthBarOffsetY', '8');    -- pixels below object edge
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletFillR',      '255'); -- default bullet fill colour (R)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletFillG',      '0');   -- default bullet fill colour (G)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletFillB',      '0');   -- default bullet fill colour (B)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletFillA',      '255'); -- default bullet fill colour (A)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletOutlineR',   '139'); -- default bullet outline colour (R)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletOutlineG',   '0');   -- default bullet outline colour (G)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletOutlineB',   '0');   -- default bullet outline colour (B)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletOutlineA',   '255'); -- default bullet outline colour (A)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletOutlineWidth', '2'); -- default bullet outline width (px)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletPenetration',  '0'); -- default bullet penetration (armor bypass; 0 = no bypass)

---------------------------------------------------------------------------------------------------------------------------
