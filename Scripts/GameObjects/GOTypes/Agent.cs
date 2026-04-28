using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class Agent : GameObject
    {
        public class BodySlot
        {
            public Attributes_Body Attrs;
            public string Name;
            public Color FillColor;
            public Color OutlineColor;
            public int OutlineWidth;

            public BodySlot(Attributes_Body attrs, Color fillColor, Color outlineColor, int outlineWidth)
            {
                Attrs = attrs;
                FillColor = fillColor;
                OutlineColor = outlineColor;
                OutlineWidth = Math.Max(0, outlineWidth);
            }
        }

        private readonly List<BodySlot> _bodies = new();
        public IReadOnlyList<BodySlot> Bodies => _bodies;
        public int BodyCount => _bodies.Count;
        public int ActiveBodyIndex { get; private set; }
        public bool BodyTransitionAnimating { get; private set; }
        public float BodyTransitionProgress =>
            BodyTransitionAnimating
                ? MathHelper.Clamp(_bodyTransitionElapsedSeconds / MathF.Max(BodyTransitionDurationSeconds, 0.0001f), 0f, 1f)
                : 1f;
        public float BodyTransitionCooldownRemaining => MathF.Max(0f, _bodyTransitionCooldownRemaining);

        private int _bodyTransitionTargetIndex;
        private float _bodyTransitionElapsedSeconds;
        private float _bodyTransitionCooldownRemaining;
        private Attributes_Body _bodyTransitionFromAttributes;
        private Color _bodyTransitionFromFillColor;
        private Color _bodyTransitionFromOutlineColor;
        private int _bodyTransitionFromOutlineWidth;

        private static float? _cachedBodyTransitionDurationSeconds;
        private static float? _cachedBodyTransitionBufferSeconds;
        public static float BodyTransitionDurationSeconds =>
            _cachedBodyTransitionDurationSeconds ??= MathF.Max(0f, DatabaseFetch.GetSetting(
                "FXSettings", "Value", "SettingKey", "BodyTransitionDurationSeconds", 0.3f));
        public static float BodyTransitionBufferSeconds =>
            _cachedBodyTransitionBufferSeconds ??= MathF.Max(0f, DatabaseFetch.GetSetting(
                "FXSettings", "Value", "SettingKey", "BodyTransitionBufferSeconds", 0.5f));

        public void AddBody(
            Attributes_Body attrs,
            Color? fillColor = null,
            Color? outlineColor = null,
            int? outlineWidth = null)
        {
            Color resolvedFillColor = fillColor ?? FillColor;
            Color resolvedOutlineColor = outlineColor ?? OutlineColor;
            int resolvedOutlineWidth = outlineWidth ?? OutlineWidth;

            _bodies.Add(new BodySlot(attrs, resolvedFillColor, resolvedOutlineColor, resolvedOutlineWidth));
            if (_bodies.Count == 1)
            {
                ApplyBodyAttributes(0);
            }
        }

        public void ClearBodies()
        {
            _bodies.Clear();
            ActiveBodyIndex = 0;
            _bodyTransitionTargetIndex = 0;
            _bodyTransitionElapsedSeconds = 0f;
            _bodyTransitionCooldownRemaining = 0f;
            BodyTransitionAnimating = false;
        }

        public bool SwitchBodyLeft()
        {
            if (BodyCount < 2)
            {
                return false;
            }

            int newIndex = (ActiveBodyIndex - 1 + BodyCount) % BodyCount;
            return SwitchBodyToIndex(newIndex);
        }

        public bool SwitchBodyRight()
        {
            if (BodyCount < 2)
            {
                return false;
            }

            int newIndex = (ActiveBodyIndex + 1) % BodyCount;
            return SwitchBodyToIndex(newIndex);
        }

        public bool SwitchBodyToIndex(int index)
        {
            if (index < 0 || index >= BodyCount)
            {
                return false;
            }

            if (BodyCount < 2 || BodyTransitionAnimating || _bodyTransitionCooldownRemaining > 0f)
            {
                return false;
            }

            if (index == ActiveBodyIndex)
            {
                return false;
            }

            BodySlot targetSlot = _bodies[index];
            _bodyTransitionTargetIndex = index;
            _bodyTransitionElapsedSeconds = 0f;
            _bodyTransitionFromAttributes = BodyAttributes;
            _bodyTransitionFromFillColor = FillColor;
            _bodyTransitionFromOutlineColor = OutlineColor;
            _bodyTransitionFromOutlineWidth = OutlineWidth;
            ActiveBodyIndex = index;

            if (BodyTransitionDurationSeconds <= 0f)
            {
                ApplyResolvedBodyState(
                    targetSlot.Attrs,
                    targetSlot.FillColor,
                    targetSlot.OutlineColor,
                    targetSlot.OutlineWidth);
                BodyTransitionAnimating = false;
                _bodyTransitionCooldownRemaining = BodyTransitionBufferSeconds;
                return true;
            }

            BodyTransitionAnimating = true;
            return true;
        }

        private void ApplyBodyAttributes(int index)
        {
            if (index < 0 || index >= BodyCount)
            {
                return;
            }

            ActiveBodyIndex = index;
            _bodyTransitionTargetIndex = index;
            _bodyTransitionElapsedSeconds = 0f;
            _bodyTransitionCooldownRemaining = 0f;
            BodyTransitionAnimating = false;

            BodySlot slot = _bodies[index];
            ApplyResolvedBodyState(
                slot.Attrs,
                slot.FillColor,
                slot.OutlineColor,
                slot.OutlineWidth);
        }

        public void UpdateCircleDimensions(
            float mass,
            Color? fillColor = null,
            Color? outlineColor = null,
            int? outlineWidth = null)
        {
            FillColor = fillColor ?? FillColor;
            OutlineColor = outlineColor ?? OutlineColor;
            OutlineWidth = outlineWidth.HasValue ? Math.Max(0, outlineWidth.Value) : OutlineWidth;

            if (Shape == null)
            {
                return;
            }

            if (Shape.ShapeType != "Circle")
            {
                Shape.UpdateDimensions(
                    Shape.Width,
                    Shape.Height,
                    Core.Instance?.GraphicsDevice,
                    FillColor,
                    OutlineColor,
                    OutlineWidth);
                return;
            }

            float radius = AttributeDerived.BodyRadius(mass, CollisionResolver.BodyRadiusScalar);
            int diameter = Math.Max(1, (int)MathF.Round(2f * radius));
            Shape.UpdateDimensions(
                diameter,
                diameter,
                Core.Instance?.GraphicsDevice,
                FillColor,
                OutlineColor,
                OutlineWidth);
        }

        public class BarrelSlot
        {
            public Attributes_Barrel Attrs;
            public Shape FullShape;
            public float CurrentHeightScale;
            public float TargetHeightScale;
            public string Name;

            public BarrelSlot(Attributes_Barrel attrs, Shape shape, float initialScale)
            {
                Attrs = attrs;
                FullShape = shape;
                CurrentHeightScale = initialScale;
                TargetHeightScale = initialScale;
            }
        }

        private readonly List<BarrelSlot> _barrels = new();
        public IReadOnlyList<BarrelSlot> Barrels => _barrels;
        public int BarrelCount => _barrels.Count;
        public int ActiveBarrelIndex { get; private set; }

        private float _carouselAngle;
        private float _targetCarouselAngle;
        public float CarouselAngle => _carouselAngle;

        private const float StandbyHeightScale = 0.18f;
        private const float SwitchAnimSpeed = 15f;

        public Attributes_Barrel BarrelAttributes => BarrelCount > 0 ? _barrels[ActiveBarrelIndex].Attrs : default;
        public Shape BarrelShape => BarrelCount > 0 ? _barrels[ActiveBarrelIndex].FullShape : null;
        public GameObject BarrelObject => null;

        public float TriggerCooldown { get; set; }
        public float SwitchCooldown { get; set; }

        private int GetMode(int mode, string modeSettingName)
        {
            if (mode == -1)
            {
                if (ControlStateManager.ContainsSwitchState(modeSettingName))
                {
                    _crouchMode = TypeConversionFunctions.BoolToInt(ControlStateManager.GetSwitchState(modeSettingName));
                }
                else
                {
                    _crouchMode = 0;
                }
            }

            return mode;
        }

        private int _crouchMode = -1;
        public bool IsCrouching
        {
            get => TypeConversionFunctions.IntToBool(GetMode(_crouchMode, "Crouch"));
            set => _crouchMode = TypeConversionFunctions.BoolToInt(value);
        }

        private int _sprintMode = -1;
        public bool IsSprinting
        {
            get => TypeConversionFunctions.IntToBool(GetMode(_sprintMode, "Sprint"));
            set => _sprintMode = TypeConversionFunctions.BoolToInt(value);
        }

        public bool IsPlayer { get; private set; }
        public long PlayerID { get; set; }
        public Attributes_Body BodyAttributes { get; set; }

        private Attributes_Unit _unitAttributes;
        public Attributes_Unit UnitAttributes
        {
            get => _unitAttributes;
            set
            {
                _unitAttributes = value;
                if (!string.IsNullOrWhiteSpace(value.Name))
                {
                    Name = value.Name;
                }
            }
        }

        private float _baseSpeed;
        private static float? _cachedTriggerCooldown;
        private static float? _cachedSwitchCooldown;

        public float BaseSpeed
        {
            get => _baseSpeed;
            set
            {
                _baseSpeed = value;
                if (value < 0)
                {
                    DebugLogger.PrintWarning($"BaseSpeed updated to negative value: {value}");
                }
            }
        }

        public new float Speed => IsDeadOrDying
            ? 0f
            : AttributeDerived.BodySpeed(BodyAttributes.Speed, BaseSpeed) * InputManager.SpeedMultiplier();

        public Vector2 MovementVelocity { get; set; }
        public bool IsDeadOrDying => IsDying || CurrentHealth <= 0f;

        public Agent(
            int id,
            string name,
            Vector2 position,
            float rotation,
            float mass,
            bool isDestructible,
            bool isCollidable,
            bool dynamicPhysics,
            Shape shape,
            float baseSpeed,
            bool isPlayer,
            Color fillColor,
            Color outlineColor,
            int outlineWidth,
            Attributes_Barrel barrelAttributes = default,
            Attributes_Body bodyAttributes = default)
            : base(id, name, position, rotation, mass, isDestructible, isCollidable, dynamicPhysics, shape, fillColor, outlineColor, outlineWidth)
        {
            TriggerCooldown = 0f;
            SwitchCooldown = 0f;
            IsCrouching = false;
            IsSprinting = false;
            IsPlayer = isPlayer;
            DrawLayer = 200;
            BaseSpeed = baseSpeed;
            BodyAttributes = bodyAttributes;

            float maxHp = AttributeDerived.MaxHealth(bodyAttributes.Mass);
            CurrentHealth = maxHp;
            MaxHealth = maxHp;
            CurrentShield = bodyAttributes.MaxShield;
            MaxShield = bodyAttributes.MaxShield;
            HealthRegen = bodyAttributes.HealthRegen;
            HealthRegenDelay = bodyAttributes.HealthRegenDelay;
            ShieldRegen = bodyAttributes.ShieldRegen;
            ShieldRegenDelay = bodyAttributes.ShieldRegenDelay;

            AddBarrel(barrelAttributes);

            DebugLogger.PrintPlayer($"Agent created with TriggerCooldown: {TriggerCooldown}, SwitchCooldown: {SwitchCooldown}");
        }

        public void AddBarrel(Attributes_Barrel attrs)
        {
            float bulletMass = attrs.BulletMass > 0f ? attrs.BulletMass : BulletManager.DefaultBulletMass;
            float bulletSpeed = attrs.BulletSpeed > 0f ? attrs.BulletSpeed : BulletManager.DefaultBulletSpeed;

            int barrelWidth = Math.Max(1, (int)MathF.Round(AttributeDerived.BarrelWidth(bulletMass, BulletManager.BulletRadiusScalar)));
            int barrelLength = Math.Max(4, (int)MathF.Round(AttributeDerived.BarrelHeight(bulletSpeed, BulletManager.BarrelHeightScalar)));

            float initialScale = _barrels.Count == 0 ? 1f : StandbyHeightScale;

            var shape = new Shape(
                "Rectangle",
                barrelLength,
                barrelWidth,
                0,
                new Color(192, 192, 192),
                new Color(64, 64, 64),
                1)
            {
                SkipHover = true
            };

            if (Core.Instance?.GraphicsDevice != null)
            {
                shape.LoadContent(Core.Instance.GraphicsDevice);
            }

            _barrels.Add(new BarrelSlot(attrs, shape, initialScale));
            RefreshTargetScales();
        }

        public void ClearBarrels()
        {
            foreach (BarrelSlot slot in _barrels)
            {
                slot.FullShape?.Dispose();
            }

            _barrels.Clear();
            ActiveBarrelIndex = 0;
            _carouselAngle = 0f;
            _targetCarouselAngle = 0f;
        }

        public void SwitchBarrelLeft()
        {
            if (BarrelCount < 2)
            {
                return;
            }

            ActiveBarrelIndex = (ActiveBarrelIndex - 1 + BarrelCount) % BarrelCount;
            _targetCarouselAngle -= MathF.Tau / BarrelCount;
            RefreshTargetScales();
        }

        public void SwitchBarrelRight()
        {
            if (BarrelCount < 2)
            {
                return;
            }

            ActiveBarrelIndex = (ActiveBarrelIndex + 1) % BarrelCount;
            _targetCarouselAngle += MathF.Tau / BarrelCount;
            RefreshTargetScales();
        }

        private void RefreshTargetScales()
        {
            for (int i = 0; i < _barrels.Count; i++)
            {
                _barrels[i].TargetHeightScale = i == ActiveBarrelIndex ? 1f : StandbyHeightScale;
            }
        }

        public bool TryGetBarrelWorldTransform(
            int barrelIndex,
            out Vector2 center,
            out Vector2 direction,
            out float angle,
            out float scaledLength,
            out float bodyRadius)
        {
            center = Position;
            direction = Vector2.UnitX;
            angle = Rotation;
            scaledLength = 0f;
            bodyRadius = 0f;

            if (Shape == null || barrelIndex < 0 || barrelIndex >= _barrels.Count)
            {
                return false;
            }

            BarrelSlot slot = _barrels[barrelIndex];
            if (slot.FullShape == null)
            {
                return false;
            }

            int barrelCount = _barrels.Count;
            float angleStep = barrelCount > 1 ? MathF.Tau / barrelCount : 0f;
            angle = Rotation + barrelIndex * angleStep - _carouselAngle;
            direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            bodyRadius = Math.Max(Shape.Width, Shape.Height) / 2f;
            scaledLength = slot.FullShape.Width * slot.CurrentHeightScale;
            center = Position + direction * (bodyRadius + (scaledLength * 0.5f));
            return true;
        }

        public bool TryGetBarrelWorldSegment(
            int barrelIndex,
            out Vector2 back,
            out Vector2 front,
            out Vector2 direction,
            out float angle,
            out float scaledLength)
        {
            back = Position;
            front = Position;
            direction = Vector2.UnitX;
            angle = Rotation;
            scaledLength = 0f;

            if (!TryGetBarrelWorldTransform(
                barrelIndex,
                out Vector2 center,
                out direction,
                out angle,
                out scaledLength,
                out _))
            {
                return false;
            }

            Vector2 halfSegment = direction * (scaledLength * 0.5f);
            back = center - halfSegment;
            front = center + halfSegment;
            return true;
        }

        public Vector2 GetLinearVelocity()
        {
            return Core.DELTATIME > 0f
                ? (Position - PreviousPosition) / Core.DELTATIME
                : Vector2.Zero;
        }

        public void LoadTriggerCooldown()
        {
            if (!_cachedTriggerCooldown.HasValue)
            {
                TriggerCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "TriggerCooldown");
                if (TriggerCooldown == 0f)
                {
                    DebugLogger.PrintError("TriggerCooldown is 0 after loading from the database.");
                }

                DebugLogger.PrintPlayer($"TriggerCooldown loaded: {TriggerCooldown}");
                _cachedTriggerCooldown = TriggerCooldown;
            }
            else
            {
                TriggerCooldown = _cachedTriggerCooldown.Value;
                DebugLogger.PrintPlayer($"Loaded from cache: TriggerCooldown: {TriggerCooldown}");
            }
        }

        public void LoadSwitchCooldown()
        {
            if (!_cachedSwitchCooldown.HasValue)
            {
                SwitchCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "SwitchCooldown");
                if (SwitchCooldown == 0f)
                {
                    DebugLogger.PrintError("SwitchCooldown is 0 after loading from the database.");
                }

                DebugLogger.PrintPlayer($"SwitchCooldown loaded: {SwitchCooldown}");
                _cachedSwitchCooldown = SwitchCooldown;
            }
            else
            {
                SwitchCooldown = _cachedSwitchCooldown.Value;
                DebugLogger.PrintPlayer($"Loaded from cache: SwitchCooldown: {SwitchCooldown}");
            }
        }

        protected override GOFlags ComputeFlags()
        {
            GOFlags flags = base.ComputeFlags();
            if (IsPlayer)
            {
                flags |= GOFlags.Player;
            }

            return flags;
        }

        public override void Update()
        {
            bool suppressDeadPlayerMotion = IsPlayer && IsDeadOrDying;
            if (suppressDeadPlayerMotion)
            {
                MovementVelocity = Vector2.Zero;
                PhysicsVelocity = Vector2.Zero;
            }

            base.Update();

            if (suppressDeadPlayerMotion)
            {
                MovementVelocity = Vector2.Zero;
                PhysicsVelocity = Vector2.Zero;
            }

            if (TriggerCooldown > 0f)
            {
                TriggerCooldown -= Core.DELTATIME;
            }

            if (SwitchCooldown > 0f)
            {
                SwitchCooldown -= Core.DELTATIME;
            }

            UpdateBodyTransition();

            float t = Math.Min(SwitchAnimSpeed * Core.DELTATIME, 1f);

            _carouselAngle += (_targetCarouselAngle - _carouselAngle) * t;
            if (MathF.Abs(_carouselAngle - _targetCarouselAngle) < 0.001f)
            {
                _carouselAngle = _targetCarouselAngle;
            }

            foreach (BarrelSlot slot in _barrels)
            {
                slot.CurrentHeightScale += (slot.TargetHeightScale - slot.CurrentHeightScale) * t;
                if (MathF.Abs(slot.CurrentHeightScale - slot.TargetHeightScale) < 0.001f)
                {
                    slot.CurrentHeightScale = slot.TargetHeightScale;
                }
            }
        }

        private void UpdateBodyTransition()
        {
            if (_bodyTransitionCooldownRemaining > 0f)
            {
                _bodyTransitionCooldownRemaining = MathF.Max(0f, _bodyTransitionCooldownRemaining - Core.DELTATIME);
            }

            if (!BodyTransitionAnimating)
            {
                return;
            }

            float duration = BodyTransitionDurationSeconds;
            if (duration <= 0f)
            {
                BodyTransitionAnimating = false;
                _bodyTransitionCooldownRemaining = BodyTransitionBufferSeconds;
                return;
            }

            _bodyTransitionElapsedSeconds = MathF.Min(_bodyTransitionElapsedSeconds + Core.DELTATIME, duration);

            BodySlot targetSlot = _bodies[_bodyTransitionTargetIndex];
            float t = MathHelper.Clamp(_bodyTransitionElapsedSeconds / duration, 0f, 1f);
            Attributes_Body blendedBody = LerpBodyAttributes(_bodyTransitionFromAttributes, targetSlot.Attrs, t);
            Color blendedFillColor = Color.Lerp(_bodyTransitionFromFillColor, targetSlot.FillColor, t);
            Color blendedOutlineColor = Color.Lerp(_bodyTransitionFromOutlineColor, targetSlot.OutlineColor, t);
            int blendedOutlineWidth = (int)MathF.Round(MathHelper.Lerp(
                _bodyTransitionFromOutlineWidth,
                targetSlot.OutlineWidth,
                t));

            ApplyResolvedBodyState(
                blendedBody,
                blendedFillColor,
                blendedOutlineColor,
                blendedOutlineWidth);

            if (t < 1f)
            {
                return;
            }

            BodyTransitionAnimating = false;
            _bodyTransitionCooldownRemaining = BodyTransitionBufferSeconds;
            ApplyResolvedBodyState(
                targetSlot.Attrs,
                targetSlot.FillColor,
                targetSlot.OutlineColor,
                targetSlot.OutlineWidth);
        }

        private void ApplyResolvedBodyState(
            Attributes_Body body,
            Color fillColor,
            Color outlineColor,
            int outlineWidth)
        {
            float previousMaxHealth = MaxHealth;
            float newMaxHealth = AttributeDerived.MaxHealth(body.Mass);

            BodyAttributes = body;
            MaxHealth = newMaxHealth;
            if (previousMaxHealth > 0f)
            {
                float healthScale = newMaxHealth / previousMaxHealth;
                CurrentHealth = MathF.Min(CurrentHealth * healthScale, newMaxHealth);
            }
            else
            {
                CurrentHealth = MathF.Min(CurrentHealth, newMaxHealth);
            }

            MaxShield = MathF.Max(0f, body.MaxShield);
            CurrentShield = MathF.Min(CurrentShield, MaxShield);
            HealthRegen = body.HealthRegen;
            HealthRegenDelay = body.HealthRegenDelay;
            ShieldRegen = body.ShieldRegen;
            ShieldRegenDelay = body.ShieldRegenDelay;
            HealthArmor = body.HealthArmor;
            ShieldArmor = body.ShieldArmor;
            BodyCollisionDamage = body.BodyCollisionDamage;
            BodyPenetration = body.BodyPenetration;
            CollisionDamageResistance = body.CollisionDamageResistance;
            BulletDamageResistance = body.BulletDamageResistance;
            Mass = body.Mass;

            UpdateCircleDimensions(body.Mass, fillColor, outlineColor, outlineWidth);
        }

        private static Attributes_Body LerpBodyAttributes(Attributes_Body from, Attributes_Body to, float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return new Attributes_Body
            {
                Mass = MathHelper.Lerp(from.Mass, to.Mass, t),
                HealthRegen = MathHelper.Lerp(from.HealthRegen, to.HealthRegen, t),
                HealthRegenDelay = MathHelper.Lerp(from.HealthRegenDelay, to.HealthRegenDelay, t),
                HealthArmor = MathHelper.Lerp(from.HealthArmor, to.HealthArmor, t),
                MaxShield = MathHelper.Lerp(from.MaxShield, to.MaxShield, t),
                ShieldRegen = MathHelper.Lerp(from.ShieldRegen, to.ShieldRegen, t),
                ShieldRegenDelay = MathHelper.Lerp(from.ShieldRegenDelay, to.ShieldRegenDelay, t),
                ShieldArmor = MathHelper.Lerp(from.ShieldArmor, to.ShieldArmor, t),
                BodyCollisionDamage = MathHelper.Lerp(from.BodyCollisionDamage, to.BodyCollisionDamage, t),
                BodyPenetration = MathHelper.Lerp(from.BodyPenetration, to.BodyPenetration, t),
                CollisionDamageResistance = MathHelper.Lerp(from.CollisionDamageResistance, to.CollisionDamageResistance, t),
                BulletDamageResistance = MathHelper.Lerp(from.BulletDamageResistance, to.BulletDamageResistance, t),
                Speed = MathHelper.Lerp(from.Speed, to.Speed, t),
                Control = MathHelper.Lerp(from.Control, to.Control, t),
                Sight = MathHelper.Lerp(from.Sight, to.Sight, t),
                BodyActionBuff = MathHelper.Lerp(from.BodyActionBuff, to.BodyActionBuff, t),
            };
        }
    }
}
