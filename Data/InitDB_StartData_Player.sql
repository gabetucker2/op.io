-- InitDB_StartData_Player.sql
-- Player game object and agent stats.

INSERT INTO GameObjects (
    Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Circle', 'Player1',
    100, 100, 50, 50, 0, 0,
    0, 255, 255, 255,
    0, 150, 150, 255, 4,
    1, 1, 3.0, 0
);

INSERT INTO Agents (
    ID, IsPlayer, TriggerCooldown, SwitchCooldown, BaseSpeed,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, BodyActionBuff
) VALUES (
    (SELECT last_insert_rowid()),
    1, 0.0, 0.0, 300.0,
    3.0,                -- Mass=3 → MaxHealth=100 (computed), BodyKnockback=12 (computed)
    5, 5.0, 0,
    10, 3, 3.0, 0,
    62.5, 0,
    0, 0,
    1.0, 1.0, 0.0       -- Speed=1×BaseSpeed, Control=1 → RotDelay=0.15s, AccelDelay=0.2s (computed)
);
