using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameInitializer
    {
        public static void Initialize()
        {
            SafeLog("GameInitializer.Initialize: start");
            DebugLogger.Print("Initializing game...");
            _transparentWindowHandle = IntPtr.Zero;
            _clickThroughEnabled = false;

            // Ensure Core.Instance exists
            if (Core.Instance == null)
            {
                DebugLogger.PrintError("Core.Instance is null. Make sure the Core constructor has been called before initialization.");
                return;
            }

            // Ensure the database is initialized before loading settings
            SafeLog("GameInitializer.Initialize: DatabaseInitializer.InitializeDatabase");
            DatabaseInitializer.InitializeDatabase();
            SafeLog("GameInitializer.Initialize: ControlKeyMigrations.EnsureApplied");
            ControlKeyMigrations.EnsureApplied();
            try
            {
                SafeLog("GameInitializer.Initialize: ColorScheme.Initialize");
                ColorScheme.Initialize();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"ColorScheme initialization failed; falling back to defaults. {ex.Message}");
                SafeLog($"GameInitializer.Initialize: ColorScheme.Initialize failed: {ex.Message}");
                ColorScheme.InitializeWithDefaultsOnly();
            }

            // Load general settings BEFORE initializing anything else
            SafeLog("GameInitializer.Initialize: LoadGeneralSettings");
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
                    _transparentWindowHandle = hwnd;
                    ApplyTransparencyKey(hwnd, Core.TransparentWindowColor);
                    SetWindowClickThrough(false);
                    GameInitializer.ApplyWindowCaptionColor(UIStyle.DragBarBackground);
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
            SafeLog("GameInitializer.Initialize: complete");
        }

        public static void RefreshTransparencyKey()
        {
            if (_transparentWindowHandle == IntPtr.Zero)
            {
                return;
            }

            ApplyTransparencyKey(_transparentWindowHandle, Core.TransparentWindowColor);

            if (_clickThroughEnabled)
            {
                // Reapply click-through so the style persists across window mode changes.
                ForceSetWindowClickThrough(_clickThroughEnabled);
            }
        }

        private static void LoadGeneralSettings()
        {
            DebugLogger.PrintDatabase("Loading general settings...");
            SafeLog("LoadGeneralSettings: start");

            try
            {
                Core.Instance.BackgroundColor = ColorPalette.GameBackground;

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
                SafeLog("LoadGeneralSettings: done");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load general settings: {ex.Message}");
                SafeLog($"LoadGeneralSettings: failed {ex.Message}");
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

        public static void SetWindowClickThrough(bool enable)
        {
            if (_transparentWindowHandle == IntPtr.Zero)
            {
                DebugLogger.PrintWarning("Cannot toggle click-through: window handle is not available.");
                return;
            }

            long styles = GetWindowLongPtr(_transparentWindowHandle, GWL_EXSTYLE);
            if (enable)
            {
                styles |= WS_EX_TRANSPARENT;
            }
            else
            {
                styles &= ~WS_EX_TRANSPARENT;
            }

            long previous = SetWindowLongPtr(_transparentWindowHandle, GWL_EXSTYLE, styles);
            if (previous == 0)
            {
                DebugLogger.PrintError("Failed to update window transparency hit-test style.");
            }
            else
            {
                _clickThroughEnabled = enable;
                ApplyTopmostForClickThrough(enable);
            }
        }

        public static void ApplyWindowCaptionColor(Color xnaColor)
        {
            if (_transparentWindowHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                uint colorRef = TypeConversionFunctions.XnaColorToColorRef(xnaColor);
                int hr = DwmSetWindowAttribute(_transparentWindowHandle, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(uint));
                if (hr != 0)
                {
                    DebugLogger.PrintUI($"DwmSetWindowAttribute(DWMWA_CAPTION_COLOR) failed with HRESULT 0x{hr:X}.");
                }
            }
            catch (DllNotFoundException ex)
            {
                DebugLogger.PrintUI($"DWM API unavailable; cannot set caption color: {ex.Message}");
            }
            catch (EntryPointNotFoundException ex)
            {
                DebugLogger.PrintUI($"DWMWA_CAPTION_COLOR not supported on this OS: {ex.Message}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintUI($"Failed to set caption color: {ex.Message}");
            }
        }

        private static void SafeLog(string message)
        {
            try
            {
                File.AppendAllText("run_output.txt", $"{DateTime.Now:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private static void ForceSetWindowClickThrough(bool enable)
        {
            long styles = GetWindowLongPtr(_transparentWindowHandle, GWL_EXSTYLE);
            if (enable)
            {
                styles |= WS_EX_TRANSPARENT;
            }
            else
            {
                styles &= ~WS_EX_TRANSPARENT;
            }

            SetWindowLongPtr(_transparentWindowHandle, GWL_EXSTYLE, styles);
            ApplyTopmostForClickThrough(enable, force: true);
        }

        private static void ApplyTopmostForClickThrough(bool clickThroughEnabled, bool force = false)
        {
            if (_transparentWindowHandle == IntPtr.Zero)
            {
                return;
            }

            if (!force && _isTopmost == clickThroughEnabled)
            {
                return;
            }

            IntPtr target = clickThroughEnabled ? HWND_TOPMOST : HWND_NOTOPMOST;
            const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER;
            bool success = SetWindowPos(_transparentWindowHandle, target, 0, 0, 0, 0, flags);
            if (!success)
            {
                DebugLogger.PrintWarning("Failed to adjust window z-order for click-through toggle.");
                return;
            }

            _isTopmost = clickThroughEnabled;
        }

        private static IntPtr _transparentWindowHandle;
        private static bool _clickThroughEnabled;
        private static bool _isTopmost;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int LWA_COLORKEY = 0x00000001;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);

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

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint pvAttribute, uint cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

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
