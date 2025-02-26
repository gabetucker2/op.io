using System.Text.Json;
using System;
using Microsoft.Xna.Framework;
using System.IO;

namespace op.io
{
    public static class BaseFunctions
    {
        private static readonly string DataDirectory = "Data/";

        public static JsonDocument LoadJson(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            string fullPath = Path.Combine(DataDirectory, fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"JSON file not found: {fullPath}");

            string jsonText = File.ReadAllText(fullPath);
            return JsonDocument.Parse(jsonText);
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

        public static Color GetColor(JsonElement colorElement)
        {
            if (!colorElement.TryGetProperty("R", out var r) ||
                !colorElement.TryGetProperty("G", out var g) ||
                !colorElement.TryGetProperty("B", out var b) ||
                !colorElement.TryGetProperty("A", out var a))
            {
                throw new ArgumentException("Color must include R, G, B, and A properties.");
            }

            return new Color(
                r.GetByte(),
                g.GetByte(),
                b.GetByte(),
                a.GetByte()
            );
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
