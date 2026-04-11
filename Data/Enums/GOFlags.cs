using System;

namespace op.io
{
    [Flags]
    public enum GOFlags
    {
        Player       = 1 << 0,
        Dynamic      = 1 << 1,
        Collidable   = 1 << 2,
        Destructible = 1 << 3,
        Interact     = 1 << 4,
        ZoneBlock    = 1 << 5,
        Prototype    = 1 << 6,
    }
}
