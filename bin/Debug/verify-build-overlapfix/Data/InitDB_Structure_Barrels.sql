-- InitDB_Structure_Barrels.sql

-- ================================
-- Barrel Prototypes Table
-- ================================
-- Named barrel configurations that can be assigned to any number of agents.
-- All numeric fields default to 0, which instructs BulletManager to fall back
-- to the corresponding Default* entries in PhysicsSettings at fire time.
-- Color alpha = 0 is the sentinel for "use PhysicsSettings default colour".

CREATE TABLE IF NOT EXISTS BarrelPrototypes (
    ID                INTEGER PRIMARY KEY AUTOINCREMENT,
    Name              TEXT    NOT NULL UNIQUE,
    -- Normal (stored) attributes
    BulletDamage      REAL    DEFAULT -1,   -- -1 → DefaultBulletDamage
    BulletPenetration REAL    DEFAULT -1,
    BulletSpeed       REAL    DEFAULT -1,   -- -1 → DefaultBulletSpeed
    ReloadSpeed       REAL    DEFAULT -1,
    BulletMaxLifespan REAL    DEFAULT -1,   -- -1 → DefaultBulletLifespan
    BulletMass        REAL    DEFAULT -1,   -- -1 → DefaultBulletMass
    BulletHealth      REAL    DEFAULT -1,   -- -1 → derived from BulletMass via AttributeDerived
    -- Hidden (not stored): BulletRadius (from BulletMass),
    --                       BulletDrag (from BulletRadius), BulletHealthRegen (from BulletMass),
    --                       BulletHealthRegenDelay (from BulletMass), BulletHealthArmor (from BulletMass),
    --                       BulletCollisionDamageResistance (from BulletMass),
    --                       BulletDamageResistance (from BulletMass) — computed via AttributeDerived
    -- Bullet effectors — stored
    BulletControl          REAL DEFAULT 0,
    -- Visual
    BulletFillR       INTEGER DEFAULT -1,   -- RGBA 0,0,0,0 → DefaultBulletFill* settings
    BulletFillG       INTEGER DEFAULT -1,
    BulletFillB       INTEGER DEFAULT -1,
    BulletFillA       INTEGER DEFAULT -1,
    BulletOutlineR    INTEGER DEFAULT -1,   -- RGBA 0,0,0,0 → DefaultBulletOutline* settings
    BulletOutlineG    INTEGER DEFAULT -1,
    BulletOutlineB    INTEGER DEFAULT -1,
    BulletOutlineA    INTEGER DEFAULT -1,
    BulletOutlineWidth INTEGER DEFAULT -1   -- 0 → DefaultBulletOutlineWidth
);

-- ================================
-- Agent Barrel Assignments Table
-- ================================
-- Maps agents to their ordered list of barrel prototypes.
-- SlotIndex 0 is the initially active barrel; higher indices are standby.
-- An agent may have 0–N barrels; 0 causes BulletManager to use all defaults.

CREATE TABLE IF NOT EXISTS AgentBarrels (
    ID                INTEGER PRIMARY KEY AUTOINCREMENT,
    AgentID           INTEGER NOT NULL,
    BarrelPrototypeID INTEGER NOT NULL,
    SlotIndex         INTEGER NOT NULL DEFAULT -1,
    FOREIGN KEY (AgentID)           REFERENCES Agents(ID),
    FOREIGN KEY (BarrelPrototypeID) REFERENCES BarrelPrototypes(ID)
);
