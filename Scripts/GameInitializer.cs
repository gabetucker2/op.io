using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public static class GameInitializer
    {
        public static void Initialize(Core game)
        {
            DebugLogger.Print("Initializing game...");

            // Ensure the database is initialized before loading settings
            DatabaseInitializer.InitializeDatabase();

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings(game);

            ApplyWindowMode(game);

            game.Graphics.SynchronizeWithVerticalRetrace = game.VSyncEnabled;
            game.IsFixedTimeStep = game.UseFixedTimeStep;

            int safeFps = Math.Clamp(game.TargetFrameRate, 1, 1000);
            game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            game.Graphics.ApplyChanges();

            // Initialize game objects AFTER general settings are loaded
            ObjectManager.InitializeObjects(game);

            DebugLogger.Print("Game initialization complete.");
        }

        private static void LoadGeneralSettings(Core game)
        {
            DebugLogger.Print("Loading general settings...");

            try
            {
                int r = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_R", 30);
                int g = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_G", 30);
                int b = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_B", 30);
                int a = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_A", 255);
                game.BackgroundColor = new Color(r, g, b, a);

                string modeStr = BaseFunctions.GetValue<string>("GeneralSettings", "Value", "SettingKey", "WindowMode", "BorderedWindowed");
                if (!Enum.TryParse(modeStr, true, out WindowMode mode))
                {
                    DebugLogger.PrintWarning($"Unrecognized WindowMode '{modeStr}'. Defaulting to BorderedWindowed.");
                    mode = WindowMode.BorderedWindowed;
                }
                game.WindowMode = mode;

                game.ViewportWidth = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth", 1280);
                game.ViewportHeight = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight", 720);

                game.VSyncEnabled = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "VSync", false);
                game.UseFixedTimeStep = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "FixedTimeStep", false);
                game.TargetFrameRate = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "TargetFrameRate", 240);

                game.Graphics.PreferredBackBufferWidth = game.ViewportWidth;
                game.Graphics.PreferredBackBufferHeight = game.ViewportHeight;

                game.Graphics.ApplyChanges();

                DebugLogger.Print(
                    $"Loaded general settings: BackgroundColor={game.BackgroundColor}, Viewport={game.ViewportWidth}x{game.ViewportHeight}, Mode={game.WindowMode}, VSync={game.VSyncEnabled}, FixedTimeStep={game.UseFixedTimeStep}, FPS={game.TargetFrameRate}"
                );
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load general settings: {ex.Message}");
                game.BackgroundColor = Color.CornflowerBlue;
                game.ViewportWidth = 1280;
                game.ViewportHeight = 720;
            }
        }

        private static void ApplyWindowMode(Core game)
        {
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

                case WindowMode.LegacyFullscreen:
                    game.Graphics.IsFullScreen = true;
                    game.Window.IsBorderless = false;
                    break;

                case WindowMode.BorderlessFullscreen:
                    game.Graphics.IsFullScreen = false;
                    game.Window.IsBorderless = true;
                    var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    game.Graphics.PreferredBackBufferWidth = display.Width;
                    game.Graphics.PreferredBackBufferHeight = display.Height;
                    break;
            }
        }
    }
}
