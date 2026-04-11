namespace op.io
{
    public readonly struct PlayerGameObject
    {
        public PlayerGameObject(SimpleGameObject baseObject, float baseSpeed, bool isPlayer, Attributes_Barrel barrelAttributes, Attributes_Body bodyAttributes)
        {
            _baseSpeed = baseSpeed;
            _isPlayer = isPlayer;
            Player player = new(GeneratePlayerId());
            Barrel barrel = new(barrelAttributes);
            Unit unit = new(player, barrel);
            Body body = new(bodyAttributes, baseObject.Body.BodyTransform);
            BaseObject = new SimpleGameObject(
                baseObject.Identity,
                body,
                baseObject.Physics,
                baseObject.Geometry,
                baseObject.Appearance,
                unit,
                baseObject.StructSet);
        }

        private readonly float _baseSpeed;
        private readonly bool _isPlayer;

        public SimpleGameObject BaseObject { get; }

        public Unit Unit => BaseObject.Unit;
        public float BaseSpeed => _baseSpeed;
        public bool IsPlayer => _isPlayer;
        public Attributes_Barrel Barrel => BaseObject.Unit.Barrel.BarrelAttributes;
        public Attributes_Body Body => BaseObject.Body.BodyAttributes;

        public GameObjectStructSet StructSet =>
            BaseObject.StructSet | GameObjectStructSet.Agent | GameObjectStructSet.Barrel | GameObjectStructSet.Body;

        private static long GeneratePlayerId()
        {
            return System.Random.Shared.NextInt64(1_000_000_000L, 10_000_000_000L);
        }

        public Agent ToAgent()
        {
            Shape shape = BaseObject.CreateShape();
            Agent agent = new(
                BaseObject.Identity.Id,
                BaseObject.Identity.Name,
                BaseObject.Body.BodyTransform.Position,
                BaseObject.Body.BodyTransform.Rotation,
                BaseObject.Physics.Mass,
                BaseObject.Physics.IsDestructible,
                BaseObject.Physics.IsCollidable,
                BaseObject.Physics.DynamicPhysics,
                shape,
                _baseSpeed,
                _isPlayer,
                BaseObject.Appearance.FillColor,
                BaseObject.Appearance.OutlineColor,
                BaseObject.Appearance.OutlineWidth,
                Barrel,
                Body);
            agent.PlayerID = BaseObject.Unit.Player.PlayerID;
            return agent;
        }
    }
}
