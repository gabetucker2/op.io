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

        public static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

    }
}
