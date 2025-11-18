-- InitDB_Structure_Settings.sql

CREATE TABLE IF NOT EXISTS DebugSettings (
    Setting TEXT PRIMARY KEY,
    ForceEnabled INTEGER NOT NULL,
    MaxRepeats INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS DebugVisuals (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS GeneralSettings (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ControlKey (
    SettingKey TEXT PRIMARY KEY,
    InputKey TEXT NOT NULL,
    InputType TEXT NOT NULL,
    SwitchStartState BOOLEAN DEFAULT 0,
    MetaControl INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS ControlSettings (
    SettingKey TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
