using System;
using System.Collections.Generic;

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
                EnsureBarrelPrototypesExist();
                EnsureBulletRangeRenamedToDragFactor();
                EnsurePlayerBarrelAssignments();
                EnsureAgentsRegenDelayColumns();
                EnsurePlayerMaxShield();
                EnsureHealthBarColors();
                EnsureHealthBarSegmentSize();
                EnsureAgentsMaxXP();
                EnsureBarConfigTable();
                EnsureBarConfigIsHidden();
                EnsureRegenBarConfigs();
                EnsureBarsVisibleSetting();
                EnsureBarsInPropertiesPanel();
                EnsureBarConfigShowPercent();
                EnsureBarConfigDefaultHidden();
                EnsureShieldAboveHealth();
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
                EnsureDropAgentsHiddenColumns();
                EnsureDropBarrelHiddenColumns();
                EnsurePlayerBodyCollisionDamage();
                EnsureBlueLootFarmPrototype();
                EnsureFarmMassQuadrupled();
                EnsureBlueLootCollisionDamage();
                EnsureDropGameObjectsTypeColumn();
                EnsureBarrelHeightScalarSetting();
                EnsureBulletKnockbackScalarSetting();
                EnsureBulletRecoilScalarSetting();
                EnsureBulletFarmKnockbackScalarSetting();
                EnsureBulletEffectorColumns();
                EnsureBarrelBulletHealthColumn();
                EnsureBodyRadiusScalarSetting();
                EnsureDeathFadeFxSettings();
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
                    SegmentsEnabled INTEGER NOT NULL DEFAULT 1
                );");

            DatabaseQuery.ExecuteNonQuery("INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled) VALUES ('Shield', 0, 0, 10, 1);");
            DatabaseQuery.ExecuteNonQuery("INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled) VALUES ('Health', 1, 0, 10, 1);");
            DatabaseQuery.ExecuteNonQuery("INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled) VALUES ('XP',     2, 0, 10, 1);");

            DebugLogger.PrintDatabase("EnsureBarConfigTable: BarConfig seeded.");
        }

        private static void EnsureRegenBarConfigs()
        {
            // Seed HealthRegen and ShieldRegen rows into BarConfig if missing.
            // They default to row 2, hidden = 0. Users can reposition/hide them in BarsBlock.
            DatabaseQuery.ExecuteNonQuery("INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden) VALUES ('HealthRegen', 2, 0, 10, 1, 0);");
            DatabaseQuery.ExecuteNonQuery("INSERT OR IGNORE INTO BarConfig (BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden) VALUES ('ShieldRegen', 2, 1, 10, 1, 0);");
            DebugLogger.PrintDatabase("EnsureRegenBarConfigs: HealthRegen/ShieldRegen rows present.");
        }

        private static void EnsureBarConfigIsHidden()
        {
            if (ColumnExists("BarConfig", "IsHidden")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE BarConfig ADD COLUMN IsHidden INTEGER NOT NULL DEFAULT 0;");
            DebugLogger.PrintDatabase("EnsureBarConfigIsHidden: added IsHidden column to BarConfig.");
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
                    const string specsEntry = "{ \"kind\": \"Specs\", \"mode\": \"Toggle\", \"count\": 0, \"visible\": true }";
                    const string barsEntry  = ",\n    { \"kind\": \"Bars\", \"mode\": \"Toggle\", \"count\": 0, \"visible\": true }";
                    int idx = json.IndexOf(specsEntry, System.StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        json    = json.Insert(idx + specsEntry.Length, barsEntry);
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
