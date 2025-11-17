using System;

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

            // Load control switch states from the database
            ControlStateManager.LoadControlSwitchStates();

            // Hydrate the input switch cache so runtime toggles honor persisted values
            InputTypeManager.InitializeControlStates();

            // Initialize the console after loading in switch states (which, importantly, contain DebugMode)
            ConsoleManager.InitializeConsoleIfEnabled();

            // Setting instance variables in Core.cs
            Core.Instance.IsMouseVisible = true;
            Core.Instance.PhysicsManager = new PhysicsManager();

            // If the settings are not loaded via SQL, these defaults will be applied so that debugging is possible when there's a low-level issue with loading settings
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
                Core.Instance.BackgroundColor = DatabaseFetch.GetColor("GeneralSettings", "SettingKey", "BackgroundColor");

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
                int configuredLogFiles = DatabaseFetch.GetValue<int>("GeneralSettings", "Value", "SettingKey", "NumLogFiles");
                if (configuredLogFiles <= 0)
                {
                    configuredLogFiles = LogFileHandler.DefaultMaxLogFiles;
                }
                LogFileHandler.ConfigureMaxLogFiles(configuredLogFiles);

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
