using Microsoft.Xna.Framework;

namespace op.io
{
    public readonly struct SimpleGameObject
    {
        public SimpleGameObject(
            Attributes.Identity identity,
            Attributes.Transform transform,
            Attributes.Physics physics,
            Attributes.Geometry geometry,
            Attributes.Appearance appearance,
            GameObjectStructSet structSet = GameObjectStructSet.Base)
        {
            Identity = identity;
            Transform = transform;
            Physics = physics;
            Geometry = geometry;
            Appearance = appearance;
            StructSet = structSet;
        }

        public Attributes.Identity Identity { get; }
        public Attributes.Transform Transform { get; }
        public Attributes.Physics Physics { get; }
        public Attributes.Geometry Geometry { get; }
        public Attributes.Appearance Appearance { get; }
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
            Attributes.Transform transform = new(source.Position, source.Rotation);
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

            archetype = new SimpleGameObject(
                identity,
                transform,
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
                Transform.Position,
                Transform.Rotation,
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
            Attributes.Transform transform = new(position, rotation);
            return new SimpleGameObject(
                identity,
                transform,
                Physics,
                Geometry,
                Appearance,
                StructSet);
        }
    }
}
