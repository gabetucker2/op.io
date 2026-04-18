-- InitDB_Structure_Bodies.sql

-- ================================
-- Body Prototypes Table
-- ================================
-- Named body configurations that can be assigned to any number of agents.
-- All fields mirror Attributes_Body; hidden attrs computed via AttributeDerived.

CREATE TABLE IF NOT EXISTS BodyPrototypes (
    ID                        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                      TEXT    NOT NULL UNIQUE,
    -- Mass (MaxHealth, BodyKnockback are hidden — derived via AttributeDerived)
    Mass                      REAL    DEFAULT 3,
    -- Health group
    HealthRegen               REAL    DEFAULT 0,
    HealthRegenDelay          REAL    DEFAULT 0,
    HealthArmor               REAL    DEFAULT 0,
    -- Shield group
    MaxShield                 REAL    DEFAULT 0,
    ShieldRegen               REAL    DEFAULT 0,
    ShieldRegenDelay          REAL    DEFAULT 0,
    ShieldArmor               REAL    DEFAULT 0,
    -- Combat group
    BodyCollisionDamage       REAL    DEFAULT 0,
    BodyPenetration           REAL    DEFAULT 0,
    -- Resistance group
    CollisionDamageResistance REAL    DEFAULT 0,
    BulletDamageResistance    REAL    DEFAULT 0,
    -- Movement group (RotationSpeed, AccelSpeed are hidden — derived from Control)
    Speed                     REAL    DEFAULT 1.0,
    Control                   REAL    DEFAULT 1.0,
    -- Action buff
    BodyActionBuff            REAL    DEFAULT 0
);

-- ================================
-- Agent Body Assignments Table
-- ================================
-- Maps agents to their ordered list of body prototypes.
-- SlotIndex 0 is the initially active body; higher indices are standby.

CREATE TABLE IF NOT EXISTS AgentBodies (
    ID              INTEGER PRIMARY KEY AUTOINCREMENT,
    AgentID         INTEGER NOT NULL,
    BodyPrototypeID INTEGER NOT NULL,
    SlotIndex       INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (AgentID)         REFERENCES Agents(ID),
    FOREIGN KEY (BodyPrototypeID) REFERENCES BodyPrototypes(ID)
);
