-- InitDB_StartData_Bodies.sql

-- ================================
-- Body Prototypes
-- ================================

-- Normal: the default body — balanced stats matching InitDB_StartData_Player.sql
INSERT INTO BodyPrototypes (
    Name,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, BodyActionBuff
) VALUES (
    'Normal',
    3.0,                -- Mass=3 → MaxHealth=100
    5, 5.0, 0,
    10, 3, 3.0, 0,
    62.5, 0,
    0, 0,
    1.0, 1.0, 0.0
);

-- Tank: slow, tanky body — higher mass, more collision damage, lower speed
INSERT INTO BodyPrototypes (
    Name,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, BodyActionBuff
) VALUES (
    'Tank',
    9.0,                -- Mass=9 → MaxHealth=300
    3, 8.0, 2,
    20, 2, 5.0, 1,
    125.0, 0,
    0.3, 0.2,
    0.55, 0.6, 0.0
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
