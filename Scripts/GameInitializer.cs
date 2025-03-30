using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public static class GameInitializer
    {
        public static void Initialize(Core game)
        {
            //DatabaseInitializer.InitializeDatabase();
            LoadGeneralSettings(game);
            DebugLogger.Print("Game initialized");
        }

        private static void LoadGeneralSettings(Core game)
        {
            DebugLogger.Print("Loading general settings...");

            try
            {
                // Background color
                int r = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_R", 30);
                int g = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_G", 30);
                int b = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_B", 30);
                int a = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_A", 255);
                game.BackgroundColor = new Color(r, g, b, a);

                // Load WindowMode first
                string modeStr = BaseFunctions.GetValue<string>("GeneralSettings", "Value", "SettingKey", "WindowMode", "BorderedWindowed");
                if (!Enum.TryParse(modeStr, true, out WindowMode mode))
                {
                    DebugLogger.PrintWarning($"Unrecognized WindowMode '{modeStr}'. Defaulting to BorderedWindowed.");
                    mode = WindowMode.BorderedWindowed;
                }
                game.WindowMode = mode;

                // Check if fullscreen mode was selected
                if (mode == WindowMode.BorderlessFullscreen || mode == WindowMode.LegacyFullscreen)
                {
                    var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    game.ViewportWidth = display.Width;
                    game.ViewportHeight = display.Height;

                    DebugLogger.Print($"Fullscreen mode detected. Using display resolution: {display.Width}x{display.Height}");
                }
                else
                {
                    game.ViewportWidth = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth", 1280);
                    game.ViewportHeight = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight", 720);
                }

                game.Graphics.PreferredBackBufferWidth = game.ViewportWidth;
                game.Graphics.PreferredBackBufferHeight = game.ViewportHeight;

                // Render settings
                game.VSyncEnabled = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "VSync", false);
                game.UseFixedTimeStep = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "FixedTimeStep", false);
                game.TargetFrameRate = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "TargetFrameRate", 240);

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
    }
}
