-- InitDatabase_Structure_GOs.sql

-- ================================
-- General GameObjects Table (Shared by All Types)
-- ================================

CREATE TABLE IF NOT EXISTS GameObjects (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Type TEXT,  -- This should match the GOTypes enum ('Player', 'Farm', 'None')
    Shape TEXT, -- This should match the Shape enum ('Circle', 'Rectangle', 'Polygon')
    Name TEXT,
    PositionX REAL,
    PositionY REAL,
    Width INTEGER,
    Height INTEGER,
    Sides INTEGER,
    Rotation REAL,
    FillR INTEGER, FillG INTEGER, FillB INTEGER, FillA INTEGER,
    OutlineR INTEGER, OutlineG INTEGER, OutlineB INTEGER, OutlineA INTEGER,
    OutlineWidth INTEGER,
    IsCollidable INTEGER,
    IsDestructible INTEGER,
    Mass REAL,
    StaticPhysics BOOLEAN
);

-- ================================
-- Agent Table
-- ================================

CREATE TABLE IF NOT EXISTS Agents (
    ID INTEGER PRIMARY KEY,
    IsPlayer BOOLEAN,
    TriggerCooldown REAL,
    SwitchCooldown REAL,
    BaseSpeed REAL,              -- Base movement speed, multiplied by input modifiers at runtime
    -- Body Attributes (Attributes_Body)
    MaxHealth REAL DEFAULT 100,
    HealthRegen REAL DEFAULT 0,
    HealthArmor REAL DEFAULT 0,
    MaxShield REAL DEFAULT 0,
    ShieldRegen REAL DEFAULT 0,
    ShieldArmor REAL DEFAULT 0,
    BodyPenetration REAL DEFAULT 0,
    BodyCollisionDamage REAL DEFAULT 0,
    BodyKnockback REAL DEFAULT 0,
    CollisionDamageResistance REAL DEFAULT 0,
    BulletDamageResistance REAL DEFAULT 0,
    Speed REAL DEFAULT 0,        -- Body speed stat (distinct from BaseSpeed)
    RotationSpeed REAL DEFAULT 0,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Farm Table
-- ================================

CREATE TABLE IF NOT EXISTS FarmData (
    ID INTEGER PRIMARY KEY,
    Count INTEGER,
    MaxHealth REAL DEFAULT 50,
    DeathPointReward REAL DEFAULT 0,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Map Data Table
-- ================================

CREATE TABLE IF NOT EXISTS MapData (
    ID INTEGER PRIMARY KEY,
    MaxHealth REAL DEFAULT 0,
    DeathPointReward REAL DEFAULT 0,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);
