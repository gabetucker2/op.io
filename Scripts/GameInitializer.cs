using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

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
            ControlKeyMigrations.EnsureApplied();

            // Load general settings BEFORE initializing anything else
            LoadGeneralSettings();

            // Load control switch states from the database
            ControlStateManager.LoadControlSwitchStates();

            // Hydrate the input switch cache so runtime toggles honor persisted values
            InputTypeManager.InitializeControlStates();
            SwitchStateScanner.Initialize();

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

            ScreenManager.ApplyWindowMode(Core.Instance);

            Core.Instance.Graphics.SynchronizeWithVerticalRetrace = Core.Instance.VSyncEnabled;
            Core.Instance.IsFixedTimeStep = Core.Instance.UseFixedTimeStep;

            int safeFps = Math.Clamp(Core.Instance.TargetFrameRate, 10, 1000);
            Core.Instance.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            Core.Instance.Graphics.ApplyChanges();

            // Make the background to the game be transparent (https://stackoverflow.com/a)
            var contentManager = Core.Instance.Content;
            var gameWindow = Core.Instance.Window;
            if (contentManager == null)
            {
                DebugLogger.PrintError("Content is null. Cannot configure window transparency.");
            }
            else if (gameWindow == null)
            {
                DebugLogger.PrintError("Window is null. Cannot configure window transparency.");
            }
            else
            {
                contentManager.RootDirectory = "Content";
                IntPtr hwnd = GetWin32WindowHandle(gameWindow.Handle);
                if (hwnd == IntPtr.Zero)
                {
                    DebugLogger.PrintError("Failed to resolve native window handle; skipping transparency setup.");
                }
                else
                {
                    ApplyTransparencyKey(hwnd, Core.TransparentWindowColor);
                }
            }

            // Initialize gameobjects AFTER general settings are loaded
            GameObjectInitializer.Initialize();
            SwitchConsumerBootstrapper.RegisterDefaultConsumers();

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

        private static void ApplyTransparencyKey(IntPtr hwnd, Color xnaColor)
        {
            uint colorRef = TypeConversionFunctions.XnaColorToColorRef(xnaColor);
            long styles = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, styles | WS_EX_LAYERED);
            int result = SetLayeredWindowAttributes(hwnd, colorRef, 0, LWA_COLORKEY);
            if (result == 0)
            {
                DebugLogger.PrintError("SetLayeredWindowAttributes failed; window transparency key not applied.");
            }
        }

        private static IntPtr GetWin32WindowHandle(IntPtr rawHandle)
        {
            if (rawHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // For WindowsDX, the MonoGame GameWindow handle is already an HWND.
            return rawHandle;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int LWA_COLORKEY = 0x00000001;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern long SetWindowLongPtr64(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private static long GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        private static long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, unchecked((int)dwNewLong));
        }
    }
}

