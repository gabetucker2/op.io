-- InitDB_StartData_Bodies.sql

-- ================================
-- Body Prototypes
-- ================================

-- Normal: the default body, matching the base player profile.
INSERT INTO BodyPrototypes (
    Name,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, Sight, BodyActionBuff,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth
) VALUES (
    'Normal',
    3.0,                -- Mass=3 -> MaxHealth=100
    5, 5.0, 0,
    10, 3, 3.0, 0,
    62.5, 0,
    0, 0,
    1.0, 1.0, 50.0, 0.0,
    0, 255, 255, 255,
    0, 150, 150, 255, 5
);

-- Tank: slower, heavier body with visibly denser visuals.
INSERT INTO BodyPrototypes (
    Name,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, Sight, BodyActionBuff,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth
) VALUES (
    'Tank',
    9.0,                -- Mass=9 -> MaxHealth=300
    3, 8.0, 2,
    20, 2, 5.0, 1,
    125.0, 0,
    0.3, 0.2,
    0.55, 0.6, 50.0, 0.0,
    0, 160, 255, 255,
    0, 88, 184, 255, 6
);

-- ScoutSentryBody: stationary support body that contributes one-third of player sight.
INSERT INTO BodyPrototypes (
    Name,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, Sight, BodyActionBuff,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth
) VALUES (
    'ScoutSentryBody',
    3.0,
    0, 0.0, 0,
    0, 0, 0.0, 0,
    0.0, 0,
    0, 0,
    0.0, 1.0,
    (SELECT COALESCE(a.Sight, 50.0) / 3.0 FROM Agents a WHERE a.IsPlayer = 1 LIMIT 1),
    0.0,
    96, 255, 160, 255,
    24, 140, 88, 255, 4
);

-- ================================
-- Player Body Assignments
-- ================================

INSERT INTO AgentBodies (AgentID, BodyPrototypeID, SlotIndex) VALUES (
    (SELECT ID FROM Agents WHERE IsPlayer = 1),
    (SELECT ID FROM BodyPrototypes WHERE Name = 'Normal'),
    0
);

INSERT INTO AgentBodies (AgentID, BodyPrototypeID, SlotIndex) VALUES (
    (SELECT ID FROM Agents WHERE IsPlayer = 1),
    (SELECT ID FROM BodyPrototypes WHERE Name = 'Tank'),
    1
);

INSERT INTO AgentBodies (AgentID, BodyPrototypeID, SlotIndex)
SELECT
    a.ID,
    bp.ID,
    0
FROM Agents a
INNER JOIN GameObjects g ON g.ID = a.ID
INNER JOIN BodyPrototypes bp ON bp.Name = 'ScoutSentryBody'
WHERE g.Name = 'ScoutSentry1'
  AND a.IsPlayer = 0
LIMIT 1;
