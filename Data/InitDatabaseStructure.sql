-- Drop existing tables to start fresh
DROP TABLE IF EXISTS GeneralSettings;
DROP TABLE IF EXISTS DebugSettings;
DROP TABLE IF EXISTS Players;
DROP TABLE IF EXISTS GameObjects;
DROP TABLE IF EXISTS MapData;
DROP TABLE IF EXISTS FarmData;

-- Create GeneralSettings table (Stores global settings as key-value pairs)
CREATE TABLE GeneralSettings (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

-- Create DebugSettings table (Stores debugging configuration)
CREATE TABLE DebugSettings (
    Setting TEXT PRIMARY KEY,
    Enabled BOOLEAN NOT NULL DEFAULT 0,
    TextureDebugView BOOLEAN NOT NULL DEFAULT 0,
    MaxRepeats INTEGER NOT NULL DEFAULT 5
);

-- Create Players table (Stores player-related data)
CREATE TABLE Players (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL DEFAULT 'Circle',
    PositionX REAL NOT NULL DEFAULT 100,
    PositionY REAL NOT NULL DEFAULT 100,
    Speed REAL NOT NULL DEFAULT 500.0,
    Radius INTEGER NOT NULL DEFAULT 25,
    FillR INTEGER NOT NULL DEFAULT 0,
    FillG INTEGER NOT NULL DEFAULT 255,
    FillB INTEGER NOT NULL DEFAULT 255,
    FillA INTEGER NOT NULL DEFAULT 255,
    OutlineR INTEGER NOT NULL DEFAULT 0,
    OutlineG INTEGER NOT NULL DEFAULT 150,
    OutlineB INTEGER NOT NULL DEFAULT 150,
    OutlineA INTEGER NOT NULL DEFAULT 255,
    OutlineWidth INTEGER NOT NULL DEFAULT 4,
    IsPlayer BOOLEAN NOT NULL DEFAULT 1,
    IsDestructible BOOLEAN NOT NULL DEFAULT 0,
    IsCollidable BOOLEAN NOT NULL DEFAULT 1
);

-- Create GameObjects table (Stores all non-player objects)
CREATE TABLE GameObjects (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Type TEXT NOT NULL,
    PositionX REAL NOT NULL,
    PositionY REAL NOT NULL,
    Width INTEGER NOT NULL,
    Height INTEGER NOT NULL,
    FillR INTEGER NOT NULL,
    FillG INTEGER NOT NULL,
    FillB INTEGER NOT NULL,
    FillA INTEGER NOT NULL,
    OutlineR INTEGER NOT NULL,
    OutlineG INTEGER NOT NULL,
    OutlineB INTEGER NOT NULL,
    OutlineA INTEGER NOT NULL,
    OutlineWidth INTEGER NOT NULL,
    IsCollidable BOOLEAN NOT NULL,
    IsDestructible BOOLEAN NOT NULL
);

-- Create MapData table (Stores static map elements)
CREATE TABLE MapData (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL DEFAULT 'Rectangle',
    PositionX REAL NOT NULL,
    PositionY REAL NOT NULL,
    Width INTEGER NOT NULL,
    Height INTEGER NOT NULL,
    FillR INTEGER NOT NULL DEFAULT 255,
    FillG INTEGER NOT NULL DEFAULT 255,
    FillB INTEGER NOT NULL DEFAULT 255,
    FillA INTEGER NOT NULL DEFAULT 255,
    OutlineR INTEGER NOT NULL DEFAULT 0,
    OutlineG INTEGER NOT NULL DEFAULT 0,
    OutlineB INTEGER NOT NULL DEFAULT 0,
    OutlineA INTEGER NOT NULL DEFAULT 255,
    OutlineWidth INTEGER NOT NULL DEFAULT 4,
    IsCollidable BOOLEAN NOT NULL DEFAULT 1,
    IsDestructible BOOLEAN NOT NULL DEFAULT 0,
    Mass REAL NOT NULL DEFAULT 1.0,
    IsPlayer BOOLEAN NOT NULL DEFAULT 0,
);

-- Create FarmData table (Stores farm-related objects)
CREATE TABLE FarmData (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Type TEXT NOT NULL,
    PositionX REAL NOT NULL,
    PositionY REAL NOT NULL,
    Size INTEGER NOT NULL,
    Sides INTEGER NOT NULL DEFAULT 0,
    Weight INTEGER NOT NULL DEFAULT 0,
    Count INTEGER NOT NULL DEFAULT 1,
    FillR INTEGER NOT NULL,
    FillG INTEGER NOT NULL,
    FillB INTEGER NOT NULL,
    FillA INTEGER NOT NULL,
    OutlineR INTEGER NOT NULL,
    OutlineG INTEGER NOT NULL,
    OutlineB INTEGER NOT NULL,
    OutlineA INTEGER NOT NULL,
    OutlineWidth INTEGER NOT NULL,
    IsCollidable BOOLEAN NOT NULL,
    IsDestructible BOOLEAN NOT NULL
);
