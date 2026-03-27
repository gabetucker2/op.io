using Attributes = op.io.Transform;

namespace op.io
{
    public readonly struct Body
    {
        public Body(Attributes_Body bodyAttributes, Attributes.Transform bodyTransform)
        {
            BodyAttributes = bodyAttributes;
            BodyTransform = bodyTransform;
        }

        public Attributes_Body BodyAttributes { get; }
        public Attributes.Transform BodyTransform { get; }
    }
}
