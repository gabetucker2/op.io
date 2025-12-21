using System;
using System.IO;
using System.Collections.Generic;

namespace op.io
{
    internal static class ControlKeyMigrations
    {
        internal const string BlockMenuKey = "BlockMenu";
        internal const string LegacyPanelMenuKey = "PanelMenu";
        internal const string HoldInputsKey = "HoldInputs";
        internal const string NextConfigurationKey = "UseNextConfiguration";
        internal const string PreviousConfigurationKey = "UsePreviousConfiguration";
        private const string TransparentTabBlockingKey = "TransparentTabBlocking";
        private static bool _applied;
        private static readonly string[] MetaControlKeys = ["Exit", BlockMenuKey, LegacyPanelMenuKey, HoldInputsKey, InspectModeState.InspectModeKey, "DockingMode", "DebugMode", "AllowGameInputFreeze", TransparentTabBlockingKey, NextConfigurationKey, PreviousConfigurationKey];

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
                EnsureBlockMenuControl();
                EnsureExitControl();
                EnsureTransparentTabBlockingControl();
                EnsureHoldInputsControl();
                EnsureInspectModeControl();
                EnsureConfigurationCycleControls();
                EnsureDockingModeDefaultOff();
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

        private static void EnsureBlockMenuControl()
        {
            try
            {
                ControlKeyData.ControlKeyRecord existing = ControlKeyData.GetControl(BlockMenuKey);
                if (existing == null && ControlKeyData.GetControl(LegacyPanelMenuKey) != null)
                {
                    var parameters = new Dictionary<string, object>
                    {
                        ["@newKey"] = BlockMenuKey,
                        ["@legacyKey"] = LegacyPanelMenuKey
                    };

                    const string migrateSql = @"
UPDATE ControlKey
SET SettingKey = @newKey
WHERE SettingKey = @legacyKey;";

                    DatabaseQuery.ExecuteNonQuery(migrateSql, parameters);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to migrate legacy {LegacyPanelMenuKey} binding to {BlockMenuKey}: {ex.Message}");
            }

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = BlockMenuKey,
                InputKey = "Shift + X",
                InputType = "SaveSwitch",
                SwitchStartState = 0,
                MetaControl = true,
                RenderOrder = 11
            });

            ControlKeyData.SetInputType(BlockMenuKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(BlockMenuKey, 0);
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
                SwitchStartState = 1,
                MetaControl = false,
                RenderOrder = 15
            });

