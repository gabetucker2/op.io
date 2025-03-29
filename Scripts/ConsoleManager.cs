using System;
using System.Runtime.InteropServices;

namespace op.io
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static bool ConsoleInitialized { get; private set; }

        public static void InitializeConsole()
        {
            if (ConsoleInitialized)
                return;

            AllocConsole();
            ConsoleInitialized = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int width = 225;
                int height = 40;
                Console.SetBufferSize(width, height + 800);
                Console.SetWindowSize(width, height);
            }

            DebugLogger.PrintMeta("Console initialized.");
        }

        public static void ResetConsole()
        {
            if (ConsoleInitialized)
            {
                Console.Clear();
                ConsoleInitialized = false;
            }
        }
    }
}
