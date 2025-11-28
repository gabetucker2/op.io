using System;
using System.IO;
using System.Runtime.InteropServices;

namespace op.io
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;

        public static bool ConsoleInitialized { get; set; }

        public static void InitializeConsoleIfEnabled()
        {
            DebugLogger.PrintDebug("Initializing console...");
            SetConsoleVisibility(DebugModeHandler.DEBUGENABLED);
        }

        public static void InitializeConsole()
        {
            if (ConsoleInitialized)
                return;

            if (!AllocConsole())
            {
                DebugLogger.PrintError("AllocConsole failed; debug console will not be available.");
                return;
            }

            if (!RewireConsoleStreams())
            {
                DebugLogger.PrintError("Failed to bind console streams after AllocConsole; console output unavailable.");
                return;
            }

            ConsoleInitialized = true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int width = 225;
                int height = 40;
                Console.SetBufferSize(width, height + 800);
                Console.SetWindowSize(width, height);

                IntPtr hwnd = GetConsoleWindow();
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                }

                RestoreGameWindowFocus();
            }

            PrintQueuedMessages();
        }

        public static void SetConsoleVisibility(bool shouldBeVisible)
        {
            if (shouldBeVisible == ConsoleInitialized)
            {
                return;
            }

            if (shouldBeVisible)
            {
                InitializeConsole();
                if (ConsoleInitialized)
                {
                    DebugLogger.PrintDebug("Console opened due to debug mode being enabled.");
                    DebugLogger.ReplaySessionLogToConsole();
                }
                return;
            }

            ConsoleInitialized = false; // prevent writes while detaching
            DebugLogger.PrintDebug("Hiding console because debug mode was disabled.");

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    IntPtr hwnd = GetConsoleWindow();
                    if (hwnd != IntPtr.Zero)
                    {
                        ShowWindow(hwnd, SW_HIDE);
                    }

                    bool freed = FreeConsole();
                    if (!freed)
                    {
                        DebugLogger.PrintWarning("FreeConsole failed while hiding the debug console.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to hide the debug console: {ex.Message}");
            }

            ConsoleInitialized = false;
        }

        public static void ResetConsole()
        {
            if (ConsoleInitialized)
            {
                Console.Clear();
                SetConsoleVisibility(false);
            }
        }

        private static void RestoreGameWindowFocus()
        {
            try
            {
                IntPtr windowHandle = Core.Instance?.Window?.Handle ?? IntPtr.Zero;
                if (windowHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(windowHandle);
                }
            }
            catch
            {
                // Non-fatal; ignore focus restoration failures.
            }
        }

        private static bool RewireConsoleStreams()
        {
            try
            {
                var stdout = Console.OpenStandardOutput();
                var stderr = Console.OpenStandardError();
                var stdin = Console.OpenStandardInput();

                var standardOutput = new StreamWriter(stdout) { AutoFlush = true };
                var standardError = new StreamWriter(stderr) { AutoFlush = true };
                var standardInput = new StreamReader(stdin);

                Console.SetOut(standardOutput);
                Console.SetError(standardError);
                Console.SetIn(standardInput);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void PrintQueuedMessages()
        {
            var logs = DebugLogger.FlushQueuedLogs();

            foreach (var log in logs)
            {
                string logMessage = $"[DEFERRED OUTPUT] {log.Item1}";           // Formatted message string
                ConsoleColor color = log.Item2;                                 // Console color for the message
                int suppressionBehavior = log.Item3;                            // Suppression behavior for the message

                DebugLogger.PrintToConsole(logMessage, color, suppressionBehavior);
            }
        }

    }
}
