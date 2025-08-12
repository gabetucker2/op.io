using Microsoft.Xna.Framework;
using System;
using System.Drawing;
using System.Collections.Generic;

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

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings();
            LoadControlSwitchStates(); // Load control switch states from the database

            // Initialize the console after loading in switch states (which, importantly, contain DebugMode)
            ConsoleManager.InitializeConsoleIfEnabled();

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
            GameObjectInitializer.Initialize();

            foreach (GameObject obj in Core.Instance.GameObjects)
            {
                obj.LoadContent(Core.Instance.GraphicsDevice);
            }

            // Now initialize physics
            PhysicsManager.Initialize();

            DebugLogger.Print("Game initialization complete.");
        }

        private static void LoadGeneralSettings()
        {
            DebugLogger.PrintDatabase("Loading general settings...");

            try
            {
                int r = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_R");
                int g = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_G");
                int b = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_B");
                int a = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_A");
                Core.Instance.BackgroundColor = new Microsoft.Xna.Framework.Color(r, g, b, a);

                string modeStr = DatabaseFetch.GetValue<string>("GeneralSettings", "Value", "SettingKey", "WindowMode");
                if (!Enum.TryParse(modeStr, true, out WindowMode mode))
                {
                    DebugLogger.PrintError($"Unrecognized WindowMode '{modeStr}'");
                }
                Core.Instance.WindowMode = mode;

                Core.Instance.ViewportWidth = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth");
                Core.Instance.ViewportHeight = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight");

                Core.Instance.VSyncEnabled = DatabaseFetch.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "VSync");
                Core.Instance.UseFixedTimeStep = DatabaseFetch.GetValue<bool>("GeneralSettings", "Value", "SettingKey", "FixedTimeStep");
                Core.Instance.TargetFrameRate = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "TargetFrameRate");

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

        private static void LoadControlSwitchStates()
        {
            DebugLogger.PrintDatabase("Loading control switch states...");

            try
            {
                // Fetch all control keys with SwitchStartState from the database
                var result = DatabaseQuery.ExecuteQuery("SELECT SettingKey, SwitchStartState FROM ControlKey WHERE InputType = 'Switch';");

                if (result.Count == 0)
                {
                    DebugLogger.PrintWarning("No switch control states found in the database.");
                    return;
                }

                foreach (var row in result)
                {
                    if (row.ContainsKey("SettingKey") && row.ContainsKey("SwitchStartState"))
                    {
                        string settingKey = row["SettingKey"].ToString();
                        int switchState = Convert.ToInt32(row["SwitchStartState"]);
                        bool switchStateBool = TypeConversionFunctions.IntToBool(switchState);

                        // Store this information in ControlStateManager
                        ControlStateManager.SetSwitchState(settingKey, switchStateBool);
                        DebugLogger.PrintDatabase($"Loaded switch state: {settingKey} = {(switchStateBool ? "ON" : "OFF")}");
                    }
                    else
                    {
                        DebugLogger.PrintWarning("Invalid row format when loading control switch states.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load control switch states: {ex.Message}");
            }
        }
    }
}
