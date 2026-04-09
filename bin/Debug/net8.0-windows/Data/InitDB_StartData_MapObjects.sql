-- InitDB_StartData_MapObjects.sql
-- Static and destructible map objects.
-- Destructible attributes live in the Destructibles table (no more MapData).
-- BlueLoot is defined in InitDB_StartData_Farms.sql as a manual farm prototype.

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
INSERT INTO Destructibles (
    ID,
    MaxHealth, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    0, 0
);

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
INSERT INTO Destructibles (
    ID,
    MaxHealth, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    0, 0
);
