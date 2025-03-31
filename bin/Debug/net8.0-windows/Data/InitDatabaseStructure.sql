-- InitDatabaseStructure.sql

-- Table for debug settings
CREATE TABLE IF NOT EXISTS DebugSettings (
    Setting TEXT PRIMARY KEY,
    Enabled INTEGER NOT NULL,
    MaxRepeats INTEGER NOT NULL
);

-- New table for debug visuals (specifically for debug circle color and radius)
CREATE TABLE IF NOT EXISTS DebugVisuals (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

-- Table for general game settings
CREATE TABLE IF NOT EXISTS GeneralSettings (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

-- Table for control keys
CREATE TABLE IF NOT EXISTS ControlKey (
    SettingKey TEXT PRIMARY KEY,
    InputKey TEXT NOT NULL,
    InputType TEXT NOT NULL,
    SwitchStartState BOOLEAN DEFAULT 0
);

-- Table for control settings
CREATE TABLE IF NOT EXISTS ControlSettings (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

-- Table for player data
CREATE TABLE IF NOT EXISTS Players (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT,
    Type TEXT,
    PositionX REAL,
    PositionY REAL,
    Speed REAL,
    Radius INTEGER,
    Width INTEGER,
    Height INTEGER,
    Sides INTEGER,
    FillR INTEGER, FillG INTEGER, FillB INTEGER, FillA INTEGER,
    OutlineR INTEGER, OutlineG INTEGER, OutlineB INTEGER, OutlineA INTEGER,
    OutlineWidth INTEGER,
    IsPlayer INTEGER,
    IsDestructible INTEGER,
    IsCollidable INTEGER,
    Mass REAL,
    StaticPhysics BOOLEAN
);

-- Table for static map objects
CREATE TABLE IF NOT EXISTS MapData (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT,
    Type TEXT,
    PositionX REAL,
    PositionY REAL,
    Width INTEGER,
    Height INTEGER,
    Sides INTEGER,
    FillR INTEGER, FillG INTEGER, FillB INTEGER, FillA INTEGER,
    OutlineR INTEGER, OutlineG INTEGER, OutlineB INTEGER, OutlineA INTEGER,
    OutlineWidth INTEGER,
    IsCollidable INTEGER,
    IsDestructible INTEGER,
    Mass REAL,
    IsPlayer INTEGER,
    StaticPhysics BOOLEAN
);

-- Table for farm prototype data
CREATE TABLE IF NOT EXISTS FarmData (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT,
    Type TEXT,
    PositionX REAL,
    PositionY REAL,
    Size INTEGER,
    Sides INTEGER,
    Width INTEGER,
    Height INTEGER,
    Weight REAL,
    Count INTEGER,
    FillR INTEGER, FillG INTEGER, FillB INTEGER, FillA INTEGER,
    OutlineR INTEGER, OutlineG INTEGER, OutlineB INTEGER, OutlineA INTEGER,
    OutlineWidth INTEGER,
    IsCollidable INTEGER,
    IsDestructible INTEGER,
    IsPlayer INTEGER,
    Mass REAL,
    StaticPhysics BOOLEAN
);
