using System.Text.Json;
using System;
using Microsoft.Xna.Framework;
using System.IO;

namespace op.io
{
    public static class BaseFunctions
    {
        public static JsonDocument Config()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Config.json");
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Config file not found at path: {configPath}");

            string json = File.ReadAllText(configPath);
            return JsonDocument.Parse(json);
        }

        public static Color GetColor(JsonDocument doc, string section, string key, Color defaultColor)
        {
            try
            {
                if (doc.RootElement.TryGetProperty(section, out var sectionElement) &&
                    sectionElement.TryGetProperty(key, out var colorElement))
                {
                    return ParseColor(colorElement);
                }
            }
            catch (Exception)
            {
                // Swallow exception and return default color
            }
            return defaultColor;
        }

        public static Color GetColor(JsonElement element, Color defaultColor)
        {
            try
            {
                return ParseColor(element);
            }
            catch (Exception)
            {
                // Swallow exception and return default color
            }
            return defaultColor;
        }

        private static Color ParseColor(JsonElement element)
        {
            int r = element.GetProperty("R").GetInt32();
            int g = element.GetProperty("G").GetInt32();
            int b = element.GetProperty("B").GetInt32();
            int a = element.GetProperty("A").GetInt32();
            return new Color(r, g, b, a);
        }

        public static T GetJSON<T>(JsonDocument doc, string section, string key, T defaultValue = default)
        {
            try
            {
                if (doc.RootElement.TryGetProperty(section, out var sectionElement) &&
                    sectionElement.TryGetProperty(key, out var keyElement))
                {
                    // Deserialize directly for complex types
                    if (typeof(T) == typeof(JsonElement)) return (T)(object)keyElement;
                    if (typeof(T) == typeof(string)) return (T)(object)keyElement.GetString();
                    if (typeof(T) == typeof(int)) return (T)(object)keyElement.GetInt32();
                    if (typeof(T) == typeof(float)) return (T)(object)keyElement.GetSingle();
                    if (typeof(T) == typeof(bool)) return (T)(object)keyElement.GetBoolean();

                    // If it's not a known type, attempt JSON deserialization
                    return JsonSerializer.Deserialize<T>(keyElement.GetRawText());
                }
            }
            catch (Exception)
            {
                // Swallow exception and return default value
            }
            return defaultValue;
        }
    }
}