using Microsoft.Xna.Framework;

namespace op.io // TODO: Migrate this script elsewhere
{
    public static class TypeConversionFunctions
    {

        public static System.Drawing.Point Vector2ToPoint(Vector2 vector)
        {
            return new System.Drawing.Point((int)vector.X, (int)vector.Y);
        }

        public static bool IntToBool(int value)
        {
            return value == 1;
        }

        public static uint XnaColorToColorRef(Color color)
        {
            // COLORREF is 0x00BBGGRR; alpha is ignored when using LWA_COLORKEY.
            return (uint)((color.B << 16) | (color.G << 8) | color.R);
        }

        public static System.Drawing.Color XnaColorToDrawingColor(Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

    }
}
