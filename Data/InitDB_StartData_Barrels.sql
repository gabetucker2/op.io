-- InitDB_StartData_Barrels.sql

-- ================================
-- Barrel Prototypes
-- ================================
-- All stats are explicit here for easy tinkering.
-- Set any stat to -1 to fall back to the matching PhysicsSettings default instead.
--
-- PhysicsSettings defaults for reference:
--   BulletDamage      = 10       BulletHealth      = 10
--   BulletSpeed       = 400      BulletMass        = 1
--   BulletDragFactor       = 800      BulletMaxLifespan = 3
--   BulletFill        RGBA(255,0,0,255)
--   BulletOutline     RGBA(139,0,0,255)  width = 2

INSERT INTO BarrelPrototypes (
    Name,
    BulletDamage, BulletPenetration, BulletSpeed, BulletDragFactor,
    ReloadSpeed,  BulletHealth,      BulletMaxLifespan, BulletMass,
    BulletFillR,    BulletFillG,    BulletFillB,    BulletFillA,
    BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA,
    BulletOutlineWidth
) VALUES (
    'Medium',
    4,  -- BulletDamage      (0 → 10)
    0,  -- BulletPenetration
    400,  -- BulletSpeed       (0 → 400)
    1600,  -- BulletDragFactor       (0 → 800)
    3,  -- ReloadSpeed
    10,  -- BulletHealth      (0 → 10)
    3,  -- BulletMaxLifespan (0 → 3)
    3,  -- BulletMass        (0 → 1)
    255, 0,   0, 255,   -- Fill    RGBA
    139, 0,   0, 255,   -- Outline RGBA
    0.5               -- OutlineWidth (0 → 2)
);

INSERT INTO BarrelPrototypes (
    Name,
    BulletDamage, BulletPenetration, BulletSpeed, BulletDragFactor,
    ReloadSpeed,  BulletHealth,      BulletMaxLifespan, BulletMass,
    BulletFillR,    BulletFillG,    BulletFillB,    BulletFillA,
    BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA,
    BulletOutlineWidth
) VALUES (
    'Heavy',
    15,  -- BulletDamage
    0,   -- BulletPenetration
    600, -- BulletSpeed
    800, -- BulletDragFactor
    1,   -- ReloadSpeed (shots/sec)
    10,  -- BulletHealth
    3,   -- BulletMaxLifespan
    6,   -- BulletMass        (radius = sqrt(6)*6 ≈ 14.7)
    200, 200, 200, 255,   -- Fill    RGBA
    150, 150, 150, 255,   -- Outline RGBA
    2              -- OutlineWidth
);

-- ================================
-- Player Barrel Assignments
-- ================================

INSERT INTO AgentBarrels (AgentID, BarrelPrototypeID, SlotIndex) VALUES (
    (SELECT ID FROM Agents WHERE IsPlayer = 1),
    (SELECT ID FROM BarrelPrototypes WHERE Name = 'Medium'),
    0
);

INSERT INTO AgentBarrels (AgentID, BarrelPrototypeID, SlotIndex) VALUES (
    (SELECT ID FROM Agents WHERE IsPlayer = 1),
    (SELECT ID FROM BarrelPrototypes WHERE Name = 'Heavy'),
    1
);
