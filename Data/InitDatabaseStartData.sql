-- InitDatabaseStartData.sql

-- Insert default debug settings
INSERT INTO DebugSettings (Setting, Enabled, MaxRepeats) VALUES ('General', 0, 2);

-- Insert debug circle visual settings
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_R', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_G', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_B', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_A', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleRadius', '3');

-- Insert default general settings
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_R', '30');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_G', '30');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_B', '30');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_A', '255');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportWidth', '1400');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportHeight', '1400');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('WindowMode', 'BorderedWindowed');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('VSync', 'false'); -- G-Sync will override this
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('FixedTimeStep', 'false');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('TargetFrameRate', '240');

-- Insert default player
INSERT INTO Players (
    Name, Type, PositionX, PositionY, Speed, Radius, Width, Height, Sides,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsPlayer, IsDestructible, IsCollidable, Mass
) VALUES (
    'Player1', 'Circle', 100, 100, 800.0, 25, 80, 80, 0,
    0, 255, 255, 255,
    0, 150, 150, 255, 4,
    1, 0, 1, 1.0
);

-- Insert a map object
INSERT INTO MapData (
    Name, Type, PositionX, PositionY, Width, Height, Sides,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, IsPlayer
) VALUES (
    'BlueWall', 'Rectangle', 
    500, 500, 100, 50, 4,
    50, 50, 200, 255,
    0, 0, 128, 255, 4,
    1, 0, 1.0, 0
);

-- Insert farm prototypes (used for instancing)
INSERT INTO FarmData (
    Type, PositionX, PositionY, Size, Sides, Width, Height, Weight, Count,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, IsPlayer, Mass
) VALUES
    ('Polygon', 300, 300, 30, 3, 60, 60, 5, 15,
     255, 150, 150, 255,
     128, 75, 75, 255, 2,
     1, 1, 0, 1.0),

    ('Polygon', 350, 350, 40, 4, 70, 70, 8, 10,
     255, 255, 100, 255,
     128, 128, 50, 255, 3,
     1, 1, 0, 1.0),
     
    ('Polygon', 400, 400, 50, 5, 80, 80, 12, 4,
     100, 100, 255, 255,
     50, 50, 128, 255, 4,
     1, 1, 0, 1.0),

    ('Polygon', 450, 450, 80, 8, 90, 90, 20, 2,
     255, 150, 255, 255,
     128, 75, 128, 255, 5,
     1, 1, 0, 1.0);
