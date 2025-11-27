using System;
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
                    AttachResizeHandler(game);
                    break;

                case WindowMode.BorderlessWindowed:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    game.Window.AllowUserResizing = false;
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
            GameInitializer.ApplyWindowCaptionColor(UIStyle.HeaderBackground);
            GameInitializer.RefreshTransparencyKey();
            DebugLogger.PrintUI($"Applied WindowMode: {game.WindowMode}, Resolution: {game.ViewportWidth}x{game.ViewportHeight}");
        }

        private static Core _resizeTarget;
        private static bool _dockingModesInitialized;
        private static WindowMode _dockingEnabledWindowMode = WindowMode.BorderedWindowed;
        private static WindowMode _dockingDisabledWindowMode = WindowMode.BorderlessWindowed;

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

            game.WindowMode = targetMode;
            ApplyWindowMode(game);
        }

        private static void InitializeDockingWindowModes(Core game)
        {
            if (_dockingModesInitialized || game == null)
            {
                return;
            }

            _dockingEnabledWindowMode = game.WindowMode == WindowMode.BorderedWindowed
                ? WindowMode.BorderedWindowed
                : WindowMode.BorderedWindowed;

            _dockingDisabledWindowMode = game.WindowMode == WindowMode.BorderlessFullscreen
                ? WindowMode.BorderlessFullscreen
                : WindowMode.BorderlessWindowed;

            if (game.WindowMode != _dockingEnabledWindowMode)
            {
                DebugLogger.PrintUI($"Docking mode requires window chrome; switching to {_dockingEnabledWindowMode} while docked.");
            }

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
        }
    }
}
