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

                DatabaseQuery.ExecuteNonQuery(panelMenuSql);
                DatabaseQuery.ExecuteNonQuery(ensurePanelMenuStateSql);
                DatabaseQuery.ExecuteNonQuery(exitResetSql);
                DatabaseQuery.ExecuteNonQuery(ensurePanelMenuRowSql);

                _applied = true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to apply ControlKey migrations: {ex.Message}");
            }
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
