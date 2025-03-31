-- InitDatabaseStartData.sql

-- Insert default debug settings
INSERT INTO DebugSettings (Setting, Enabled, MaxRepeats) VALUES ('General', 1, 3);

-- Insert debug circle visual settings
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_R', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_G', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_B', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_A', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleRadius', '3');

-- Insert default general settings
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_R', '20');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_G', '20');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_B', '25');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_A', '255');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportWidth', '1400');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportHeight', '1400');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('WindowMode', 'BorderedWindowed');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('VSync', 'false'); -- G-Sync will override this
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('FixedTimeStep', 'false');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('TargetFrameRate', '240');

-- Insert default control settings
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveUp', 'W', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveDown', 'S', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveLeft', 'A', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveRight', 'D', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveTowardsCursor', 'LeftClick', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveAwayFromCursor', 'RightClick', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('Sprint', 'LeftShift', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState) VALUES ('Crouch', 'LeftControl', 'Switch', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('ReturnCursorToPlayer', 'Space', 'Trigger');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('Exit', 'Escape', 'Trigger');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('DebugMode', 'B', 'Switch'); -- Autoset to DebugEnabled on start
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState) VALUES ('DockingMode', 'V', 'Switch', 0);

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.5');

-- Insert default player
INSERT INTO Players (
    Name, Type, PositionX, PositionY, Speed, Radius, Width, Height, Sides,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsPlayer, IsDestructible, IsCollidable, Mass, StaticPhysics
) VALUES (
    'Player1', 'Circle', 100, 100, 300.0, 25, 80, 80, 0,
    0, 255, 255, 255,
    0, 150, 150, 255, 4,
    1, 0, 1, 1.0, 0
);

-- Insert static MapData objects
INSERT INTO MapData (
    Name, Type,
    PositionX, PositionY, Width, Height, Sides,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, IsPlayer, StaticPhysics
) VALUES
('RedWall', 'Rectangle',
 300, 300, 300, 30, 0,
 200, 50, 50, 255,
 128, 0, 0, 255, 2,
 1, 0, 0, 0, 1),
('GreenBackground', 'Rectangle',
 800, 300, 300, 30, 0,
 50, 200, 50, 255,
 0, 128, 0, 255, 0,
 0, 0, 0, 0, 1),
('BlueLoot', 'Rectangle',
 500, 500, 150, 150, 0,
 50, 50, 200, 255,
 0, 0, 128, 255, 4,
 1, 0, 5.0, 0, 0);

-- Insert farm prototypes (used for instancing)
INSERT INTO FarmData (
    Name, Type, PositionX, PositionY, Size, Sides, Width, Height, Weight, Count,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, IsPlayer, Mass, StaticPhysics
) VALUES
('Triangle', 'Polygon', 300, 300, 30, 3, 60, 60, 5, 15,
 255, 150, 150, 255,
 128, 75, 75, 255, 2,
 1, 1, 0, 1, 0),
('Square', 'Polygon', 350, 350, 40, 4, 70, 70, 8, 10,
 255, 255, 100, 255,
 128, 128, 50, 255, 3,
 1, 1, 0, 3, 0),
('Pentagon', 'Polygon', 400, 400, 50, 5, 80, 80, 12, 4,
 100, 100, 255, 255,
 50, 50, 128, 255, 4,
 1, 1, 0, 5, 0),
('Octagon', 'Polygon', 450, 450, 80, 8, 90, 90, 20, 2,
 255, 150, 255, 255,
 128, 75, 128, 255, 5,
 1, 1, 0, 15, 0);
