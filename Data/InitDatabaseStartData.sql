--Insert default general settings
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_R', '30');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_G', '30');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_B', '30');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_A', '255');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportWidth', '1280');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportHeight', '720');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('Fullscreen', 'false');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('VSync', 'true');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('TargetFrameRate', '120');

--Insert default debug settings
INSERT INTO DebugSettings (Setting, Enabled, MaxRepeats) VALUES ('General', 0, 3);

--Insert default player
INSERT INTO Players (Name, Type, PositionX, PositionY) VALUES ('Player1', 'Circle', 100, 100);

--Insert default map object
INSERT INTO MapData (
    Name, Type, PositionX, PositionY, Width, Height,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Rotation, Mass, IsPlayer
) VALUES (
    'CentralGreyBlock', 'Rectangle', 
    640, 360, 800, 600,              -- Center of 1280x720
    150, 150, 150, 255,              -- Grey fill
    50, 50, 50, 255, 4,              -- Dark grey outline
    1, 0,                            -- Collidable, not destructible
    0.0, 1.0, 0                      -- Rotation, mass, isPlayer
);

--Insert example farm objects
INSERT INTO FarmData (Type, PositionX, PositionY, Size, Sides, Weight, Count, FillR, FillG, FillB, FillA, OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth, IsCollidable, IsDestructible)
VALUES ('Circle', 200, 200, 25, 0, 15, 5, 200, 255, 150, 255, 75, 128, 75, 255, 4, 1, 1);

INSERT INTO FarmData (Type, PositionX, PositionY, Size, Sides, Weight, Count, FillR, FillG, FillB, FillA, OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth, IsCollidable, IsDestructible)
VALUES ('Polygon', 300, 300, 30, 3, 5, 30, 255, 150, 150, 255, 128, 75, 75, 255, 2, 1, 1);

--Confirm completion
SELECT 'Database initialized successfully' AS Status;
