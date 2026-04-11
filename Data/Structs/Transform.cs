using Microsoft.Xna.Framework;

namespace op.io.Transform
{
    public readonly struct Identity
    {
        public Identity(int id, string name)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        public int Id { get; }
        public string Name { get; }
    }

    public readonly struct Transform
    {
        public Transform(Vector2 position, float rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector2 Position { get; }
        public float Rotation { get; }
    }

    public readonly struct Geometry
    {
        public Geometry(string shapeType, int width, int height, Shape shapeAttributes)
        {
            ShapeType = string.IsNullOrWhiteSpace(shapeType) ? "Rectangle" : shapeType;
            Width = width;
            Height = height;
            ShapeAttributes = shapeAttributes;
        }

        public string ShapeType { get; }
        public int Width { get; }
        public int Height { get; }
        public Shape ShapeAttributes { get; }
    }

    public readonly struct Appearance
    {
        public Appearance(Color fillColor, Color outlineColor, int outlineWidth)
        {
            FillColor = fillColor;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
        }

        public Color FillColor { get; }
        public Color OutlineColor { get; }
        public int OutlineWidth { get; }
    }

    public enum PhysicsMotion
    {
        Static,
        Dynamic
    }

    public enum CollisionMode
    {
        Collidable,
        NonCollidable
    }

    public enum DestructionMode
    {
        Destructible,
        Indestructible
    }

    public struct Physics
    {
        public PhysicsMotion Motion { get; set; }
        public CollisionMode Collision { get; set; }
        public DestructionMode Destruction { get; set; }
        public float Mass { get; set; }

        public Physics(PhysicsMotion motion, CollisionMode collision, DestructionMode destruction)
        {
            Motion = motion;
            Collision = collision;
            Destruction = destruction;
            Mass = 0f;
        }

        public Physics(PhysicsMotion motion, CollisionMode collision, DestructionMode destruction, float mass)
        {
            Motion = motion;
            Collision = collision;
            Destruction = destruction;
            Mass = mass;
        }

        public bool DynamicPhysics => Motion == PhysicsMotion.Dynamic;
        public bool IsCollidable => Collision == CollisionMode.Collidable;
        public bool IsDestructible => Destruction == DestructionMode.Destructible;

        public void ApplyTo(global::op.io.GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.DynamicPhysics = DynamicPhysics;
            target.IsCollidable = IsCollidable;
            target.IsDestructible = IsDestructible;
            target.Mass = Mass;
        }
    }

    public struct Shape
    {
        public int Sides { get; set; }

        public Shape(int sides)
        {
            Sides = sides;
        }

        public bool IsPolygon => Sides >= 3;
    }
}
