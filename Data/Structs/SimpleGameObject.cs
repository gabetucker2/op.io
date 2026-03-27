using Microsoft.Xna.Framework;
using Attributes = op.io.Transform;

namespace op.io
{
    public readonly struct SimpleGameObject
    {
        public SimpleGameObject(
            Attributes.Identity identity,
            Body body,
            Attributes.Physics physics,
            Attributes.Geometry geometry,
            Attributes.Appearance appearance,
            Unit unit = default,
            GameObjectStructSet structSet = GameObjectStructSet.Base)
        {
            Identity = identity;
            Body = body;
            Physics = physics;
            Geometry = geometry;
            Appearance = appearance;
            Unit = unit;
            StructSet = structSet;
        }

        public Attributes.Identity Identity { get; }
        public Body Body { get; }
        public Attributes.Physics Physics { get; }
        public Attributes.Geometry Geometry { get; }
        public Attributes.Appearance Appearance { get; }
        public Unit Unit { get; }
        public GameObjectStructSet StructSet { get; }

        public bool Has(GameObjectStructSet set) => (StructSet & set) == set;

        public static bool TryFromGameObject(GameObject source, out SimpleGameObject archetype)
        {
            archetype = default;
            if (source == null || source.Shape == null)
            {
                DebugLogger.PrintError("SimpleGameObject creation failed: source is null or missing shape.");
                return false;
            }

            Attributes.Identity identity = new(source.ID, source.Name, source.Type);
            Attributes.Transform bodyTransform = new(source.Position, source.Rotation);
            Attributes.Physics physics = new(
                source.StaticPhysics ? Attributes.PhysicsMotion.Static : Attributes.PhysicsMotion.Dynamic,
                source.IsCollidable ? Attributes.CollisionMode.Collidable : Attributes.CollisionMode.NonCollidable,
                source.IsDestructible ? Attributes.DestructionMode.Destructible : Attributes.DestructionMode.Indestructible,
                source.Mass);

            Attributes.Shape shapeAttributes = new(source.Shape.Sides);
            Attributes.Geometry geometry = new(
                source.Shape.ShapeType,
                source.Shape.Width,
                source.Shape.Height,
                shapeAttributes);

            Attributes.Appearance appearance = new(
                source.FillColor,
                source.OutlineColor,
                source.OutlineWidth);

            Attributes_Body bodyAttributes = source is Agent agent ? agent.BodyAttributes : default;
            Body body = new(bodyAttributes, bodyTransform);

            archetype = new SimpleGameObject(
                identity,
                body,
                physics,
                geometry,
                appearance);

            return true;
        }

        public Shape CreateShape(bool isPrototype = false)
        {
            return new Shape(
                Geometry.ShapeType,
                Geometry.Width,
                Geometry.Height,
                Geometry.ShapeAttributes.Sides,
                Appearance.FillColor,
                Appearance.OutlineColor,
                Appearance.OutlineWidth,
                isPrototype);
        }

        public GameObject ToGameObject(bool isPrototype = false)
        {
            Shape shape = CreateShape(isPrototype);
            return new GameObject(
                Identity.Id,
                Identity.Name,
                Identity.Type,
                Body.BodyTransform.Position,
                Body.BodyTransform.Rotation,
                Physics.Mass,
                Physics.IsDestructible,
                Physics.IsCollidable,
                Physics.StaticPhysics,
                shape,
                Appearance.FillColor,
                Appearance.OutlineColor,
                Appearance.OutlineWidth,
                isPrototype);
        }

        public SimpleGameObject WithTransform(int id, Vector2 position, float rotation)
        {
            Attributes.Identity identity = new(id, Identity.Name, Identity.Type);
            Attributes.Transform bodyTransform = new(position, rotation);
            Body body = new(Body.BodyAttributes, bodyTransform);
            return new SimpleGameObject(
                identity,
                body,
                Physics,
                Geometry,
                Appearance,
                Unit,
                StructSet);
        }
    }
}
