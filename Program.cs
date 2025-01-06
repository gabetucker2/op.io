using System;

namespace op_io
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
