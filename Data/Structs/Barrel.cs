using Attributes = op.io.Transform;

namespace op.io
{
    public readonly struct Barrel
    {
        public Barrel(Attributes_Barrel barrelAttributes, Attributes.Transform barrelTransform)
        {
            BarrelAttributes = barrelAttributes;
            BarrelTransform = barrelTransform;
        }

        public Barrel(Attributes_Barrel barrelAttributes)
            : this(barrelAttributes, default) { }

        public Attributes_Barrel BarrelAttributes { get; }
        public Attributes.Transform BarrelTransform { get; }
    }
}
