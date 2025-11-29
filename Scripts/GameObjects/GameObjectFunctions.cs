using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameObjectFunctions
    {
        public static Vector2 GetGOLocalScreenPosition(GameObject gameObject)
        {
            GameObject target = gameObject ?? Core.Instance.Player;

            if (target == null)
            {
                DebugLogger.PrintError("Target GameObject is null. Make sure the object is properly initialized.");
                return new Vector2(0, 0);
            }

            Vector2 position = new(target.Position.X, target.Position.Y);

            Vector2 screenPosition = new((int)position.X, (int)position.Y);

            DebugLogger.PrintUI($"GameObject screen position calculated: {screenPosition}");

            return screenPosition;
        }

        public static Vector2 GetGOGlobalScreenPosition(GameObject gameObject)
        {
            GameObject target = gameObject ?? Core.Instance.Player;

            if (target == null)
            {
                DebugLogger.PrintError("Target GameObject is null. Make sure the object is properly initialized.");
                return new Vector2(0, 0);
            }

            Vector2 localScreenPosition = GetGOLocalScreenPosition(target);

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

            // Translate client coordinates to absolute screen coordinates so the cursor aligns with the rendered center.
            POINT clientPoint = new()
            {
                X = (int)localScreenPosition.X,
                Y = (int)localScreenPosition.Y
            };

            if (!ClientToScreen(windowHandle, ref clientPoint))
            {
                DebugLogger.PrintError("Failed to translate client coordinates to screen coordinates.");
                return localScreenPosition;
            }

            Vector2 globalScreenPosition = new(clientPoint.X, clientPoint.Y);

            DebugLogger.PrintUI($"Player global screen position calculated: {globalScreenPosition}");

            return globalScreenPosition;
        }

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        private struct POINT
        {
            public int X;
            public int Y;
        }
    }
}
