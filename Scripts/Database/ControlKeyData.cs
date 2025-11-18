using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io
{
    internal static class ControlKeyData
    {
        internal sealed class ControlKeyRecord
        {
            public string SettingKey { get; init; }
            public string InputKey { get; init; }
            public string InputType { get; init; }
            public int? SwitchStartState { get; init; }
            public bool MetaControl { get; init; }
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
SELECT SettingKey, InputKey, InputType, SwitchStartState, MetaControl
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
                MetaControl = row.TryGetValue("MetaControl", out object metaValue) && Convert.ToInt32(metaValue) != 0
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
                ["@switchStartState"] = record.SwitchStartState,
                ["@metaControl"] = record.MetaControl ? 1 : 0
            };

            const string sql = @"
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl)
VALUES (@settingKey, @inputKey, @inputType, @switchStartState, @metaControl);";

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
