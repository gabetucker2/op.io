-- InitDatabase_Structure_GOs.sql

-- ================================
-- General GameObjects Table (Shared by All Types)
-- ================================

CREATE TABLE IF NOT EXISTS GameObjects (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
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
    StaticPhysics BOOLEAN,
    RotationSpeed REAL DEFAULT 0
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
    -- Body Attributes (Attributes_Body) — normal (stored) only; hidden attrs computed via AttributeDerived
    Mass REAL DEFAULT 1,
    -- Health group (MaxHealth is hidden — derived from Mass via AttributeDerived.MaxHealth)
    HealthRegen REAL DEFAULT 0,
    HealthRegenDelay REAL DEFAULT 0,
    HealthArmor REAL DEFAULT 0,
    -- Shield group
    MaxShield REAL DEFAULT 0,
    ShieldRegen REAL DEFAULT 0,
    ShieldRegenDelay REAL DEFAULT 0,
    ShieldArmor REAL DEFAULT 0,
    -- Combat group (BodyKnockback is hidden — derived from Mass)
    BodyCollisionDamage REAL DEFAULT 0,
    BodyPenetration REAL DEFAULT 0,
    -- Resistance group
    CollisionDamageResistance REAL DEFAULT 0,
    BulletDamageResistance REAL DEFAULT 0,
    -- Movement group (RotationSpeed/AccelSpeed are hidden — derived from Control)
    Speed REAL DEFAULT 1.0,      -- Movement speed multiplier
    Control REAL DEFAULT 1.0,    -- Controls rotation and acceleration responsiveness
    -- Vision group
    Sight REAL DEFAULT 0,        -- World-space sight radius used by fog-of-war (0 = inactive)
    -- Action buff
    BodyActionBuff REAL DEFAULT 0,  -- Multiplier applied during body actions (crouch, sprint, etc.)
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Destructibles Table
-- Holds all destructible attributes for any non-agent GameObject that can take damage.
-- Both map objects and farm prototypes reference this table.
-- Objects not in this table are indestructible (no health bars, no death).
-- ================================

CREATE TABLE IF NOT EXISTS Destructibles (
    ID INTEGER PRIMARY KEY,
    -- Health
    MaxHealth REAL DEFAULT 0,
    HealthRegen REAL DEFAULT 0,
    HealthRegenDelay REAL DEFAULT 5,
    HealthArmor REAL DEFAULT 0,
    -- Shield
    MaxShield REAL DEFAULT 0,
    ShieldRegen REAL DEFAULT 0,
    ShieldRegenDelay REAL DEFAULT 5,
    ShieldArmor REAL DEFAULT 0,
    -- Combat
    BodyPenetration REAL DEFAULT 0,
    BodyCollisionDamage REAL DEFAULT 0,
    CollisionDamageResistance REAL DEFAULT 0,
    BulletDamageResistance REAL DEFAULT 0,
    -- Reward
    DeathPointReward REAL DEFAULT 0,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Farm Table
-- Farm-specific spawn and animation data. Destructible attributes live in Destructibles.
-- ================================

CREATE TABLE IF NOT EXISTS FarmData (
    ID INTEGER PRIMARY KEY,
    Count INTEGER DEFAULT 1,
    RotationSpeed REAL DEFAULT 0,   -- Fallback jitter amplitude (rad/s); overridden by FloatAmplitude
    -- Manual placement: spawn at a fixed position instead of random generation
    IsManual INTEGER DEFAULT 0,
    ManualX REAL DEFAULT 0,
    ManualY REAL DEFAULT 0,
    -- FarmAttributes: back-and-forth sine-wave float animation
    FloatAmplitude REAL DEFAULT 0,
    FloatSpeed REAL DEFAULT 0,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Bar Configuration Table
-- ================================
-- Controls how health, shield, and XP bars are arranged.
-- BarRow: vertical stacking order (0 = closest to object).
-- PositionInRow: left-to-right order within a row.
-- SegmentCount: pts per segment tick; SegmentsEnabled: show ticks.
CREATE TABLE IF NOT EXISTS BarConfig (
    BarType         TEXT    PRIMARY KEY,
    BarRow          INTEGER NOT NULL DEFAULT 0,
    PositionInRow   INTEGER NOT NULL DEFAULT 0,
    SegmentCount    INTEGER NOT NULL DEFAULT 10,
    SegmentsEnabled INTEGER NOT NULL DEFAULT 1,
    IsHidden        INTEGER NOT NULL DEFAULT 0,
    VisibilityRelations TEXT NOT NULL DEFAULT '',
    ShowPercent     INTEGER NOT NULL DEFAULT 0,
    VisibilityFade  TEXT    NOT NULL DEFAULT '0.18'
);

INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade)
VALUES ('Shield', 0, 0, 10, 1, 0, 'Shield:BelowFull|Health:BelowFull', 0, '0.18');
INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade)
VALUES ('Health', 1, 0, 10, 1, 0, 'Health:Change|Shield:Empty', 0, '0.18');
INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade)
VALUES ('XP', 2, 0, 10, 0, 0, 'XP:Change', 0, '0.18');
INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade)
VALUES ('HealthRegen', 3, 0, 10, 1, 1, '', 0, '0.18');
INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade)
VALUES ('ShieldRegen', 3, 1, 10, 1, 1, '', 0, '0.18');

CREATE TABLE IF NOT EXISTS BarConfigGroupOverrides (
    GroupKey         TEXT    NOT NULL,
    BarType          TEXT    NOT NULL,
    BarRow           INTEGER NOT NULL DEFAULT 0,
    PositionInRow    INTEGER NOT NULL DEFAULT 0,
    SegmentCount     INTEGER NOT NULL DEFAULT 10,
    SegmentsEnabled  INTEGER NOT NULL DEFAULT 1,
    IsHidden         INTEGER NOT NULL DEFAULT 0,
    VisibilityRelations TEXT NOT NULL DEFAULT '',
    ShowPercent      INTEGER NOT NULL DEFAULT 0,
    VisibilityFade   TEXT    NOT NULL DEFAULT '0.18',
    PRIMARY KEY (GroupKey, BarType)
);
