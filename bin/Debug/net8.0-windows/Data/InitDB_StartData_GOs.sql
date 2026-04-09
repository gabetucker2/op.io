-- InitDB_StartData_GOs.sql
-- LEGACY reference file. Active data is split into:
--   InitDB_StartData_Player.sql
--   InitDB_StartData_MapObjects.sql
--   InitDB_StartData_Farms.sql
-- This file is NOT loaded by DatabaseInitializer.

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
    1, 1, 1.0, 0
);

INSERT INTO Agents (
    ID, IsPlayer, TriggerCooldown, SwitchCooldown, BaseSpeed,
    MaxHealth, HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyPenetration, BodyCollisionDamage, BodyKnockback,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, RotationSpeed
) VALUES (
    (SELECT last_insert_rowid()),
    1, 0.0, 0.0, 300.0,
    100, 5, 5.0, 0,
    10, 3, 3.0, 0,
    0, 10, 200,
    0, 0,
    0, 0
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
INSERT INTO MapData (
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
INSERT INTO MapData (
    ID,
    MaxHealth, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    0, 0
);

-- NOTE: BlueLoot is defined in InitDB_StartData_Farms.sql as a farm
-- prototype with manual placement. See that file for its definition.

-- NOTE: Farm prototypes (Triangle, Square, Pentagon, Octagon, BlueLoot)
-- are defined in InitDB_StartData_Farms.sql.
