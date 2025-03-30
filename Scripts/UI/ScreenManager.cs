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
                    break;

                case WindowMode.BorderlessWindowed:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    break;

                case WindowMode.BorderlessFullscreen:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    game.ViewportWidth = display.Width;       // Match display width
                    game.ViewportHeight = display.Height;     // Match display height
                    game.Graphics.PreferredBackBufferWidth = display.Width;
                    game.Graphics.PreferredBackBufferHeight = display.Height;
                    break;

                case WindowMode.LegacyFullscreen:
                    game.Graphics.IsFullScreen = true;
                    game.Window.IsBorderless = false;
                    game.ViewportWidth = display.Width;       // Match display width
                    game.ViewportHeight = display.Height;     // Match display height
                    game.Graphics.PreferredBackBufferWidth = display.Width;
                    game.Graphics.PreferredBackBufferHeight = display.Height;
                    break;
            }

            game.Graphics.ApplyChanges();
            DebugLogger.PrintUI($"Applied WindowMode: {game.WindowMode}, Resolution: {game.ViewportWidth}x{game.ViewportHeight}");
        }
    }
}