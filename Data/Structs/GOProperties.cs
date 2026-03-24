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
            public Section(string title, IEnumerable<Row> rows)
            {
                Title = title ?? string.Empty;
                Rows = rows ?? Array.Empty<Row>();
            }

            public string Title { get; }
            public IEnumerable<Row> Rows { get; }
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

                yield return new Section("Transform", TransformRows());

                if (_target.Source is Agent agent)
                {
                    yield return BuildStructSection("Body", agent.BodyAttributes);
                    if (agent.IsPlayer)
                        yield return BuildStructSection("Meta", agent.MetaAttributes);
                }
            }
        }

        private static Section BuildStructSection(string title, object structValue)
        {
            return new Section(title, ReflectAttributeStruct(structValue));
        }

        private IEnumerable<Row> TransformRows()
        {
            yield return new Row("Type", $"{_target.Type} (ID {_target.Id})");
            yield return BuildFlagsRow();
            yield return new Row("Position", $"{_target.Position.X:0.0}, {_target.Position.Y:0.0}");

            float rotationDeg = MathHelper.ToDegrees(_target.Rotation);
            yield return new Row("Rotation", $"{rotationDeg:0.0} deg");

            yield return new Row("Size", BuildSizeText());
            yield return new Row("Mass", $"{_target.Mass:0.##}");
            yield return new Row("Fill", _target.FillColor, ToHex(_target.FillColor));
            yield return new Row("Outline", _target.OutlineColor, ToHex(_target.OutlineColor));
        }

        private Row BuildFlagsRow()
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

        private string BuildSizeText()
        {
            string sizeText = $"{_target.Width} x {_target.Height}";
            Shape shape = _target.Shape;
            if (shape != null && shape.ShapeType == "Polygon" && _target.Sides > 0)
                sizeText = $"{sizeText} ({_target.Sides}-sided polygon)";
            else if (shape != null)
                sizeText = $"{sizeText} ({shape.ShapeType})";

            return sizeText;
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        }
    }
}
