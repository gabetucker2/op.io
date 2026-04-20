using System.Globalization;
using Microsoft.Xna.Framework;

namespace op.io
{
    internal static class CentifootUnits
    {
        // Copied from the current pink octagon farm prototype diameter (90).
        // This is an independent unit baseline and is intentionally not tied
        // to runtime farm object dimensions.
        public const float WorldUnitsPerCentifoot = 90f;
        public const string UnitName = "centifoot";
        public const string UnitAbbreviation = "cft";

        public static float WorldToCentifoot(float worldUnits)
        {
            return worldUnits / WorldUnitsPerCentifoot;
        }

        public static float CentifootToWorld(float centifoot)
        {
            return centifoot * WorldUnitsPerCentifoot;
        }

        public static string FormatDistance(float worldUnits, string format = "0.###")
        {
            return $"{FormatNumber(WorldToCentifoot(worldUnits), format)} {UnitAbbreviation}";
        }

        public static string FormatSpeed(float worldUnitsPerSecond, string format = "0.###")
        {
            return $"{FormatNumber(WorldToCentifoot(worldUnitsPerSecond), format)} {UnitAbbreviation}/s";
        }

        public static string FormatVector2(Vector2 worldPosition, string format = "0.###")
        {
            float x = WorldToCentifoot(worldPosition.X);
            float y = WorldToCentifoot(worldPosition.Y);
            return $"{FormatNumber(x, format)}, {FormatNumber(y, format)} {UnitAbbreviation}";
        }

        public static string FormatDimensions(float widthWorldUnits, float heightWorldUnits, string format = "0.###")
        {
            float width = WorldToCentifoot(widthWorldUnits);
            float height = WorldToCentifoot(heightWorldUnits);
            return $"{FormatNumber(width, format)} x {FormatNumber(height, format)} {UnitAbbreviation}";
        }

        public static string FormatNumber(float value, string format = "0.###")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
