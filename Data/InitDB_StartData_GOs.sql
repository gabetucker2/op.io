-- InitDatabaseStartData.sql

-- ================================
-- Insert Player
-- ================================
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Normal', 'Circle', 'Player1',
    100, 100, 50, 50, 0, 0,
    0, 255, 255, 255,
    0, 150, 150, 255, 4,
    1, 0, 1.0, 0
);

INSERT INTO Agents (
    ID, IsPlayer, TriggerCooldown, SwitchCooldown, BaseSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    1, 0.0, 0.0, 300.0
);

-- ================================
-- Insert Map Objects
-- ================================
-- RedWall
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Normal', 'Rectangle', 'RedWall',
    300, 300, 300, 30, 0, 0,
    200, 50, 50, 255,
    128, 0, 0, 255, 2,
    1, 0, 0.0, 1
);
INSERT INTO MapData (ID) VALUES ((SELECT last_insert_rowid()));

-- GreenBackground
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Normal', 'Rectangle', 'GreenBackground',
    800, 300, 300, 30, 0, 0,
    50, 200, 50, 255,
    0, 128, 0, 255, 0,
    0, 0, 0.0, 1
);
INSERT INTO MapData (ID) VALUES ((SELECT last_insert_rowid()));

-- BlueLoot
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Normal', 'Rectangle', 'BlueLoot',
    500, 500, 150, 150, 0, 0,
    50, 50, 200, 255,
    0, 0, 128, 255, 4,
    1, 0, 5.0, 0
);
INSERT INTO MapData (ID) VALUES ((SELECT last_insert_rowid()));

-- ================================
-- Insert Farm (Polygons) as Prototypes
-- ================================

-- Triangle Farm Prototype
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES
(
    'Prototype', 'Polygon', 'Triangle',
    300, 300, 60, 60, 3, 0,
    255, 150, 150, 255,
    128, 75, 75, 255, 2,
    1, 0, 1.0, 0
);
INSERT INTO FarmData (ID, Count) VALUES ((SELECT last_insert_rowid()), 15);

-- Square Farm Prototype
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES
(
    'Prototype', 'Polygon', 'Square',
    350, 350, 70, 70, 4, 0,
    255, 255, 100, 255,
    128, 128, 50, 255, 3,
    1, 1, 3.0, 0
);
INSERT INTO FarmData (ID, Count) VALUES ((SELECT last_insert_rowid()), 10);

-- Pentagon Farm Prototype
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES
(
    'Prototype', 'Polygon', 'Pentagon',
    400, 400, 80, 80, 5, 0,
    100, 100, 255, 255,
    50, 50, 128, 255, 4,
    1, 0, 5.0, 0
);
INSERT INTO FarmData (ID, Count) VALUES ((SELECT last_insert_rowid()), 4);

-- Octagon Farm Prototype
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES
(
    'Prototype', 'Polygon', 'Octagon',
    450, 450, 90, 90, 8, 0,
    255, 150, 255, 255,
    128, 75, 128, 255, 5,
    1, 0, 15.0, 0
);
INSERT INTO FarmData (ID, Count) VALUES ((SELECT last_insert_rowid()), 2);
