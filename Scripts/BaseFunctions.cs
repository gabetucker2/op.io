using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using SDL2;

namespace op.io
{
    public static class BaseFunctions
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        public static T GetValue<T>(
            string tableName,
            string column,
            string conditionColumn,
            object conditionValue,
            bool suppressLog = false)
        {
            string query = $"SELECT {column} FROM {tableName} WHERE {conditionColumn} = @value LIMIT 1;";
            var result = DatabaseQuery.ExecuteQuery(query, new Dictionary<string, object> { { "@value", conditionValue } });

            if (result.Count > 0 && result[0].ContainsKey(column))
            {
                return (T)Convert.ChangeType(result[0][column], typeof(T));
            }

            if (!suppressLog)
                DebugLogger.PrintError($"No data collected in {column} from {tableName}");

            return default;
        }


        /// <summary>
        /// Retrieves a color value from the database.
        /// </summary>
        public static Color GetColor(string tableName, string conditionColumn, object conditionValue, Color defaultColor)
        {
            try
            {
                int r = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_R");
                int g = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_G");
                int b = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_B");
                int a = GetValue<int>(tableName, "Value", "SettingKey", $"{conditionValue}_A");

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

        public static Vector2 GetGOLocalScreenPosition(GameObject gameObject)
        {
            var player = Player.InstancePlayer;

            if (player == null)
            {
                DebugLogger.PrintError("Player instance is null. Make sure the player is properly initialized.");
                return new Vector2(0,0);
            }

            // Assuming Player's position is stored as a Vector2
            Vector2 playerPosition = new(player.Position.X, player.Position.Y);

            // Convert player's position to screen coordinates
            Vector2 screenPosition = new((int)playerPosition.X, (int)playerPosition.Y);

            DebugLogger.PrintUI($"Player screen position calculated: {screenPosition}");

            return screenPosition;
        }

        public static Vector2 GetGOGlobalScreenPosition(GameObject gameObject)
        {
            var player = Player.InstancePlayer;

            if (player == null)
            {
                DebugLogger.PrintError("Player instance is null. Make sure the player is properly initialized.");
                return new Vector2(0, 0);
            }

            // Get the local screen position of the player
            Vector2 localScreenPosition = GetGOLocalScreenPosition(gameObject);
            DebugLogger.PrintUI($"Local screen position of player: {localScreenPosition}");

            IntPtr windowHandle = Core.InstanceCore?.Window?.Handle ?? IntPtr.Zero;

            if (windowHandle == IntPtr.Zero)
            {
                DebugLogger.PrintError("Failed to retrieve valid game window handle.");
                return localScreenPosition;
            }

            DebugLogger.PrintUI($"Window Handle Retrieved: {windowHandle}");

            // SDL requires window handle from SDL2 functions
            int windowX = 0, windowY = 0;
            SDL.SDL_GetWindowPosition(windowHandle, out windowX, out windowY);

            if (windowX == 0 && windowY == 0)
            {
                DebugLogger.PrintError("Failed to retrieve SDL window position. Ensure SDL2 is correctly integrated.");
                return localScreenPosition;
            }

            DebugLogger.PrintUI($"SDL Window Position Retrieved: X={windowX}, Y={windowY}");

            // Calculate global screen position
            Vector2 globalScreenPosition = new(
                localScreenPosition.X + windowX,
                localScreenPosition.Y + windowY
            );

            DebugLogger.PrintUI($"Player global screen position calculated: {globalScreenPosition}");

            return globalScreenPosition;
        }

        public static System.Drawing.Point Vector2ToPoint(Vector2 vector)
        {
            return new System.Drawing.Point((int)vector.X, (int)vector.Y);
        }

    }
}
