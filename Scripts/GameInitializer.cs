using Microsoft.Xna.Framework;
using System;
using System.Drawing;

namespace op.io
{
    public static class GameInitializer
    {
        public static void Initialize()
        {
            DebugLogger.Print("Initializing game...");

            // Ensure Core.Instance exists
            if (Core.Instance == null)
            {
                DebugLogger.PrintError("Core.Instance is null. Make sure the Core constructor has been called before initialization.");
                return;
            }

            // Ensure the database is initialized before loading settings
            DatabaseInitializer.InitializeDatabase();

            DebugManager.InitializeConsoleIfEnabled();

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings();

            // Setting instance variables in Core.cs
            Core.Instance.IsMouseVisible = true;
            Core.Instance.PhysicsManager = new PhysicsManager();

            // If the settings are not loaded via SQL, these defaults will be applied
            if (Core.Instance.TargetFrameRate <= 0)
                Core.Instance.TargetFrameRate = 240;

            if (Core.Instance.WindowMode == 0)
                Core.Instance.WindowMode = WindowMode.BorderedWindowed;

            if (Core.Instance.Graphics == null)
            {
                DebugLogger.PrintError("GraphicsDeviceManager is null. Ensure Core.Instance.Graphics is initialized properly.");
                return;
            }

            BlockManager.ApplyWindowMode(Core.Instance);

            Core.Instance.Graphics.SynchronizeWithVerticalRetrace = Core.Instance.VSyncEnabled;
            Core.Instance.IsFixedTimeStep = Core.Instance.UseFixedTimeStep;

            int safeFps = Math.Clamp(Core.Instance.TargetFrameRate, 10, 1000);
            Core.Instance.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            Core.Instance.Graphics.ApplyChanges();

            // Initialize gameobjects AFTER general settings are loaded
            ObjectManager.InitializeObjects(Core.Instance);

            DebugLogger.Print("Game initialization complete.");
        }
        private static void LoadGeneralSettings()
        {
            DebugLogger.PrintDatabase("Loading general settings...");

            try
            {
                int r = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_R");
                int g = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_G");
                int b = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_B");
                int a = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_A");
                Core.Instance.BackgroundColor = new Microsoft.Xna.Framework.Color(r, g, b, a);

                string modeStr = BaseFunctions.GetValue<string>("GeneralSettings", "Value", "SettingKey", "WindowMode");
                if (!Enum.TryParse(modeStr, true, out WindowMode mode))
                {
                    DebugLogger.PrintError($"Unrecognized WindowMode '{modeStr}'.");
                }
                Core.Instance.WindowMode = mode;

                Core.Instance.ViewportWidth = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
                Core.Instance.ViewportHeight = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

                Core.Instance.VSyncEnabled = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "VSync");
                Core.Instance.UseFixedTimeStep = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "FixedTimeStep");
                Core.Instance.TargetFrameRate = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "TargetFrameRate");

                Core.Instance.Graphics.PreferredBackBufferWidth = Core.Instance.ViewportWidth;
                Core.Instance.Graphics.PreferredBackBufferHeight = Core.Instance.ViewportHeight;

                Core.Instance.Graphics.ApplyChanges();

                DebugLogger.PrintDatabase(
                    $"Loaded general settings: BackgroundColor={Core.Instance.BackgroundColor}, Viewport={Core.Instance.ViewportWidth}x{Core.Instance.ViewportHeight}, Mode={Core.Instance.WindowMode}, VSync={Core.Instance.VSyncEnabled}, FixedTimeStep={Core.Instance.UseFixedTimeStep}, FPS={Core.Instance.TargetFrameRate}"
                );
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load general settings: {ex.Message}");
            }
        }

    }
}
