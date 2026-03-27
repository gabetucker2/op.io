using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace op.io
{
    public struct Properties
    {
        public enum RowKind
        {
            Text,
            Color,
            BulletList
        }

        public readonly struct Row
        {
            public Row(string label, string value)
            {
                Kind = RowKind.Text;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = default;
                Items = Array.Empty<string>();
            }

            public Row(string label, Color color, string value)
            {
                Kind = RowKind.Color;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = color;
                Items = Array.Empty<string>();
            }

            public Row(string label, string[] items)
            {
                Kind = RowKind.BulletList;
                Label = label ?? string.Empty;
                Value = string.Empty;
                Color = default;
                Items = items ?? Array.Empty<string>();
            }

            public RowKind Kind { get; }
            public string Label { get; }
            public string Value { get; }
            public Color Color { get; }
            public string[] Items { get; }
            public int LineCount => Kind == RowKind.BulletList ? Math.Max(1, Items?.Length ?? 1) : 1;
        }

        public readonly struct Section
        {
            public Section(string title, IEnumerable<Row> rows, int depth = 0)
            {
                Title = title ?? string.Empty;
                Rows = rows ?? Array.Empty<Row>();
                Depth = depth;
            }

            public string Title { get; }
            public IEnumerable<Row> Rows { get; }
            public int Depth { get; }
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

        public IEnumerable<Section> Sections
        {
            get
            {
                if (!HasTarget)
                    yield break;

                // Object (depth 0) — base identity, physics flags, geometry, appearance
                yield return new Section("Object", ObjectRows(), 0);

                // Body (depth 1) — groups body transform + body attributes
                yield return new Section("Body", [], 1);
                yield return new Section("Body Transform", BodyTransformRows(), 2);

                if (_target.Source is Agent agent)
                {
                    yield return new Section("Body Attributes", ReflectAttributeStruct(agent.BodyAttributes), 2);

                    // Unit (depth 1) — character-level attributes + player + barrel sub-structs
                    yield return new Section("Unit", UnitRows(agent), 1);
                    yield return new Section("Player", PlayerRows(agent), 2);

                    // Barrel (depth 2) — groups barrel attributes + barrel transform
                    yield return new Section("Barrel", [], 2);
                    yield return new Section("Barrel Attributes", ReflectAttributeStruct(agent.BarrelAttributes), 3);
                    yield return new Section("Barrel Transform", BarrelTransformRows(agent), 3);
                }
            }
        }

        // Object-level rows: identity, physics flags, and parent/child relationships
        private IEnumerable<Row> ObjectRows()
        {
            yield return new Row("ID", _target.Id.ToString());
            yield return new Row("Type", _target.Type);
            yield return BuildFlagsRow();

            if (_target.Source?.Parent != null)
            {
                GameObject p = _target.Source.Parent;
                yield return new Row("Parent", string.IsNullOrEmpty(p.Name) ? $"{p.Type} (ID {p.ID})" : $"{p.Name} (ID {p.ID})");
            }

            if (_target.Source?.Children != null && _target.Source.Children.Count > 0)
            {
                string[] childNames = new string[_target.Source.Children.Count];
                for (int i = 0; i < _target.Source.Children.Count; i++)
                {
                    GameObject c = _target.Source.Children[i];
                    childNames[i] = string.IsNullOrEmpty(c.Name) ? (c.Type ?? "Child") : c.Name;
                }
                yield return new Row("Children", childNames);
            }
        }

        // Body Transform rows: position, rotation, size, shape, mass, colors
        private readonly IEnumerable<Row> BodyTransformRows()
        {
            yield return new Row("Position", $"{_target.Position.X:0.0}, {_target.Position.Y:0.0}");
            float rotationDeg = MathHelper.ToDegrees(_target.Rotation);
            yield return new Row("Rotation", $"{rotationDeg:0.0} deg");
            yield return new Row("Size", $"{_target.Width} x {_target.Height}");
            yield return new Row("Shape", BuildShapeText());
            yield return new Row("Mass", $"{_target.Mass:0.##}");
            yield return new Row("Fill", _target.FillColor, ToHex(_target.FillColor));
            yield return new Row("Outline", _target.OutlineColor, ToHex(_target.OutlineColor));
        }

        // Unit-level character rows
        private static IEnumerable<Row> UnitRows(Agent agent)
        {
            yield return new Row("Name", string.IsNullOrWhiteSpace(agent.UnitAttributes.Name) ? "-" : agent.UnitAttributes.Name);
            yield return new Row("Base Speed", $"{agent.BaseSpeed:0.##}");
            yield return new Row("Death Reward", $"{agent.UnitAttributes.DeathPointReward:0.##}");
            yield return new Row("Body Switch Speed", $"{agent.UnitAttributes.BodySwitchSpeed:0.##}");
            yield return new Row("Barrel Switch Speed", $"{agent.UnitAttributes.BarrelSwitchSpeed:0.##}");
        }

        // Player-specific rows
        private static IEnumerable<Row> PlayerRows(Agent agent)
        {
            yield return new Row("Player ID", agent.PlayerID.ToString());
        }

        // Barrel Transform rows — sourced from the agent's BarrelObject child
        private static IEnumerable<Row> BarrelTransformRows(Agent agent)
        {
            GameObject barrel = agent.BarrelObject;
            if (barrel == null)
            {
                yield return new Row("Offset", "0.0, 0.0");
                yield return new Row("Rotation", "0.0 deg");
                yield break;
            }

            Vector2 offset = barrel.Position;
            yield return new Row("Offset", $"{offset.X:0.0}, {offset.Y:0.0}");
            float rotDeg = MathHelper.ToDegrees(agent.Rotation);
            yield return new Row("Rotation", $"{rotDeg:0.0} deg");
            yield return new Row("Size", $"{barrel.Shape.Width} x {barrel.Shape.Height}");
            yield return new Row("Fill", barrel.FillColor, ToHex(barrel.FillColor));
            yield return new Row("Outline", barrel.OutlineColor, ToHex(barrel.OutlineColor));
        }

        private readonly Row BuildFlagsRow()
        {
            List<string> parts = new();
            if (_target.IsPlayer)
                parts.Add("Player");
            parts.Add(_target.StaticPhysics ? "Static" : "Dynamic");
            parts.Add(_target.IsCollidable ? "Collidable" : "Non-collidable");
            parts.Add(_target.IsDestructible ? "Destructible" : "Indestructible");
            return new Row("Flags", parts.ToArray());
        }

        private static IEnumerable<Row> ReflectAttributeStruct(object attrStruct)
        {
            if (attrStruct == null)
                yield break;

            foreach (PropertyInfo prop in attrStruct.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = prop.GetValue(attrStruct);
                yield return MakeRow(prop.Name, value);
            }
        }

        private static Row MakeRow(string label, object value)
        {
            return value switch
            {
                Color color => new Row(label, color, ToHex(color)),
                float f => new Row(label, $"{f:0.##}"),
                double d => new Row(label, $"{d:0.##}"),
                int i => new Row(label, i.ToString()),
                bool b => new Row(label, b ? "Yes" : "No"),
                _ => new Row(label, value?.ToString() ?? "-")
            };
        }

        private readonly string BuildShapeText()
        {
            Shape shape = _target.Shape;
            if (shape != null && shape.ShapeType == "Polygon" && _target.Sides > 0)
                return $"Polygon ({_target.Sides} sides)";
            return shape?.ShapeType ?? "-";
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        }
    }
}
