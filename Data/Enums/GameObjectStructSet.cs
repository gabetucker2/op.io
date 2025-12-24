using System;

namespace op.io
{
    [Flags]
    public enum GameObjectStructSet
    {
        None = 0,
        Identity = 1 << 0,
        Transform = 1 << 1,
        Physics = 1 << 2,
        Geometry = 1 << 3,
        Appearance = 1 << 4,
        Agent = 1 << 5,
        Barrel = 1 << 6,
        Body = 1 << 7,
        FarmData = 1 << 8,
        BlockType = 1 << 9,
        Base = Identity | Transform | Physics | Geometry | Appearance,
        All = Base | Agent | Barrel | Body | FarmData | BlockType
    }
}
