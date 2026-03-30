using System;

namespace op.io
{
    [Flags]
    public enum GOFlags
    {
        None         = 0,
        Player       = 1 << 0,
        Static       = 1 << 1,
        Collidable   = 1 << 2,
        Destructible = 1 << 3,
    }
}
