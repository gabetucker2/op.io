using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public struct Attributes_Barrel
    {
        public float BulletDamage { get; set; }
        public float BulletPenetration { get; set; }
        public float BulletSpeed { get; set; }
        public float BulletRange { get; set; }
        public float ReloadSpeed { get; set; }
        public float BulletHealth { get; set; }
        public float BulletMaxLifespan { get; set; }
        public string BulletSpecialBuff { get; set; }
    }

    public struct Attributes_Body
    {
        public float MaxHealth { get; set; }
        public float HealthRegen { get; set; }
        public float HealthArmor { get; set; }

        public float MaxShield { get; set; }
        public float ShieldRegen { get; set; }
        public float ShieldArmor { get; set; }
        
        public float BodyPenetration { get; set; }
        public float BodyCollisionDamage { get; set; }
        public float BodyKnockback { get; set; }
        
        public float CollisionDamageResistance { get; set; }
        public float BulletDamageResistance { get; set; }
        
        public float Speed { get; set; }
        public float RotationSpeed { get; set; }
        public float BodySpecialBuff { get; set; }
    }

    public struct Attributes_Misc
    {
        public float BodySwitchSpeed { get; set; }
        public float BarrelSwitchSpeed { get; set; }
        public float DeathPointReward { get; set; }
    }

    public struct Attributes_Meta
    {        
        public string Name { get; set; }
        public string Notes { get; set; }
    }

    public struct Properties
    {
        public enum RowKind
        {
            Text,
            Color
        }

        public readonly struct Row
        {
            public Row(string label, string value)
            {
                Kind = RowKind.Text;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = default;
            }

            public Row(string label, Color color, string value)
            {
                Kind = RowKind.Color;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = color;
            }

            public RowKind Kind { get; }
            public string Label { get; }
            public string Value { get; }
            public Color Color { get; }
        }

        private InspectableObjectInfo _target;
        private InspectableObjectInfo _lockedTarget;

        public Properties(InspectableObjectInfo target, InspectableObjectInfo lockedTarget = null)
        {
            _target = target;
            _lockedTarget = lockedTarget;
        }

        public InspectableObjectInfo Target
        {
            get => _target;
            set => _target = value;
        }

        public InspectableObjectInfo LockedTarget
        {
            get => _lockedTarget;
            set => _lockedTarget = value;
        }

        public bool HasTarget => _target != null && _target.IsValid;

        public string Title => HasTarget ? _target.DisplayName : string.Empty;

        public bool IsLocked => HasTarget &&
            _lockedTarget != null &&
            ReferenceEquals(_lockedTarget.Source, _target.Source);

        public string LockedTag => IsLocked ? "LOCKED" : string.Empty;

        public IEnumerable<Row> Rows
        {
            get
            {
                if (!HasTarget)
                {
                    yield break;
                }

                yield return new Row("Type", $"{_target.Type} (ID {_target.Id})");
                yield return new Row("Flags", BuildFlags());
                yield return new Row("Position", $"{_target.Position.X:0.0}, {_target.Position.Y:0.0}");

                float rotationDeg = MathHelper.ToDegrees(_target.Rotation);
                yield return new Row("Rotation", $"{rotationDeg:0.0} deg");

                yield return new Row("Size", BuildSizeText());
                yield return new Row("Mass", $"{_target.Mass:0.##}");
                yield return new Row("Fill", _target.FillColor, ToHex(_target.FillColor));
                yield return new Row("Outline", _target.OutlineColor, ToHex(_target.OutlineColor));
            }
        }

        private string BuildFlags()
        {
            List<string> parts = new();
            if (_target.IsPlayer)
            {
                parts.Add("Player");
            }

            parts.Add(_target.StaticPhysics ? "Static" : "Dynamic");
            parts.Add(_target.IsCollidable ? "Collidable" : "Non-collidable");
            parts.Add(_target.IsDestructible ? "Destructible" : "Indestructible");

            return string.Join(" | ", parts);
        }

        private string BuildSizeText()
        {
            string sizeText = $"{_target.Width} x {_target.Height}";
            Shape shape = _target.Shape;
            if (shape != null && shape.ShapeType == "Polygon" && _target.Sides > 0)
            {
                sizeText = $"{sizeText} ({_target.Sides}-sided polygon)";
            }
            else if (shape != null)
            {
                sizeText = $"{sizeText} ({shape.ShapeType})";
            }

            return sizeText;
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        }
    }
}

namespace op.io.Attributes
{
    public readonly struct Identity
    {
        public Identity(int id, string name, string type)
        {
            Id = id;
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
        }

        public int Id { get; }
        public string Name { get; }
        public string Type { get; }
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

        public bool StaticPhysics => Motion == PhysicsMotion.Static;
        public bool IsCollidable => Collision == CollisionMode.Collidable;
        public bool IsDestructible => Destruction == DestructionMode.Destructible;

        public void ApplyTo(global::op.io.GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.StaticPhysics = StaticPhysics;
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
