using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace op.io
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        //public RECT(int left, int top, int right, int bottom)
        //{
        //    Left = left;
        //    Top = top;
        //    Right = right;
        //    Bottom = bottom;
        //}
        //public int Width => Right - Left;
        //public int Height => Bottom - Top;
        //public override string ToString()
        //{
        //    return $"{{Left={Left},Top={Top},Right={Right},Bottom={Bottom}}}";
        //}
    }
}