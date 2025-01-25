using System;

namespace op.io
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new Core())
                game.Run();
        }
    }
}
