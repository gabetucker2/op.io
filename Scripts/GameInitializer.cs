using Microsoft.Xna.Framework;
using System;
using System.Drawing;
using System.Collections.Generic;

namespace op.io
{
    public static class GameInitializer
    {
        public static void Initialize() // Order is very important here
        {
            DebugLogger.Print("Initializing game...");

            // Ensure Core.InstanceCore exists
            if (Core.InstanceCore == null)
            {
                DebugLogger.PrintError("Core.InstanceCore is null. Make sure the Core constructor has been called before initialization.");
                return;
            }

            // Ensure the database is initialized before loading settings
            DatabaseInitializer.InitializeDatabase();

            LogFormatter.LoadMaxRepeats();
            ControlStateManager.SyncDebugModeSwitchState();

            DebugManager.InitializeConsoleIfEnabled();

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings();
            ControlStateManager.LoadControlSwitchStates(); // Load control switch states from the database

            // Setting instance variables in Core.cs
            Core.InstanceCore.IsMouseVisible = true;
            Core.InstanceCore.PhysicsManager = new PhysicsManager();

            if (Core.InstanceCore.Graphics == null)
            {
                DebugLogger.PrintError("GraphicsDeviceManager is null. Ensure Core.InstanceCore.Graphics is initialized properly.");
                return;
            }

            BlockManager.ApplyWindowMode(Core.InstanceCore);

            Core.InstanceCore.Graphics.SynchronizeWithVerticalRetrace = Core.InstanceCore.VSyncEnabled;
            Core.InstanceCore.IsFixedTimeStep = Core.InstanceCore.UseFixedTimeStep;

            int safeFps = Math.Clamp(Core.InstanceCore.TargetFrameRate, 10, 1000);
            if (Core.InstanceCore.TargetFrameRate != safeFps)
            {
                DebugLogger.PrintWarning($"TargetFrameRate {Core.InstanceCore.TargetFrameRate} is out of safe range and has been clamped appropriately.");
                return;
            }
            Core.InstanceCore.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            Core.InstanceCore.Graphics.ApplyChanges();

            // Initialize gameobjects AFTER general settings are loaded
            ObjectManager.InitializeObjects(Core.InstanceCore);

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
                Core.InstanceCore.BackgroundColor = new Microsoft.Xna.Framework.Color(r, g, b, a);

                string modeStr = BaseFunctions.GetValue<string>("GeneralSettings", "Value", "SettingKey", "WindowMode");
                if (!Enum.TryParse(modeStr, true, out WindowMode mode))
                {
                    DebugLogger.PrintError($"Unrecognized WindowMode '{modeStr}'.");
                }
                Core.InstanceCore.WindowMode = mode;

                Core.InstanceCore.ViewportWidth = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
                Core.InstanceCore.ViewportHeight = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

                Core.InstanceCore.VSyncEnabled = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "VSync");
                Core.InstanceCore.UseFixedTimeStep = BaseFunctions.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "FixedTimeStep");
                Core.InstanceCore.TargetFrameRate = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "TargetFrameRate");

                Core.InstanceCore.Graphics.PreferredBackBufferWidth = Core.InstanceCore.ViewportWidth;
                Core.InstanceCore.Graphics.PreferredBackBufferHeight = Core.InstanceCore.ViewportHeight;

                Core.InstanceCore.Graphics.ApplyChanges();

                DebugLogger.PrintDatabase(
                    $"Loaded general settings: BackgroundColor={Core.InstanceCore.BackgroundColor}, Viewport={Core.InstanceCore.ViewportWidth}x{Core.InstanceCore.ViewportHeight}, Mode={Core.InstanceCore.WindowMode}, VSync={Core.InstanceCore.VSyncEnabled}, FixedTimeStep={Core.InstanceCore.UseFixedTimeStep}, FPS={Core.InstanceCore.TargetFrameRate}"
                );
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load general settings: {ex.Message}");
            }
        }
    }
}
