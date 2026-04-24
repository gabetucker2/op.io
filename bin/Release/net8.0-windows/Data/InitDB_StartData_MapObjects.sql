-- InitDB_StartData_MapObjects.sql
-- Static and destructible map objects.
-- Destructible attributes live in the Destructibles table (no more MapData).
-- BlueLoot is defined in InitDB_StartData_Farms.sql as a manual farm prototype.

-- RedWall
INSERT INTO GameObjects (
    Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Rectangle', 'RedWall',
    300, 300, 300, 30, 0, 0,
    200, 50, 50, 255,
    128, 0, 0, 255, 5,
    1, 0, 0.0, 1
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    0, 0
);

-- ScoutSentry1 (stationary vision unit)
INSERT INTO GameObjects (
    Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Circle', 'ScoutSentry1',
    420, 100, 50, 50, 0, 0,
    250, 220, 70, 255,
    120, 95, 30, 255, 4,
    0, 1, 3.0, 1
);

INSERT INTO Agents (
    ID, IsPlayer, TriggerCooldown, SwitchCooldown, BaseSpeed,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, Sight, BodyActionBuff
) VALUES (
    (SELECT last_insert_rowid()),
    0, 0.0, 0.0, 0.0,
    3.0,
    0.0, 0.0, 0.0,
    0.0, 0.0, 0.0, 0.0,
    0.0, 0.0,
    0.0, 0.0,
    0.0, 1.0,
    (SELECT COALESCE(a.Sight, 50.0) / 3.0 FROM Agents a WHERE a.IsPlayer = 1 LIMIT 1),
    0.0
);

INSERT INTO Destructibles (
    ID,
    MaxHealth, DeathPointReward
) VALUES (
    (SELECT ID FROM GameObjects WHERE Name = 'ScoutSentry1' ORDER BY ID LIMIT 1),
    100.0, 7.0
);

-- GreenBackground
INSERT INTO GameObjects (
    Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Rectangle', 'GreenBackground',
    800, 300, 300, 30, 0, 0,
    50, 200, 50, 255,
    0, 128, 0, 255, 5,
    0, 0, 0.0, 1
);
INSERT INTO Destructibles (
    ID,
    MaxHealth, DeathPointReward
) VALUES (
    (SELECT last_insert_rowid()),
    0, 0
);
