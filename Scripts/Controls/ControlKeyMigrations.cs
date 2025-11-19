using System;
using System.IO;
using System.Collections.Generic;

namespace op.io
{
    internal static class ControlKeyMigrations
    {
        private static bool _applied;
        private static readonly string[] MetaControlKeys = ["Exit", "PanelMenu", "DockingMode", "DebugMode", "AllowGameInputFreeze"];

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
                EnsureRenderOrderColumn();
                EnsurePanelMenuControl();
                EnsureExitControl();
                EnsureMetaControlColumn();
                ControlKeyData.ApplyMetaControlFlags(MetaControlKeys);

                _applied = true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to apply ControlKey migrations: {ex.Message}");
            }
        }

        private static void EnsureRenderOrderColumn()
        {
            const string columnName = "RenderOrder";
            if (!ControlKeyData.ColumnExists(columnName))
            {
                ControlKeyData.AddColumn(columnName, "INTEGER NOT NULL DEFAULT 0");
            }

            ControlKeyData.NormalizeRenderOrderValues();
        }

        private static void EnsureMetaControlColumn()
        {
            const string columnName = "MetaControl";
            if (!ControlKeyData.ColumnExists(columnName))
            {
                ControlKeyData.AddColumn(columnName, "INTEGER NOT NULL DEFAULT 0");
            }
        }

        private static void EnsurePanelMenuControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = "PanelMenu",
                InputKey = "Shift + X",
                InputType = "Switch",
                SwitchStartState = 0,
                MetaControl = true
            });

            ControlKeyData.SetInputType("PanelMenu", "Switch");
            ControlKeyData.EnsureSwitchStartState("PanelMenu", 0);
        }

        private static void EnsureExitControl()
        {
            ControlKeyData.SetInputType("Exit", "Trigger");
            ControlKeyData.ClearSwitchStartState("Exit");
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
