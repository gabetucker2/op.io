-- InitDB_StartData_Farms.sql
-- Farm prototype game objects.
-- FarmData holds spawn/placement/animation config.
-- Destructibles holds all health/combat/reward attributes.
--
-- FloatAmplitude: reserved (unused by current rotation model).
-- FloatSpeed:     direction-reversal frequency in cycles per second (higher = faster direction changes).
-- RotationSpeed:  max angular velocity in rad/s; physics accelerates toward this each direction cycle.
-- IsManual=1 places the object at (ManualX, ManualY) instead of random spawning.

-- ================================
-- Triangle Farm Prototype
-- ================================
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Prototype', 'Polygon', 'Triangle',
    300, 300, 60, 60, 3, 0,
    255, 150, 150, 255,
    128, 75, 75, 255, 2,
    1, 1, 12.0, 0
);
INSERT INTO FarmData (
    ID,
    Count, RotationSpeed,
    IsManual, ManualX, ManualY,
    FloatAmplitude, FloatSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    15, 0.20,
    0, 0, 0,
    0.20, 0.25
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, HealthRegen, HealthRegenDelay,
    BodyCollisionDamage, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    25, 1.25, 6.0,
    100, 10
);

-- ================================
-- Square Farm Prototype
-- ================================
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Prototype', 'Polygon', 'Square',
    350, 350, 70, 70, 4, 0,
    255, 255, 100, 255,
    128, 128, 50, 255, 3,
    1, 1, 15, 0
);
INSERT INTO FarmData (
    ID,
    Count, RotationSpeed,
    IsManual, ManualX, ManualY,
    FloatAmplitude, FloatSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    10, 0.20,
    0, 0, 0,
    0.15, 0.20
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, HealthRegen, HealthRegenDelay,
    BodyCollisionDamage, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    50, 2.5, 6.0,
    200, 20
);

-- ================================
-- Pentagon Farm Prototype
-- ================================
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Prototype', 'Polygon', 'Pentagon',
    400, 400, 80, 80, 5, 0,
    100, 100, 255, 255,
    50, 50, 128, 255, 4,
    1, 1, 25, 0
);
INSERT INTO FarmData (
    ID,
    Count, RotationSpeed,
    IsManual, ManualX, ManualY,
    FloatAmplitude, FloatSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    4, 0.20,
    0, 0, 0,
    0.12, 0.15
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, HealthRegen, HealthRegenDelay,
    BodyCollisionDamage, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    100, 5.0, 6.0,
    350, 50
);

-- ================================
-- Octagon Farm Prototype
-- ================================
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Prototype', 'Polygon', 'Octagon',
    450, 450, 90, 90, 8, 0,
    255, 150, 255, 255,
    128, 75, 128, 255, 5,
    1, 1, 35, 0
);
INSERT INTO FarmData (
    ID,
    Count, RotationSpeed,
    IsManual, ManualX, ManualY,
    FloatAmplitude, FloatSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    2, 0.20,
    0, 0, 0,
    0.08, 0.10
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, HealthRegen, HealthRegenDelay,
    BodyCollisionDamage, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    200, 10.0, 6.0,
    600, 100
);

-- ================================
-- BlueLoot Farm Prototype (manually placed at a fixed position)
-- IsManual=1 spawns exactly 1 instance at (ManualX, ManualY).
-- ================================
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Prototype', 'Rectangle', 'BlueLoot',
    500, 500, 150, 150, 0, 0,
    50, 50, 200, 255,
    0, 0, 128, 255, 4,
    1, 1, 50, 0
);
INSERT INTO FarmData (
    ID,
    Count, RotationSpeed,
    IsManual, ManualX, ManualY,
    FloatAmplitude, FloatSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    1, 0.0,
    1, 500, 500,
    0.10, 0.125
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, HealthRegen, HealthRegenDelay,
    BodyCollisionDamage, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    400, 20.0, 6.0,
    1000, 200
);
