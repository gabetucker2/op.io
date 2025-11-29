using System;
using System.IO;
using System.Collections.Generic;

namespace op.io
{
    internal static class ControlKeyMigrations
    {
        private static bool _applied;
        private static readonly string[] MetaControlKeys = ["Exit", "PanelMenu", "DockingMode", "DebugMode", "AllowGameInputFreeze", "TransparentTabBlocking"];
        private const string TransparentTabBlockingKey = "TransparentTabBlocking";

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
                EnsureTransparentTabBlockingControl();
                EnsureLockModeColumn();
                MigrateLegacySwitchType();
                EnsureMetaControlColumn();
                ControlKeyData.ApplyMetaControlFlags(MetaControlKeys);
                EnsureCrouchUsesNoSaveSwitch();
                LockExitBinding();

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
                InputType = "SaveSwitch",
                SwitchStartState = 0,
                MetaControl = true
            });

            ControlKeyData.SetInputType("PanelMenu", "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState("PanelMenu", 0);
        }

        private static void EnsureExitControl()
        {
            ControlKeyData.SetInputType("Exit", "Trigger");
            ControlKeyData.ClearSwitchStartState("Exit");
        }

        private static void EnsureTransparentTabBlockingControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = TransparentTabBlockingKey,
                InputKey = "Shift + V",
                InputType = "SaveSwitch",
                SwitchStartState = 0,
                MetaControl = false,
                RenderOrder = 15
            });

            ControlKeyData.SetInputType(TransparentTabBlockingKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(TransparentTabBlockingKey, 0);
            ControlKeyData.EnsureInputKey(TransparentTabBlockingKey, "Shift + V");
        }

        private static void EnsureCrouchUsesNoSaveSwitch()
        {
            ControlKeyData.SetInputType("Crouch", "NoSaveSwitch");
            ControlKeyData.EnsureSwitchStartState("Crouch", 0);
        }

        private static void EnsureLockModeColumn()
        {
            const string column = "LockMode";
            if (!ControlKeyData.ColumnExists(column))
            {
                ControlKeyData.AddColumn(column, "INTEGER NOT NULL DEFAULT 0");
            }

            try
            {
                const string normalizeSql = "UPDATE ControlKey SET LockMode = COALESCE(LockMode, 0);";
                DatabaseQuery.ExecuteNonQuery(normalizeSql);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to normalize LockMode column: {ex.Message}");
            }
        }

        private static void LockExitBinding()
        {
            try
            {
                const string sql = "UPDATE ControlKey SET LockMode = 1 WHERE SettingKey = 'Exit';";
                DatabaseQuery.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to lock Exit binding: {ex.Message}");
            }
        }

        private static void MigrateLegacySwitchType()
        {
            try
            {
                const string sql = "UPDATE ControlKey SET InputType = 'SaveSwitch' WHERE InputType = 'Switch';";
                DatabaseQuery.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to migrate legacy switch input types: {ex.Message}");
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
