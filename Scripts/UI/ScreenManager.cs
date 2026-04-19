using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class ScreenManager
    {
        public static bool NativeWindowResizeEdgesEnabled => _nativeWindowResizeEdgesEnabled;
        public static bool CustomDockingResizeEdgesEnabled => _customDockingResizeEdgesEnabled;

        public enum DockingWindowResizeEdge
        {
            Top,
            Left,
            Right,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        public static void ApplyWindowMode(Core game)
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            bool nativeResizeEdgesEnabled = ShouldEnableNativeResizeEdges(game);

            switch (game.WindowMode)
            {
                case WindowMode.BorderedWindowed:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = false;
                    game.Window.AllowUserResizing = IsWindowResizeAllowed(game);
                    SyncViewportToClient(game);
                    AttachResizeHandler(game);
                    break;

                case WindowMode.BorderlessWindowed:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    game.Window.AllowUserResizing = IsWindowResizeAllowed(game);
                    SyncViewportToClient(game);
                    AttachResizeHandler(game);
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
                    nativeResizeEdgesEnabled = false;
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
                    nativeResizeEdgesEnabled = false;
                    break;
            }

            game.Graphics.ApplyChanges();
            _nativeWindowResizeEdgesEnabled = nativeResizeEdgesEnabled;
            _customDockingResizeEdgesEnabled = ShouldEnableCustomDockingResizeEdges(game);
            ConfigureNativeResizeFrame(game.Window?.Handle ?? IntPtr.Zero, game.WindowMode);
            GameInitializer.ApplyWindowIcon(game);
            GameInitializer.ApplyWindowCaptionColor(UIStyle.DragBarBackground);
            GameInitializer.RefreshTransparencyKey();
            DebugLogger.PrintUI($"Applied WindowMode: {game.WindowMode}, Resolution: {game.ViewportWidth}x{game.ViewportHeight}");
        }

        public static void CenterWindowOnCurrentMonitor(Core game)
        {
            if (game == null || game.Window == null || !IsWindowedMode(game.WindowMode))
            {
                return;
            }

            IntPtr hwnd = game.Window.Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            NativeWindowPlacement placement = new() { Length = (uint)Marshal.SizeOf<NativeWindowPlacement>() };
            bool hasPlacement = GetWindowPlacement(hwnd, ref placement);
            bool isMinimized = IsIconic(hwnd) || (hasPlacement && placement.ShowCmd == SW_SHOWMINIMIZED);

            int outerWidth = 0;
            int outerHeight = 0;
            bool hasOuterRect = GetWindowRect(hwnd, out NativeRect windowRect);
            if (hasOuterRect)
            {
                outerWidth = windowRect.Right - windowRect.Left;
                outerHeight = windowRect.Bottom - windowRect.Top;
            }

            if (isMinimized && hasPlacement)
            {
                int normalWidth = placement.NormalPosition.Right - placement.NormalPosition.Left;
                int normalHeight = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
                if (normalWidth > 64 && normalHeight > 64)
                {
                    outerWidth = normalWidth;
                    outerHeight = normalHeight;
                }
            }

            if (outerWidth <= 64 || outerHeight <= 64)
            {
                if (!TryGetExpectedWindowOuterSize(game, hwnd, out outerWidth, out outerHeight))
                {
                    int fallbackClientWidth = Math.Max(game.ViewportWidth, game.Graphics?.PreferredBackBufferWidth ?? 0);
                    int fallbackClientHeight = Math.Max(game.ViewportHeight, game.Graphics?.PreferredBackBufferHeight ?? 0);
                    if (fallbackClientWidth <= 0 || fallbackClientHeight <= 0)
                    {
                        return;
                    }

                    outerWidth = fallbackClientWidth;
                    outerHeight = fallbackClientHeight;
                }
            }

            int clientWidth = game.Window.ClientBounds.Width;
            int clientHeight = game.Window.ClientBounds.Height;
            if (clientWidth <= 1 || clientHeight <= 1)
            {
                clientWidth = Math.Max(game.ViewportWidth, game.Graphics?.PreferredBackBufferWidth ?? 0);
                clientHeight = Math.Max(game.ViewportHeight, game.Graphics?.PreferredBackBufferHeight ?? 0);
            }

            if (clientWidth <= 0 || clientHeight <= 0)
            {
                return;
            }

            Rectangle workArea = GetMonitorWorkArea(hwnd);
            if (workArea.Width <= 0 || workArea.Height <= 0)
            {
                return;
            }

            int targetOuterX = workArea.Left + (workArea.Width - outerWidth) / 2;
            int targetOuterY = workArea.Top + (workArea.Height - outerHeight) / 2;
            targetOuterX = Math.Clamp(targetOuterX, workArea.Left, Math.Max(workArea.Left, workArea.Right - outerWidth));
            targetOuterY = Math.Clamp(targetOuterY, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - outerHeight));

            // Persist restore position as well so minimizing/restoring or late startup style
            // changes cannot knock the window off-screen.
            if (hasPlacement)
            {
                placement.NormalPosition = new NativeRect
                {
                    Left = targetOuterX,
                    Top = targetOuterY,
                    Right = targetOuterX + outerWidth,
                    Bottom = targetOuterY + outerHeight
                };

                SetWindowPlacement(hwnd, ref placement);
            }

            if (isMinimized)
            {
                _desiredClientTopLeft = null;
                return;
            }

            const uint centerFlags = SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            SetWindowPos(hwnd, IntPtr.Zero, targetOuterX, targetOuterY, outerWidth, outerHeight, centerFlags);

            int targetClientX = workArea.Left + (workArea.Width - clientWidth) / 2;
            int targetClientY = workArea.Top + (workArea.Height - clientHeight) / 2;

            int maxClientX = Math.Max(workArea.Left, workArea.Right - clientWidth);
            int maxClientY = Math.Max(workArea.Top, workArea.Bottom - clientHeight);
            targetClientX = Math.Clamp(targetClientX, workArea.Left, maxClientX);
            targetClientY = Math.Clamp(targetClientY, workArea.Top, maxClientY);

            // Client-anchored centering is robust against border/caption sizing and late startup
            // frame adjustments. Pending anchor guarantees a follow-up correction if needed.
            _desiredClientTopLeft = new Point(targetClientX, targetClientY);
            EnsurePendingClientAnchor(hwnd);
        }

        public static void RefreshWindowResizeIntegration(Core game)
        {
            if (game == null || game.Window == null)
            {
                return;
            }

            bool windowedMode = IsWindowedMode(game.WindowMode);
            bool nativeResizeEdgesEnabled = ShouldEnableNativeResizeEdges(game);
            game.Window.AllowUserResizing = IsWindowResizeAllowed(game);
            if (windowedMode)
            {
                AttachResizeHandler(game);
            }
            else
            {
                DetachResizeHandler();
            }

            _nativeWindowResizeEdgesEnabled = nativeResizeEdgesEnabled;
            _customDockingResizeEdgesEnabled = ShouldEnableCustomDockingResizeEdges(game);
            ConfigureNativeResizeFrame(game.Window.Handle, game.WindowMode);
        }

        private static Core _resizeTarget;
        private static bool _dockingModesInitialized;
        private static WindowMode _dockingEnabledWindowMode = WindowMode.BorderedWindowed;
        private static WindowMode _dockingDisabledWindowMode = WindowMode.BorderlessWindowed;
        private static Point? _desiredClientTopLeft;
        private static ResizePaintHook _resizePaintHook;
        private static IntPtr _resizeHookHandle;
        private static bool _inResizeDraw;
        private static bool _nativeWindowResizeEdgesEnabled;
        private static bool _customDockingResizeEdgesEnabled;

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
                RefreshWindowResizeIntegration(game);
                if (dockingEnabled)
                {
                    GameInitializer.ApplyWindowCaptionColor(UIStyle.DragBarBackground);
                }

                return;
            }

            IntPtr windowHandle = game.Window.Handle;
            bool sourceWindowed = IsWindowedMode(game.WindowMode);
            bool targetWindowed = IsWindowedMode(targetMode);
            bool wasMaximized = sourceWindowed && IsWindowMaximized(windowHandle);
            bool preserveOuterBounds = sourceWindowed && targetWindowed && !wasMaximized;
            Rectangle previousOuterBounds = Rectangle.Empty;
            if (preserveOuterBounds && GetWindowRect(windowHandle, out NativeRect outerRect))
            {
                int outerWidth = outerRect.Right - outerRect.Left;
                int outerHeight = outerRect.Bottom - outerRect.Top;
                if (outerWidth > 0 && outerHeight > 0)
                {
                    previousOuterBounds = new Rectangle(outerRect.Left, outerRect.Top, outerWidth, outerHeight);
                }
                else
                {
                    preserveOuterBounds = false;
                }
            }
            else if (preserveOuterBounds)
            {
                preserveOuterBounds = false;
            }

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

            if (preserveOuterBounds)
            {
                _desiredClientTopLeft = null;
                PreserveWindowOuterBounds(windowHandle, previousOuterBounds);
            }
            else
            {
                PreserveClientTopLeft(windowHandle, desiredClientTopLeft);
            }

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
            if (game?.Window == null)
            {
                return;
            }

            IntPtr hwnd = game.Window.Handle;
            bool sameTarget = _resizeTarget == game;
            bool sameHook = _resizePaintHook != null && _resizeHookHandle == hwnd && hwnd != IntPtr.Zero;

            if (!sameTarget)
            {
                DetachResizeHandler();
                _resizeTarget = game;
                game.Window.ClientSizeChanged += OnClientSizeChanged;
            }
            else if (sameHook)
            {
                return;
            }

            if (_resizePaintHook != null && !sameHook)
            {
                _resizePaintHook.Detach();
                _resizePaintHook = null;
                _resizeHookHandle = IntPtr.Zero;
            }

            if (hwnd != IntPtr.Zero)
            {
                _resizePaintHook = new ResizePaintHook(hwnd);
                _resizeHookHandle = hwnd;
            }
        }

        private static void DetachResizeHandler()
        {
            if (_resizeTarget == null)
            {
                return;
            }

            _resizeTarget.Window.ClientSizeChanged -= OnClientSizeChanged;
            _resizeTarget = null;

            _resizePaintHook?.Detach();
            _resizePaintHook = null;
            _resizeHookHandle = IntPtr.Zero;
            _inResizeDraw = false;
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

            IntPtr hwnd = _resizeTarget.Window?.Handle ?? IntPtr.Zero;

            // Snapshot the outer window position before ApplyChanges() so we can undo any
            // spurious repositioning that MonoGame does internally (e.g. re-centering the
            // window based on the new back-buffer size). GetWindowRect is used instead of
            // ClientToScreen because ClientToScreen can return unreliable coordinates inside
            // the live-resize modal loop. Only snapshot when no pending chrome-toggle anchor
            // is active — that anchor takes priority via EnsurePendingClientAnchor.
            NativeRect preRect = default;
            bool hasPre = hwnd != IntPtr.Zero
                && _desiredClientTopLeft == null
                && GetWindowRect(hwnd, out preRect);

            _resizeTarget.ViewportWidth = newWidth;
            _resizeTarget.ViewportHeight = newHeight;
            _resizeTarget.Graphics.PreferredBackBufferWidth = newWidth;
            _resizeTarget.Graphics.PreferredBackBufferHeight = newHeight;
            _resizeTarget.Graphics.ApplyChanges();

            if (_desiredClientTopLeft != null)
            {
                EnsurePendingClientAnchor(hwnd);
            }
            else if (hasPre && GetWindowRect(hwnd, out NativeRect postRect))
            {
                int deltaX = preRect.Left - postRect.Left;
                int deltaY = preRect.Top - postRect.Top;
                if (Math.Abs(deltaX) > 1 || Math.Abs(deltaY) > 1)
                {
                    const uint flags = SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
                    SetWindowPos(hwnd, IntPtr.Zero, postRect.Left + deltaX, postRect.Top + deltaY, 0, 0, flags);
                }
            }

            // Invalidate the window so WM_PAINT fires after the swap chain is resized.
            // The ResizePaintHook handles WM_PAINT by running a game tick, which is the
            // context the DWM actually flushes composition from during the modal resize loop.
            InvalidateRect(_resizeTarget.Window?.Handle ?? IntPtr.Zero, IntPtr.Zero, false);
        }

        private static void RunResizeDraw()
        {
            if (_inResizeDraw)
            {
                DebugLogger.PrintUI("[ScreenManager] [RunResizeDraw] Blocked: already in resize draw.");
                return;
            }

            if (_resizeTarget?.SpriteBatch == null)
                return;

            if (GameRenderer.IsDrawing)
            {
                DebugLogger.PrintUI("[ScreenManager] [RunResizeDraw] Blocked: GameRenderer.Draw() is already executing (re-entrant WndProc call).");
                return;
            }

            _inResizeDraw = true;
            try
            {
                DebugLogger.PrintUI("[ScreenManager] [RunResizeDraw] Starting resize draw.");
                GameRenderer.Draw();
                _resizeTarget.GraphicsDevice.Present();
                DebugLogger.PrintUI("[ScreenManager] [RunResizeDraw] Resize draw complete.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"[ScreenManager] [RunResizeDraw] Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                try { _resizeTarget?.GraphicsDevice?.SetRenderTarget(null); } catch { }
            }
            finally
            {
                _inResizeDraw = false;
            }
        }

        private static bool IsWindowedMode(WindowMode mode)
        {
            return mode == WindowMode.BorderedWindowed || mode == WindowMode.BorderlessWindowed;
        }

        private static bool IsWindowResizeAllowed(Core game)
        {
            return game != null && IsWindowedMode(game.WindowMode) && BlockManager.DockingModeEnabled;
        }

        private static bool ShouldEnableCustomDockingResizeEdges(Core game)
        {
            return game != null && IsWindowedMode(game.WindowMode) && BlockManager.DockingModeEnabled;
        }

        private static bool ShouldEnableNativeResizeEdges(Core game)
        {
            return IsWindowResizeAllowed(game) && !ShouldEnableCustomDockingResizeEdges(game);
        }

        public static bool TryGetWindowBounds(Core game, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (game?.Window == null)
            {
                return false;
            }

            IntPtr hwnd = game.Window.Handle;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out NativeRect rect))
            {
                return false;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            bounds = new Rectangle(rect.Left, rect.Top, width, height);
            return true;
        }

        public static bool TryGetCursorScreenPosition(out Point screenPosition)
        {
            screenPosition = Point.Zero;
            if (!GetCursorPos(out NativePoint cursor))
            {
                return false;
            }

            screenPosition = new Point(cursor.X, cursor.Y);
            return true;
        }

        public static bool TryResizeWindowFromDockingEdgeDelta(Core game, DockingWindowResizeEdge edge, Rectangle dragStartWindowBounds, Point dragDeltaScreen)
        {
            if (game?.Window == null ||
                !CustomDockingResizeEdgesEnabled ||
                !IsWindowedMode(game.WindowMode))
            {
                return false;
            }

            IntPtr hwnd = game.Window.Handle;
            if (hwnd == IntPtr.Zero || IsWindowMaximized(hwnd) || dragStartWindowBounds.Width <= 0 || dragStartWindowBounds.Height <= 0)
            {
                return false;
            }

            int left = dragStartWindowBounds.Left;
            int top = dragStartWindowBounds.Top;
            int right = dragStartWindowBounds.Right;
            int bottom = dragStartWindowBounds.Bottom;

            switch (edge)
            {
                case DockingWindowResizeEdge.Top:
                    top += dragDeltaScreen.Y;
                    break;
                case DockingWindowResizeEdge.Left:
                    left += dragDeltaScreen.X;
                    break;
                case DockingWindowResizeEdge.Right:
                    right += dragDeltaScreen.X;
                    break;
                case DockingWindowResizeEdge.Bottom:
                    bottom += dragDeltaScreen.Y;
                    break;
                case DockingWindowResizeEdge.TopLeft:
                    top += dragDeltaScreen.Y;
                    left += dragDeltaScreen.X;
                    break;
                case DockingWindowResizeEdge.TopRight:
                    top += dragDeltaScreen.Y;
                    right += dragDeltaScreen.X;
                    break;
                case DockingWindowResizeEdge.BottomLeft:
                    left += dragDeltaScreen.X;
                    bottom += dragDeltaScreen.Y;
                    break;
                case DockingWindowResizeEdge.BottomRight:
                    right += dragDeltaScreen.X;
                    bottom += dragDeltaScreen.Y;
                    break;
            }

            int minWidth = Math.Max(320, Math.Max(UIStyle.MinBlockSize, GetSystemMetrics(SM_CXMINTRACK)));
            int minHeight = Math.Max(220, Math.Max(UIStyle.MinBlockSize, GetSystemMetrics(SM_CYMINTRACK)));

            GetDockingResizeClampBounds(hwnd, out int minLeft, out int minTop, out int maxRight, out int maxBottom);

            if (edge is DockingWindowResizeEdge.Top)
            {
                top = Math.Clamp(top, minTop, bottom - minHeight);
            }

            if (edge is DockingWindowResizeEdge.TopLeft or DockingWindowResizeEdge.TopRight)
            {
                top = Math.Clamp(top, minTop, bottom - minHeight);
            }

            if (edge is DockingWindowResizeEdge.Left or DockingWindowResizeEdge.BottomLeft)
            {
                left = Math.Clamp(left, minLeft, right - minWidth);
            }

            if (edge is DockingWindowResizeEdge.TopLeft)
            {
                left = Math.Clamp(left, minLeft, right - minWidth);
            }

            if (edge is DockingWindowResizeEdge.Right or DockingWindowResizeEdge.BottomRight)
            {
                int minRight = left + minWidth;
                right = Math.Clamp(right, minRight, Math.Max(minRight, maxRight));
            }

            if (edge is DockingWindowResizeEdge.TopRight)
            {
                int minRight = left + minWidth;
                right = Math.Clamp(right, minRight, Math.Max(minRight, maxRight));
            }

            if (edge is DockingWindowResizeEdge.Bottom or DockingWindowResizeEdge.BottomLeft or DockingWindowResizeEdge.BottomRight)
            {
                int minBottom = top + minHeight;
                bottom = Math.Clamp(bottom, minBottom, Math.Max(minBottom, maxBottom));
            }

            int newWidth = Math.Max(minWidth, right - left);
            int newHeight = Math.Max(minHeight, bottom - top);
            if (newWidth <= 0 || newHeight <= 0)
            {
                return false;
            }

            if (newWidth == dragStartWindowBounds.Width &&
                newHeight == dragStartWindowBounds.Height &&
                left == dragStartWindowBounds.X &&
                top == dragStartWindowBounds.Y)
            {
                return false;
            }

            const uint flags = SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            return SetWindowPos(hwnd, IntPtr.Zero, left, top, newWidth, newHeight, flags);
        }

        public static bool TryResizeWindowFromDockingEdge(Core game, DockingWindowResizeEdge edge, Point clientMousePosition)
        {
            if (game?.Window == null ||
                !CustomDockingResizeEdgesEnabled ||
                !IsWindowedMode(game.WindowMode))
            {
                return false;
            }

            IntPtr hwnd = game.Window.Handle;
            if (hwnd == IntPtr.Zero || IsWindowMaximized(hwnd) || !GetWindowRect(hwnd, out NativeRect windowRect))
            {
                return false;
            }

            NativePoint screenPoint = new() { X = clientMousePosition.X, Y = clientMousePosition.Y };
            if (!ClientToScreen(hwnd, ref screenPoint))
            {
                return false;
            }

            int left = windowRect.Left;
            int top = windowRect.Top;
            int right = windowRect.Right;
            int bottom = windowRect.Bottom;
            int width = right - left;
            int height = bottom - top;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            int minWidth = Math.Max(320, Math.Max(UIStyle.MinBlockSize, GetSystemMetrics(SM_CXMINTRACK)));
            int minHeight = Math.Max(220, Math.Max(UIStyle.MinBlockSize, GetSystemMetrics(SM_CYMINTRACK)));

            GetDockingResizeClampBounds(hwnd, out int minLeft, out int minTop, out int maxRight, out int maxBottom);

            switch (edge)
            {
                case DockingWindowResizeEdge.Top:
                {
                    int maxTop = bottom - minHeight;
                    top = Math.Clamp(screenPoint.Y, minTop, maxTop);
                    break;
                }
            }

            switch (edge)
            {
                case DockingWindowResizeEdge.Left:
                case DockingWindowResizeEdge.BottomLeft:
                case DockingWindowResizeEdge.TopLeft:
                {
                    int maxLeft = right - minWidth;
                    left = Math.Clamp(screenPoint.X, minLeft, maxLeft);
                    break;
                }
            }

            switch (edge)
            {
                case DockingWindowResizeEdge.Right:
                case DockingWindowResizeEdge.BottomRight:
                case DockingWindowResizeEdge.TopRight:
                {
                    int minRight = left + minWidth;
                    right = Math.Clamp(screenPoint.X, minRight, Math.Max(minRight, maxRight));
                    break;
                }
            }

            switch (edge)
            {
                case DockingWindowResizeEdge.Bottom:
                case DockingWindowResizeEdge.BottomLeft:
                case DockingWindowResizeEdge.BottomRight:
                {
                    int minBottom = top + minHeight;
                    bottom = Math.Clamp(screenPoint.Y, minBottom, Math.Max(minBottom, maxBottom));
                    break;
                }
            }

            switch (edge)
            {
                case DockingWindowResizeEdge.TopLeft:
                case DockingWindowResizeEdge.TopRight:
                {
                    int maxTop = bottom - minHeight;
                    top = Math.Clamp(screenPoint.Y, minTop, maxTop);
                    break;
                }
            }

            int newWidth = Math.Max(minWidth, right - left);
            int newHeight = Math.Max(minHeight, bottom - top);

            if (newWidth == width && newHeight == height && left == windowRect.Left && top == windowRect.Top)
            {
                return false;
            }

            const uint flags = SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            return SetWindowPos(hwnd, IntPtr.Zero, left, top, newWidth, newHeight, flags);
        }

        private static bool TryGetExpectedWindowOuterSize(Core game, IntPtr hwnd, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (game == null)
            {
                return false;
            }

            int clientWidth = game.Window?.ClientBounds.Width ?? 0;
            int clientHeight = game.Window?.ClientBounds.Height ?? 0;
            if (clientWidth <= 1 || clientHeight <= 1)
            {
                clientWidth = Math.Max(1, game.ViewportWidth);
                clientHeight = Math.Max(1, game.ViewportHeight);
            }

            if (clientWidth <= 0 || clientHeight <= 0)
            {
                return false;
            }

            if (game.WindowMode == WindowMode.BorderlessWindowed)
            {
                width = clientWidth;
                height = clientHeight;
                return true;
            }

            long style = hwnd != IntPtr.Zero ? GetWindowLongPtr(hwnd, GWL_STYLE) : 0;
            long exStyle = hwnd != IntPtr.Zero ? GetWindowLongPtr(hwnd, GWL_EXSTYLE) : 0;
            NativeRect frame = new()
            {
                Left = 0,
                Top = 0,
                Right = clientWidth,
                Bottom = clientHeight
            };

            if (AdjustWindowRectEx(ref frame, unchecked((uint)style), false, unchecked((uint)exStyle)))
            {
                width = frame.Right - frame.Left;
                height = frame.Bottom - frame.Top;
            }
            else
            {
                width = clientWidth;
                height = clientHeight;
            }

            return width > 0 && height > 0;
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

        private static void PreserveWindowOuterBounds(IntPtr hwnd, Rectangle desiredOuterBounds)
        {
            if (hwnd == IntPtr.Zero || desiredOuterBounds.Width <= 0 || desiredOuterBounds.Height <= 0)
            {
                return;
            }

            GetDockingResizeClampBounds(hwnd, out int minLeft, out int minTop, out int maxRight, out int maxBottom);

            int width = Math.Clamp(desiredOuterBounds.Width, 64, Math.Max(64, maxRight - minLeft));
            int height = Math.Clamp(desiredOuterBounds.Height, 64, Math.Max(64, maxBottom - minTop));
            int maxX = Math.Max(minLeft, maxRight - width);
            int maxY = Math.Max(minTop, maxBottom - height);

            int targetX = Math.Clamp(desiredOuterBounds.X, minLeft, maxX);
            int targetY = Math.Clamp(desiredOuterBounds.Y, minTop, maxY);

            const uint flags = SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            if (!SetWindowPos(hwnd, IntPtr.Zero, targetX, targetY, width, height, flags))
            {
                DebugLogger.PrintUI("Failed to preserve outer window bounds after docking chrome toggle.");
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

            Point desired = _desiredClientTopLeft.Value;

            Point? current = GetClientTopLeftOnScreen(hwnd);
            if (current == null)
            {
                return;
            }

            int deltaX = desired.X - current.Value.X;
            int deltaY = desired.Y - current.Value.Y;
            if (Math.Abs(deltaX) <= 1 && Math.Abs(deltaY) <= 1)
            {
                _desiredClientTopLeft = null;
                return;
            }

            // Use the outer window rect as the base for SetWindowPos — passing client
            // coordinates directly would place the window 8px off due to the invisible
            // DWM resize border, causing delta to cycle at -8 and never converge.
            if (!GetWindowRect(hwnd, out NativeRect windowRect))
            {
                return;
            }

            const uint flags = SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE;
            if (SetWindowPos(hwnd, IntPtr.Zero, windowRect.Left + deltaX, windowRect.Top + deltaY, 0, 0, flags))
            {
                _desiredClientTopLeft = null;
            }
        }

        private static bool IsWindowMaximized(IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero && IsZoomed(hwnd);
        }

        private static void ConfigureNativeResizeFrame(IntPtr hwnd, WindowMode mode)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            bool enableThickFrame = mode == WindowMode.BorderedWindowed;
            bool enableMaximizeBox = mode == WindowMode.BorderedWindowed;

            long style = GetWindowLongPtr(hwnd, GWL_STYLE);
            if (style == 0)
            {
                return;
            }

            long desiredStyle = style;
            desiredStyle = enableThickFrame
                ? desiredStyle | WS_THICKFRAME
                : desiredStyle & ~WS_THICKFRAME;
            desiredStyle = enableMaximizeBox
                ? desiredStyle | WS_MAXIMIZEBOX
                : desiredStyle & ~WS_MAXIMIZEBOX;

            if (desiredStyle == style)
            {
                return;
            }

            SetWindowLongPtr(hwnd, GWL_STYLE, desiredStyle);
            const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED;
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, flags);
        }

        private static bool TryGetNativeResizeHit(IntPtr hwnd, IntPtr lParam, out IntPtr hitResult)
        {
            hitResult = IntPtr.Zero;
            if (!_nativeWindowResizeEdgesEnabled || hwnd == IntPtr.Zero || IsWindowMaximized(hwnd))
            {
                return false;
            }

            if (!GetWindowRect(hwnd, out NativeRect windowRect))
            {
                return false;
            }

            int screenX = GetSignedLowWord(lParam);
            int screenY = GetSignedHighWord(lParam);
            if (screenX < windowRect.Left || screenX >= windowRect.Right ||
                screenY < windowRect.Top || screenY >= windowRect.Bottom)
            {
                return false;
            }

            int borderX = GetNativeResizeBorderThicknessX();
            int borderY = GetNativeResizeBorderThicknessY();

            bool onLeft = screenX - windowRect.Left < borderX;
            bool onRight = windowRect.Right - screenX <= borderX;
            bool onTop = screenY - windowRect.Top < borderY;
            bool onBottom = windowRect.Bottom - screenY <= borderY;

            if (onTop && onLeft)
            {
                hitResult = (IntPtr)HTTOPLEFT;
                return true;
            }

            if (onTop && onRight)
            {
                hitResult = (IntPtr)HTTOPRIGHT;
                return true;
            }

            if (onBottom && onLeft)
            {
                hitResult = (IntPtr)HTBOTTOMLEFT;
                return true;
            }

            if (onBottom && onRight)
            {
                hitResult = (IntPtr)HTBOTTOMRIGHT;
                return true;
            }

            if (onLeft)
            {
                hitResult = (IntPtr)HTLEFT;
                return true;
            }

            if (onRight)
            {
                hitResult = (IntPtr)HTRIGHT;
                return true;
            }

            if (onTop)
            {
                hitResult = (IntPtr)HTTOP;
                return true;
            }

            if (onBottom)
            {
                hitResult = (IntPtr)HTBOTTOM;
                return true;
            }

            return false;
        }

        private static bool IsNativeResizeHitResult(IntPtr hitResult)
        {
            int value = hitResult.ToInt32();
            return value == HTLEFT ||
                value == HTRIGHT ||
                value == HTTOP ||
                value == HTTOPLEFT ||
                value == HTTOPRIGHT ||
                value == HTBOTTOM ||
                value == HTBOTTOMLEFT ||
                value == HTBOTTOMRIGHT;
        }

        private static int GetNativeResizeBorderThicknessX()
        {
            int frame = Math.Max(0, GetSystemMetrics(SM_CXSIZEFRAME));
            int padded = Math.Max(0, GetSystemMetrics(SM_CXPADDEDBORDER));
            int systemBorder = frame + padded;
            return Math.Max(UIStyle.ResizeEdgeThickness, Math.Max(2, systemBorder));
        }

        private static int GetNativeResizeBorderThicknessY()
        {
            int frame = Math.Max(0, GetSystemMetrics(SM_CYSIZEFRAME));
            int padded = Math.Max(0, GetSystemMetrics(SM_CXPADDEDBORDER));
            int systemBorder = frame + padded;
            return Math.Max(UIStyle.ResizeEdgeThickness, Math.Max(2, systemBorder));
        }

        private static int GetSignedLowWord(IntPtr value)
        {
            return unchecked((short)((long)value & 0xFFFF));
        }

        private static int GetSignedHighWord(IntPtr value)
        {
            return unchecked((short)(((long)value >> 16) & 0xFFFF));
        }

        private readonly struct WindowVisibleFrameInsets
        {
            public WindowVisibleFrameInsets(int left, int top, int right, int bottom)
            {
                Left = Math.Max(0, left);
                Top = Math.Max(0, top);
                Right = Math.Max(0, right);
                Bottom = Math.Max(0, bottom);
            }

            public int Left { get; }
            public int Top { get; }
            public int Right { get; }
            public int Bottom { get; }
        }

        private static bool TryGetWindowVisibleFrameInsets(IntPtr hwnd, out WindowVisibleFrameInsets insets)
        {
            insets = new WindowVisibleFrameInsets(0, 0, 0, 0);

            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out NativeRect outerRect))
            {
                return false;
            }

            int windowWidth = outerRect.Right - outerRect.Left;
            int windowHeight = outerRect.Bottom - outerRect.Top;
            if (windowWidth <= 0 || windowHeight <= 0)
            {
                return false;
            }

            int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out NativeRect visibleRect, Marshal.SizeOf<NativeRect>());
            if (hr == 0)
            {
                int visibleWidth = visibleRect.Right - visibleRect.Left;
                int visibleHeight = visibleRect.Bottom - visibleRect.Top;
                if (visibleWidth > 0 && visibleHeight > 0)
                {
                    int leftInset = Math.Clamp(visibleRect.Left - outerRect.Left, 0, windowWidth);
                    int topInset = Math.Clamp(visibleRect.Top - outerRect.Top, 0, windowHeight);
                    int rightInset = Math.Clamp(outerRect.Right - visibleRect.Right, 0, windowWidth);
                    int bottomInset = Math.Clamp(outerRect.Bottom - visibleRect.Bottom, 0, windowHeight);
                    insets = new WindowVisibleFrameInsets(leftInset, topInset, rightInset, bottomInset);
                    return true;
                }
            }

            int frameX = Math.Max(0, GetSystemMetrics(SM_CXSIZEFRAME) + GetSystemMetrics(SM_CXPADDEDBORDER));
            int frameY = Math.Max(0, GetSystemMetrics(SM_CYSIZEFRAME) + GetSystemMetrics(SM_CXPADDEDBORDER));
            insets = new WindowVisibleFrameInsets(frameX, frameY, frameX, frameY);
            return true;
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

        private static Rectangle GetMonitorBounds(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                NativeMonitorInfo info = new() { Size = Marshal.SizeOf<NativeMonitorInfo>() };
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    NativeRect monitorRect = info.Monitor;
                    int width = monitorRect.Right - monitorRect.Left;
                    int height = monitorRect.Bottom - monitorRect.Top;
                    if (width > 0 && height > 0)
                    {
                        return new Rectangle(monitorRect.Left, monitorRect.Top, width, height);
                    }
                }
            }

            return GetDockingResizeBounds();
        }

        private static void GetDockingResizeLimits(IntPtr hwnd, out Rectangle monitorBounds, out Rectangle monitorWorkArea)
        {
            monitorBounds = GetMonitorBounds(hwnd);
            monitorWorkArea = GetMonitorWorkArea(hwnd);

            if (monitorBounds.Width <= 0 || monitorBounds.Height <= 0)
            {
                monitorBounds = GetDockingResizeBounds();
            }

            if (monitorWorkArea.Width <= 0 || monitorWorkArea.Height <= 0)
            {
                monitorWorkArea = monitorBounds;
            }

            Rectangle constrainedWorkArea = Rectangle.Intersect(monitorBounds, monitorWorkArea);
            if (constrainedWorkArea.Width > 0 && constrainedWorkArea.Height > 0)
            {
                monitorWorkArea = constrainedWorkArea;
            }
            else
            {
                monitorWorkArea = monitorBounds;
            }
        }

        private static void GetDockingResizeClampBounds(IntPtr hwnd, out int minLeft, out int minTop, out int maxRight, out int maxBottom)
        {
            GetDockingResizeLimits(hwnd, out Rectangle monitorBounds, out Rectangle monitorWorkArea);
            if (!TryGetWindowVisibleFrameInsets(hwnd, out WindowVisibleFrameInsets insets))
            {
                insets = new WindowVisibleFrameInsets(0, 0, 0, 0);
            }

            minLeft = monitorBounds.Left - insets.Left;
            maxRight = monitorBounds.Right + insets.Right;
            minTop = monitorWorkArea.Top - insets.Top;
            maxBottom = monitorWorkArea.Bottom + insets.Bottom;
        }

        private static Rectangle GetDockingResizeBounds()
        {
            System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
            if (virtualScreen.Width > 0 && virtualScreen.Height > 0)
            {
                return new Rectangle(virtualScreen.Left, virtualScreen.Top, virtualScreen.Width, virtualScreen.Height);
            }

            Screen[] screens = Screen.AllScreens;
            if (screens is { Length: > 0 })
            {
                int left = int.MaxValue;
                int top = int.MaxValue;
                int right = int.MinValue;
                int bottom = int.MinValue;

                foreach (Screen screen in screens)
                {
                    System.Drawing.Rectangle bounds = screen.Bounds;
                    left = Math.Min(left, bounds.Left);
                    top = Math.Min(top, bounds.Top);
                    right = Math.Max(right, bounds.Right);
                    bottom = Math.Max(bottom, bounds.Bottom);
                }

                if (right > left && bottom > top)
                {
                    return new Rectangle(left, top, right - left, bottom - top);
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

        // Hooks WM_PAINT on the game window via Win32 SetWindowLongPtr subclassing.
        // WM_PAINT is the only context where DWM actually composites frames during the
        // modal resize loop. Drawing from WM_SIZE is intentionally skipped: Present()
        // pumps the Win32 message queue while waiting for VBlank, which dispatches the
        // WM_PAINT queued by OnClientSizeChanged's InvalidateRect while _inResizeDraw is
        // still true — blocking it. ValidateRect inside that blocked WM_PAINT clears the
        // update region, preventing any further WM_PAINT from drawing. By not calling
        // RunResizeDraw from WM_SIZE, WM_PAINT always fires cleanly after the swap chain
        // is resized and draws the updated layout in the correct DWM compositing context.
        private sealed class ResizePaintHook
        {
            private const int WM_SIZE = 0x0005;
            private const int WM_PAINT = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;
            private const int WM_NCHITTEST = 0x0084;
            private const int GWLP_WNDPROC = -4;

            private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

            private readonly IntPtr _hwnd;
            private readonly IntPtr _originalWndProc;
            private readonly WndProcDelegate _wndProcDelegate; // keep alive to prevent GC

            public ResizePaintHook(IntPtr hwnd)
            {
                _hwnd = hwnd;
                _wndProcDelegate = WndProc;
                IntPtr newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
                _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, newProc);
            }

            public void Detach()
            {
                if (_originalWndProc != IntPtr.Zero)
                    SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _originalWndProc);
            }

            private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            {
                if (msg == WM_NCHITTEST)
                {
                    IntPtr baseResult = CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
                    if (!_nativeWindowResizeEdgesEnabled && IsNativeResizeHitResult(baseResult))
                    {
                        baseResult = (IntPtr)HTCLIENT;
                    }

                    if (baseResult != (IntPtr)HTCLIENT)
                    {
                        return baseResult;
                    }

                    return TryGetNativeResizeHit(hwnd, lParam, out IntPtr resizeHit)
                        ? resizeHit
                        : baseResult;
                }

                if (msg == WM_SIZE)
                {
                    // Forward to the original proc so ClientSizeChanged fires and
                    // ApplyChanges() resizes the swap chain. OnClientSizeChanged then
                    // calls InvalidateRect to queue WM_PAINT, which handles the draw.
                    // Do NOT call RunResizeDraw here — see class comment above.
                    return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
                }
                if (msg == WM_ERASEBKGND)
                {
                    // Prevent default background erase during resize to avoid white flashes
                    // between swap-chain presents.
                    return (IntPtr)1;
                }
                if (msg == WM_PAINT)
                {
                    RunResizeDraw();
                    ValidateRect(hwnd, IntPtr.Zero);
                    return IntPtr.Zero;
                }
                return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
            }

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
            private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

            [DllImport("user32.dll")]
            private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        }

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool ValidateRect(IntPtr hWnd, IntPtr lpRect);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeWindowPlacement
        {
            public uint Length;
            public uint Flags;
            public uint ShowCmd;
            public NativePoint MinPosition;
            public NativePoint MaxPosition;
            public NativeRect NormalPosition;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref NativeWindowPlacement lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref NativeWindowPlacement lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectEx(ref NativeRect lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out NativeRect pvAttribute, int cbAttribute);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern long SetWindowLongPtr64(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfo lpmi);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int SM_CXSIZEFRAME = 32;
        private const int SM_CYSIZEFRAME = 33;
        private const int SM_CXMINTRACK = 34;
        private const int SM_CYMINTRACK = 35;
        private const int SM_CXPADDEDBORDER = 92;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const uint SW_SHOWMINIMIZED = 2;

        private static long GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        private static long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, unchecked((int)dwNewLong));
        }
    }
}
