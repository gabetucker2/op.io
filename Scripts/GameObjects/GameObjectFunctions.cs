using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameObjectFunctions
    {
        public static Vector2 GetGOLocalScreenPosition(GameObject gameObject)
        {
            var player = Core.Instance.Player;

            if (player == null)
            {
                DebugLogger.PrintError("Player instance is null. Make sure the player is properly initialized.");
                return new Vector2(0, 0);
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
            var player = Core.Instance.Player;

            if (player == null)
            {
                DebugLogger.PrintError("Player instance is null. Make sure the player is properly initialized.");
                return new Vector2(0, 0);
            }

            // Get the local screen position of the player
            Vector2 localScreenPosition = GetGOLocalScreenPosition(gameObject);

            if (BlockManager.TryProjectGameToWindow(localScreenPosition, out Vector2 projectedPosition))
            {
                localScreenPosition = projectedPosition;
            }

            DebugLogger.PrintUI($"Local screen position of player: {localScreenPosition}");

            IntPtr windowHandle = Core.Instance?.Window?.Handle ?? IntPtr.Zero;

            if (windowHandle == IntPtr.Zero)
            {
                DebugLogger.PrintError("Failed to retrieve valid game window handle.");
                return localScreenPosition;
            }

            DebugLogger.PrintUI($"Window Handle Retrieved: {windowHandle}");

            if (!GetWindowRect(windowHandle, out RECT rect))
            {
                DebugLogger.PrintError("Failed to retrieve window rectangle for global screen position.");
                return localScreenPosition;
            }

            int windowX = rect.Left;
            int windowY = rect.Top;
            DebugLogger.PrintUI($"Window rectangle retrieved: X={windowX}, Y={windowY}");

            // Calculate global screen position
            Vector2 globalScreenPosition = new(
                localScreenPosition.X + windowX,
                localScreenPosition.Y + windowY
            );

            DebugLogger.PrintUI($"Player global screen position calculated: {globalScreenPosition}");

            return globalScreenPosition;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
