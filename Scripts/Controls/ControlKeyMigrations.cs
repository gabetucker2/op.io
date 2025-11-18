using System;
using System.IO;

namespace op.io
{
    internal static class ControlKeyMigrations
    {
        private static bool _applied;

        public static void EnsureApplied()
        {
            if (_applied)
            {
                return;
            }

            if (!File.Exists(DatabaseConfig.DatabaseFilePath))
            {
                return;
            }

            try
            {
                const string panelMenuSql = @"
UPDATE ControlKey
SET InputType = 'Switch',
    SwitchStartState = COALESCE(SwitchStartState, 0)
WHERE SettingKey = 'PanelMenu' AND InputType <> 'Switch';";

                const string ensurePanelMenuStateSql = @"
UPDATE ControlKey
SET SwitchStartState = COALESCE(SwitchStartState, 0)
WHERE SettingKey = 'PanelMenu';";

                const string exitResetSql = @"
UPDATE ControlKey
SET InputType = 'Trigger',
    SwitchStartState = NULL
WHERE SettingKey = 'Exit';";

                const string ensurePanelMenuRowSql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState)
SELECT 'PanelMenu', 'Shift + X', 'Switch', 0
WHERE NOT EXISTS (SELECT 1 FROM ControlKey WHERE SettingKey = 'PanelMenu');";

                const string ensureAllowFreezeSwitchSql = @"
UPDATE ControlKey
SET InputType = 'Switch',
    SwitchStartState = COALESCE(SwitchStartState, 1)
WHERE SettingKey = 'AllowGameInputFreeze';";

                const string ensureAllowFreezeRowSql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState)
SELECT 'AllowGameInputFreeze', 'Shift + C', 'Switch', 1
WHERE NOT EXISTS (SELECT 1 FROM ControlKey WHERE SettingKey = 'AllowGameInputFreeze');";

                DatabaseQuery.ExecuteNonQuery(panelMenuSql);
                DatabaseQuery.ExecuteNonQuery(ensurePanelMenuStateSql);
                DatabaseQuery.ExecuteNonQuery(exitResetSql);
                DatabaseQuery.ExecuteNonQuery(ensurePanelMenuRowSql);
                DatabaseQuery.ExecuteNonQuery(ensureAllowFreezeSwitchSql);
                DatabaseQuery.ExecuteNonQuery(ensureAllowFreezeRowSql);
                EnsureMetaControlColumn();

                _applied = true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to apply ControlKey migrations: {ex.Message}");
            }
        }

        private static void EnsureMetaControlColumn()
        {
            const string columnName = "MetaControl";
            if (!DoesControlKeyColumnExist(columnName))
            {
                DatabaseQuery.ExecuteNonQuery($"ALTER TABLE ControlKey ADD COLUMN {columnName} INTEGER NOT NULL DEFAULT 0;");
            }

            const string syncMetaControls = @"
UPDATE ControlKey
SET MetaControl = CASE WHEN SettingKey IN ('Exit','PanelMenu','DockingMode','DebugMode','AllowGameInputFreeze') THEN 1 ELSE 0 END;";
            DatabaseQuery.ExecuteNonQuery(syncMetaControls);
        }

        private static bool DoesControlKeyColumnExist(string columnName)
        {
            const string columnInfoSql = "PRAGMA table_info(ControlKey);";
            var columns = DatabaseQuery.ExecuteQuery(columnInfoSql);
            foreach (var column in columns)
            {
                if (column.TryGetValue("name", out object nameValue) &&
                    string.Equals(Convert.ToString(nameValue), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class ControlKeyRules
    {
        public static bool RequiresSwitchSemantics(string settingKey)
        {
            return string.Equals(settingKey, "PanelMenu", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldScannerTrackSwitch(string settingKey)
        {
            return !string.Equals(settingKey, "PanelMenu", StringComparison.OrdinalIgnoreCase);
        }
    }
}
