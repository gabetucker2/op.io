using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace op.io
{
    /// <summary>
    /// Migrations for the GameObjects-related tables (FarmData, MapData, etc.).
    /// Safe to call on every launch; each migration is idempotent.
    /// </summary>
    internal static class GameObjectMigrations
    {
        private static bool _applied;

        public static void EnsureApplied()
        {
            if (_applied) return;
            if (!System.IO.File.Exists(DatabaseConfig.DatabaseFilePath)) return;

            try
            {
                EnsureDestructiblesTable();   // Must run before legacy MapData/FarmData migrations
                EnsureFarmDataBodyAttributes();
                EnsureFarmDataDeathPointReward();
                EnsureMapDataDeathPointReward();
                EnsureMapDataMaxHealth();
                EnsureAgentsDestructible();
                EnsureAgentsDestructibleRows();
                EnsureBarrelPrototypesExist();
                EnsureBulletRangeRenamedToDragFactor();
                EnsurePlayerBarrelAssignments();
                EnsurePlayerStarterBarrelDamageBuff();
                EnsureAgentsRegenDelayColumns();
                EnsurePlayerMaxShield();
                EnsureHealthBarColors();
                EnsureHealthBarSegmentSize();
                EnsureAgentsMaxXP();
                EnsureBarConfigTable();
                EnsureBarConfigGroupOverridesTable();
                EnsureBarConfigIsHidden();
                EnsureBarConfigVisibilityRelations();
                EnsureBarConfigVisibilityFade();
                EnsureRegenBarConfigs();
                EnsureBarsVisibleSetting();
                EnsureBarsInPropertiesPanel();
                EnsureDefaultDockingSetupVisibilityDefaults();
                EnsureDockingSetupAuxiliaryBlocks();
                EnsureBarConfigShowPercent();
                EnsureBarConfigDefaultHidden();
                EnsureBarConfigDefaultRelations();
                EnsureShieldAboveHealth();
                EnsurePlainDefaultXpBar();
                EnsureBrightGreenDefaultXpBarColor();
                EnsureFarmBodyCollisionDamage();
                EnsureAgentsAccelerationDelay();
                EnsureAgentsRotationDelay();
                EnsureDropBodyKnockback();
                EnsureKnockbackMassScaleSetting();
                EnsureAgentsMass();
                EnsureRecoilMassScaleSetting();
                // Attribute hidden/normal framework migrations
                EnsureAgentsAgility();
                EnsureAgentsSpeedControl();
                EnsureAgentsBodyActionBuff();
                EnsureBodySightColumns();
                EnsureScoutSentryVisionUnit();
                EnsureDropAgentsHiddenColumns();
                EnsureDropBarrelHiddenColumns();
                EnsurePlayerBodyCollisionDamage();
                EnsureBlueLootFarmPrototype();
                EnsureFarmMassQuadrupled();
                EnsureBlueLootCollisionDamage();
                EnsureFarmBodyCollisionDamageQuintupled();
                EnsureDropGameObjectsTypeColumn();
                EnsureBarrelHeightScalarSetting();
                EnsureBulletKnockbackScalarSetting();
                EnsureBulletRecoilScalarSetting();
                EnsureBulletFarmKnockbackScalarSetting();
                EnsureBulletEffectorColumns();
                EnsureBarrelBulletHealthColumn();
                EnsureBodyRadiusScalarSetting();
                EnsureDeathFadeFxSettings();
                EnsureXPClumpFxSettings();
                EnsureXPClumpBackendTooltips();
                _applied = true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"GameObjectMigrations failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the Destructibles table if it doesn't exist and migrates existing data
        /// from legacy MapData and FarmData tables so older databases remain functional.
        /// </summary>
        private static void EnsureDestructiblesTable()
        {
            // Create the Destructibles table with all health/combat/reward columns.
            DatabaseQuery.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Destructibles (
                    ID INTEGER PRIMARY KEY,
                    MaxHealth REAL DEFAULT 0,
                    HealthRegen REAL DEFAULT 0,
                    HealthRegenDelay REAL DEFAULT 5,
                    HealthArmor REAL DEFAULT 0,
                    MaxShield REAL DEFAULT 0,
                    ShieldRegen REAL DEFAULT 0,
                    ShieldRegenDelay REAL DEFAULT 5,
                    ShieldArmor REAL DEFAULT 0,
                    BodyPenetration REAL DEFAULT 0,
                    BodyCollisionDamage REAL DEFAULT 0,
                    CollisionDamageResistance REAL DEFAULT 0,
                    BulletDamageResistance REAL DEFAULT 0,
                    DeathPointReward REAL DEFAULT 0,
                    FOREIGN KEY (ID) REFERENCES GameObjects(ID)
                );");

            // Migrate legacy MapData rows into Destructibles (backwards compat for old DBs).
            if (TableExists("MapData"))
            {
                DatabaseQuery.ExecuteNonQuery(@"
                    INSERT OR IGNORE INTO Destructibles (ID, MaxHealth, DeathPointReward)
                    SELECT ID, COALESCE(MaxHealth, 0), COALESCE(DeathPointReward, 0)
                    FROM MapData;");
                DebugLogger.PrintDatabase("EnsureDestructiblesTable: migrated MapData rows to Destructibles.");
            }

            // Migrate legacy FarmData health columns into Destructibles (backwards compat).
            if (TableExists("FarmData") && ColumnExists("FarmData", "MaxHealth"))
            {
                DatabaseQuery.ExecuteNonQuery(@"
                    INSERT OR IGNORE INTO Destructibles (
                        ID, MaxHealth, HealthRegen, HealthRegenDelay, HealthArmor,
                        MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
                        BodyPenetration, BodyCollisionDamage,
                        CollisionDamageResistance, BulletDamageResistance, DeathPointReward)
                    SELECT
                        ID,
                        COALESCE(MaxHealth, 0), COALESCE(HealthRegen, 0), COALESCE(HealthRegenDelay, 5), COALESCE(HealthArmor, 0),
                        COALESCE(MaxShield, 0), COALESCE(ShieldRegen, 0), COALESCE(ShieldRegenDelay, 5), COALESCE(ShieldArmor, 0),
                        COALESCE(BodyPenetration, 0), COALESCE(BodyCollisionDamage, 0),
                        COALESCE(CollisionDamageResistance, 0), COALESCE(BulletDamageResistance, 0),
                        COALESCE(DeathPointReward, 0)
                    FROM FarmData;");
                DebugLogger.PrintDatabase("EnsureDestructiblesTable: migrated FarmData health rows to Destructibles.");
            }

            // Ensure FarmData has the new slim columns for placement and animation.
            if (!ColumnExists("FarmData", "IsManual"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE FarmData ADD COLUMN IsManual INTEGER DEFAULT 0;");
            if (!ColumnExists("FarmData", "ManualX"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE FarmData ADD COLUMN ManualX REAL DEFAULT 0;");
            if (!ColumnExists("FarmData", "ManualY"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE FarmData ADD COLUMN ManualY REAL DEFAULT 0;");
            if (!ColumnExists("FarmData", "FloatAmplitude"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE FarmData ADD COLUMN FloatAmplitude REAL DEFAULT 0;");
            if (!ColumnExists("FarmData", "FloatSpeed"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE FarmData ADD COLUMN FloatSpeed REAL DEFAULT 0;");
            if (!ColumnExists("FarmData", "RotationSpeed"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE FarmData ADD COLUMN RotationSpeed REAL DEFAULT 0;");

            DebugLogger.PrintDatabase("EnsureDestructiblesTable complete.");
        }

        // Skipped: health/combat columns now live in Destructibles, not FarmData.
        // Gate on Destructibles existing to avoid re-adding stale columns to slim FarmData.
        private static void EnsureFarmDataBodyAttributes()
        {
            if (TableExists("Destructibles")) return;

            string[] columns =
            [
                "HealthRegen", "HealthRegenDelay", "HealthArmor",
                "MaxShield", "ShieldRegen", "ShieldRegenDelay", "ShieldArmor",
                "BodyPenetration", "BodyCollisionDamage",
                "CollisionDamageResistance", "BulletDamageResistance",
                "Speed", "RotationSpeed"
            ];
            foreach (string col in columns)
            {
                if (!ColumnExists("FarmData", col))
                {
                    DatabaseQuery.ExecuteNonQuery($"ALTER TABLE FarmData ADD COLUMN {col} REAL DEFAULT 0;");
                    DebugLogger.PrintDatabase($"Added {col} column to FarmData.");
                }
            }

            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE FarmData
                SET    HealthRegen      = MaxHealth * 0.05,
                       HealthRegenDelay = 6.0
                WHERE  HealthRegen      = 0
                AND    HealthRegenDelay = 0
                AND    MaxHealth        > 0;");
            DebugLogger.PrintDatabase("EnsureFarmDataBodyAttributes: backfilled HealthRegen/HealthRegenDelay for existing farms.");
        }

        private static void EnsureFarmDataDeathPointReward()
        {
            if (TableExists("Destructibles")) return;
            if (ColumnExists("FarmData", "DeathPointReward")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE FarmData ADD COLUMN DeathPointReward REAL DEFAULT 0;");
            DebugLogger.PrintDatabase("Added DeathPointReward column to FarmData.");
        }

        private static void EnsureMapDataDeathPointReward()
        {
            if (!TableExists("MapData")) return;
            if (ColumnExists("MapData", "DeathPointReward")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE MapData ADD COLUMN DeathPointReward REAL DEFAULT 0;");
            DebugLogger.PrintDatabase("Added DeathPointReward column to MapData.");
        }

        private static void EnsureMapDataMaxHealth()
        {
            if (!TableExists("MapData")) return;
            const string sql = @"
                UPDATE MapData
                SET    MaxHealth = 100
                WHERE  MaxHealth = 0
                AND    ID IN (SELECT ID FROM GameObjects WHERE IsDestructible = 1);";
            DatabaseQuery.ExecuteNonQuery(sql);
            DebugLogger.PrintDatabase("EnsureMapDataMaxHealth: backfilled MaxHealth=100 for destructible MapData rows with MaxHealth=0.");
        }

        private static void EnsureAgentsDestructible()
        {
            const string sql = @"
                UPDATE GameObjects
                SET    IsDestructible = 1
                WHERE  IsDestructible = 0
                AND    ID IN (SELECT ID FROM Agents);";
            DatabaseQuery.ExecuteNonQuery(sql);
            DebugLogger.PrintDatabase("EnsureAgentsDestructible: set IsDestructible=1 for all agent rows.");
        }

        private static void EnsureAgentsDestructibleRows()
        {
            if (!TableExists("Destructibles") || !TableExists("Agents") || !TableExists("GameObjects"))
            {
                return;
            }

            const string insertSql = @"
INSERT INTO Destructibles (ID, MaxHealth, DeathPointReward)
SELECT
    a.ID,
    MAX(1.0, (COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0))),
    CASE
        WHEN MAX(1.0, (COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0))) <= 30  THEN 2
        WHEN MAX(1.0, (COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0))) <= 60  THEN 4
        WHEN MAX(1.0, (COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0))) <= 120 THEN 7
        WHEN MAX(1.0, (COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0))) <= 250 THEN 12
        ELSE 16
    END
FROM Agents a
INNER JOIN GameObjects g ON g.ID = a.ID
WHERE NOT EXISTS (SELECT 1 FROM Destructibles d WHERE d.ID = a.ID);";

            const string backfillSql = @"
UPDATE Destructibles
SET MaxHealth = CASE
        WHEN COALESCE(MaxHealth, 0) > 0 THEN MaxHealth
        ELSE MAX(1.0, (
            SELECT COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0)
            FROM Agents a
            INNER JOIN GameObjects g ON g.ID = a.ID
            WHERE a.ID = Destructibles.ID
        ))
    END,
    DeathPointReward = CASE
        WHEN COALESCE(DeathPointReward, 0) > 0 THEN DeathPointReward
        ELSE (
            CASE
                WHEN MAX(1.0, (
                    SELECT COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0)
                    FROM Agents a
                    INNER JOIN GameObjects g ON g.ID = a.ID
                    WHERE a.ID = Destructibles.ID
                )) <= 30 THEN 2
                WHEN MAX(1.0, (
                    SELECT COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0)
                    FROM Agents a
                    INNER JOIN GameObjects g ON g.ID = a.ID
                    WHERE a.ID = Destructibles.ID
                )) <= 60 THEN 4
                WHEN MAX(1.0, (
                    SELECT COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0)
                    FROM Agents a
                    INNER JOIN GameObjects g ON g.ID = a.ID
                    WHERE a.ID = Destructibles.ID
                )) <= 120 THEN 7
                WHEN MAX(1.0, (
                    SELECT COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0)
                    FROM Agents a
                    INNER JOIN GameObjects g ON g.ID = a.ID
                    WHERE a.ID = Destructibles.ID
                )) <= 250 THEN 12
                ELSE 16
            END
        )
    END
WHERE ID IN (SELECT ID FROM Agents);";

            DatabaseQuery.ExecuteNonQuery(insertSql);
            DatabaseQuery.ExecuteNonQuery(backfillSql);
            DebugLogger.PrintDatabase("EnsureAgentsDestructibleRows: ensured agent destructible rows and non-zero drop rewards.");
        }

        private static void EnsureBarrelPrototypesExist()
        {
            // Ensure the tables exist (they may be missing in databases created before barrel support).
            DatabaseQuery.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS BarrelPrototypes (
                    ID                INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name              TEXT    NOT NULL UNIQUE,
                    BulletDamage      REAL    DEFAULT -1,
                    BulletPenetration REAL    DEFAULT -1,
                    BulletSpeed       REAL    DEFAULT -1,
                    ReloadSpeed       REAL    DEFAULT -1,
                    BulletMaxLifespan REAL    DEFAULT -1,
                    BulletMass        REAL    DEFAULT -1,
                    BulletFillR       INTEGER DEFAULT -1,
                    BulletFillG       INTEGER DEFAULT -1,
                    BulletFillB       INTEGER DEFAULT -1,
                    BulletFillA       INTEGER DEFAULT -1,
                    BulletOutlineR    INTEGER DEFAULT -1,
                    BulletOutlineG    INTEGER DEFAULT -1,
                    BulletOutlineB    INTEGER DEFAULT -1,
                    BulletOutlineA    INTEGER DEFAULT -1,
                    BulletOutlineWidth INTEGER DEFAULT -1
                );");

            DatabaseQuery.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS AgentBarrels (
                    ID                INTEGER PRIMARY KEY AUTOINCREMENT,
                    AgentID           INTEGER NOT NULL,
                    BarrelPrototypeID INTEGER NOT NULL,
                    SlotIndex         INTEGER NOT NULL DEFAULT -1,
                    FOREIGN KEY (AgentID)           REFERENCES Agents(ID),
                    FOREIGN KEY (BarrelPrototypeID) REFERENCES BarrelPrototypes(ID)
                );");

            // Seed the three canonical prototypes if missing (INSERT OR IGNORE).
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarrelPrototypes (Name, BulletMass) VALUES ('Default', -1);");
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarrelPrototypes (Name, BulletMass) VALUES ('Medium', 3);");
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarrelPrototypes (Name, BulletDamage, BulletPenetration, BulletSpeed, ReloadSpeed, BulletMaxLifespan, BulletMass, BulletFillR, BulletFillG, BulletFillB, BulletFillA, BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA, BulletOutlineWidth) VALUES ('Heavy', 10, 0, 400, 3, 3, 6, 255, 0, 0, 255, 139, 0, 0, 255, 2);");

            DebugLogger.PrintDatabase("EnsureBarrelPrototypesExist: Default/Medium/Heavy prototypes present.");
        }

        private static void EnsureBulletRangeRenamedToDragFactor()
        {
            if (ColumnExists("BarrelPrototypes", "BulletDragFactor")) return;
            if (ColumnExists("BarrelPrototypes", "BulletRange"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE BarrelPrototypes RENAME COLUMN BulletRange TO BulletDragFactor;");

            if (!SettingKeyExists("DefaultBulletDragFactor", "BulletDefaults") && SettingKeyExists("DefaultBulletRange", "BulletDefaults"))
                DatabaseQuery.ExecuteNonQuery("UPDATE BulletDefaults SET SettingKey = 'DefaultBulletDragFactor' WHERE SettingKey = 'DefaultBulletRange';");

            DebugLogger.PrintDatabase("EnsureBulletRangeRenamedToDragFactor: column and setting key migrated.");
        }

        private static void EnsureAgentsRegenDelayColumns()
        {
            if (!ColumnExists("Agents", "HealthRegenDelay"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN HealthRegenDelay REAL DEFAULT 0;");
                DebugLogger.PrintDatabase("Added HealthRegenDelay column to Agents.");
            }
            if (!ColumnExists("Agents", "ShieldRegenDelay"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN ShieldRegenDelay REAL DEFAULT 0;");
                DebugLogger.PrintDatabase("Added ShieldRegenDelay column to Agents.");
            }
        }

        private static void EnsurePlayerMaxShield()
        {
            // Backfill player with MaxShield=10, ShieldRegen=3, ShieldRegenDelay=3,
            // HealthRegen=5, HealthRegenDelay=5 if they are still at their zero defaults.
            const string sql = @"
                UPDATE Agents
                SET    MaxShield       = 10,
                       ShieldRegen     = 3,
                       ShieldRegenDelay = 3,
                       HealthRegen     = 5,
                       HealthRegenDelay = 5
                WHERE  IsPlayer = 1
                AND    MaxShield = 0
                AND    HealthRegen = 0;";
            DatabaseQuery.ExecuteNonQuery(sql);
            DebugLogger.PrintDatabase("EnsurePlayerMaxShield: backfilled player shield and regen stats.");
        }

        private static void EnsureHealthBarColors()
        {
            // Insert all color settings that may be missing from older databases.
            (string key, string value)[] colors =
            [
                ("HealthBarFillLowR",  "220"), ("HealthBarFillLowG",  "50"),  ("HealthBarFillLowB",  "50"),  ("HealthBarFillLowA",  "255"),
                ("HealthBarFillHighR", "60"),  ("HealthBarFillHighG", "200"), ("HealthBarFillHighB", "60"),  ("HealthBarFillHighA", "255"),
                ("HealthBarBgR",       "64"),  ("HealthBarBgG",       "64"),  ("HealthBarBgB",       "64"),  ("HealthBarBgA",       "255"),
                ("ShieldBarFillR",     "0"),   ("ShieldBarFillG",     "180"), ("ShieldBarFillB",     "255"), ("ShieldBarFillA",     "255"),
                ("PropBarHealthLowR",  "200"), ("PropBarHealthLowG",  "50"),  ("PropBarHealthLowB",  "50"),  ("PropBarHealthLowA",  "255"),
                ("PropBarHealthHighR", "50"),  ("PropBarHealthHighG", "200"), ("PropBarHealthHighB", "80"),  ("PropBarHealthHighA", "255"),
                ("PropBarEmptyR",      "35"),  ("PropBarEmptyG",      "35"),  ("PropBarEmptyB",      "35"),  ("PropBarEmptyA",      "210"),
            ];

            foreach ((string key, string value) in colors)
            {
                if (!SettingKeyExists(key, "BarSettings"))
                {
                    DatabaseQuery.ExecuteNonQuery(
                        $"INSERT INTO BarSettings (SettingKey, Value) VALUES ('{key}', '{value}');");
                }
            }
            DebugLogger.PrintDatabase("EnsureHealthBarColors: all color settings present.");
        }

        private static void EnsureHealthBarSegmentSize()
        {
            if (!SettingKeyExists("HealthBarSegmentSize", "BarSettings"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarSegmentSize', '10');");
                DebugLogger.PrintDatabase("EnsureHealthBarSegmentSize: inserted HealthBarSegmentSize = 10.");
            }
        }

        private static bool SettingKeyExists(string key, string tableName = "PhysicsSettings")
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery($"SELECT 1 FROM {tableName} WHERE SettingKey = @k;",
                    new System.Collections.Generic.Dictionary<string, object> { { "@k", key } });
                return rows.Count > 0;
            }
            catch { return false; }
        }

        private static void EnsurePlayerBarrelAssignments()
        {
            // Assign Medium (slot 0) and Heavy (slot 1) to the player if they have no barrels at all.
            const string countSql = @"
                SELECT COUNT(*) AS C FROM AgentBarrels
                WHERE AgentID = (SELECT ID FROM Agents WHERE IsPlayer = 1);";
            var rows = DatabaseQuery.ExecuteQuery(countSql);
            if (rows.Count > 0 && rows[0].TryGetValue("C", out object countObj) && Convert.ToInt32(countObj) > 0)
                return;

            DatabaseQuery.ExecuteNonQuery(@"
                INSERT INTO AgentBarrels (AgentID, BarrelPrototypeID, SlotIndex)
                SELECT a.ID, bp.ID, 0
                FROM Agents a, BarrelPrototypes bp
                WHERE a.IsPlayer = 1 AND bp.Name = 'Medium';");

            DatabaseQuery.ExecuteNonQuery(@"
                INSERT INTO AgentBarrels (AgentID, BarrelPrototypeID, SlotIndex)
                SELECT a.ID, bp.ID, 1
                FROM Agents a, BarrelPrototypes bp
                WHERE a.IsPlayer = 1 AND bp.Name = 'Heavy';");

            DebugLogger.PrintDatabase("EnsurePlayerBarrelAssignments: assigned Medium (slot 0) and Heavy (slot 1) to player.");
        }

        private static void EnsurePlayerStarterBarrelDamageBuff()
        {
            // Buff the player's starter barrels by +50% from legacy defaults:
            // Medium 4 -> 6 and Heavy 15 -> 22.5 (with support for older Heavy=10 -> 15 data).
            DatabaseQuery.ExecuteNonQuery(@"
UPDATE BarrelPrototypes
SET BulletDamage = CASE
    WHEN BulletDamage = 4 THEN 6
    WHEN BulletDamage = 10 THEN 15
    WHEN BulletDamage = 15 THEN 22.5
    ELSE BulletDamage
END
WHERE ID IN (
    SELECT bp.ID
    FROM BarrelPrototypes bp
    INNER JOIN AgentBarrels ab ON ab.BarrelPrototypeID = bp.ID
    INNER JOIN Agents a ON a.ID = ab.AgentID
    WHERE a.IsPlayer = 1
      AND ab.SlotIndex IN (0, 1)
)
AND BulletDamage IN (4, 10, 15);");

            DebugLogger.PrintDatabase("EnsurePlayerStarterBarrelDamageBuff: ensured player slot 0/1 barrel damage is buffed by +50% from legacy defaults.");
        }

        private static void EnsureAgentsMaxXP()
        {
            if (ColumnExists("Agents", "MaxXP")) return;
            DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN MaxXP REAL DEFAULT 0;");
            // Give the player a default MaxXP so the XP bar is visible when toggled on.
            DatabaseQuery.ExecuteNonQuery(
                "UPDATE Agents SET MaxXP = 100 WHERE IsPlayer = 1 AND (MaxXP IS NULL OR MaxXP = 0);");
            DebugLogger.PrintDatabase("EnsureAgentsMaxXP: added MaxXP column, set player MaxXP = 100.");
        }

        private static void EnsureBarConfigTable()
        {
            // Create BarConfig table if it doesn't exist and seed defaults.
            DatabaseQuery.ExecuteNonQuery(@"
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
                );");

            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade) " +
                "VALUES ('Shield', 0, 0, 10, 1, 0, 'Shield:BelowFull|Health:BelowFull', 0, '0.18');");
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade) " +
                "VALUES ('Health', 1, 0, 10, 1, 0, 'Shield:Empty', 0, '0.18');");
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade) " +
                "VALUES ('XP', 2, 0, 10, 0, 0, 'XP:Change', 0, '0.18');");

            DebugLogger.PrintDatabase("EnsureBarConfigTable: BarConfig seeded.");
        }

        private static void EnsureBarConfigGroupOverridesTable()
        {
            DatabaseQuery.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS BarConfigGroupOverrides (
                    GroupKey TEXT NOT NULL,
                    BarType TEXT NOT NULL,
                    BarRow INTEGER NOT NULL DEFAULT 0,
                    PositionInRow INTEGER NOT NULL DEFAULT 0,
                    SegmentCount INTEGER NOT NULL DEFAULT 10,
                    SegmentsEnabled INTEGER NOT NULL DEFAULT 1,
                    IsHidden INTEGER NOT NULL DEFAULT 0,
                    VisibilityRelations TEXT NOT NULL DEFAULT '',
                    ShowPercent INTEGER NOT NULL DEFAULT 0,
                    VisibilityFade TEXT NOT NULL DEFAULT '0.18',
                    PRIMARY KEY (GroupKey, BarType)
                );");
        }

        private static void EnsureRegenBarConfigs()
        {
            // Seed HealthRegen and ShieldRegen rows into BarConfig if missing.
            // They default to the hidden section and can be repositioned in BarsBlock.
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade) " +
                "VALUES ('HealthRegen', 3, 0, 10, 1, 1, '', 0, '0.18');");
            DatabaseQuery.ExecuteNonQuery(
                "INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade) " +
                "VALUES ('ShieldRegen', 3, 1, 10, 1, 1, '', 0, '0.18');");
            DebugLogger.PrintDatabase("EnsureRegenBarConfigs: HealthRegen/ShieldRegen rows present.");
        }

        private static void EnsureBarConfigIsHidden()
        {
            if (ColumnExists("BarConfig", "IsHidden")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE BarConfig ADD COLUMN IsHidden INTEGER NOT NULL DEFAULT 0;");
            DebugLogger.PrintDatabase("EnsureBarConfigIsHidden: added IsHidden column to BarConfig.");
        }

        private static void EnsureBarConfigVisibilityRelations()
        {
            if (ColumnExists("BarConfig", "VisibilityRelations")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE BarConfig ADD COLUMN VisibilityRelations TEXT NOT NULL DEFAULT '';");
            DebugLogger.PrintDatabase("EnsureBarConfigVisibilityRelations: added VisibilityRelations column to BarConfig.");
        }

        private static void EnsureBarConfigVisibilityFade()
        {
            if (ColumnExists("BarConfig", "VisibilityFade")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE BarConfig ADD COLUMN VisibilityFade TEXT NOT NULL DEFAULT '0.18';");
            DebugLogger.PrintDatabase("EnsureBarConfigVisibilityFade: added VisibilityFade column to BarConfig.");
        }

        private static void EnsureBarsVisibleSetting()
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery(
                    "SELECT 1 FROM GeneralSettings WHERE SettingKey = 'BarsVisible';");
                if (rows.Count == 0)
                {
                    DatabaseQuery.ExecuteNonQuery(
                        "INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BarsVisible', 'true');");
                    DebugLogger.PrintDatabase("EnsureBarsVisibleSetting: inserted BarsVisible = true.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureBarsVisibleSetting failed: {ex.Message}");
            }
        }

        private static void EnsureBarsInPropertiesPanel()
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery(
                    "SELECT RowData FROM BlockDockingSetups WHERE RowKey = 'Default';");
                if (rows.Count == 0) return;

                string json = rows[0]["RowData"]?.ToString() ?? "";
                if (json.Contains("\"bars\"")) return; // already present

                // Add "bars" to the properties panel blocks list
                bool updated = false;
                string oldBlocks = "\"blocks\": [\"properties\"]";
                string newBlocks = "\"blocks\": [\"properties\", \"bars\"]";
                if (json.Contains(oldBlocks))
                {
                    json    = json.Replace(oldBlocks, newBlocks);
                    updated = true;
                }

                // Add Bars entry to menu if missing
                if (!json.Contains("\"kind\": \"Bars\""))
                {
                    // Insert before the closing menu bracket — find last "}," in menu array
                    const string specsEntryVisible = "{ \"kind\": \"Specs\", \"mode\": \"Toggle\", \"count\": 0, \"visible\": true }";
                    const string specsEntryHidden = "{ \"kind\": \"Specs\", \"mode\": \"Toggle\", \"count\": 0, \"visible\": false }";
                    const string barsEntry  = ",\n    { \"kind\": \"Bars\", \"mode\": \"Toggle\", \"count\": 0, \"visible\": true }";
                    int idx = json.IndexOf(specsEntryVisible, System.StringComparison.Ordinal);
                    int markerLength = specsEntryVisible.Length;
                    if (idx < 0)
                    {
                        idx = json.IndexOf(specsEntryHidden, System.StringComparison.Ordinal);
                        markerLength = specsEntryHidden.Length;
                    }
                    if (idx >= 0)
                    {
                        json    = json.Insert(idx + markerLength, barsEntry);
                        updated = true;
                    }
                }

                if (!updated) return;

                DatabaseQuery.ExecuteNonQuery(
                    "UPDATE BlockDockingSetups SET RowData = @data WHERE RowKey = 'Default';",
                    new Dictionary<string, object> { ["@data"] = json });
                DebugLogger.PrintDatabase("EnsureBarsInPropertiesPanel: added Bars to Default docking setup.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureBarsInPropertiesPanel failed: {ex.Message}");
            }
        }

        private static void EnsureDefaultDockingSetupVisibilityDefaults()
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery(
                    "SELECT RowData FROM BlockDockingSetups WHERE RowKey = 'Default';");
                if (rows.Count == 0)
                {
                    return;
                }

                string payload = rows[0]["RowData"]?.ToString();
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return;
                }

                JsonNode root = JsonNode.Parse(payload);
                JsonArray menu = root?["menu"] as JsonArray;
                if (menu == null)
                {
                    return;
                }

                bool updated = false;
                updated |= UpsertMenuToggleVisibility(menu, "ColorScheme", false);
                updated |= UpsertMenuToggleVisibility(menu, "Notes", false);
                updated |= UpsertMenuToggleVisibility(menu, "ControlSetups", false);
                updated |= UpsertMenuToggleVisibility(menu, "DockingSetups", false);
                updated |= UpsertMenuToggleVisibility(menu, "Backend", false);
                updated |= UpsertMenuToggleVisibility(menu, "Specs", false);
                updated |= UpsertMenuToggleVisibility(menu, "DebugLogs", false);
                updated |= UpsertMenuToggleVisibility(menu, "Chat", false);
                updated |= UpsertMenuToggleVisibility(menu, "Performance", false);

                if (!updated)
                {
                    return;
                }

                string normalized = root.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                DatabaseQuery.ExecuteNonQuery(
                    "UPDATE BlockDockingSetups SET RowData = @data WHERE RowKey = 'Default';",
                    new Dictionary<string, object>
                    {
                        ["@data"] = normalized
                    });

                DebugLogger.PrintDatabase("EnsureDefaultDockingSetupVisibilityDefaults: normalized hidden-by-default panels in Default setup.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureDefaultDockingSetupVisibilityDefaults failed: {ex.Message}");
            }
        }

        private static bool UpsertMenuToggleVisibility(JsonArray menu, string kind, bool visible)
        {
            if (menu == null || string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            foreach (JsonNode node in menu)
            {
                if (node is not JsonObject entry)
                {
                    continue;
                }

                string entryKind = entry["kind"]?.ToString();
                if (!string.Equals(entryKind, kind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool changed = false;
                if (!string.Equals(entry["mode"]?.ToString(), "Toggle", StringComparison.OrdinalIgnoreCase))
                {
                    entry["mode"] = "Toggle";
                    changed = true;
                }

                if (!int.TryParse(entry["count"]?.ToString(), out int count) || count != 0)
                {
                    entry["count"] = 0;
                    changed = true;
                }

                bool parsedVisible = bool.TryParse(entry["visible"]?.ToString(), out bool currentVisible) && currentVisible;
                if (parsedVisible != visible)
                {
                    entry["visible"] = visible;
                    changed = true;
                }

                return changed;
            }

            menu.Add(new JsonObject
            {
                ["kind"] = kind,
                ["mode"] = "Toggle",
                ["count"] = 0,
                ["visible"] = visible
            });
            return true;
        }

        private static bool EnsureMenuToggleEntry(JsonArray menu, string kind, bool defaultVisible)
        {
            if (menu == null || string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            foreach (JsonNode node in menu)
            {
                if (node is not JsonObject entry)
                {
                    continue;
                }

                string entryKind = entry["kind"]?.ToString();
                if (!string.Equals(entryKind, kind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool changed = false;
                if (!string.Equals(entry["mode"]?.ToString(), "Toggle", StringComparison.OrdinalIgnoreCase))
                {
                    entry["mode"] = "Toggle";
                    changed = true;
                }

                if (!int.TryParse(entry["count"]?.ToString(), out int count) || count != 0)
                {
                    entry["count"] = 0;
                    changed = true;
                }

                if (!bool.TryParse(entry["visible"]?.ToString(), out _))
                {
                    entry["visible"] = defaultVisible;
                    changed = true;
                }

                return changed;
            }

            menu.Add(new JsonObject
            {
                ["kind"] = kind,
                ["mode"] = "Toggle",
                ["count"] = 0,
                ["visible"] = defaultVisible
            });
            return true;
        }

        private static void EnsureDockingSetupAuxiliaryBlocks()
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery(
                    "SELECT RowKey, RowData FROM BlockDockingSetups WHERE RowKey <> 'BlockLock' AND RowKey NOT LIKE '__%';");
                if (rows.Count == 0)
                {
                    return;
                }

                foreach (Dictionary<string, object> row in rows)
                {
                    string rowKey = row.TryGetValue("RowKey", out object keyValue) ? keyValue?.ToString() : null;
                    string payload = row.TryGetValue("RowData", out object dataValue) ? dataValue?.ToString() : null;
                    if (string.IsNullOrWhiteSpace(rowKey) || string.IsNullOrWhiteSpace(payload))
                    {
                        continue;
                    }

                    JsonNode root = JsonNode.Parse(payload);
                    if (root is not JsonObject rootObject)
                    {
                        continue;
                    }

                    JsonArray menu = rootObject["menu"] as JsonArray;
                    JsonArray panels = rootObject["panels"] as JsonArray;
                    JsonArray overlays = rootObject["overlays"] as JsonArray;
                    if (menu == null)
                    {
                        menu = new JsonArray();
                        rootObject["menu"] = menu;
                    }

                    if (panels == null)
                    {
                        panels = new JsonArray();
                        rootObject["panels"] = panels;
                    }

                    bool updated = false;
                    updated |= EnsureMenuToggleEntry(menu, "Bars", true);
                    updated |= UpsertMenuToggleVisibility(menu, "Ambience", true);
                    updated |= EnsureMenuToggleEntry(menu, "Interact", true);
                    updated |= UpsertMenuToggleVisibility(menu, "ControlSetups", false);
                    updated |= UpsertMenuToggleVisibility(menu, "DockingSetups", false);
                    updated |= UpsertMenuToggleVisibility(menu, "Notes", false);
                    updated |= UpsertMenuToggleVisibility(menu, "Specs", false);
                    updated |= UpsertMenuToggleVisibility(menu, "DebugLogs", false);
                    updated |= UpsertMenuToggleVisibility(menu, "Chat", false);
                    updated |= UpsertMenuToggleVisibility(menu, "Performance", false);
                    updated |= MoveBlockToPanel(panels, "properties", "bars", "properties");
                    updated |= MoveBlockToPanel(panels, "controls", "interact");
                    updated |= MoveBlockToPanel(panels, "controls", "ambience", "controls");
                    updated |= MoveBlockToPanel(panels, "controls", "controlsetups", "ambience");
                    updated |= MoveBlockToPanel(panels, "colors", "dockingsetups", "colors");
                    updated |= MoveBlockToPanel(panels, "backend", "notes", "backend");
                    updated |= MoveBlockToPanel(panels, "backend", "specs", "notes");
                    updated |= MoveBlockToPanel(panels, "backend", "debuglogs", "specs");
                    updated |= MoveBlockToPanel(panels, "backend", "chat", "debuglogs");
                    updated |= MoveBlockToPanel(panels, "backend", "performance", "chat");
                    if (string.Equals(rowKey, "Default", StringComparison.OrdinalIgnoreCase))
                    {
                        updated |= SetPanelActiveBlock(panels, "controls", "ambience");
                    }
                    updated |= RemoveBlocksFromOverlays(overlays,
                        "bars",
                        "interact",
                        "ambience",
                        "controlsetups",
                        "dockingsetups",
                        "notes",
                        "specs",
                        "debuglogs",
                        "chat",
                        "performance");

                    if (!updated)
                    {
                        continue;
                    }

                    string normalized = rootObject.ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    DatabaseQuery.ExecuteNonQuery(
                        "UPDATE BlockDockingSetups SET RowData = @data WHERE RowKey = @rowKey;",
                        new Dictionary<string, object>
                        {
                            ["@data"] = normalized,
                            ["@rowKey"] = rowKey
                        });
                }

                DebugLogger.PrintDatabase("EnsureDockingSetupAuxiliaryBlocks: normalized anchored support tabs, panel membership, and overlay exclusions.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureDockingSetupAuxiliaryBlocks failed: {ex.Message}");
            }
        }

        private static bool SetPanelActiveBlock(JsonArray panels, string panelId, string blockId)
        {
            if (panels == null || string.IsNullOrWhiteSpace(panelId) || string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            foreach (JsonNode node in panels)
            {
                if (node is not JsonObject panel)
                {
                    continue;
                }

                if (!string.Equals(panel["id"]?.ToString(), panelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                JsonArray blocks = panel["blocks"] as JsonArray;
                if (blocks == null)
                {
                    return false;
                }

                bool containsBlock = false;
                foreach (JsonNode blockNode in blocks)
                {
                    if (string.Equals(blockNode?.ToString(), blockId, StringComparison.OrdinalIgnoreCase))
                    {
                        containsBlock = true;
                        break;
                    }
                }
                if (!containsBlock)
                {
                    return false;
                }

                string currentActive = panel["active"]?.ToString();
                if (string.Equals(currentActive, blockId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                panel["active"] = blockId;
                return true;
            }

            return false;
        }

        private static bool MoveBlockToPanel(JsonArray panels, string panelId, string blockId, string insertAfterBlockId = null)
        {
            if (panels == null || string.IsNullOrWhiteSpace(panelId) || string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            JsonObject targetPanel = null;
            JsonArray targetBlocks = null;
            string containingPanelId = null;

            foreach (JsonNode node in panels)
            {
                if (node is not JsonObject panel)
                {
                    continue;
                }

                string existingPanelId = panel["id"]?.ToString();
                if (string.Equals(existingPanelId, panelId, StringComparison.OrdinalIgnoreCase))
                {
                    targetPanel = panel;
                    targetBlocks = panel["blocks"] as JsonArray;
                }

                JsonArray existingBlocks = panel["blocks"] as JsonArray;
                if (existingBlocks == null)
                {
                    continue;
                }

                foreach (JsonNode blockNode in existingBlocks)
                {
                    if (string.Equals(blockNode?.ToString(), blockId, StringComparison.OrdinalIgnoreCase))
                    {
                        containingPanelId = existingPanelId;
                        break;
                    }
                }

                if (containingPanelId != null && targetPanel != null)
                {
                    break;
                }
            }

            bool alreadyAnchored = string.Equals(containingPanelId, panelId, StringComparison.OrdinalIgnoreCase) &&
                IsBlockPositionedAfterAnchor(targetBlocks, blockId, insertAfterBlockId);
            if (alreadyAnchored)
            {
                return false;
            }

            bool changed = RemoveBlockFromPanels(panels, blockId);

            if (targetPanel == null)
            {
                targetBlocks = new JsonArray();
                targetPanel = new JsonObject
                {
                    ["id"] = panelId,
                    ["active"] = string.IsNullOrWhiteSpace(insertAfterBlockId) ? blockId : insertAfterBlockId,
                    ["blocks"] = targetBlocks
                };
                panels.Add(targetPanel);
                changed = true;
            }
            else if (targetBlocks == null)
            {
                targetBlocks = new JsonArray();
                targetPanel["blocks"] = targetBlocks;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(insertAfterBlockId) &&
                !BlocksContain(targetBlocks, insertAfterBlockId))
            {
                targetBlocks.Insert(0, insertAfterBlockId);
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(insertAfterBlockId))
            {
                targetBlocks.Add(blockId);
                return true;
            }

            for (int i = 0; i < targetBlocks.Count; i++)
            {
                if (!string.Equals(targetBlocks[i]?.ToString(), insertAfterBlockId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                targetBlocks.Insert(i + 1, blockId);
                return true;
            }

            targetBlocks.Add(blockId);
            return true;
        }

        private static bool RemoveBlockFromPanels(JsonArray panels, string blockId)
        {
            if (panels == null || string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            bool changed = false;
            foreach (JsonNode node in panels)
            {
                if (node is not JsonObject panel)
                {
                    continue;
                }

                JsonArray blocks = panel["blocks"] as JsonArray;
                if (blocks == null)
                {
                    continue;
                }

                for (int i = blocks.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(blocks[i]?.ToString(), blockId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    blocks.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool RemoveBlocksFromOverlays(JsonArray overlays, params string[] blockIds)
        {
            if (overlays == null || blockIds == null || blockIds.Length == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = overlays.Count - 1; i >= 0; i--)
            {
                if (overlays[i] is not JsonObject overlay)
                {
                    continue;
                }

                string overlayBlockId = overlay["blockId"]?.ToString();
                if (string.IsNullOrWhiteSpace(overlayBlockId))
                {
                    continue;
                }

                foreach (string blockId in blockIds)
                {
                    if (!string.Equals(overlayBlockId, blockId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    overlays.RemoveAt(i);
                    changed = true;
                    break;
                }
            }

            return changed;
        }

        private static bool IsBlockPositionedAfterAnchor(JsonArray blocks, string blockId, string anchorBlockId)
        {
            if (blocks == null || string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(anchorBlockId))
            {
                return BlocksContain(blocks, blockId);
            }

            for (int i = 0; i < blocks.Count - 1; i++)
            {
                if (!string.Equals(blocks[i]?.ToString(), anchorBlockId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return string.Equals(blocks[i + 1]?.ToString(), blockId, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool BlocksContain(JsonArray blocks, string blockId)
        {
            if (blocks == null || string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            foreach (JsonNode blockNode in blocks)
            {
                if (string.Equals(blockNode?.ToString(), blockId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureBarConfigShowPercent()
        {
            if (ColumnExists("BarConfig", "ShowPercent")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE BarConfig ADD COLUMN ShowPercent INTEGER NOT NULL DEFAULT 0;");
            DebugLogger.PrintDatabase("EnsureBarConfigShowPercent: added ShowPercent column to BarConfig.");
        }

        private static void EnsureBarConfigDefaultHidden()
        {
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".barconfig_defaults_v2_applied");
            if (System.IO.File.Exists(marker)) return;

            // XP, HealthRegen, ShieldRegen are hidden by default so the bar UI is clean out-of-the-box.
            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET IsHidden = 1 WHERE BarType IN ('XP', 'HealthRegen', 'ShieldRegen');");
            System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            DebugLogger.PrintDatabase("EnsureBarConfigDefaultHidden: hid XP, HealthRegen, ShieldRegen bars by default.");
        }

        private static void EnsureBarConfigDefaultRelations()
        {
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".barconfig_relations_v1_applied");
            if (System.IO.File.Exists(marker)) return;

            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE BarConfig
                SET VisibilityRelations = 'Shield:BelowFull|Health:BelowFull'
                WHERE BarType = 'Shield'
                  AND TRIM(COALESCE(VisibilityRelations, '')) = '';");
            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE BarConfig
                SET VisibilityRelations = 'Shield:Empty'
                WHERE BarType = 'Health'
                  AND TRIM(COALESCE(VisibilityRelations, '')) = '';");
            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE BarConfig
                SET VisibilityRelations = 'XP:Change'
                WHERE BarType = 'XP'
                  AND TRIM(COALESCE(VisibilityRelations, '')) = '';");

            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET IsHidden = 0 WHERE BarType = 'XP';");

            System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            DebugLogger.PrintDatabase("EnsureBarConfigDefaultRelations: seeded default Shield/Health/XP visibility relations and unhid XP.");
        }

        private static void EnsureShieldAboveHealth()
        {
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".barconfig_shield_above_health_applied");
            if (System.IO.File.Exists(marker)) return;

            // Split Health and Shield onto separate rows, with Shield above Health.
            // Renumber downstream rows to avoid collisions.
            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET BarRow = 3, PositionInRow = 1 WHERE BarType = 'ShieldRegen';");
            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET BarRow = 3, PositionInRow = 0 WHERE BarType = 'HealthRegen';");
            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET BarRow = 2, PositionInRow = 0 WHERE BarType = 'XP';");
            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET BarRow = 1, PositionInRow = 0 WHERE BarType = 'Health';");
            DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET BarRow = 0, PositionInRow = 0 WHERE BarType = 'Shield';");

            System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            DebugLogger.PrintDatabase("EnsureShieldAboveHealth: Shield row 0, Health row 1, XP row 2, regens row 3.");
        }

        private static void EnsurePlainDefaultXpBar()
        {
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".barconfig_xp_plain_v1_applied");
            if (System.IO.File.Exists(marker)) return;

            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE BarConfig
                SET SegmentsEnabled = 0
                WHERE BarType = 'XP'
                  AND BarRow = 2
                  AND PositionInRow = 0
                  AND SegmentCount = 10
                  AND COALESCE(SegmentsEnabled, 1) = 1
                  AND COALESCE(IsHidden, 0) = 0
                  AND COALESCE(ShowPercent, 0) = 0
                  AND TRIM(COALESCE(VisibilityRelations, '')) = 'XP:Change';");

            System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            DebugLogger.PrintDatabase("EnsurePlainDefaultXpBar: disabled segment ticks for default XP bars so they render as a plain fill.");
        }

        private static void EnsureBrightGreenDefaultXpBarColor()
        {
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".colorscheme_xpbar_default_v1_applied");
            if (System.IO.File.Exists(marker)) return;

            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE BlockColorScheme
                SET RowData = '#32FF50FF'
                WHERE RowKey IN ('XPBar', 'Scheme:DarkMode::XPBar', 'Scheme:LightMode::XPBar')
                  AND UPPER(TRIM(COALESCE(RowData, ''))) = '#32DC50FF';");

            System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            DebugLogger.PrintDatabase("EnsureBrightGreenDefaultXpBarColor: updated built-in XP bar defaults to #32FF50FF when still using the old seed color.");
        }

        private static void EnsureFarmBodyCollisionDamage()
        {
            // Backfill BodyCollisionDamage for farm prototypes that still have 0.
            // Values are proportional to the farm's health tier.
            // Only updates rows that are FarmData prototypes with BodyCollisionDamage = 0.
            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE Destructibles
                SET BodyCollisionDamage = CASE
                    WHEN MaxHealth <= 30  THEN 2
                    WHEN MaxHealth <= 60  THEN 4
                    WHEN MaxHealth <= 120 THEN 7
                    WHEN MaxHealth <= 250 THEN 12
                    ELSE 3
                END
                WHERE BodyCollisionDamage = 0
                AND ID IN (SELECT ID FROM FarmData);");
            DebugLogger.PrintDatabase("EnsureFarmBodyCollisionDamage: backfilled BodyCollisionDamage for farm prototypes.");
        }

        private static void EnsureAgentsAccelerationDelay()
        {
            if (!ColumnExists("Agents", "AccelerationDelay"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN AccelerationDelay REAL DEFAULT 0.2;");
                DatabaseQuery.ExecuteNonQuery("UPDATE Agents SET AccelerationDelay = 0.2 WHERE IsPlayer = 1;");
                DebugLogger.PrintDatabase("EnsureAgentsAccelerationDelay: added AccelerationDelay column, set player value = 0.2.");
            }
        }

        private static void EnsureAgentsRotationDelay()
        {
            if (!ColumnExists("Agents", "RotationDelay"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN RotationDelay REAL DEFAULT 0.15;");
                DatabaseQuery.ExecuteNonQuery("UPDATE Agents SET RotationDelay = 0.15 WHERE IsPlayer = 1;");
                DebugLogger.PrintDatabase("EnsureAgentsRotationDelay: added RotationDelay column, set player value = 0.15.");
            }
        }

        private static void EnsureDropBodyKnockback()
        {
            if (ColumnExists("Agents", "BodyKnockback"))
            {
                try
                {
                    DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents DROP COLUMN BodyKnockback;");
                    DebugLogger.PrintDatabase("EnsureDropBodyKnockback: dropped BodyKnockback from Agents.");
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"EnsureDropBodyKnockback: could not drop Agents.BodyKnockback: {ex.Message}");
                }
            }

            if (ColumnExists("Destructibles", "BodyKnockback"))
            {
                try
                {
                    DatabaseQuery.ExecuteNonQuery("ALTER TABLE Destructibles DROP COLUMN BodyKnockback;");
                    DebugLogger.PrintDatabase("EnsureDropBodyKnockback: dropped BodyKnockback from Destructibles.");
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"EnsureDropBodyKnockback: could not drop Destructibles.BodyKnockback: {ex.Message}");
                }
            }
        }

        private static void EnsureKnockbackMassScaleSetting()
        {
            if (!SettingKeyExists("KnockbackMassScale", "PhysicsSettings"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('KnockbackMassScale', '4.0');");
                DebugLogger.PrintDatabase("EnsureKnockbackMassScaleSetting: inserted KnockbackMassScale = 4.0.");
            }
        }

        private static void EnsureBarrelMassColumn()
        {
            if (ColumnExists("BarrelPrototypes", "BarrelMass")) return;
            DatabaseQuery.ExecuteNonQuery("ALTER TABLE BarrelPrototypes ADD COLUMN BarrelMass REAL DEFAULT -1;");
            DebugLogger.PrintDatabase("EnsureBarrelMassColumn: added BarrelMass to BarrelPrototypes.");
        }

        private static void EnsureRecoilMassScaleSetting()
        {
            if (!SettingKeyExists("RecoilMassScale", "PhysicsSettings"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('RecoilMassScale', '50.0');");
                DebugLogger.PrintDatabase("EnsureRecoilMassScaleSetting: inserted RecoilMassScale = 50.0.");
            }
        }

        private static void EnsureAgentsMass()
        {
            if (ColumnExists("Agents", "Mass")) return;
            DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN Mass REAL DEFAULT 1;");
            // Migrate existing mass values from GameObjects so agents keep their current physics mass.
            DatabaseQuery.ExecuteNonQuery(@"
                UPDATE Agents
                SET Mass = (SELECT Mass FROM GameObjects WHERE GameObjects.ID = Agents.ID)
                WHERE Mass IS NULL OR Mass = 1;");
            DebugLogger.PrintDatabase("EnsureAgentsMass: added Mass column to Agents, migrated from GameObjects.Mass.");
        }

        private static void EnsureAgentsAgility()
        {
            if (ColumnExists("Agents", "Agility")) return;
            DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN Agility REAL DEFAULT 1.0;");
            DebugLogger.PrintDatabase("EnsureAgentsAgility: added Agility column (default 1.0).");
        }

        /// <summary>
        /// Splits the former Agility column into Speed (movement multiplier) and
        /// Control (rotation/acceleration responsiveness). Copies Agility value into
        /// both new columns so existing agents preserve their tuned feel.
        /// </summary>
        private static void EnsureAgentsSpeedControl()
        {
            if (!ColumnExists("Agents", "Speed"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN Speed REAL DEFAULT 1.0;");
                if (ColumnExists("Agents", "Agility"))
                    DatabaseQuery.ExecuteNonQuery("UPDATE Agents SET Speed = COALESCE(Agility, 1.0);");
                DebugLogger.PrintDatabase("EnsureAgentsSpeedControl: added Speed column (migrated from Agility).");
            }
            if (!ColumnExists("Agents", "Control"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN Control REAL DEFAULT 1.0;");
                if (ColumnExists("Agents", "Agility"))
                    DatabaseQuery.ExecuteNonQuery("UPDATE Agents SET Control = COALESCE(Agility, 1.0);");
                DebugLogger.PrintDatabase("EnsureAgentsSpeedControl: added Control column (migrated from Agility).");
            }
        }

        private static void EnsureAgentsBodyActionBuff()
        {
            if (ColumnExists("Agents", "BodyActionBuff")) return;
            DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN BodyActionBuff REAL DEFAULT 0.0;");
            DebugLogger.PrintDatabase("EnsureAgentsBodyActionBuff: added BodyActionBuff column (default 0.0).");
        }

        /// <summary>
        /// Adds Sight to agent and body-prototype rows, then performs a one-time
        /// backfill so the player defaults to Sight=50 on databases created before
        /// the Sight attribute existed.
        /// </summary>
        private static void EnsureBodySightColumns()
        {
            if (!ColumnExists("Agents", "Sight"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE Agents ADD COLUMN Sight REAL DEFAULT 0.0;");
                DebugLogger.PrintDatabase("EnsureBodySightColumns: added Agents.Sight column (default 0.0).");
            }

            if (TableExists("BodyPrototypes") && !ColumnExists("BodyPrototypes", "Sight"))
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE BodyPrototypes ADD COLUMN Sight REAL DEFAULT 0.0;");
                DebugLogger.PrintDatabase("EnsureBodySightColumns: added BodyPrototypes.Sight column (default 0.0).");
            }

            // One-time backfill for legacy DBs so player vision is immediately usable.
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".body_sight_default_player_50_applied");
            if (System.IO.File.Exists(marker))
            {
                return;
            }

            try
            {
                DatabaseQuery.ExecuteNonQuery(@"
UPDATE Agents
SET Sight = 50.0
WHERE IsPlayer = 1
  AND COALESCE(Sight, 0.0) = 0.0;");

                if (TableExists("BodyPrototypes") && TableExists("AgentBodies"))
                {
                    DatabaseQuery.ExecuteNonQuery(@"
UPDATE BodyPrototypes
SET Sight = 50.0
WHERE ID IN (
    SELECT DISTINCT ab.BodyPrototypeID
    FROM AgentBodies ab
    INNER JOIN Agents a ON a.ID = ab.AgentID
    WHERE a.IsPlayer = 1
)
AND COALESCE(Sight, 0.0) = 0.0;");
                }

                System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
                DebugLogger.PrintDatabase("EnsureBodySightColumns: applied one-time player Sight backfill (50.0).");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureBodySightColumns failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Drops the former normal attributes from Agents that are now hidden (derived):
        /// MaxHealth (derived from Mass), AccelerationDelay and RotationDelay (derived from Control).
        /// </summary>
        private static void EnsureDropAgentsHiddenColumns()
        {
            foreach (string col in new[] { "MaxHealth", "AccelerationDelay", "RotationDelay" })
            {
                if (!ColumnExists("Agents", col)) continue;
                try
                {
                    DatabaseQuery.ExecuteNonQuery($"ALTER TABLE Agents DROP COLUMN {col};");
                    DebugLogger.PrintDatabase($"EnsureDropAgentsHiddenColumns: dropped Agents.{col} (now a hidden derived attribute).");
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"EnsureDropAgentsHiddenColumns: could not drop Agents.{col}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Drops the former normal attributes from BarrelPrototypes that are now hidden (derived):
        /// BulletHealth (derived from BulletMass) and BulletDragFactor (derived from BulletRadius).
        /// </summary>
        private static void EnsureDropBarrelHiddenColumns()
        {
            foreach (string col in new[] { "BulletHealth", "BulletDragFactor" })
            {
                if (!ColumnExists("BarrelPrototypes", col)) continue;
                try
                {
                    DatabaseQuery.ExecuteNonQuery($"ALTER TABLE BarrelPrototypes DROP COLUMN {col};");
                    DebugLogger.PrintDatabase($"EnsureDropBarrelHiddenColumns: dropped BarrelPrototypes.{col} (now a hidden derived attribute).");
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintWarning($"EnsureDropBarrelHiddenColumns: could not drop BarrelPrototypes.{col}: {ex.Message}");
                }
            }
        }

        private static void EnsureBlueLootFarmPrototype()
        {
            // Ensure the BlueLoot farm prototype (Rectangle, blue, manually placed) exists in the DB.
            // It may be absent on databases created before the prototype was added to the SQL.
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".blueloot_farm_prototype_applied");
            if (System.IO.File.Exists(marker)) return;
            try
            {
                // Check if the prototype already exists.
                var existing = DatabaseQuery.ExecuteQuery(
                    "SELECT g.ID FROM GameObjects g INNER JOIN FarmData f ON g.ID = f.ID WHERE g.Name = 'BlueLoot' LIMIT 1;");
                if (existing.Count == 0)
                {
                    DatabaseQuery.ExecuteNonQuery(@"
INSERT INTO GameObjects (
    Type, Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
) VALUES (
    'Prototype', 'Rectangle', 'BlueLoot',
    500, 500, 150, 150, 0, 0,
    50, 50, 200, 255,
    0, 0, 128, 255, 4,
    1, 1, 50, 0
);");
                    var newRow = DatabaseQuery.ExecuteQuery(
                        "SELECT g.ID FROM GameObjects g INNER JOIN FarmData f ON g.ID = f.ID WHERE g.Name = 'BlueLoot' LIMIT 1;");
                    if (newRow.Count > 0)
                    {
                        int newId = Convert.ToInt32(newRow[0]["ID"]);
                        DatabaseQuery.ExecuteNonQuery(@"
INSERT OR IGNORE INTO FarmData (ID, Count, RotationSpeed, IsManual, ManualX, ManualY, FloatAmplitude, FloatSpeed)
VALUES (@id, 1, 0.0, 1, 500, 500, 0.10, 0.125);",
                            new Dictionary<string, object> { ["@id"] = newId });
                        DatabaseQuery.ExecuteNonQuery(@"
INSERT OR IGNORE INTO Destructibles (ID, MaxHealth, HealthRegen, HealthRegenDelay, BodyCollisionDamage, DeathPointReward)
VALUES (@id, 400, 20.0, 6.0, 150, 200);",
                            new Dictionary<string, object> { ["@id"] = newId });
                        DebugLogger.PrintDatabase($"EnsureBlueLootFarmPrototype: inserted BlueLoot prototype (ID={newId}).");
                    }
                }
                System.IO.File.WriteAllText(marker, System.DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureBlueLootFarmPrototype failed: {ex.Message}");
            }
        }

        private static void EnsureFarmMassQuadrupled()
        {
            // Multiply the Mass of all farm prototypes (FarmData entries) by 4.
            // One-time migration gated by a marker file.
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".farm_mass_quadrupled_applied");
            if (System.IO.File.Exists(marker)) return;
            try
            {
                DatabaseQuery.ExecuteNonQuery(@"
UPDATE GameObjects
SET Mass = Mass * 4
WHERE ID IN (SELECT ID FROM FarmData);");
                System.IO.File.WriteAllText(marker, System.DateTime.UtcNow.ToString("O"));
                DebugLogger.PrintDatabase("EnsureFarmMassQuadrupled: multiplied farm prototype Mass × 4.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureFarmMassQuadrupled failed: {ex.Message}");
            }
        }

        private static void EnsureBlueLootCollisionDamage()
        {
            // BlueLoot (150×150) had BodyCollisionDamage=150, lower than every smaller farm object.
            // Correct it to 1000 so collision damage scales with size: Triangle(100) < Square(200) < Pentagon(350) < Octagon(600) < BlueLoot(1000).
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".blueloot_collision_damage_1000_applied");
            if (System.IO.File.Exists(marker)) return;
            try
            {
                DatabaseQuery.ExecuteNonQuery(@"
UPDATE Destructibles
SET BodyCollisionDamage = 1000
WHERE ID = (SELECT g.ID FROM GameObjects g INNER JOIN FarmData f ON g.ID = f.ID WHERE g.Name = 'BlueLoot' LIMIT 1)
  AND BodyCollisionDamage = 150;");
                System.IO.File.WriteAllText(marker, System.DateTime.UtcNow.ToString("O"));
                DebugLogger.PrintDatabase("EnsureBlueLootCollisionDamage: set BlueLoot BodyCollisionDamage to 1000.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureBlueLootCollisionDamage failed: {ex.Message}");
            }
        }

        private static void EnsurePlayerBodyCollisionDamage()
        {
            // Reduce player default BodyCollisionDamage from 500 to 62.5 (÷8).
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".player_body_collision_damage_62_5_applied");
            if (System.IO.File.Exists(marker)) return;
            try
            {
                const string sql = @"
UPDATE Agents
SET BodyCollisionDamage = 62.5
WHERE ID = (SELECT ID FROM GameObjects WHERE Name = 'Player1' LIMIT 1)
  AND BodyCollisionDamage = 500;";
                DatabaseQuery.ExecuteNonQuery(sql);
                System.IO.File.WriteAllText(marker, System.DateTime.UtcNow.ToString("O"));
                DebugLogger.PrintDatabase("EnsurePlayerBodyCollisionDamage: set player BodyCollisionDamage to 62.5.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsurePlayerBodyCollisionDamage failed: {ex.Message}");
            }
        }

        private static void EnsureScoutSentryVisionUnit()
        {
            const string scoutName = "ScoutSentry1";
            const string scoutBodyName = "ScoutSentryBody";
            const float scoutSpawnX = 420f;
            const float scoutSpawnY = 100f;

            try
            {
                Dictionary<string, object> parameters = new()
                {
                    ["@scoutName"] = scoutName,
                    ["@scoutBodyName"] = scoutBodyName,
                    ["@spawnX"] = scoutSpawnX,
                    ["@spawnY"] = scoutSpawnY
                };

                if (TableExists("BodyPrototypes"))
                {
                    DatabaseQuery.ExecuteNonQuery(@"
INSERT OR IGNORE INTO BodyPrototypes (
    Name,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, Sight, BodyActionBuff
)
SELECT
    @scoutBodyName,
    3.0,
    0.0, 0.0, 0.0,
    0.0, 0.0, 0.0, 0.0,
    0.0, 0.0,
    0.0, 0.0,
    0.0, 1.0,
    COALESCE((SELECT p.Sight / 3.0 FROM Agents p WHERE p.IsPlayer = 1 ORDER BY p.ID LIMIT 1), 16.6666667),
    0.0;",
                        parameters);

                    DatabaseQuery.ExecuteNonQuery(@"
UPDATE BodyPrototypes
SET Sight = COALESCE((SELECT p.Sight / 3.0 FROM Agents p WHERE p.IsPlayer = 1 ORDER BY p.ID LIMIT 1), Sight)
WHERE Name = @scoutBodyName;",
                        parameters);
                }

                DatabaseQuery.ExecuteNonQuery(@"
INSERT INTO GameObjects (
    Shape, Name,
    PositionX, PositionY, Width, Height, Sides, Rotation,
    FillR, FillG, FillB, FillA,
    OutlineR, OutlineG, OutlineB, OutlineA, OutlineWidth,
    IsCollidable, IsDestructible, Mass, StaticPhysics
)
SELECT
    'Circle', @scoutName,
    @spawnX, @spawnY, 50, 50, 0, 0,
    250, 220, 70, 255,
    120, 95, 30, 255, 4,
    1, 1, 3.0, 0
WHERE NOT EXISTS (
    SELECT 1
    FROM GameObjects g
    INNER JOIN Agents a ON a.ID = g.ID
    WHERE g.Name = @scoutName
);",
                    parameters);

                DatabaseQuery.ExecuteNonQuery(@"
INSERT INTO Agents (
    ID, IsPlayer, TriggerCooldown, SwitchCooldown, BaseSpeed,
    Mass,
    HealthRegen, HealthRegenDelay, HealthArmor,
    MaxShield, ShieldRegen, ShieldRegenDelay, ShieldArmor,
    BodyCollisionDamage, BodyPenetration,
    CollisionDamageResistance, BulletDamageResistance,
    Speed, Control, Sight, BodyActionBuff
)
SELECT
    g.ID, 0, 0.0, 0.0, 0.0,
    3.0,
    0.0, 0.0, 0.0,
    0.0, 0.0, 0.0, 0.0,
    0.0, 0.0,
    0.0, 0.0,
    0.0, 1.0,
    COALESCE((SELECT p.Sight / 3.0 FROM Agents p WHERE p.IsPlayer = 1 ORDER BY p.ID LIMIT 1), 16.6666667),
    0.0
FROM GameObjects g
WHERE g.Name = @scoutName
  AND NOT EXISTS (SELECT 1 FROM Agents a WHERE a.ID = g.ID)
ORDER BY g.ID
LIMIT 1;",
                    parameters);

                if (TableExists("Destructibles"))
                {
                    DatabaseQuery.ExecuteNonQuery(@"
INSERT INTO Destructibles (ID, MaxHealth, DeathPointReward)
SELECT
    g.ID,
    MAX(1.0, (COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0))),
    7.0
FROM GameObjects g
INNER JOIN Agents a ON a.ID = g.ID
WHERE g.Name = @scoutName
  AND NOT EXISTS (SELECT 1 FROM Destructibles d WHERE d.ID = g.ID)
ORDER BY g.ID
LIMIT 1;",
                        parameters);

                    DatabaseQuery.ExecuteNonQuery(@"
UPDATE Destructibles
SET MaxHealth = CASE
        WHEN COALESCE(MaxHealth, 0) > 0 THEN MaxHealth
        ELSE MAX(1.0, (
            SELECT COALESCE(NULLIF(a.Mass, 0), NULLIF(g.Mass, 0), 3.0) * (100.0 / 3.0)
            FROM Agents a
            INNER JOIN GameObjects g ON g.ID = a.ID
            WHERE g.Name = @scoutName
            ORDER BY g.ID
            LIMIT 1
        ))
    END,
    DeathPointReward = CASE
        WHEN COALESCE(DeathPointReward, 0) > 0 THEN DeathPointReward
        ELSE 7.0
    END
WHERE ID IN (
    SELECT g.ID
    FROM GameObjects g
    INNER JOIN Agents a ON a.ID = g.ID
    WHERE g.Name = @scoutName
);",
                        parameters);
                }

                DatabaseQuery.ExecuteNonQuery(@"
UPDATE GameObjects
SET IsCollidable = 1,
    StaticPhysics = 0
WHERE ID IN (
    SELECT g.ID
    FROM GameObjects g
    INNER JOIN Agents a ON a.ID = g.ID
    WHERE g.Name = @scoutName
);",
                    parameters);

                // Move legacy default scout placements into the current in-map spawn so
                // the second unit is always visible and contributes vision from startup.
                DatabaseQuery.ExecuteNonQuery(@"
UPDATE GameObjects
SET PositionX = @spawnX,
    PositionY = @spawnY
WHERE Name = @scoutName
  AND (
      (ABS(PositionX - 960) < 0.01 AND ABS(PositionY - 180) < 0.01)
      OR
      (ABS(PositionX - 1700) < 0.01 AND ABS(PositionY - 100) < 0.01)
  );",
                    parameters);

                DatabaseQuery.ExecuteNonQuery(@"
UPDATE Agents
SET BaseSpeed = 0.0,
    Speed = 0.0,
    Control = CASE WHEN COALESCE(Control, 0.0) <= 0.0 THEN 1.0 ELSE Control END,
    Sight = COALESCE((SELECT p.Sight / 3.0 FROM Agents p WHERE p.IsPlayer = 1 ORDER BY p.ID LIMIT 1), Sight)
WHERE ID IN (
    SELECT g.ID
    FROM GameObjects g
    INNER JOIN Agents a ON a.ID = g.ID
    WHERE g.Name = @scoutName
);",
                    parameters);

                if (TableExists("AgentBodies") && TableExists("BodyPrototypes"))
                {
                    DatabaseQuery.ExecuteNonQuery(@"
INSERT INTO AgentBodies (AgentID, BodyPrototypeID, SlotIndex)
SELECT
    a.ID,
    bp.ID,
    0
FROM Agents a
INNER JOIN GameObjects g ON g.ID = a.ID
INNER JOIN BodyPrototypes bp ON bp.Name = @scoutBodyName
WHERE g.Name = @scoutName
  AND a.IsPlayer = 0
  AND NOT EXISTS (
      SELECT 1
      FROM AgentBodies ab
      WHERE ab.AgentID = a.ID
        AND ab.BodyPrototypeID = bp.ID
        AND ab.SlotIndex = 0
  );",
                        parameters);
                }

                DebugLogger.PrintDatabase("EnsureScoutSentryVisionUnit: ensured ScoutSentry1 and ScoutSentryBody are present.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureScoutSentryVisionUnit failed: {ex.Message}");
            }
        }

        private static void EnsureFarmBodyCollisionDamageQuintupled()
        {
            // One-time global balance pass: all farm collision damage x5.
            // Compatibility:
            // - if legacy x4 migration already applied, only apply x1.25 so total becomes x5.
            // - otherwise apply x5 directly.
            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".farm_body_collision_damage_quintupled_v1_applied");
            if (System.IO.File.Exists(marker))
            {
                return;
            }

            string legacyQuadMarker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".farm_body_collision_damage_quadrupled_v1_applied");
            try
            {
                bool hasLegacyQuad = System.IO.File.Exists(legacyQuadMarker);
                string sql = hasLegacyQuad
                    ? @"
UPDATE Destructibles
SET BodyCollisionDamage = BodyCollisionDamage * 1.25
WHERE ID IN (SELECT ID FROM FarmData);"
                    : @"
UPDATE Destructibles
SET BodyCollisionDamage = BodyCollisionDamage * 5
WHERE ID IN (SELECT ID FROM FarmData);";
                DatabaseQuery.ExecuteNonQuery(sql);

                System.IO.File.WriteAllText(marker, System.DateTime.UtcNow.ToString("O"));
                DebugLogger.PrintDatabase(hasLegacyQuad
                    ? "EnsureFarmBodyCollisionDamageQuintupled: upgraded legacy farm BodyCollisionDamage from x4 to x5."
                    : "EnsureFarmBodyCollisionDamageQuintupled: multiplied farm BodyCollisionDamage x5.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureFarmBodyCollisionDamageQuintupled failed: {ex.Message}");
            }
        }

        private static void EnsureDropGameObjectsTypeColumn()
        {
            if (!ColumnExists("GameObjects", "Type")) return;
            try
            {
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE GameObjects DROP COLUMN Type;");
                DebugLogger.PrintDatabase("EnsureDropGameObjectsTypeColumn: dropped Type column from GameObjects.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureDropGameObjectsTypeColumn failed: {ex.Message}");
            }
        }

        private static void EnsureBarrelHeightScalarSetting()
        {
            if (!SettingKeyExists("BarrelHeightScalar", "BulletPhysics"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('BarrelHeightScalar', '0.075');");
                DebugLogger.PrintDatabase("EnsureBarrelHeightScalarSetting: inserted BarrelHeightScalar = 0.075.");
            }
        }

        private static void EnsureBulletKnockbackScalarSetting()
        {
            if (!SettingKeyExists("BulletKnockbackScalar", "BulletPhysics"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('BulletKnockbackScalar', '0.5');");
                DebugLogger.PrintDatabase("EnsureBulletKnockbackScalarSetting: inserted BulletKnockbackScalar = 0.5.");
            }
        }

        private static void EnsureBulletRecoilScalarSetting()
        {
            const string key = "BulletRecoilScalar";
            const float updatedDefault = 0.3125f;

            if (!SettingKeyExists(key, "BulletPhysics"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('BulletRecoilScalar', '0.3125');");
                DebugLogger.PrintDatabase("EnsureBulletRecoilScalarSetting: inserted BulletRecoilScalar = 0.3125.");
                return;
            }

            string marker = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".bullet_recoil_scalar_halved_v1_applied");
            if (System.IO.File.Exists(marker))
            {
                return;
            }

            try
            {
                float current = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", key);
                if (MathF.Abs(current - 0.625f) <= 0.0001f)
                {
                    DatabaseQuery.ExecuteNonQuery(
                        "UPDATE BulletPhysics SET Value = '0.3125' WHERE SettingKey = 'BulletRecoilScalar';");
                    DebugLogger.PrintDatabase("EnsureBulletRecoilScalarSetting: halved BulletRecoilScalar from 0.625 to 0.3125.");
                }
                else
                {
                    DebugLogger.PrintDatabase($"EnsureBulletRecoilScalarSetting: skipped update (current={current:0.#######}, target={updatedDefault:0.#######}).");
                }

                System.IO.File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"EnsureBulletRecoilScalarSetting failed: {ex.Message}");
            }
        }

        private static void EnsureBulletFarmKnockbackScalarSetting()
        {
            if (!SettingKeyExists("BulletFarmKnockbackScalar", "BulletPhysics"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('BulletFarmKnockbackScalar', '0.15');");
                DebugLogger.PrintDatabase("EnsureBulletFarmKnockbackScalarSetting: inserted BulletFarmKnockbackScalar = 0.15.");
            }
        }

        private static void EnsureBulletEffectorColumns()
        {
            string[] columns =
            {
                "BulletControl"
            };
            foreach (string col in columns)
            {
                if (!ColumnExists("BarrelPrototypes", col))
                {
                    DatabaseQuery.ExecuteNonQuery($"ALTER TABLE BarrelPrototypes ADD COLUMN {col} REAL DEFAULT 0;");
                    DebugLogger.PrintDatabase($"EnsureBulletEffectorColumns: added {col} to BarrelPrototypes.");
                }
            }
        }

        private static void EnsureBarrelBulletHealthColumn()
        {
            if (!TableExists("BarrelPrototypes")) return;
            if (ColumnExists("BarrelPrototypes", "BulletHealth")) return;
            DatabaseQuery.ExecuteNonQuery("ALTER TABLE BarrelPrototypes ADD COLUMN BulletHealth REAL DEFAULT -1;");
            DebugLogger.Print("Migration: added BulletHealth column to BarrelPrototypes.");
        }

        private static void EnsureBodyRadiusScalarSetting()
        {
            if (!SettingKeyExists("BodyRadiusScalar", "PhysicsSettings"))
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('BodyRadiusScalar', '14.43');");
                DebugLogger.PrintDatabase("EnsureBodyRadiusScalarSetting: inserted BodyRadiusScalar = 14.43.");
            }
        }

        private static void EnsureDeathFadeFxSettings()
        {
            (string key, string value)[] settings =
            [
                ("DeathFadeScaleMultiplier", "1.5"),
                ("DeathFadeSpinMinDegPerSecond", "90"),
                ("DeathFadeSpinMaxDegPerSecond", "240"),
            ];

            foreach ((string key, string value) in settings)
            {
                if (SettingKeyExists(key, "FXSettings"))
                {
                    continue;
                }

                DatabaseQuery.ExecuteNonQuery(
                    $"INSERT INTO FXSettings (SettingKey, Value) VALUES ('{key}', '{value}');");
                DebugLogger.PrintDatabase($"EnsureDeathFadeFxSettings: inserted {key} = {value}.");
            }
        }

        private static void EnsureXPClumpFxSettings()
        {
            (string key, string value)[] settings =
            [
                ("XPClumpPickupPerSecond", "12"),
                ("XPClumpDeadZoneRadius", "240"),
                ("XPClumpPullZoneRadius", "240"),
                ("XPClumpAbsorbZoneRadius", "85"),
                ("XPClumpDeadZoneStartSeconds", "20"),
                ("XPClumpDeadZoneDespawnSeconds", "10"),
                ("XPClumpPullSpeedMin", "0"),
                ("XPClumpPullSpeedMax", "250"),
                ("XPClumpPullVelocityLerpPerSecond", "4"),
                ("XPClumpRadius", "6"),
                ("XPClumpSpawnSpreadRadius", "18"),
                ("XPClumpSpawnInitialSpeed", "24"),
                ("XPClumpMaxSpeed", "320"),
                ("XPClumpVelocityDampingPerSecond", "1.8"),
                ("XPClumpClusterRadius", "110"),
                ("XPClumpClusterAttractForce", "45"),
                ("XPClumpClusterHomeostasisDistance", "8"),
                ("XPClumpClusterRepelForce", "95"),
                ("XPClumpClusterHomeostasisVariance", "0.28"),
                ("XPClumpClusterInstabilityForce", "9.5"),
                ("XPClumpClusterInstabilityPulseHz", "1.4"),
                ("XPClumpVisualMergeRadius", "24"),
                ("XPClumpVisualMergeGrowth", "0.19"),
                ("XPClumpVisualMergeMaxScale", "4.5"),
                ("XPClumpVisualMergeScaleLerpPerSecond", "8"),
                ("XPClumpAbsorbConsumeDistance", "10"),
                ("XPClumpAbsorbFadeSeconds", "0.42"),
                ("XPClumpAbsorbConsumeGrowMaxScale", "2.6"),
                ("XPClumpConsumeGrowthExponent", "2.2"),
                ("XPClumpAbsorbOrbitMaxAngularSpeedDeg", "170"),
                ("XPClumpAbsorbOrbitAngularBlendPerSecond", "3.2"),
                ("XPClumpAbsorbOrbitAngularDampingPerSecond", "3.6"),
                ("XPClumpAbsorbOrbitCollapseSpeed", "72"),
                ("XPClumpAbsorbConsumeCollapseSpeed", "120"),
                ("XPClumpAbsorbOrbitFollowGain", "6.2"),
                ("XPClumpAbsorbVelocityLerpPerSecond", "8"),
                ("XPClumpAbsorbOrbitTangentialVelocityWeight", "0.35"),
                ("XPClumpAbsorbOrbitInitialRadiusFactor", "0.35"),
                ("XPClumpAbsorbOrbitMinRadiusFactor", "0.05"),
                ("XPClumpAbsorbOrbitMinRadiusAbsolute", "0.35"),
                ("XPClumpAbsorbOrbitCollapseBoostLow", "1.15"),
                ("XPClumpAbsorbOrbitCollapseBoostHigh", "1.0"),
                ("XPClumpAbsorbOrbitInwardCollapseFactor", "0.12"),
                ("XPClumpAbsorbOrbitMaxInwardSpeedMin", "40"),
                ("XPClumpAbsorbOrbitMaxInwardSpeedPullScale", "1.5"),
                ("XPClumpAbsorbConsumeExtraInwardFactor", "0.35"),
                ("XPClumpCoreHighlightScale", "0.45"),
                ("XPClumpCoreHighlightAlphaScale", "0.24"),
                ("XPClumpGlowScale", "1.9"),
                ("XPClumpGlowAlphaScale", "0.58"),
                ("XPClumpShadowScale", "1.55"),
                ("XPClumpShadowAlphaScale", "0.4"),
                ("XPClumpDeadPulseSpeedMin", "3.8"),
                ("XPClumpDeadPulseSpeedMax", "12.0"),
                ("XPClumpDeadPulseLowAlphaStart", "0.55"),
                ("XPClumpDeadPulseLowAlphaEnd", "0.03"),
                ("XPDropUnstableHealthThresholdRatio", "0.3"),
                ("XPDropUnstableJitterAccel", "1500"),
                ("XPDropUnstableCenterPullAccel", "300"),
                ("XPDropUnstableVelocityDampingPerSecond", "2.7"),
                ("XPDropUnstableMaxSpeed", "360"),
                ("XPDropUnstableAlphaMin", "0.2"),
                ("XPDropUnstableAlphaMax", "0.95"),
                ("XPDropUnstableAlphaPulseHz", "10.5"),
                ("XPDropUnstableRadiusScale", "0.49"),
                ("XPDropUnstableRenderScale", "1.0"),
                ("XPDropUnstableFadeOutSeconds", "0.55"),
                ("XPDropUnstableShowLerpPerSecond", "12"),
                ("XPDropUnstableVisibilityEpsilon", "0.001"),
                ("XPDropUnstableBurstRatePerSecond", "2.6"),
                ("XPDropUnstableRandomKickFactorMin", "1.0"),
                ("XPDropUnstableRandomKickFactorMax", "2.15"),
                ("XPDropUnstableTangentialKickBaseFactor", "0.34"),
                ("XPDropUnstableTangentialKickPressureFactor", "0.62"),
                ("XPDropUnstableCenterPullBaseFactor", "0.18"),
                ("XPDropUnstableCenterPullPressureFactor", "0.5"),
                ("XPDropUnstableBurstFactorMin", "1.3"),
                ("XPDropUnstableBurstFactorMax", "2.35"),
                ("XPDropUnstableBoundaryBounceFactor", "2.2"),
                ("XPDropUnstableBoundaryTangentialFactor", "0.35"),
                ("XPDropUnstableAlphaNoiseAmplitude", "0.9"),
                ("XPDropUnstableAlphaPulseWeight", "0.62"),
                ("XPDropUnstableAlphaPressureWeight", "0.28"),
                ("XPDropUnstableAlphaLerpPerSecond", "14"),
            ];

            foreach ((string key, string value) in settings)
            {
                if (SettingKeyExists(key, "FXSettings"))
                {
                    continue;
                }

                DatabaseQuery.ExecuteNonQuery(
                    $"INSERT INTO FXSettings (SettingKey, Value) VALUES ('{key}', '{value}');");
                DebugLogger.PrintDatabase($"EnsureXPClumpFxSettings: inserted {key} = {value}.");
            }

            // Retune legacy unstable-clump defaults to the new behavior without
            // overriding user-customized values.
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '0.3' WHERE SettingKey = 'XPDropUnstableHealthThresholdRatio';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '1500'      WHERE SettingKey = 'XPDropUnstableJitterAccel' AND Value = '640';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '300'       WHERE SettingKey = 'XPDropUnstableCenterPullAccel' AND Value = '460';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '2.7'       WHERE SettingKey = 'XPDropUnstableVelocityDampingPerSecond' AND Value = '5.8';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '360'       WHERE SettingKey = 'XPDropUnstableMaxSpeed' AND Value = '170';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '10.5'      WHERE SettingKey = 'XPDropUnstableAlphaPulseHz' AND Value = '7.6';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '0.49'      WHERE SettingKey = 'XPDropUnstableRadiusScale' AND Value IN ('0.93', '0.98');");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '1.0'       WHERE SettingKey = 'XPDropUnstableRenderScale' AND Value IN ('0.9', '1.2');");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '2.6'       WHERE SettingKey = 'XPClumpAbsorbConsumeGrowMaxScale' AND Value = '1.95';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '0.42'      WHERE SettingKey = 'XPClumpAbsorbFadeSeconds' AND Value = '0.14';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '1.55'      WHERE SettingKey = 'XPClumpShadowScale' AND Value = '1.35';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '0.4'       WHERE SettingKey = 'XPClumpShadowAlphaScale' AND Value = '0.32';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '20'        WHERE SettingKey = 'XPClumpDeadZoneStartSeconds' AND Value = '60';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '10'        WHERE SettingKey = 'XPClumpDeadZoneDespawnSeconds' AND Value = '20';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '45'        WHERE SettingKey = 'XPClumpClusterAttractForce' AND Value = '70';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '3.8'       WHERE SettingKey = 'XPClumpDeadPulseSpeedMin' AND Value = '2.2';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '12.0'      WHERE SettingKey = 'XPClumpDeadPulseSpeedMax' AND Value IN ('7', '7.0');");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '0.55'      WHERE SettingKey = 'XPClumpDeadPulseLowAlphaStart' AND Value = '0.82';");
            DatabaseQuery.ExecuteNonQuery("UPDATE FXSettings SET Value = '0.03'      WHERE SettingKey = 'XPClumpDeadPulseLowAlphaEnd' AND Value = '0.15';");
        }

        private static void EnsureXPClumpBackendTooltips()
        {
            if (!TableExists("UITooltips"))
            {
                return;
            }

            (string key, string text)[] entries =
            [
                ("FogOfWarEnabled", "Whether fog-of-war currently has a viewing unit context available."),
                ("FogOfWarActive", "Whether fog-of-war is actively rendering this frame."),
                ("FogVisionSourceCount", "How many sight sources are currently revealing vision territory."),
                ("PlayerSightRadius", "The player's active sight radius in centifoots."),
                ("FogFrontierBorderThickness", "Reference world-space thickness used to normalize fog frontier widths and amplitudes."),
                ("FogFrontierFieldSmoothingRadius", "Current world-space smoothing radius applied to the fog frontier scalar field."),
                ("CentifootWorldUnits", "Copied baseline conversion: 1 centifoot = this many world units."),
                ("DistanceUnit", "Name of the active distance unit used by backend distance displays."),
                ("XPClumpCount", "Number of active free clumps currently in the world."),
                ("XPUnstableClumpCount", "Number of low-health unstable clumps currently visualized on destructible drop sources."),
                ("PendingFarmXPDrops", "Number of destructible drop sources currently fading out that still have queued free clumps to spawn."),
                ("XPClumpsAbsorbedThisSecond", "How many XP clumps have been absorbed across all units during the current one-second pickup window."),
                ("XPClumpPickupPerSecond", "Maximum number of XP clumps a single unit may absorb per second."),
                ("XPClumpDeadZoneRadius", "Distance from a clump where units are considered outside active pickup influence, shown in centifoots."),
                ("XPClumpPullZoneRadius", "Distance from a clump where units begin applying pull-zone attraction, shown in centifoots."),
                ("XPClumpAbsorbZoneRadius", "Distance from a clump where orbit-lock and absorption behavior begins, shown in centifoots."),
                ("XPClumpClusterHomeostasisVariance", "How much pair-by-pair variance is injected into clump cluster homeostasis distance."),
                ("XPClumpClusterInstabilityForce", "Tangential magnetic wobble force that keeps clump clusters fluid and irregular."),
                ("XPClumpClusterInstabilityPulseHz", "Pulse frequency for magnetic wobble and dynamic homeostasis modulation in clump clusters."),
            ];

            foreach ((string key, string text) in entries)
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT OR REPLACE INTO UITooltips (RowKey, TooltipText) VALUES (@key, @text);",
                    new Dictionary<string, object>
                    {
                        ["@key"] = key,
                        ["@text"] = text
                    });
            }
        }

        private static bool TableExists(string tableName)
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery(
                    $"SELECT name FROM sqlite_master WHERE type='table' AND name=@t;",
                    new Dictionary<string, object> { { "@t", tableName } });
                return rows.Count > 0;
            }
            catch { return false; }
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery($"PRAGMA table_info({tableName});");
                foreach (var row in rows)
                {
                    if (row.TryGetValue("name", out object nameObj) &&
                        string.Equals(nameObj?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"ColumnExists check failed for {tableName}.{columnName}: {ex.Message}");
            }
            return false;
        }
    }
}
