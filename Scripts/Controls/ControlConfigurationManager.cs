using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.BlockScripts.Blocks;

namespace op.io
{
    internal static class ControlConfigurationManager
    {
        private const string ActiveSetupRowKey = "__ActiveControlSetup";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        internal sealed class ControlConfiguration
        {
            public List<ControlBindingSnapshot> Bindings { get; set; } = new();
            public Dictionary<string, string> RowData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> RowOrders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        internal sealed class ControlBindingSnapshot
        {
            public string SettingKey { get; set; }
            public string InputKey { get; set; }
            public string InputType { get; set; }
            public int? SwitchStartState { get; set; }
            public bool MetaControl { get; set; }
            public int RenderOrder { get; set; }
            public bool LockMode { get; set; }
        }

        public static IReadOnlyList<string> ListConfigurations()
        {
            EnsureTables();
            Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.ControlSetups);
            return data.Keys
                .Where(name => !string.Equals(name, ActiveSetupRowKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string GetActiveConfigurationName()
        {
            EnsureTables();
            try
            {
                Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.ControlSetups);
                return data.TryGetValue(ActiveSetupRowKey, out string stored)
                    ? NormalizeName(stored)
                    : null;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load active control setup: {ex.Message}");
                return null;
            }
        }

        public static bool TrySave(string name, out string error)
        {
            error = null;
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Enter a configuration name.";
                return false;
            }

            EnsureTables();

            string payload = Serialize(CaptureSnapshot(), out error);
            if (!string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            BlockDataStore.SetRowData(DockBlockKind.ControlSetups, name, payload);
            PersistActiveName(name);
            return true;
        }

        public static bool TryCreate(string name, out string error)
        {
            error = null;
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Enter a configuration name.";
                return false;
            }

            EnsureTables();

            if (ListConfigurations().Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
            {
                error = "That name already exists.";
                return false;
            }

            return TrySave(name, out error);
        }

        public static bool TryRename(string oldName, string newName, out string error)
        {
            error = null;
            oldName = NormalizeName(oldName);
            newName = NormalizeName(newName);

            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            {
                error = "Select a configuration and enter a new name.";
                return false;
            }

            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Choose a different name.";
                return false;
            }

            EnsureTables();
            Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.ControlSetups);
            if (!data.TryGetValue(oldName, out string payload) || string.IsNullOrWhiteSpace(payload))
            {
                error = "Unable to find that configuration.";
                return false;
            }

            if (data.ContainsKey(newName))
            {
                error = "That name already exists.";
                return false;
            }

            BlockDataStore.SetRowData(DockBlockKind.ControlSetups, newName, payload);
            BlockDataStore.DeleteRows(DockBlockKind.ControlSetups, new[] { oldName });

            string active = GetActiveConfigurationName();
            if (string.Equals(active, oldName, StringComparison.OrdinalIgnoreCase))
            {
                PersistActiveName(newName);
            }

            return true;
        }

        public static bool TryDelete(string name, out string error)
        {
            error = null;
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Select a configuration to delete.";
                return false;
            }

            EnsureTables();
            BlockDataStore.DeleteRows(DockBlockKind.ControlSetups, new[] { name });

            string active = GetActiveConfigurationName();
            if (string.Equals(active, name, StringComparison.OrdinalIgnoreCase))
            {
                PersistActiveName(string.Empty);
            }

            return true;
        }

        public static bool TryApply(string name, out string error)
        {
            error = null;
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Select a configuration to load.";
                return false;
            }

