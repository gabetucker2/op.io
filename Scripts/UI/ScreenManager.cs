using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class ScreenManager
    {
        public static void ApplyWindowMode(Core game)
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

            switch (game.WindowMode)
            {
                case WindowMode.BorderedWindowed:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = false;
                    game.Window.AllowUserResizing = true;
                    SyncViewportToClient(game);
                    AttachResizeHandler(game);
                    break;

                case WindowMode.BorderlessWindowed:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    game.Window.AllowUserResizing = false;
                    SyncViewportToClient(game);
                    DetachResizeHandler();
                    break;

                case WindowMode.BorderlessFullscreen:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    game.Window.AllowUserResizing = false;
                    game.ViewportWidth = display.Width;       // Match display width
                    game.ViewportHeight = display.Height;     // Match display height
                    game.Graphics.PreferredBackBufferWidth = display.Width;
                    game.Graphics.PreferredBackBufferHeight = display.Height;
                    DetachResizeHandler();
                    break;

                case WindowMode.LegacyFullscreen:
                    game.Graphics.IsFullScreen = true;
                    game.Window.IsBorderless = false;
                    game.Window.AllowUserResizing = false;
                    game.ViewportWidth = display.Width;       // Match display width
                    game.ViewportHeight = display.Height;     // Match display height
                    game.Graphics.PreferredBackBufferWidth = display.Width;
                    game.Graphics.PreferredBackBufferHeight = display.Height;
                    DetachResizeHandler();
                    break;
            }

            game.Graphics.ApplyChanges();
            GameInitializer.ApplyWindowCaptionColor(UIStyle.DragBarBackground);
            GameInitializer.RefreshTransparencyKey();
            DebugLogger.PrintUI($"Applied WindowMode: {game.WindowMode}, Resolution: {game.ViewportWidth}x{game.ViewportHeight}");
        }

        private static Core _resizeTarget;
        private static bool _dockingModesInitialized;
        private static WindowMode _dockingEnabledWindowMode = WindowMode.BorderedWindowed;
        private static WindowMode _dockingDisabledWindowMode = WindowMode.BorderlessWindowed;
        private static Point? _desiredClientTopLeft;

        public static void ApplyDockingWindowChrome(Core game, bool dockingEnabled)
        {
            if (game == null || game.Graphics == null || game.Window == null)
            {
                return;
            }

            InitializeDockingWindowModes(game);

            WindowMode targetMode = dockingEnabled ? _dockingEnabledWindowMode : _dockingDisabledWindowMode;
            if (game.WindowMode == targetMode)
            {
                return;
            }

            IntPtr windowHandle = game.Window.Handle;
            bool sourceWindowed = IsWindowedMode(game.WindowMode);
            bool targetWindowed = IsWindowedMode(targetMode);
            bool wasMaximized = sourceWindowed && IsWindowMaximized(windowHandle);
            Point? previousClientTopLeft = sourceWindowed && targetWindowed
                ? GetClientTopLeftOnScreen(windowHandle)
                : null;

            Rectangle targetWorkArea = Rectangle.Empty;
            bool snapToWorkArea = !dockingEnabled && wasMaximized && targetWindowed;
            if (snapToWorkArea)
            {
                targetWorkArea = GetMonitorWorkArea(windowHandle);
            }

            Point? desiredClientTopLeft = snapToWorkArea
                ? new Point(targetWorkArea.Left, targetWorkArea.Top)
                : previousClientTopLeft;

            game.WindowMode = targetMode;
            ApplyWindowMode(game);
            if (snapToWorkArea)
            {
                ApplyWorkAreaBounds(game, windowHandle, targetWorkArea);
            }
            ForceImmediateClear(game);
            PreserveClientTopLeft(windowHandle, desiredClientTopLeft);
            _desiredClientTopLeft = desiredClientTopLeft;
            if (snapToWorkArea)
            {
                EnsureWindowWithinWorkArea(windowHandle, targetWorkArea);
            }
        }

        private static void InitializeDockingWindowModes(Core game)
        {
            if (_dockingModesInitialized || game == null)
            {
                return;
            }

            // When docking is enabled, show window chrome; when disabled, prefer borderless unless already in fullscreen.
            _dockingEnabledWindowMode = WindowMode.BorderedWindowed;

            _dockingDisabledWindowMode = game.WindowMode == WindowMode.BorderlessFullscreen
                ? WindowMode.BorderlessFullscreen
                : WindowMode.BorderlessWindowed;

            _dockingModesInitialized = true;
        }

        private static void AttachResizeHandler(Core game)
        {
            if (_resizeTarget == game)
            {
                return;
            }

            DetachResizeHandler();
            _resizeTarget = game;
            game.Window.ClientSizeChanged += OnClientSizeChanged;
        }

        private static void DetachResizeHandler()
        {
            if (_resizeTarget == null)
            {
                return;
            }

            _resizeTarget.Window.ClientSizeChanged -= OnClientSizeChanged;
            _resizeTarget = null;
        }

        private static void OnClientSizeChanged(object sender, EventArgs e)
        {
            if (_resizeTarget == null)
            {
                return;
            }

            int newWidth = _resizeTarget.Window.ClientBounds.Width;
            int newHeight = _resizeTarget.Window.ClientBounds.Height;

            if (newWidth <= 0 || newHeight <= 0)
            {
                return;
            }

            if (newWidth == _resizeTarget.ViewportWidth && newHeight == _resizeTarget.ViewportHeight)
            {
                return;
            }

            _resizeTarget.ViewportWidth = newWidth;
            _resizeTarget.ViewportHeight = newHeight;
            _resizeTarget.Graphics.PreferredBackBufferWidth = newWidth;
            _resizeTarget.Graphics.PreferredBackBufferHeight = newHeight;
            _resizeTarget.Graphics.ApplyChanges();
            EnsurePendingClientAnchor(_resizeTarget.Window?.Handle ?? IntPtr.Zero);
        }

        private static bool IsWindowedMode(WindowMode mode)
        {
            return mode == WindowMode.BorderedWindowed || mode == WindowMode.BorderlessWindowed;
        }

        private static Point? GetClientTopLeftOnScreen(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            if (!GetClientRect(hwnd, out NativeRect clientRect))
            {
                return null;
            }

            NativePoint topLeft = new() { X = clientRect.Left, Y = clientRect.Top };
            if (!ClientToScreen(hwnd, ref topLeft))
            {
                return null;
            }

            return new Point(topLeft.X, topLeft.Y);
        }

        private static void PreserveClientTopLeft(IntPtr hwnd, Point? desiredClientTopLeft)
        {
            if (hwnd == IntPtr.Zero || desiredClientTopLeft == null)
            {
                return;
            }

            Point? currentTopLeft = GetClientTopLeftOnScreen(hwnd);
            if (currentTopLeft == null)
            {
                return;
            }

            int deltaX = desiredClientTopLeft.Value.X - currentTopLeft.Value.X;
            int deltaY = desiredClientTopLeft.Value.Y - currentTopLeft.Value.Y;
            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            if (!GetWindowRect(hwnd, out NativeRect windowRect))
            {
                return;
            }

            int newX = windowRect.Left + deltaX;
            int newY = windowRect.Top + deltaY;

            const uint flags = SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            if (!SetWindowPos(hwnd, IntPtr.Zero, newX, newY, 0, 0, flags))
            {
                DebugLogger.PrintUI("Failed to reposition window after toggling docking chrome.");
            }
        }

        private static void ForceImmediateClear(Core game)
        {
            try
            {
                GraphicsDevice device = game?.GraphicsDevice;
                if (device == null)
                {
                    return;
                }

                device.SetRenderTarget(null);
                Color clearColor = game?.BackgroundColor != default
                    ? game.BackgroundColor
                    : UIStyle.DragBarBackground;
                device.Clear(clearColor);

                MethodInfo present = device.GetType().GetMethod("Present", BindingFlags.Instance | BindingFlags.Public);
                present?.Invoke(device, null);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintUI($"Immediate clear after docking toggle failed: {ex.Message}");
            }
        }

        private static void SyncViewportToClient(Core game)
        {
            try
            {
                Rectangle bounds = game?.Window?.ClientBounds ?? Rectangle.Empty;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return;
                }

                game.ViewportWidth = bounds.Width;
                game.ViewportHeight = bounds.Height;
                game.Graphics.PreferredBackBufferWidth = bounds.Width;
                game.Graphics.PreferredBackBufferHeight = bounds.Height;
            }
            catch
            {
                // Ignore sync failures; resize handler will correct later.
            }
        }

        private static void EnsurePendingClientAnchor(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || _desiredClientTopLeft == null)
            {
                return;
            }

            Point? current = GetClientTopLeftOnScreen(hwnd);
            if (current == null)
            {
                return;
            }

            int deltaX = _desiredClientTopLeft.Value.X - current.Value.X;
            int deltaY = _desiredClientTopLeft.Value.Y - current.Value.Y;
            if (Math.Abs(deltaX) <= 1 && Math.Abs(deltaY) <= 1)
            {
                _desiredClientTopLeft = null;
                return;
            }

            const uint flags = SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            SetWindowPos(hwnd, IntPtr.Zero, current.Value.X + deltaX, current.Value.Y + deltaY, 0, 0, flags);
        }

        private static bool IsWindowMaximized(IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero && IsZoomed(hwnd);
        }

        private static Rectangle GetMonitorWorkArea(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                NativeMonitorInfo info = new() { Size = Marshal.SizeOf<NativeMonitorInfo>() };
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    NativeRect work = info.Work;
                    int width = work.Right - work.Left;
                    int height = work.Bottom - work.Top;
                    if (width > 0 && height > 0)
                    {
                        return new Rectangle(work.Left, work.Top, width, height);
                    }
                }
            }

            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Rectangle(0, 0, display.Width, display.Height);
        }

        private static void ApplyWorkAreaBounds(Core game, IntPtr hwnd, Rectangle workArea)
        {
            if (game?.Graphics == null || hwnd == IntPtr.Zero || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return;
            }

            game.ViewportWidth = workArea.Width;
            game.ViewportHeight = workArea.Height;
            game.Graphics.PreferredBackBufferWidth = workArea.Width;
            game.Graphics.PreferredBackBufferHeight = workArea.Height;
            game.Graphics.ApplyChanges();

            const uint flags = SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            SetWindowPos(hwnd, IntPtr.Zero, workArea.Left, workArea.Top, workArea.Width, workArea.Height, flags);
        }

        private static void EnsureWindowWithinWorkArea(IntPtr hwnd, Rectangle workArea)
        {
            if (hwnd == IntPtr.Zero || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return;
            }

            if (!GetWindowRect(hwnd, out NativeRect windowRect))
            {
                return;
            }

            int width = windowRect.Right - windowRect.Left;
            int height = windowRect.Bottom - windowRect.Top;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            int targetX = Math.Clamp(windowRect.Left, workArea.Left, workArea.Right - width);
            int targetY = Math.Clamp(windowRect.Top, workArea.Top, workArea.Bottom - height);

            const uint flags = SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            SetWindowPos(hwnd, IntPtr.Zero, targetX, targetY, width, height, flags);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfo lpmi);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    }
}
