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
    BaseSpeed REAL,  -- Base speed of agent, used to calculate effective speed in C#
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Farm Table
-- ================================

CREATE TABLE IF NOT EXISTS FarmData (
    ID INTEGER PRIMARY KEY,
    Count INTEGER,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);

-- ================================
-- Map Data Table
-- ================================

CREATE TABLE IF NOT EXISTS MapData (
    ID INTEGER PRIMARY KEY,
    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
);
