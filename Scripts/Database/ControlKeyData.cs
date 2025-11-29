using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io
{
    internal static class ControlKeyData
    {
        private const string RenderOrderColumnName = "RenderOrder";

        internal sealed class ControlKeyRecord
        {
            public string SettingKey { get; init; }
            public string InputKey { get; init; }
            public string InputType { get; init; }
            public int? SwitchStartState { get; init; }
            public bool MetaControl { get; init; }
            public int RenderOrder { get; init; }
            public bool LockMode { get; init; }
        }

        public static ControlKeyRecord GetControl(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return null;
            }

            var parameters = new Dictionary<string, object>
            {
                ["@settingKey"] = settingKey
            };

            const string sql = @"
SELECT SettingKey, InputKey, InputType, SwitchStartState, MetaControl, COALESCE(RenderOrder, 0) AS ControlOrder, COALESCE(LockMode, 0) AS LockMode
FROM ControlKey
WHERE SettingKey = @settingKey
LIMIT 1;";

            var rows = DatabaseQuery.ExecuteQuery(sql, parameters);
            if (rows.Count == 0)
            {
                return null;
            }

            var row = rows[0];
            return new ControlKeyRecord
            {
                SettingKey = row["SettingKey"]?.ToString(),
                InputKey = row["InputKey"]?.ToString(),
                InputType = row["InputType"]?.ToString(),
                SwitchStartState = row["SwitchStartState"] == DBNull.Value ? null : Convert.ToInt32(row["SwitchStartState"]),
                MetaControl = row.TryGetValue("MetaControl", out object metaValue) && Convert.ToInt32(metaValue) != 0,
                RenderOrder = row.TryGetValue("ControlOrder", out object orderValue) ? Convert.ToInt32(orderValue) : 0,
                LockMode = row.TryGetValue("LockMode", out object lockValue) && Convert.ToInt32(lockValue) != 0
            };
        }

        public static void InsertControl(ControlKeyRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.SettingKey))
            {
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                ["@settingKey"] = record.SettingKey,
                ["@inputKey"] = record.InputKey ?? string.Empty,
                ["@inputType"] = record.InputType ?? "Hold",
                ["@metaControl"] = record.MetaControl ? 1 : 0,
                ["@switchStartState"] = record.SwitchStartState,
                ["@lockMode"] = record.LockMode ? 1 : 0
            };

            bool orderColumnAvailable = ColumnExists(RenderOrderColumnName);
            bool lockModeAvailable = ColumnExists("LockMode");
            if (orderColumnAvailable)
            {
                parameters["@renderOrder"] = record.RenderOrder > 0 ? record.RenderOrder : GetNextRenderOrderValue();
            }

            string sql;
            if (orderColumnAvailable && lockModeAvailable)
            {
                sql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode)
VALUES (@settingKey, @inputKey, @inputType, @metaControl, @renderOrder, @switchStartState, @lockMode);";
            }
            else if (orderColumnAvailable)
            {
                sql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState)
VALUES (@settingKey, @inputKey, @inputType, @metaControl, @renderOrder, @switchStartState);";
            }
            else if (lockModeAvailable)
            {
                sql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, SwitchStartState, LockMode)
VALUES (@settingKey, @inputKey, @inputType, @metaControl, @switchStartState, @lockMode);";
            }
            else
            {
                sql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, SwitchStartState)
VALUES (@settingKey, @inputKey, @inputType, @metaControl, @switchStartState);";
            }

            DatabaseQuery.ExecuteNonQuery(sql, parameters);
        }

        public static void EnsureControlExists(ControlKeyRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.SettingKey))
            {
                return;
            }

            if (GetControl(record.SettingKey) != null)
            {
                return;
            }

            InsertControl(record);
        }

        public static void SetInputType(string settingKey, string inputType)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(inputType))
            {
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                ["@inputType"] = inputType,
                ["@settingKey"] = settingKey
            };

            const string sql = "UPDATE ControlKey SET InputType = @inputType WHERE SettingKey = @settingKey;";
            DatabaseQuery.ExecuteNonQuery(sql, parameters);
        }

        public static void EnsureSwitchStartState(string settingKey, int defaultValue)
        {
            var parameters = new Dictionary<string, object>
            {
                ["@settingKey"] = settingKey,
                ["@defaultState"] = defaultValue
            };

            const string sql = @"
UPDATE ControlKey
SET SwitchStartState = COALESCE(SwitchStartState, @defaultState)
WHERE SettingKey = @settingKey;";

            DatabaseQuery.ExecuteNonQuery(sql, parameters);
        }

        public static void EnsureInputKey(string settingKey, string inputKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(inputKey))
            {
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                ["@settingKey"] = settingKey,
                ["@inputKey"] = inputKey
            };

            const string sql = @"
