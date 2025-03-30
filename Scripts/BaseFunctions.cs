using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class BaseFunctions
    {
        public static T GetValue<T>(
            string tableName,
            string column,
            string conditionColumn,
            object conditionValue,
            T defaultValue = default,
            bool suppressLog = false)
        {
            string query = $"SELECT {column} FROM {tableName} WHERE {conditionColumn} = @value LIMIT 1;";
            var result = DatabaseQuery.ExecuteQuery(query, new Dictionary<string, object> { { "@value", conditionValue } });

            if (result.Count > 0 && result[0].ContainsKey(column))
            {
                return (T)Convert.ChangeType(result[0][column], typeof(T));
            }

            if (!suppressLog)
                DebugLogger.PrintWarning($"[WARNING] Defaulting {column} from {tableName} to {defaultValue}");

            return defaultValue;
        }


        /// <summary>
        /// Retrieves a color value from the database.
        /// </summary>
        public static Color GetColor(string tableName, string conditionColumn, object conditionValue, Color defaultColor)
        {
            try
            {
                int r = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_R", defaultColor.R);
                int g = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_G", defaultColor.G);
                int b = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_B", defaultColor.B);
                int a = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_A", defaultColor.A);

                Color color = new Color(r, g, b, a);
                DebugLogger.Print($"Retrieved color from {tableName} -> {color}");
                return color;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to retrieve color from {tableName}: {ex.Message}");
                return defaultColor;
            }
        }

        /// <summary>
        /// Parses a color from a JSON document.
        /// </summary>
        public static Color ParseColor(JsonElement element)
        {
            try
            {
                int r = element.GetProperty("R").GetInt32();
                int g = element.GetProperty("G").GetInt32();
                int b = element.GetProperty("B").GetInt32();
                int a = element.GetProperty("A").GetInt32();

                Color color = new Color(r, g, b, a);
                DebugLogger.Print($"Parsed color from JSON -> {color}");
                return color;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to parse color from JSON: {ex.Message}");
                return Color.White; // Default fallback
            }
        }
    }
}
