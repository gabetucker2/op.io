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

-- Hidden attributes (BulletHealth, BulletRadius, BulletDrag) are computed from BulletMass via AttributeDerived.
-- Medium: mass=3 → health≈10, radius≈10.4px, drag≈0.90/s

INSERT INTO BarrelPrototypes (
    Name,
    BulletDamage, BulletPenetration, BulletSpeed,
    ReloadSpeed,  BulletMaxLifespan, BulletMass, BulletHealth,
    BulletFillR,    BulletFillG,    BulletFillB,    BulletFillA,
    BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA,
    BulletOutlineWidth
) VALUES (
    'Medium',
    4,   -- BulletDamage
    0,   -- BulletPenetration
    400, -- BulletSpeed
    3,   -- ReloadSpeed (shots/sec)
    3,   -- BulletMaxLifespan
    3,   -- BulletMass (→ radius≈10.4px)
    -1,  -- BulletHealth (-1 → derived from mass ≈ 10)
    80,  80,  80, 255,   -- Fill    RGBA (dark grey)
    50,  50,  50, 255,   -- Outline RGBA (darker grey)
    5                   -- OutlineWidth
);

-- Heavy: mass=6 → health≈20, radius≈14.7px, drag≈1.80/s

INSERT INTO BarrelPrototypes (
    Name,
    BulletDamage, BulletPenetration, BulletSpeed,
    ReloadSpeed,  BulletMaxLifespan, BulletMass, BulletHealth,
    BulletFillR,    BulletFillG,    BulletFillB,    BulletFillA,
    BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA,
    BulletOutlineWidth
) VALUES (
    'Heavy',
    15,  -- BulletDamage
    0,   -- BulletPenetration
    600, -- BulletSpeed
    1,   -- ReloadSpeed (shots/sec)
    3,   -- BulletMaxLifespan
    6,   -- BulletMass (→ radius≈14.7px)
    -1,  -- BulletHealth (-1 → derived from mass ≈ 20)
    80,  80,  80, 255,   -- Fill    RGBA (dark grey)
    50,  50,  50, 255,   -- Outline RGBA (darker grey)
    5                     -- OutlineWidth
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
