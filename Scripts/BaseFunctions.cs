using System.Text.Json;
using System;
using Microsoft.Xna.Framework;
using System.IO;

namespace op.io.Scripts
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
                var element = doc.RootElement.GetProperty(section).GetProperty(key);
                int r = element.GetProperty("R").GetInt32();
                int g = element.GetProperty("G").GetInt32();
                int b = element.GetProperty("B").GetInt32();
                int a = element.GetProperty("A").GetInt32();
                return new Color(r, g, b, a);
            }
            catch (Exception)
            {
                return defaultColor;
            }
        }

        public static T GetJSON<T>(JsonDocument doc, string section, string key, T defaultValue = default)
        {
            try
            {
                var element = doc.RootElement.GetProperty(section).GetProperty(key);
                return (T)Convert.ChangeType(element.GetRawText(), typeof(T));
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }
    }
}