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

            // Ensure Core.InstanceCore exists
            if (Core.InstanceCore == null)
            {
                DebugLogger.PrintError("Core.InstanceCore is null. Make sure the Core constructor has been called before initialization.");
                return;
            }

            // Ensure the database is initialized before loading settings
            DatabaseInitializer.InitializeDatabase();

            SyncDebugModeSwitchState();

            DebugManager.InitializeConsoleIfEnabled();

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings();
            LoadControlSwitchStates(); // Load control switch states from the database

            // Setting instance variables in Core.cs
            Core.InstanceCore.IsMouseVisible = true;
            Core.InstanceCore.PhysicsManager = new PhysicsManager();

            // If the settings are not loaded via SQL, these defaults will be applied
            if (Core.InstanceCore.TargetFrameRate <= 0)
                Core.InstanceCore.TargetFrameRate = 240;

            if (Core.InstanceCore.WindowMode == 0)
                Core.InstanceCore.WindowMode = WindowMode.BorderedWindowed;

            if (Core.InstanceCore.Graphics == null)
            {
                DebugLogger.PrintError("GraphicsDeviceManager is null. Ensure Core.InstanceCore.Graphics is initialized properly.");
                return;
            }

            BlockManager.ApplyWindowMode(Core.InstanceCore);

            Core.InstanceCore.Graphics.SynchronizeWithVerticalRetrace = Core.InstanceCore.VSyncEnabled;
            Core.InstanceCore.IsFixedTimeStep = Core.InstanceCore.UseFixedTimeStep;

            int safeFps = Math.Clamp(Core.InstanceCore.TargetFrameRate, 10, 1000);
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
                        bool switchStateBool = switchState == 1;

                        // Skip overriding DebugMode if it was already set by SyncDebugModeSwitchState()
                        if (settingKey == "DebugMode")
                        {
                            if (ControlStateManager.ContainsSwitchState("DebugMode")) // Check if DebugMode is already set
                            {
                                DebugLogger.PrintDatabase($"DebugMode switch state already set by SyncDebugModeSwitchState(). Skipping overwrite.");
                                continue;
                            }
                        }

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

        private static void SyncDebugModeSwitchState()
        {
            DebugLogger.PrintDatabase("Syncing DebugMode switch state with database value...");

            // Get the current value from the DebugSettings table
            int databaseDebugMode = DatabaseConfig.LoadDebugSettings();
            bool isDebugEnabled = databaseDebugMode == 1;

            // Update ControlStateManager to match the loaded value
            ControlStateManager.SetSwitchState("DebugMode", isDebugEnabled);
            DebugLogger.PrintDatabase($"Set DebugMode switch state to: {isDebugEnabled}");
        }
    }
}