            ControlKeyData.SetInputType(TransparentTabBlockingKey, "SaveSwitch");
            ForceTransparentTabBlockingDefaultOn();
            ControlKeyData.EnsureInputKey(TransparentTabBlockingKey, "Shift + V");
        }

        private static void ForceTransparentTabBlockingDefaultOn()
        {
            try
            {
                const string sql = @"
UPDATE ControlKey
SET SwitchStartState = 1
WHERE SettingKey = 'TransparentTabBlocking' AND (SwitchStartState IS NULL OR SwitchStartState = 0);";
                DatabaseQuery.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to normalize TransparentTabBlocking default: {ex.Message}");
            }

            ControlKeyData.EnsureSwitchStartState(TransparentTabBlockingKey, 1);
        }

        private static void EnsureHoldInputsControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = HoldInputsKey,
                InputKey = "M",
                InputType = "NoSaveSwitch",
                SwitchStartState = 0,
                MetaControl = true,
                RenderOrder = 16
            });

            ControlKeyData.SetInputType(HoldInputsKey, "NoSaveSwitch");
            ControlKeyData.EnsureSwitchStartState(HoldInputsKey, 0);
            ControlKeyData.EnsureInputKey(HoldInputsKey, "M");
        }

        private static void EnsureInspectModeControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = InspectModeState.InspectModeKey,
                InputKey = "Shift + I",
                InputType = "NoSaveSwitch",
                SwitchStartState = 0,
                MetaControl = true,
                RenderOrder = 17
            });

            ControlKeyData.SetInputType(InspectModeState.InspectModeKey, "NoSaveSwitch");
            ControlKeyData.EnsureSwitchStartState(InspectModeState.InspectModeKey, 0);
            ControlKeyData.EnsureInputKey(InspectModeState.InspectModeKey, "Shift + I");
        }

        private static void EnsureDockingModeDefaultOff()
        {
            const string key = "DockingMode";
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = key,
                InputKey = "V",
                InputType = "SaveSwitch",
                SwitchStartState = 0,
                MetaControl = true,
                RenderOrder = 12
            });

            ControlKeyData.SetInputType(key, "SaveSwitch");
            ControlKeyData.EnsureInputKey(key, "V");
            ControlKeyData.EnsureSwitchStartState(key, 0);

            try
            {
                string markerOff = Path.Combine(DatabaseConfig.DatabaseDirectory, ".docking_default_off_applied");
                string markerOn = Path.Combine(DatabaseConfig.DatabaseDirectory, ".docking_default_on_applied");

                int currentRawState = DatabaseConfig.GetSetting("ControlKey", "SwitchStartState", key, -1);
                bool currentBoolState = TypeConversionFunctions.IntToBool(currentRawState <= -1 ? 0 : currentRawState);
                DockingDiagnostics.RecordMigration(
                    "pre-normalize",
                    currentRawState,
                    currentBoolState,
                    File.Exists(markerOff),
                    File.Exists(markerOn),
                    note: "EnsureDockingModeDefaultOff start");

                bool shouldReset = !File.Exists(markerOff) || File.Exists(markerOn);
                if (shouldReset)
                {
                    const string sql = "UPDATE ControlKey SET SwitchStartState = 0 WHERE SettingKey = @key;";
                    DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = key });
                    File.WriteAllText(markerOff, DateTime.UtcNow.ToString("O"));
                    if (File.Exists(markerOn))
                    {
                        File.Delete(markerOn);
                    }

                    int resetRawState = DatabaseConfig.GetSetting("ControlKey", "SwitchStartState", key, -1);
                    DockingDiagnostics.RecordMigration(
                        "post-reset",
                        resetRawState,
                        TypeConversionFunctions.IntToBool(resetRawState <= -1 ? 0 : resetRawState),
                        File.Exists(markerOff),
                        File.Exists(markerOn),
                        note: "Reset DockingMode default to OFF");
                }
                else
                {
                    DockingDiagnostics.RecordMigration(
                        "skip-reset",
                        currentRawState,
                        currentBoolState,
                        File.Exists(markerOff),
                        File.Exists(markerOn),
                        note: "Markers indicate OFF already applied");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to normalize DockingMode default: {ex.Message}");
            }
        }

        private static void EnsureConfigurationCycleControls()
        {
            EnsureConfigurationControl(PreviousConfigurationKey, "Shift + [", 17);
            EnsureConfigurationControl(NextConfigurationKey, "Shift + ]", 18);
        }

        private static void EnsureConfigurationControl(string settingKey, string defaultInput, int renderOrder)
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = settingKey,
                InputKey = defaultInput,
                InputType = "Trigger",
                SwitchStartState = 0,
                MetaControl = true,
                RenderOrder = renderOrder,
                LockMode = false
            });

            ControlKeyData.SetInputType(settingKey, "Trigger");
            ControlKeyData.ClearSwitchStartState(settingKey);
            ControlKeyData.EnsureInputKey(settingKey, defaultInput);
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
            return string.Equals(settingKey, ControlKeyMigrations.BlockMenuKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settingKey, ControlKeyMigrations.LegacyPanelMenuKey, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldScannerTrackSwitch(string settingKey)
        {
            // Keep scanner coverage for switches (including DockingMode) so key-edge toggles are detected each frame.
            return !RequiresSwitchSemantics(settingKey);
        }
    }
}
