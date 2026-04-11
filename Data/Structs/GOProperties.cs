using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace op.io
{
    public struct GOProperties
    {
        public int     Id               { get; set; }
        public GOFlags Flags            { get; set; }
        public float   CurrentXP        { get; set; }
        public float   MaxXP            { get; set; }
        public float   DeathPointReward { get; set; }
        public float   CurrentHealth    { get; set; }
        public float   CurrentShield    { get; set; }
    }

    public struct Properties
    {
        public enum RowKind
        {
            Text,
            Color,
            BulletList,
            BarGraph,
            CombinedHealthBar,
            Boolean
        }

        public readonly struct Row
        {
            // ── Text / basic ──────────────────────────────────────────────────────
            public Row(string label, string value, bool isHidden = false, string[] affectsList = null)
            {
                Kind = RowKind.Text;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = default;
                Items = Array.Empty<string>();
                CurrentValue = 0f;
                MaxValue = 0f;
                CurrentShieldValue = 0f;
                MaxShieldValue = 0f;
                SegmentCount = 0;
                BarFillColor = null;
                BoolValue = false;
                IsHidden = isHidden;
                AffectsList = affectsList ?? Array.Empty<string>();
            }

            // ── Color swatch ──────────────────────────────────────────────────────
            public Row(string label, Color color, string value, bool isHidden = false)
            {
                Kind = RowKind.Color;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = color;
                Items = Array.Empty<string>();
                CurrentValue = 0f;
                MaxValue = 0f;
                CurrentShieldValue = 0f;
                MaxShieldValue = 0f;
                SegmentCount = 0;
                BarFillColor = null;
                BoolValue = false;
                IsHidden = isHidden;
                AffectsList = Array.Empty<string>();
            }

            // ── Bullet list ───────────────────────────────────────────────────────
            public Row(string label, string[] items)
            {
                Kind = RowKind.BulletList;
                Label = label ?? string.Empty;
                Value = string.Empty;
                Color = default;
                Items = items ?? Array.Empty<string>();
                CurrentValue = 0f;
                MaxValue = 0f;
                CurrentShieldValue = 0f;
                MaxShieldValue = 0f;
                SegmentCount = 0;
                BarFillColor = null;
                BoolValue = false;
                IsHidden = false;
                AffectsList = Array.Empty<string>();
            }

            // ── Bar graph ─────────────────────────────────────────────────────────
            public Row(string label, float currentValue, float maxValue, int segmentCount = 10, Color? barFillColor = null)
            {
                Kind = RowKind.BarGraph;
                Label = label ?? string.Empty;
                Value = $"{currentValue:0.##} / {maxValue:0.##}";
                Color = default;
                Items = [];
                CurrentValue = currentValue;
                MaxValue = maxValue;
                CurrentShieldValue = 0f;
                MaxShieldValue = 0f;
                SegmentCount = segmentCount;
                BarFillColor = barFillColor;
                BoolValue = false;
                IsHidden = false;
                AffectsList = Array.Empty<string>();
            }

            // ── Combined health+shield bar ────────────────────────────────────────
            public Row(string label, float currentHealth, float maxHealth, float currentShield, float maxShield, int segmentCount)
            {
                Kind = RowKind.CombinedHealthBar;
                Label = label ?? string.Empty;
                Value = string.Empty;
                Color = default;
                Items = [];
                CurrentValue = currentHealth;
                MaxValue = maxHealth;
                CurrentShieldValue = currentShield;
                MaxShieldValue = maxShield;
                SegmentCount = segmentCount;
                BarFillColor = null;
                BoolValue = false;
                IsHidden = false;
                AffectsList = Array.Empty<string>();
            }

            // ── Boolean indicator ─────────────────────────────────────────────────
            public Row(string label, bool boolValue, bool isHidden = false)
            {
                Kind = RowKind.Boolean;
                Label = label ?? string.Empty;
                Value = string.Empty;
                Color = default;
                Items = Array.Empty<string>();
                CurrentValue = 0f;
                MaxValue = 0f;
                CurrentShieldValue = 0f;
                MaxShieldValue = 0f;
                SegmentCount = 0;
                BarFillColor = null;
                BoolValue = boolValue;
                IsHidden = isHidden;
                AffectsList = Array.Empty<string>();
            }

            public RowKind Kind { get; }
            public string  Label { get; }
            public string  Value { get; }
            public Color   Color { get; }
            public string[] Items { get; }
            public float   CurrentValue { get; }
            public float   MaxValue { get; }
            public float   CurrentShieldValue { get; }
            public float   MaxShieldValue { get; }
            public int     SegmentCount { get; }
            public Color?  BarFillColor { get; }
            public bool    BoolValue { get; }

            /// <summary>True if this is a hidden (derived) attribute.</summary>
            public bool    IsHidden { get; }
            /// <summary>List of other attributes this hidden attribute affects (shown in 3rd column).</summary>
            public string[] AffectsList { get; }

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

        private const string UnitSectionTitle = "Unit";

        public IEnumerable<Section> Sections
        {
            get
            {
                if (!HasTarget)
                    yield break;

                Agent agent = _target.Source as Agent;

                // ── Unit + sub-sections (Agent only, shown first) ─────────────────
                if (agent != null)
                {
                    yield return new Section(UnitSectionTitle, UnitRows(agent), 0);
                    if (agent.IsPlayer)
                        yield return new Section($"Player: {UnitSectionTitle}", PlayerRows(agent), 2);
                }

                // ── GameObject ────────────────────────────────────────────────────
                yield return new Section("GameObject", GameObjectRows(), agent != null ? 1 : 0);

                // ── Destructible Attributes — only for destructible objects ───────
                if (_target.IsDestructible)
                {
                    yield return new Section("Destructible Attributes", DestructibleRows(), 1);

                    // Farm — sub-section under Destructible Attributes, farm objects only
                    if (_target.Source.FarmAttributes.HasValue)
                        yield return new Section("Farm", ReflectAttributeStruct(_target.Source.FarmAttributes.Value), 2);
                }

                // ── Body ──────────────────────────────────────────────────────────
                if (agent != null)
                {
                    yield return new Section("Body", System.Array.Empty<Row>(), 1);
                    yield return new Section("Body Transform", BodyTransformRows(), 2);
                    yield return new Section("Body Attributes", BodyAttributeRows(agent), 2);

                    yield return new Section("Barrel", System.Array.Empty<Row>(), 2);
                    yield return new Section("Barrel Attributes", BarrelAttributeRows(agent), 3);
                    yield return new Section("Barrel Transform", BarrelTransformRows(agent), 3);
                }
                else
                {
                    yield return new Section("Body Transform", BodyTransformRows(), 1);
                    if (_target.IsDestructible)
                        yield return new Section("Body Attributes", NonAgentBodyAttributeRows(), 2);
                }
            }
        }

        // ── Body attribute rows with hidden attributes interspersed ──────────────

        private IEnumerable<Row> BodyAttributeRows(Agent agent)
        {
            Attributes_Body a = agent.BodyAttributes;
            float baseSpeed   = agent.BaseSpeed;
            float control     = a.Control > 0f ? a.Control : 1f;

            // ── Mass ──────────────────────────────────────────────────────────────
            yield return new Row("Mass",               $"{a.Mass:0.##}");
            yield return new Row("Max Health",         $"{AttributeDerived.MaxHealth(a.Mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsMaxHealth);

            // ── Health group ──────────────────────────────────────────────────────
            yield return new Row("Health Regen",       $"{a.HealthRegen:0.##}");
            yield return new Row("Health Armor",       $"{a.HealthArmor:0.##}");
            yield return new Row("Dmg Regen Delay",    $"{a.HealthRegenDelay:0.##}");

            // ── Shield group ──────────────────────────────────────────────────────
            yield return new Row("Max Shield",         $"{a.MaxShield:0.##}");
            yield return new Row("Shield Regen",       $"{a.ShieldRegen:0.##}");
            yield return new Row("Shield Armor",       $"{a.ShieldArmor:0.##}");
            yield return new Row("Dmg Shield Delay",   $"{a.ShieldRegenDelay:0.##}");

            // ── Combat group ──────────────────────────────────────────────────────
            yield return new Row("Body Coll. Damage",  $"{a.BodyCollisionDamage:0.##}");
            yield return new Row("Body Penetration",   $"{a.BodyPenetration:0.##}");

            float kScale = CollisionResolver.KnockbackMassScale;
            yield return new Row("Body Knockback",     $"{AttributeDerived.BodyKnockback(a.Mass, kScale):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBodyKnockback);

            // ── Resistance group ──────────────────────────────────────────────────
            yield return new Row("Coll. Dmg Resist",   $"{a.CollisionDamageResistance:0.##}");
            yield return new Row("Bullet Dmg Resist",  $"{a.BulletDamageResistance:0.##}");

            // ── Movement group ────────────────────────────────────────────────────
            yield return new Row("Speed",              $"{a.Speed:0.##}");
            yield return new Row("Control",            $"{a.Control:0.##}");
            yield return new Row("Rotation Speed",     $"{(float)(180.0 / AttributeDerived.RotationDelay(control)):0.##} deg/s",
                                 isHidden: true, affectsList: AttributeDerived.AffectsRotationSpeed);
            yield return new Row("Accel. Speed",       $"{(1f / AttributeDerived.AccelerationDelay(control)):0.##} /s",
                                 isHidden: true, affectsList: AttributeDerived.AffectsAccelerationSpeed);

            // ── Action buff ───────────────────────────────────────────────────────
            yield return new Row("Action Buff",        $"{a.BodyActionBuff:0.##}");
        }

        // ── Barrel attribute rows with hidden attributes interspersed ─────────────

        private static IEnumerable<Row> BarrelAttributeRows(Agent agent)
        {
            Attributes_Barrel a = agent.BarrelCount > 0
                ? agent.Barrels[agent.ActiveBarrelIndex].Attrs
                : default;

            float mass   = a.BulletMass > 0f ? a.BulletMass : BulletManager.DefaultBulletMass;
            float radius = AttributeDerived.BulletRadius(mass, BulletManager.BulletRadiusScalar);
            float drag   = AttributeDerived.BulletDrag(radius, BulletManager.AirResistanceScalar, BulletManager.DefaultBulletDragFactor);

            yield return new Row("Bullet Damage",      $"{a.BulletDamage:0.##}");
            yield return new Row("Bullet Penetration", $"{a.BulletPenetration:0.##}");
            yield return new Row("Bullet Knockback",   $"{AttributeDerived.BulletKnockback(a.BulletPenetration, BulletManager.BulletKnockbackScalar):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletKnockback);
            yield return new Row("Reload Speed",       $"{a.ReloadSpeed:0.##}");

            yield return new Row("Recoil Mass",        $"{AttributeDerived.RecoilMass(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsRecoilMass);
            yield return new Row("Bullet Health",      $"{AttributeDerived.BulletHealth(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletHealth);
            yield return new Row("Bullet Radius",      $"{radius:0.##} px",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletRadius);

            yield return new Row("Bullet Mass",        $"{a.BulletMass:0.##}");
            yield return new Row("Bullet Speed",       $"{a.BulletSpeed:0.##}");
            yield return new Row("Bullet Lifespan",    $"{a.BulletMaxLifespan:0.##}");

            yield return new Row("Bullet Drag",        $"{drag:0.####}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletDrag);

            // Bullet effectors — hidden (derived from BulletMass)
            yield return new Row("Bullet Health Regen",      $"{AttributeDerived.BulletHealthRegen(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletHealthRegen);
            yield return new Row("Bullet Dmg Regen Delay",   $"{AttributeDerived.BulletHealthRegenDelay(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletHealthRegenDelay);
            yield return new Row("Bullet Health Armor",      $"{AttributeDerived.BulletHealthArmor(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletHealthArmor);
            yield return new Row("Bullet Coll. Dmg Resist",  $"{AttributeDerived.BulletCollisionDamageResistance(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletCollisionDamageResistance);
            yield return new Row("Bullet Dmg Resist",        $"{AttributeDerived.BulletBarrelDamageResistance(mass):0.##}",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBulletDamageResistance);

            // Bullet effectors — normal
            yield return new Row("Bullet Control",           $"{a.BulletControl:0.##}");
        }

        // ── Object-level rows: identity, flags ───────────────────────────────────

        private IEnumerable<Row> GameObjectRows()
        {
            yield return new Row("Name", string.IsNullOrWhiteSpace(_target.Name) ? "-" : _target.Name);
            yield return new Row("ID", _target.Id.ToString());
            yield return BuildFlagsRow();
        }

        // ── Destructible section rows ─────────────────────────────────────────────

        private IEnumerable<Row> DestructibleRows()
        {
            yield return new Row("Death XP Reward", $"{_target.DeathPointReward:0.##}");
            yield return new Row("Current Health",  $"{_target.CurrentHealth:0.##} / {_target.MaxHealth:0.##}");

            if (_target.MaxShield > 0f)
                yield return new Row("Current Shield", $"{_target.CurrentShield:0.##} / {_target.MaxShield:0.##}");

            float healthLastDmg = _target.Source?.LastHealthDamageTime ?? float.NegativeInfinity;
            float shieldLastDmg = _target.Source?.LastShieldDamageTime ?? float.NegativeInfinity;
            float healthRegenProgress = ComputeRegenProgress(healthLastDmg, GetHealthRegenDelay());
            float shieldRegenProgress = ComputeRegenProgress(shieldLastDmg, GetShieldRegenDelay());

            yield return new Row("Health Regen", FormatRegenProgress(healthRegenProgress));
            if (_target.MaxShield > 0f)
                yield return new Row("Shield Regen", FormatRegenProgress(shieldRegenProgress));
        }

        private float ComputeRegenProgress(float lastDamageTime, float regenDelay)
        {
            if (regenDelay <= 0f) return 1f;
            float elapsed = Core.GAMETIME - lastDamageTime;
            return MathHelper.Clamp(elapsed / regenDelay, 0f, 1f);
        }

        private static string FormatRegenProgress(float progress)
        {
            if (progress >= 1f) return "Done";
            return $"{progress * 100f:0}%";
        }

        private float GetHealthRegenDelay()
        {
            if (_target.Source is Agent a) return a.BodyAttributes.HealthRegenDelay;
            return _target.Source?.HealthRegenDelay ?? 5f;
        }

        private float GetShieldRegenDelay()
        {
            if (_target.Source is Agent a) return a.BodyAttributes.ShieldRegenDelay;
            return _target.Source?.ShieldRegenDelay ?? 5f;
        }

        // ── Body attribute rows for non-Agent destructible objects ────────────────

        private IEnumerable<Row> NonAgentBodyAttributeRows()
        {
            GameObject src = _target.Source;

            yield return new Row("Max Health",         $"{_target.MaxHealth:0.##}");
            yield return new Row("Health Regen",       $"{(src?.HealthRegen ?? 0f):0.##}");
            yield return new Row("Health Regen Delay", $"{(src?.HealthRegenDelay ?? 5f):0.##}");
            yield return new Row("Health Armor",       $"{(src?.HealthArmor ?? 0f):0.##}");

            yield return new Row("Max Shield",         $"{_target.MaxShield:0.##}");
            yield return new Row("Shield Regen",       $"{(src?.ShieldRegen ?? 0f):0.##}");
            yield return new Row("Shield Regen Delay", $"{(src?.ShieldRegenDelay ?? 5f):0.##}");
            yield return new Row("Shield Armor",       $"{(src?.ShieldArmor ?? 0f):0.##}");

            yield return new Row("Body Penetration",        $"{(src?.BodyPenetration ?? 0f):0.##}");
            yield return new Row("Body Collision Damage",   $"{(src?.BodyCollisionDamage ?? 0f):0.##}");
            yield return new Row("Coll. Dmg Resistance",    $"{(src?.CollisionDamageResistance ?? 0f):0.##}");
            yield return new Row("Bullet Dmg Resistance",   $"{(src?.BulletDamageResistance ?? 0f):0.##}");
        }

        // ── Body Transform rows: position, rotation, size, shape, mass, colors ───

        private readonly IEnumerable<Row> BodyTransformRows()
        {
            yield return new Row("Position", $"{_target.Position.X:0.0}, {_target.Position.Y:0.0}");
            float rotationDeg = MathHelper.ToDegrees(_target.Rotation);
            yield return new Row("Rotation", $"{rotationDeg:0.0} deg");
            yield return new Row("Size",     $"{_target.Width} x {_target.Height}");
            yield return new Row("Shape",    BuildShapeText());
            yield return new Row("Mass",       $"{_target.Mass:0.##}");
            yield return new Row("Draw Layer", $"{_target.DrawLayer}");
            yield return new Row("Fill",       _target.FillColor, ToHex(_target.FillColor));
            yield return new Row("Outline",    _target.OutlineColor, ToHex(_target.OutlineColor));
        }

        // ── Unit-level character rows ─────────────────────────────────────────────

        private static IEnumerable<Row> UnitRows(Agent agent)
        {
            yield return new Row("Body Switch Speed",   $"{agent.UnitAttributes.BodySwitchSpeed:0.##}");
            yield return new Row("Barrel Switch Speed", $"{agent.UnitAttributes.BarrelSwitchSpeed:0.##}");
            yield return new Row("ID",                  agent.ID.ToString());
        }

        // ── Player-specific rows ──────────────────────────────────────────────────

        private static IEnumerable<Row> PlayerRows(Agent agent)
        {
            yield return new Row("Player ID", agent.PlayerID.ToString());
        }

        // ── Barrel Transform rows ─────────────────────────────────────────────────

        private static IEnumerable<Row> BarrelTransformRows(Agent agent)
        {
            if (agent.BarrelCount <= 0)
            {
                yield return new Row("Offset",   "0.0, 0.0");
                yield return new Row("Rotation", "0.0 deg");
                yield break;
            }

            var slot = agent.Barrels[agent.ActiveBarrelIndex];
            Attributes_Barrel a = slot.Attrs;
            float mass        = a.BulletMass > 0f ? a.BulletMass : BulletManager.DefaultBulletMass;
            float bulletSpeed = a.BulletSpeed > 0f ? a.BulletSpeed : BulletManager.DefaultBulletSpeed;
            float barrelWidth  = AttributeDerived.BarrelWidth(mass, BulletManager.BulletRadiusScalar);
            float barrelHeight = AttributeDerived.BarrelHeight(bulletSpeed, BulletManager.BarrelHeightScalar);

            yield return new Row("Offset",   $"{agent.Position.X:0.0}, {agent.Position.Y:0.0}");
            float rotDeg = MathHelper.ToDegrees(agent.Rotation);
            yield return new Row("Rotation", $"{rotDeg:0.0} deg");
            yield return new Row("Barrel Width",  $"{barrelWidth:0.##} px",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBarrelWidth);
            yield return new Row("Barrel Height", $"{barrelHeight:0.##} px",
                                 isHidden: true, affectsList: AttributeDerived.AffectsBarrelHeight);
            if (slot.FullShape != null)
            {
                yield return new Row("Fill",     slot.FullShape.FillColor, ToHex(slot.FullShape.FillColor));
                yield return new Row("Outline",  slot.FullShape.OutlineColor, ToHex(slot.FullShape.OutlineColor));
            }
        }

        private readonly Row BuildFlagsRow()
        {
            List<string> parts = new();
            if (_target.IsPlayer)
                parts.Add("Player");
            if (_target.IsPrototype)
                parts.Add("Prototype");
            parts.Add(_target.DynamicPhysics ? "Dynamic" : "Static");
            parts.Add(_target.IsCollidable ? "Collidable" : "Non-collidable");
            parts.Add(_target.IsDestructible ? "Destructible" : "Indestructible");
            if (_target.IsInteract)
                parts.Add("Interact");
            if (_target.IsZoneBlock)
                parts.Add("ZoneBlock");
            return new Row("Flags", parts.ToArray());
        }

        // ── Reflection helper — still used for FarmAttributes ────────────────────

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
                bool b => new Row(label, b),
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
