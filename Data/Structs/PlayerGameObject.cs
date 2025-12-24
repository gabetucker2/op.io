namespace op.io
{
    public readonly struct PlayerGameObject
    {
        public PlayerGameObject(SimpleGameObject baseObject, float baseSpeed, bool isPlayer, Attributes_Barrel barrel, Attributes_Body body)
        {
            BaseObject = baseObject;
            BaseSpeed = baseSpeed;
            IsPlayer = isPlayer;
            Barrel = barrel;
            Body = body;
        }

        public SimpleGameObject BaseObject { get; }
        public float BaseSpeed { get; }
        public bool IsPlayer { get; }
        public Attributes_Barrel Barrel { get; }
        public Attributes_Body Body { get; }
        public GameObjectStructSet StructSet =>
            BaseObject.StructSet | GameObjectStructSet.Agent | GameObjectStructSet.Barrel | GameObjectStructSet.Body;

        public Agent ToAgent()
        {
            Shape shape = BaseObject.CreateShape();
            return new Agent(
                BaseObject.Identity.Id,
                BaseObject.Identity.Name,
                BaseObject.Identity.Type,
                BaseObject.Transform.Position,
                BaseObject.Transform.Rotation,
                BaseObject.Physics.Mass,
                BaseObject.Physics.IsDestructible,
                BaseObject.Physics.IsCollidable,
                BaseObject.Physics.StaticPhysics,
                shape,
                BaseSpeed,
                IsPlayer,
                BaseObject.Appearance.FillColor,
                BaseObject.Appearance.OutlineColor,
                BaseObject.Appearance.OutlineWidth,
                Barrel,
                Body);
        }
    }
}
