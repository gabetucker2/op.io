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

            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("Config path cannot be null, empty, or whitespace.", nameof(configPath));

            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Config file not found at path: {configPath}", configPath);

            string json = File.ReadAllText(configPath);

            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Config file is empty or contains only whitespace.");

            return JsonDocument.Parse(json);
        }

        public static Color GetColor(JsonDocument doc, string section, string key, Color defaultColor)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "JsonDocument cannot be null.");

            if (string.IsNullOrWhiteSpace(section))
                throw new ArgumentException("Section name cannot be null, empty, or whitespace.", nameof(section));

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key name cannot be null, empty, or whitespace.", nameof(key));

            try
            {
                if (doc.RootElement.TryGetProperty(section, out var sectionElement) &&
                    sectionElement.TryGetProperty(key, out var colorElement))
                {
                    return ParseColor(colorElement);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve color for section '{section}', key '{key}'.", ex);
            }

            return defaultColor;
        }

        public static Color GetColor(JsonElement element, Color defaultColor)
        {
            try
            {
                return ParseColor(element);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse color from JsonElement.", ex);
            }
        }

        private static Color ParseColor(JsonElement element)
        {
            if (!element.TryGetProperty("R", out var rProp) ||
                !element.TryGetProperty("G", out var gProp) ||
                !element.TryGetProperty("B", out var bProp) ||
                !element.TryGetProperty("A", out var aProp))
            {
                throw new ArgumentException("JsonElement does not contain valid color properties.");
            }

            int r = rProp.GetInt32();
            int g = gProp.GetInt32();
            int b = bProp.GetInt32();
            int a = aProp.GetInt32();

            if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255 || a < 0 || a > 255)
                throw new ArgumentOutOfRangeException("Color values must be between 0 and 255.");

            return new Color(r, g, b, a);
        }

        public static T GetJSON<T>(JsonDocument doc, string section, string key, T defaultValue = default)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "JsonDocument cannot be null.");

            if (string.IsNullOrWhiteSpace(section))
                throw new ArgumentException("Section name cannot be null, empty, or whitespace.", nameof(section));

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key name cannot be null, empty, or whitespace.", nameof(key));

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
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve JSON value for section '{section}', key '{key}'.", ex);
            }

            return defaultValue;
        }
    }
}