UPDATE ControlKey
SET InputKey = @inputKey
WHERE SettingKey = @settingKey
  AND (InputKey IS NULL OR TRIM(InputKey) = '');";

            DatabaseQuery.ExecuteNonQuery(sql, parameters);
        }

        public static void ClearSwitchStartState(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                ["@settingKey"] = settingKey
            };

            const string sql = "UPDATE ControlKey SET SwitchStartState = NULL WHERE SettingKey = @settingKey;";
            DatabaseQuery.ExecuteNonQuery(sql, parameters);
        }

        public static bool ColumnExists(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return false;
            }

            const string sql = "PRAGMA table_info(ControlKey);";
            var rows = DatabaseQuery.ExecuteQuery(sql);
            return rows.Any(row =>
                row.TryGetValue("name", out object value) &&
                string.Equals(Convert.ToString(value), columnName, StringComparison.OrdinalIgnoreCase));
        }

        public static void AddColumn(string columnName, string definition)
        {
            if (string.IsNullOrWhiteSpace(columnName) || string.IsNullOrWhiteSpace(definition))
            {
                return;
            }

            DatabaseQuery.ExecuteNonQuery($"ALTER TABLE ControlKey ADD COLUMN {columnName} {definition};");
        }

        public static void NormalizeRenderOrderValues()
        {
            if (!ColumnExists(RenderOrderColumnName))
            {
                return;
            }

            const string sql = "SELECT SettingKey FROM ControlKey ORDER BY RenderOrder, SettingKey;";
            var rows = DatabaseQuery.ExecuteQuery(sql);
            if (rows.Count == 0)
            {
                return;
            }

            int order = 1;
            foreach (var row in rows)
            {
                if (!row.TryGetValue("SettingKey", out object keyValue))
                {
                    continue;
                }

                string settingKey = keyValue?.ToString();
                if (string.IsNullOrWhiteSpace(settingKey))
                {
                    continue;
                }

                var parameters = new Dictionary<string, object>
                {
                    ["@order"] = order++,
                    ["@settingKey"] = settingKey
                };

                DatabaseQuery.ExecuteNonQuery(@"UPDATE ControlKey SET RenderOrder = @order WHERE SettingKey = @settingKey;", parameters);
            }
        }

        public static void UpdateRenderOrders(IReadOnlyList<(string SettingKey, int Order)> updates)
        {
            if (updates == null || updates.Count == 0 || !ColumnExists(RenderOrderColumnName))
            {
                return;
            }

            foreach ((string settingKey, int order) in updates)
            {
                if (string.IsNullOrWhiteSpace(settingKey) || order <= 0)
                {
                    continue;
                }

                var parameters = new Dictionary<string, object>
                {
                    ["@order"] = order,
                    ["@settingKey"] = settingKey
                };

                DatabaseQuery.ExecuteNonQuery(@"UPDATE ControlKey SET RenderOrder = @order WHERE SettingKey = @settingKey;", parameters);
            }
        }

        private static int GetNextRenderOrderValue()
        {
            if (!ColumnExists(RenderOrderColumnName))
            {
                return 0;
            }

            const string sql = "SELECT COALESCE(MAX(RenderOrder), 0) AS MaxOrder FROM ControlKey;";
            var rows = DatabaseQuery.ExecuteQuery(sql);
            if (rows.Count == 0)
            {
                return 1;
            }

            int max = rows[0].TryGetValue("MaxOrder", out object value) ? Convert.ToInt32(value) : 0;
            return max + 1;
        }

        public static void ApplyMetaControlFlags(IReadOnlyCollection<string> metaControls)
        {
            string placeholderList = string.Empty;
            Dictionary<string, object> parameters = null;

            if (metaControls != null && metaControls.Count > 0)
            {
                parameters = new Dictionary<string, object>();
                var placeholders = new List<string>();
                int index = 0;

                foreach (string settingKey in metaControls)
                {
                    string parameterName = $"@meta{index++}";
                    placeholders.Add(parameterName);
                    parameters[parameterName] = settingKey;
                }

                placeholderList = string.Join(", ", placeholders);
            }

            string sql;
            if (string.IsNullOrWhiteSpace(placeholderList))
            {
                sql = "UPDATE ControlKey SET MetaControl = 0;";
            }
            else
            {
                sql = $@"
UPDATE ControlKey
SET MetaControl = CASE WHEN SettingKey IN ({placeholderList}) THEN 1 ELSE 0 END;";
            }

            DatabaseQuery.ExecuteNonQuery(sql, parameters);
        }
    }
}
