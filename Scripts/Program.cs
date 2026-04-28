using System;

namespace op.io
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            LogFileHandler.InitializeSession();
            RuntimeErrorLogger.Initialize();
            using var game = new Core();
            game.Run();
        }
    }
}
