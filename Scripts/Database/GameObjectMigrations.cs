using System;

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
                EnsureFarmDataDeathPointReward();
                EnsureMapDataDeathPointReward();
                EnsureMapDataMaxHealth();
                EnsureAgentsDestructible();
                EnsureBarrelPrototypesExist();
                EnsureBulletRangeRenamedToDragFactor();
                EnsurePlayerBarrelAssignments();
                _applied = true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"GameObjectMigrations failed: {ex.Message}");
            }
        }

        private static void EnsureFarmDataDeathPointReward()
        {
            if (ColumnExists("FarmData", "DeathPointReward")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE FarmData ADD COLUMN DeathPointReward REAL DEFAULT 0;");
            DebugLogger.PrintDatabase("Added DeathPointReward column to FarmData.");
        }

        private static void EnsureMapDataDeathPointReward()
        {
            if (ColumnExists("MapData", "DeathPointReward")) return;
            DatabaseQuery.ExecuteNonQuery(
                "ALTER TABLE MapData ADD COLUMN DeathPointReward REAL DEFAULT 0;");
            DebugLogger.PrintDatabase("Added DeathPointReward column to MapData.");
        }

        // Destructible map objects with MaxHealth = 0 are immediately killed on frame 1.
        // Backfill a safe default so they survive and can show a health bar.
        private static void EnsureMapDataMaxHealth()
        {
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
                    BulletDragFactor  REAL    DEFAULT -1,
                    ReloadSpeed       REAL    DEFAULT -1,
                    BulletHealth      REAL    DEFAULT -1,
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
                "INSERT OR IGNORE INTO BarrelPrototypes (Name, BulletDamage, BulletPenetration, BulletSpeed, BulletDragFactor, ReloadSpeed, BulletHealth, BulletMaxLifespan, BulletMass, BulletFillR, BulletFillG, BulletFillB, BulletFillA, BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA, BulletOutlineWidth) VALUES ('Heavy', 10, 0, 400, 800, 3, 10, 3, 6, 255, 0, 0, 255, 139, 0, 0, 255, 2);");

            DebugLogger.PrintDatabase("EnsureBarrelPrototypesExist: Default/Medium/Heavy prototypes present.");
        }

        private static void EnsureBulletRangeRenamedToDragFactor()
        {
            if (ColumnExists("BarrelPrototypes", "BulletDragFactor")) return;
            if (ColumnExists("BarrelPrototypes", "BulletRange"))
                DatabaseQuery.ExecuteNonQuery("ALTER TABLE BarrelPrototypes RENAME COLUMN BulletRange TO BulletDragFactor;");

            if (!SettingKeyExists("DefaultBulletDragFactor") && SettingKeyExists("DefaultBulletRange"))
                DatabaseQuery.ExecuteNonQuery("UPDATE PhysicsSettings SET SettingKey = 'DefaultBulletDragFactor' WHERE SettingKey = 'DefaultBulletRange';");

            DebugLogger.PrintDatabase("EnsureBulletRangeRenamedToDragFactor: column and setting key migrated.");
        }

        private static bool SettingKeyExists(string key)
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery("SELECT 1 FROM PhysicsSettings WHERE SettingKey = @k;",
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
