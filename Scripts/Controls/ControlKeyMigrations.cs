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
        internal const string CombatTextKey = "CombatText";
        internal const string RespawnKey = "Respawn";
        internal const string AutoTurnInspectModeOffKey = "AutoTurnInspectModeOff";
        internal const string TabSwitchRequiresBlockModeKey = "TabSwitchRequiresBlockMode";
        internal const string CameraLockModeKey = "CameraLockMode";
        internal static readonly string[] CameraLockModeOptions = ["Locked", "Free", "Scout"];
        internal const string CameraSnapToPlayerKey = "CameraSnapToPlayer";
        internal const string ScrollInKey           = "ScrollIn";
        internal const string ScrollOutKey          = "ScrollOut";
        internal const string ScrollMinDistanceKey  = "ScrollMinDistance";
        internal const string ScrollMaxDistanceKey  = "ScrollMaxDistance";
        internal const string ScrollIncrementKey    = "ScrollIncrement";
        internal const string CtrlBufferKey         = "CtrlBuffer";
        internal const string ShowHiddenAttrsKey    = "ShowHiddenAttrs";
        private static readonly string[] MetaControlKeys = ["Exit", BlockMenuKey, LegacyPanelMenuKey, HoldInputsKey, InspectModeState.InspectModeKey, AutoTurnInspectModeOffKey, "DockingMode", "DebugMode", "AllowGameInputFreeze", TransparentTabBlockingKey, NextConfigurationKey, PreviousConfigurationKey, TabSwitchRequiresBlockModeKey, RespawnKey, CameraLockModeKey, CameraSnapToPlayerKey, ScrollInKey, ScrollOutKey, ScrollMinDistanceKey, ScrollMaxDistanceKey, ScrollIncrementKey, CtrlBufferKey, ShowHiddenAttrsKey];

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
                EnsureLockModeColumn();
                EnsureMetaControlColumn();
                EnsureFloatStartStateColumn();
                EnsureBlockMenuControl();
                EnsureExitControl();
                EnsureTransparentTabBlockingControl();
                EnsureHoldInputsControl();
                EnsureInspectModeControl();
                EnsureAutoTurnInspectModeOffControl();
                EnsureConfigurationCycleControls();
                EnsureDockingModeDefaultOff();
                MigrateLegacySwitchType();
                ControlKeyData.ApplyMetaControlFlags(MetaControlKeys);
                EnsureCrouchUsesNoSaveSwitch();
                LockExitBinding();
                EnsureMoveAwayFromCursorUnbound();
                EnsureFireControl();
                EnsureBarrelSwitchControls();
                EnsureCombatTextControl();
                RemoveHealthBarControl();
                RemoveXPBarControl();
                RemoveCombineHealthShieldBarControl();
                EnsureAllowGameInputFreezeIsEnum();
                EnsureTabSwitchRequiresBlockModeControl();
                EnsureRespawnControl();
                EnsureCameraLockModeControl();
                EnsureCameraSnapToPlayerControl();
                RemoveMouseSensitivityMultiplierControl();
                EnsureScrollControls();
                EnsureCtrlBufferControl();
                EnsureShowHiddenAttrsControl();
                EnsureControlTooltips();

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

        private static void EnsureAutoTurnInspectModeOffControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = AutoTurnInspectModeOffKey,
                InputKey = "",
                InputType = "SaveSwitch",
                SwitchStartState = 1,
                MetaControl = true,
                RenderOrder = 18
            });

            ControlKeyData.SetInputType(AutoTurnInspectModeOffKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(AutoTurnInspectModeOffKey, 1);

            string markerOn = System.IO.Path.Combine(DatabaseConfig.DatabaseDirectory, ".autoturn_inspectmode_default_on_applied");
            if (!File.Exists(markerOn))
            {
                const string sql = "UPDATE ControlKey SET SwitchStartState = 1 WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = AutoTurnInspectModeOffKey });
                File.WriteAllText(markerOn, DateTime.UtcNow.ToString("O"));
            }
        }

        private static void EnsureTabSwitchRequiresBlockModeControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = TabSwitchRequiresBlockModeKey,
                InputKey = "",
                InputType = "SaveSwitch",
                SwitchStartState = 1,
                MetaControl = true,
                RenderOrder = 19
            });

            ControlKeyData.SetInputType(TabSwitchRequiresBlockModeKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(TabSwitchRequiresBlockModeKey, 1);
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

        private static void EnsureMoveAwayFromCursorUnbound()
        {
            try
            {
                const string sql = "UPDATE ControlKey SET InputKey = '' WHERE SettingKey = 'MoveAwayFromCursor' AND InputKey = 'RightClick';";
                DatabaseQuery.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to unbind MoveAwayFromCursor: {ex.Message}");
            }
        }

        private static void EnsureFireControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = "Fire",
                InputKey = "RightClick",
                InputType = "Hold",
                SwitchStartState = 0,
                MetaControl = false,
                RenderOrder = 19,
                LockMode = false
            });

            ControlKeyData.SetInputType("Fire", "Hold");
            ControlKeyData.EnsureInputKey("Fire", "RightClick");
        }

        private static void EnsureBarrelSwitchControls()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = "BarrelLeft",
                InputKey = "Q",
                InputType = "Trigger",
                SwitchStartState = 0,
                MetaControl = false,
                RenderOrder = 20
            });
            ControlKeyData.SetInputType("BarrelLeft", "Trigger");
            ControlKeyData.EnsureInputKey("BarrelLeft", "Q");

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey = "BarrelRight",
                InputKey = "E",
                InputType = "Trigger",
                SwitchStartState = 0,
                MetaControl = false,
                RenderOrder = 21
            });
            ControlKeyData.SetInputType("BarrelRight", "Trigger");
            ControlKeyData.EnsureInputKey("BarrelRight", "E");
        }

        private static void EnsureCombatTextControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey      = CombatTextKey,
                InputKey        = "Shift + T",
                InputType       = "SaveSwitch",
                SwitchStartState = 1,
                MetaControl     = false,
                RenderOrder     = 23
            });

            ControlKeyData.SetInputType(CombatTextKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(CombatTextKey, 1);
            ControlKeyData.EnsureInputKey(CombatTextKey, "Shift + T");

            // One-time migration: force default ON for existing DBs where SwitchStartState was 0.
            // EnsureSwitchStartState uses COALESCE (only sets if NULL) and the schema default is 0,
            // so rows pre-dating this migration would stay 0 without this direct update.
            string markerOn = Path.Combine(DatabaseConfig.DatabaseDirectory, ".combat_text_default_on_applied");
            if (!File.Exists(markerOn))
            {
                const string sql = "UPDATE ControlKey SET SwitchStartState = 1 WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = CombatTextKey });
                File.WriteAllText(markerOn, DateTime.UtcNow.ToString("O"));
            }
        }

        internal const string AllowGameInputFreezeKey = "AllowGameInputFreeze";
        // 0=Limited (suppress mouse on clicks + keyboard on text focus), 1=Focus (suppress when window unfocused),
        // 2=MouseLeave (suppress when cursor outside game), 3=None (never suppress).
        internal static readonly string[] AllowGameInputFreezeOptions = ["Limited", "Focus", "MouseLeave", "None"];

        internal const string HealthBarKey = "HealthBar";
        internal const string XPBarKey    = "XPBar";
        // CombineHealthShieldBarKey kept for migration only — replaced by BarConfigManager row system.
        internal const string CombineHealthShieldBarKey = "CombineHealthShieldBar";

        private static void RemoveHealthBarControl()
        {
            try
            {
                const string sql = "DELETE FROM ControlKey WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = HealthBarKey });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to remove {HealthBarKey}: {ex.Message}");
            }
        }

        private static void RemoveXPBarControl()
        {
            try
            {
                const string sql = "DELETE FROM ControlKey WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = XPBarKey });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to remove {XPBarKey}: {ex.Message}");
            }
        }

        private static void RemoveCombineHealthShieldBarControl()
        {
            // CombineHealthShieldBar has been replaced by the BarConfigManager row system.
            // Remove it from the Controls block by deleting the row if it exists.
            try
            {
                const string sql = "DELETE FROM ControlKey WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = CombineHealthShieldBarKey });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to remove {CombineHealthShieldBarKey}: {ex.Message}");
            }
        }

        private static void EnsureCombineHealthShieldBarControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey       = CombineHealthShieldBarKey,
                InputKey         = "",
                InputType        = "SaveSwitch",
                SwitchStartState = 1,
                MetaControl      = false,
                RenderOrder      = 25
            });

            ControlKeyData.SetInputType(CombineHealthShieldBarKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(CombineHealthShieldBarKey, 1);

            try
            {
                const string tooltipSql = "INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES (@key, @tip);";
                DatabaseQuery.ExecuteNonQuery(tooltipSql, new Dictionary<string, object>
                {
                    ["@key"] = CombineHealthShieldBarKey,
                    ["@tip"] = "When enabled, health and shield share a single combined bar. When disabled, each has its own separate bar."
                });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to insert tooltip for {CombineHealthShieldBarKey}: {ex.Message}");
            }

            string markerOn = Path.Combine(DatabaseConfig.DatabaseDirectory, ".combine_healthshieldbar_default_on_applied");
            if (!File.Exists(markerOn))
            {
                const string sql = "UPDATE ControlKey SET SwitchStartState = 1 WHERE SettingKey = @key;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = CombineHealthShieldBarKey });
                File.WriteAllText(markerOn, DateTime.UtcNow.ToString("O"));
            }
        }

        private static void EnsureRespawnControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey  = RespawnKey,
                InputKey    = "Shift + R",
                InputType   = "Trigger",
                SwitchStartState = 0,
                MetaControl = true,
                RenderOrder = 24
            });

            ControlKeyData.SetInputType(RespawnKey, "Trigger");
            ControlKeyData.EnsureInputKey(RespawnKey, "Shift + R");

            // Ensure MetaControl = 1 for existing DBs where it was created as 0.
            // Respawn must be a meta-control so inspect mode doesn't suppress it.
            try
            {
                const string sql = "UPDATE ControlKey SET MetaControl = 1 WHERE SettingKey = @key AND MetaControl = 0;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = RespawnKey });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to set MetaControl for {RespawnKey}: {ex.Message}");
            }
        }

        private static void EnsureAllowGameInputFreezeIsEnum()
        {
            // Register enum options so ControlStateManager can load the index correctly.
            // Options: 0=Limited, 1=Focus, 2=MouseLeave, 3=None. Default index 1 (Focus).
            ControlStateManager.RegisterEnumOptions(AllowGameInputFreezeKey, AllowGameInputFreezeOptions, defaultIndex: 1, persist: true);

            try
            {
                // Convert from SaveSwitch to SaveEnum if still the old type.
                const string migrateSql = "UPDATE ControlKey SET InputType = 'SaveEnum' WHERE SettingKey = @key AND InputType IN ('SaveSwitch', 'Switch');";
                DatabaseQuery.ExecuteNonQuery(migrateSql, new System.Collections.Generic.Dictionary<string, object> { ["@key"] = AllowGameInputFreezeKey });

                // Ensure the control exists as SaveEnum with default index 1 (Focus).
                ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
                {
                    SettingKey = AllowGameInputFreezeKey,
                    InputKey = "Shift + C",
                    InputType = "SaveEnum",
                    SwitchStartState = 1,
                    MetaControl = true,
                    RenderOrder = 14
                });
                ControlKeyData.SetInputType(AllowGameInputFreezeKey, "SaveEnum");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to migrate AllowGameInputFreeze to SaveEnum: {ex.Message}");
            }
        }

        private static void EnsureCameraLockModeControl()
        {
            // Register enum options before loading so LoadControlSwitchStates can clamp the index.
            ControlStateManager.RegisterEnumOptions(CameraLockModeKey, CameraLockModeOptions, defaultIndex: 0, persist: true);

            // Migrate any existing SaveSwitch row to SaveEnum (index 0 = Locked).
            try
            {
                const string migrateSql = "UPDATE ControlKey SET InputType = 'SaveEnum', SwitchStartState = 0 WHERE SettingKey = @key AND InputType IN ('SaveSwitch', 'Switch', 'NoSaveSwitch');";
                DatabaseQuery.ExecuteNonQuery(migrateSql, new Dictionary<string, object> { ["@key"] = CameraLockModeKey });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to migrate {CameraLockModeKey} to SaveEnum: {ex.Message}");
            }

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey       = CameraLockModeKey,
                InputKey         = "C",
                InputType        = "SaveEnum",
                SwitchStartState = 0,
                MetaControl      = true,
                RenderOrder      = 26
            });

            ControlKeyData.SetInputType(CameraLockModeKey, "SaveEnum");
            ControlKeyData.EnsureSwitchStartState(CameraLockModeKey, 0);

            // Force SwitchStartState to 0 (Locked) — handles migration from old index order
            // where Locked was index 2 and any stale saved config values.
            try
            {
                const string setDefaultSql = "UPDATE ControlKey SET SwitchStartState = 0 WHERE SettingKey = @key AND SwitchStartState != 0;";
                DatabaseQuery.ExecuteNonQuery(setDefaultSql, new Dictionary<string, object> { ["@key"] = CameraLockModeKey });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to update {CameraLockModeKey} default to Locked: {ex.Message}");
            }

            ControlKeyData.EnsureInputKey(CameraLockModeKey, "C");
        }

        private static void EnsureCameraSnapToPlayerControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey       = CameraSnapToPlayerKey,
                InputKey         = "Ctrl + Space",
                InputType        = "Trigger",
                SwitchStartState = 0,
                MetaControl      = true,
                RenderOrder      = 27
            });

            ControlKeyData.SetInputType(CameraSnapToPlayerKey, "Trigger");
            ControlKeyData.SetInputKey(CameraSnapToPlayerKey, "Ctrl + Space");
        }

        private static void EnsureFloatStartStateColumn()
        {
            const string column = "FloatStartState";
            if (!ControlKeyData.ColumnExists(column))
            {
                ControlKeyData.AddColumn(column, "REAL DEFAULT NULL");
            }
        }

        private static void EnsureScrollControls()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey  = ScrollInKey,
                InputKey    = "ScrollUp",
                InputType   = "Trigger",
                MetaControl = true,
                RenderOrder = 28
            });

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey  = ScrollOutKey,
                InputKey    = "ScrollDown",
                InputType   = "Trigger",
                MetaControl = true,
                RenderOrder = 29
            });

            ControlKeyData.SetInputType(ScrollInKey,  "Trigger");
            ControlKeyData.SetInputType(ScrollOutKey, "Trigger");

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey     = ScrollMinDistanceKey,
                InputKey       = "",
                InputType      = "Float",
                FloatStartState = 200f,
                MetaControl    = true,
                RenderOrder    = 30
            });

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey     = ScrollMaxDistanceKey,
                InputKey       = "",
                InputType      = "Float",
                FloatStartState = 2000f,
                MetaControl    = true,
                RenderOrder    = 31
            });

            ControlKeyData.EnsureFloatStartState(ScrollMinDistanceKey, 200f);
            ControlKeyData.EnsureFloatStartState(ScrollMaxDistanceKey, 2000f);

            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey      = ScrollIncrementKey,
                InputKey        = "",
                InputType       = "Float",
                FloatStartState = 120f,
                MetaControl     = true,
                RenderOrder     = 32
            });

            ControlKeyData.EnsureFloatStartState(ScrollIncrementKey, 120f);
        }

        private static void EnsureCtrlBufferControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey      = CtrlBufferKey,
                InputKey        = "",
                InputType       = "Float",
                FloatStartState = 0.2f,
                MetaControl     = true,
                RenderOrder     = 33
            });

            ControlKeyData.EnsureFloatStartState(CtrlBufferKey, 0.2f);
        }

        private static void RemoveMouseSensitivityMultiplierControl()
        {
            try
            {
                const string sql = "DELETE FROM ControlKey WHERE SettingKey = 'MouseSensitivityMultiplier';";
                DatabaseQuery.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to remove MouseSensitivityMultiplier: {ex.Message}");
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

        /// <summary>
        /// Forces CameraLockMode to Locked (index 0) in both the database and in-memory state.
        /// Called from GameInitializer after LoadControlSwitchStates because UpsertBindings
        /// may overwrite CameraLockMode with a stale saved config value.
        /// </summary>
        internal static void ForceCameraLockModeDefault()
        {
            try
            {
                const string sql = "UPDATE ControlKey SET SwitchStartState = 0 WHERE SettingKey = @key AND SwitchStartState != 0;";
                DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@key"] = CameraLockModeKey });

                if (ControlStateManager.ContainsEnumState(CameraLockModeKey))
                {
                    int currentIndex = ControlStateManager.GetEnumIndex(CameraLockModeKey);
                    if (currentIndex != 0)
                    {
                        ControlStateManager.SetEnumIndex(CameraLockModeKey, 0, "ForceCameraLockModeDefault");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to force {CameraLockModeKey} default to Locked: {ex.Message}");
            }
        }

        private static void EnsureShowHiddenAttrsControl()
        {
            ControlKeyData.EnsureControlExists(new ControlKeyData.ControlKeyRecord
            {
                SettingKey       = ShowHiddenAttrsKey,
                InputKey         = "",
                InputType        = "SaveSwitch",
                SwitchStartState = 1,
                MetaControl      = true,
                RenderOrder      = 34
            });

            ControlKeyData.SetInputType(ShowHiddenAttrsKey, "SaveSwitch");
            ControlKeyData.EnsureSwitchStartState(ShowHiddenAttrsKey, 1);
        }

        private static void EnsureControlTooltips()
        {
            const string sql = "INSERT OR IGNORE INTO UITooltips (RowKey, TooltipText) VALUES (@key, @tip);";
            var tooltips = new (string Key, string Tip)[]
            {
                (InspectModeState.InspectModeKey,   "Toggle inspect mode to hover and examine game objects."),
                (TabSwitchRequiresBlockModeKey,     "When enabled, the tab key only switches panels while Block Menu is open."),
                ("Fire",                            "Fire the equipped weapon."),
                ("BarrelLeft",                      "Rotate barrel selection counter-clockwise."),
                ("BarrelRight",                     "Rotate barrel selection clockwise."),
                (CombatTextKey,                     "Toggle floating damage numbers and XP text during combat."),
                (CameraLockModeKey,                 "Camera follow mode: Free (no follow), Scout (always centered), or Locked (fixed offset)."),
                (CameraSnapToPlayerKey,             "Snap the camera to center on the player. In Locked mode, resets the offset."),
                (RespawnKey,                        "Respawn the player after death."),
                (ScrollInKey,                       "Scroll in (zoom in) the camera view."),
                (ScrollOutKey,                      "Scroll out (zoom out) the camera view."),
                (ScrollMinDistanceKey,              "Minimum camera scroll distance (closest zoom)."),
                (ScrollMaxDistanceKey,              "Maximum camera scroll distance (furthest zoom)."),
                (ScrollIncrementKey,               "Scroll wheel units per zoom step (default 120 = one notch)."),
                (CtrlBufferKey,                     "Seconds after releasing Ctrl that a Ctrl+key combo still registers (e.g. release Ctrl then press Space within this window)."),
                (ShowHiddenAttrsKey,                "Default visibility of hidden attributes in the Properties block. Per-object overrides are remembered separately."),
            };
            foreach (var (key, tip) in tooltips)
            {
                try
                {
                    DatabaseQuery.ExecuteNonQuery(sql, new Dictionary<string, object>
                    {
                        ["@key"] = key,
                        ["@tip"] = tip
                    });
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintError($"Failed to insert tooltip for {key}: {ex.Message}");
                }
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