            EnsureTables();
            Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.ControlSetups);
            if (!data.TryGetValue(name, out string payload) || string.IsNullOrWhiteSpace(payload))
            {
                error = "Selected configuration has no data.";
                return false;
            }

            ControlConfiguration config = Deserialize(payload, out error);
            if (!string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            if (!ApplyConfiguration(config, out error))
            {
                return false;
            }

            PersistActiveName(name);
            return true;
        }

        public static bool TryApplyAdjacent(int direction, out string error)
        {
            error = null;
            IReadOnlyList<string> names = ListConfigurations();
            if (names.Count == 0)
            {
                error = "No saved control configurations.";
                return false;
            }

            string active = GetActiveConfigurationName();
            int currentIndex = names.ToList().FindIndex(n => string.Equals(n, active, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int targetIndex = (currentIndex + direction) % names.Count;
            if (targetIndex < 0)
            {
                targetIndex += names.Count;
            }

            string target = names[targetIndex];
            return TryApply(target, out error);
        }

        private static void EnsureTables()
        {
            BlockDataStore.EnsureTables(null, DockBlockKind.ControlSetups, DockBlockKind.Controls);
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static ControlConfiguration CaptureSnapshot()
        {
            var payload = new ControlConfiguration();

            const string sql = @"
SELECT SettingKey, InputKey, InputType, COALESCE(SwitchStartState, 0) AS SwitchStartState, COALESCE(MetaControl, 0) AS MetaControl, COALESCE(RenderOrder, 0) AS ControlOrder, COALESCE(LockMode, 0) AS LockMode
FROM ControlKey
ORDER BY ControlOrder ASC, SettingKey ASC;";

            var rows = DatabaseQuery.ExecuteQuery(sql);
            foreach (var row in rows)
            {
                string settingKey = row.TryGetValue("SettingKey", out object settingObj) ? settingObj?.ToString() : null;
                if (string.IsNullOrWhiteSpace(settingKey))
                {
                    continue;
                }

                payload.Bindings.Add(new ControlBindingSnapshot
                {
                    SettingKey = settingKey,
                    InputKey = row.TryGetValue("InputKey", out object inputObj) ? inputObj?.ToString() ?? string.Empty : string.Empty,
                    InputType = row.TryGetValue("InputType", out object typeObj) ? typeObj?.ToString() ?? "Hold" : "Hold",
                    SwitchStartState = row.TryGetValue("SwitchStartState", out object switchObj) && switchObj != null && switchObj != DBNull.Value ? Convert.ToInt32(switchObj) : 0,
                    MetaControl = row.TryGetValue("MetaControl", out object metaObj) && metaObj != null && metaObj != DBNull.Value && Convert.ToInt32(metaObj) != 0,
                    RenderOrder = row.TryGetValue("ControlOrder", out object orderObj) ? Convert.ToInt32(orderObj) : 0,
                    LockMode = row.TryGetValue("LockMode", out object lockObj) && lockObj != null && lockObj != DBNull.Value && Convert.ToInt32(lockObj) != 0
                });
            }

            payload.RowData = BlockDataStore.LoadRowData(DockBlockKind.Controls);
            payload.RowOrders = BlockDataStore.LoadRowOrders(DockBlockKind.Controls);
            return payload;
        }

        private static bool ApplyConfiguration(ControlConfiguration config, out string error)
        {
            error = null;
            if (config == null || config.Bindings == null || config.Bindings.Count == 0)
            {
                error = "Configuration is empty.";
                return false;
            }

            Dictionary<string, string> rowData = config.RowData ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            UpsertBindings(config.Bindings);
            ApplyRowData(rowData);
            ApplyRowOrders(config.RowOrders, config.Bindings);
            ApplyRuntimeBindings(config.Bindings, rowData);
            ControlsBlock.InvalidateCache();
            return true;
        }

        private static void UpsertBindings(IEnumerable<ControlBindingSnapshot> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            const string sql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, SwitchStartState, RenderOrder, LockMode)
VALUES (@settingKey, @inputKey, @inputType, @metaControl, @switchStartState, @renderOrder, @lockMode)
ON CONFLICT(SettingKey) DO UPDATE SET
    InputKey = excluded.InputKey,
    InputType = excluded.InputType,
    MetaControl = excluded.MetaControl,
    SwitchStartState = excluded.SwitchStartState,
    RenderOrder = excluded.RenderOrder,
    LockMode = excluded.LockMode;";

            foreach (ControlBindingSnapshot binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding?.SettingKey))
                {
                    continue;
                }

                var parameters = new Dictionary<string, object>
                {
                    ["@settingKey"] = binding.SettingKey,
                    ["@inputKey"] = binding.InputKey ?? string.Empty,
                    ["@inputType"] = string.IsNullOrWhiteSpace(binding.InputType) ? "Hold" : binding.InputType,
                    ["@metaControl"] = binding.MetaControl ? 1 : 0,
                    ["@switchStartState"] = binding.SwitchStartState ?? 0,
                    ["@renderOrder"] = Math.Max(1, binding.RenderOrder),
                    ["@lockMode"] = binding.LockMode ? 1 : 0
                };

                DatabaseQuery.ExecuteNonQuery(sql, parameters);
            }
        }

        private static void ApplyRowData(Dictionary<string, string> rowData)
        {
            if (rowData == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in rowData)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                BlockDataStore.SetRowData(DockBlockKind.Controls, pair.Key, pair.Value ?? string.Empty);
            }
        }

        private static void ApplyRowOrders(Dictionary<string, int> rowOrders, IReadOnlyCollection<ControlBindingSnapshot> bindings)
        {
            Dictionary<string, int> merged = BlockDataStore.LoadRowOrders(DockBlockKind.Controls);
            int maxOrder = merged.Count == 0 ? 0 : merged.Values.Max();

            if (rowOrders != null && rowOrders.Count > 0)
            {
                foreach (var pair in rowOrders.OrderBy(p => p.Value))
                {
                    if (pair.Value <= 0 || string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    merged[pair.Key] = pair.Value;
                    if (pair.Value > maxOrder)
                    {
                        maxOrder = pair.Value;
                    }
                }
            }

            if (bindings != null)
            {
                foreach (ControlBindingSnapshot binding in bindings)
                {
                    if (string.IsNullOrWhiteSpace(binding?.SettingKey) || merged.ContainsKey(binding.SettingKey))
                    {
                        continue;
                    }

                    merged[binding.SettingKey] = ++maxOrder;
                }
            }

            var rows = merged
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Value)
                .Select(pair => (pair.Key, pair.Value))
                .ToList();

            if (rows.Count > 0)
            {
                BlockDataStore.SaveRowOrders(DockBlockKind.Controls, rows);
                ControlKeyData.UpdateRenderOrders(rows);
            }
        }

        private static void ApplyRuntimeBindings(IEnumerable<ControlBindingSnapshot> bindings, IReadOnlyDictionary<string, string> rowData)
        {
            if (bindings == null)
            {
                return;
            }

            foreach (ControlBindingSnapshot binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.SettingKey))
                {
                    continue;
                }

                string normalizedKey = BlockDataStore.CanonicalizeRowKey(DockBlockKind.Controls, binding.SettingKey);
                bool triggerAuto = false;
                InputType parsedType = ParseInputTypeLabel(binding.InputType);
                if (rowData != null &&
                    rowData.TryGetValue(normalizedKey, out string stored) &&
                    TryParseRowData(stored, out InputType storedType, out bool storedTrigger))
                {
                    if (!IsPersistentSwitch(storedType))
                    {
                        parsedType = storedType;
                    }
                    triggerAuto = storedTrigger;
                }

                bool isLocked = binding.LockMode || InputManager.IsTypeLocked(binding.SettingKey);
                bool hasInputKey = !string.IsNullOrWhiteSpace(binding.InputKey);

                if (!isLocked)
                {
                    if (hasInputKey)
                    {
                        if (!InputManager.TryUpdateBindingInputKey(binding.SettingKey, binding.InputKey, out _))
                        {
                            DebugLogger.PrintWarning($"Failed to update input key for '{binding.SettingKey}'.");
                        }
                    }
                    else
                    {
                        InputManager.TryUnbind(binding.SettingKey);
                    }
                }

                InputManager.UpdateBindingInputType(binding.SettingKey, parsedType, triggerAuto);

                if (IsSwitchType(parsedType))
                {
                    bool switchOn = TypeConversionFunctions.IntToBool(binding.SwitchStartState ?? 0);
                    bool persist = parsedType != InputType.NoSaveSwitch;
                    ControlStateManager.RegisterSwitchPersistence(binding.SettingKey, persist);
                    ControlStateManager.SetSwitchState(binding.SettingKey, switchOn, "ControlConfigurationManager.ApplyRuntimeBindings");
                }
            }
        }

        private static string Serialize(ControlConfiguration payload, out string error)
        {
            error = null;
            try
            {
                return JsonSerializer.Serialize(payload, SerializerOptions);
            }
            catch (Exception ex)
            {
                error = $"Failed to serialize configuration: {ex.Message}";
                return null;
            }
        }

        private static ControlConfiguration Deserialize(string payload, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                error = "Configuration payload is empty.";
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ControlConfiguration>(payload, SerializerOptions);
            }
            catch (Exception ex)
            {
                error = $"Failed to read configuration: {ex.Message}";
                return null;
            }
        }

        private static void PersistActiveName(string name)
        {
            try
            {
                BlockDataStore.SetRowData(DockBlockKind.ControlSetups, ActiveSetupRowKey, name ?? string.Empty);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist active control setup: {ex.Message}");
            }
        }

        private static bool TryParseRowData(string data, out InputType inputType, out bool triggerAuto)
        {
            inputType = InputType.Hold;
            triggerAuto = false;

            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            string[] parts = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            inputType = ParseInputTypeLabel(parts[0]);

            if (IsSwitchType(inputType) || inputType == InputType.Hold)
            {
                triggerAuto = false;
                return true;
            }

            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedAuto))
            {
                triggerAuto = parsedAuto != 0;
            }

            return true;
        }

        private static InputType ParseInputTypeLabel(string typeLabel)
        {
            if (string.Equals(typeLabel, "Switch", StringComparison.OrdinalIgnoreCase))
            {
                return InputType.SaveSwitch;
            }

            return Enum.TryParse(typeLabel, true, out InputType parsed) ? parsed : InputType.Hold;
        }

        private static bool IsSwitchType(InputType inputType) =>
            inputType == InputType.SaveSwitch || inputType == InputType.NoSaveSwitch;

        private static bool IsPersistentSwitch(InputType inputType) => inputType == InputType.SaveSwitch;
    }
}
