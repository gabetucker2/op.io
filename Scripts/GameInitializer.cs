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

            DebugManager.InitializeConsoleIfEnabled();

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings(game);

            BlockManager.ApplyWindowMode(game);

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
            DebugLogger.PrintDatabase("Loading general settings...");

            try
            {
                int r = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_R");
                int g = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_G");
                int b = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_B");
                int a = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_A");
                game.BackgroundColor = new Color(r, g, b, a);

                string modeStr = BaseFunctions.GetValue<string>("GeneralSettings", "Value", "SettingKey", "WindowMode");
                if (!Enum.TryParse(modeStr, true, out WindowMode mode))
                {
                    DebugLogger.PrintError($"Unrecognized WindowMode '{modeStr}'.");
                }
                game.WindowMode = mode;

                game.ViewportWidth = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
                game.ViewportHeight = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

                game.VSyncEnabled = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "VSync");
                game.UseFixedTimeStep = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "FixedTimeStep");
                game.TargetFrameRate = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "TargetFrameRate");

                game.Graphics.PreferredBackBufferWidth = game.ViewportWidth;
                game.Graphics.PreferredBackBufferHeight = game.ViewportHeight;

                game.Graphics.ApplyChanges();

                DebugLogger.PrintDatabase(
                    $"Loaded general settings: BackgroundColor={game.BackgroundColor}, Viewport={game.ViewportWidth}x{game.ViewportHeight}, Mode={game.WindowMode}, VSync={game.VSyncEnabled}, FixedTimeStep={game.UseFixedTimeStep}, FPS={game.TargetFrameRate}"
                );
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load general settings: {ex.Message}");
            }
        }

    }
}
