using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class Agent : GameObject
    {
        // ── Body slot ───────────────────────────────────────────────────────────
        // Holds one body's attributes and optional display name.
        public class BodySlot
        {
            public Attributes_Body Attrs;
            /// <summary>Custom display name (null = use default "Body N").</summary>
            public string Name;

            public BodySlot(Attributes_Body attrs)
            {
                Attrs = attrs;
            }
        }

        // ── Body list & switching ───────────────────────────────────────────────
        private readonly List<BodySlot> _bodies = new();
        public IReadOnlyList<BodySlot> Bodies => _bodies;
        public int BodyCount => _bodies.Count;
        public int ActiveBodyIndex { get; private set; } = 0;

        /// <summary>
        /// Attaches a new body to this agent. The first body added becomes the
        /// active one and its attributes are applied immediately.
        /// </summary>
        public void AddBody(Attributes_Body attrs)
        {
            _bodies.Add(new BodySlot(attrs));
            if (_bodies.Count == 1)
                ApplyBodyAttributes(0);
        }

        /// <summary>
        /// Removes all body slots and resets the active index.
        /// Call before re-applying a build from JSON deserialization.
        /// </summary>
        public void ClearBodies()
        {
            _bodies.Clear();
            ActiveBodyIndex = 0;
        }

        /// <summary>
        /// Rotates to the previous body slot (Ctrl+Q).
        /// No-op when fewer than 2 bodies are equipped.
        /// </summary>
        public void SwitchBodyLeft()
        {
            if (BodyCount < 2) return;
            int newIndex = (ActiveBodyIndex - 1 + BodyCount) % BodyCount;
            ApplyBodyAttributes(newIndex);
        }

        /// <summary>
        /// Rotates to the next body slot (Ctrl+E).
        /// No-op when fewer than 2 bodies are equipped.
        /// </summary>
        public void SwitchBodyRight()
        {
            if (BodyCount < 2) return;
            int newIndex = (ActiveBodyIndex + 1) % BodyCount;
            ApplyBodyAttributes(newIndex);
        }

        /// <summary>
        /// Applies the body attributes at the given index to this agent,
        /// updating all derived stats (health, shield, regen, mass, etc.).
        /// Health is clamped to the new maximum rather than reset.
        /// </summary>
        private void ApplyBodyAttributes(int index)
        {
            if (index < 0 || index >= BodyCount) return;
            ActiveBodyIndex = index;
            Attributes_Body body = _bodies[index].Attrs;
            BodyAttributes = body;

            float newMaxHp = AttributeDerived.MaxHealth(body.Mass);
            // Preserve health ratio when switching bodies
            float hpRatio = MaxHealth > 0f ? CurrentHealth / MaxHealth : 1f;
            MaxHealth = newMaxHp;
            CurrentHealth = MathF.Min(hpRatio * newMaxHp, newMaxHp);

            MaxShield = body.MaxShield;
            CurrentShield = MathF.Min(CurrentShield, body.MaxShield);
            HealthRegen = body.HealthRegen;
            HealthRegenDelay = body.HealthRegenDelay;
            ShieldRegen = body.ShieldRegen;
            ShieldRegenDelay = body.ShieldRegenDelay;
            Mass = body.Mass;

            UpdateCircleDimensions(body.Mass);
        }

        /// <summary>
        /// For circle shapes, recomputes Width/Height from the derived body radius.
        /// </summary>
        public void UpdateCircleDimensions(float mass)
        {
            if (Shape == null || Shape.ShapeType != "Circle") return;
            float radius = AttributeDerived.BodyRadius(mass, CollisionResolver.BodyRadiusScalar);
            int diameter = Math.Max(1, (int)MathF.Round(2f * radius));
            Shape.UpdateDimensions(diameter, diameter, Core.Instance.GraphicsDevice);
        }

        // ── Barrel slot ─────────────────────────────────────────────────────────
        // Holds one barrel's attributes, full-size shape, and animated height scale.
        public class BarrelSlot
        {
            public Attributes_Barrel Attrs;
            public Shape FullShape;
            public float CurrentHeightScale;
            public float TargetHeightScale;
            /// <summary>Custom display name (null = use default "Barrel N").</summary>
            public string Name;

            public BarrelSlot(Attributes_Barrel attrs, Shape shape, float initialScale)
            {
                Attrs = attrs;
                FullShape = shape;
                CurrentHeightScale = initialScale;
                TargetHeightScale = initialScale;
            }
        }

        // ── Barrel list & carousel ───────────────────────────────────────────────
        private readonly List<BarrelSlot> _barrels = new();
        public IReadOnlyList<BarrelSlot> Barrels => _barrels;
        public int BarrelCount => _barrels.Count;
        public int ActiveBarrelIndex { get; private set; } = 0;

        // Carousel angle tracks the cumulative rotation of the barrel ring (radians).
        // Barrel i is displayed at: agentRotation + i*(2π/N) - _carouselAngle
        private float _carouselAngle = 0f;
        private float _targetCarouselAngle = 0f;
        public float CarouselAngle => _carouselAngle;

        private const float StandbyHeightScale = 0.18f;
        private const float SwitchAnimSpeed = 15f; // exponential lerp speed (units/s)

        // Computed: delegates to the active barrel (or default when none equipped).
        public Attributes_Barrel BarrelAttributes => BarrelCount > 0 ? _barrels[ActiveBarrelIndex].Attrs : default;
        public Shape BarrelShape => BarrelCount > 0 ? _barrels[ActiveBarrelIndex].FullShape : null;
        // Legacy compat: BarrelObject is surfaced as null; callers that checked for
        // null already handle this gracefully (e.g. GOProperties.BarrelTransformRows).
        public GameObject BarrelObject => null;

        // ── Agent-specific properties ────────────────────────────────────────────
        public float TriggerCooldown { get; set; }
        public float SwitchCooldown { get; set; }

        private int GetMode(int _mode, string modeSettingName)
        {
            if (_mode == -1)
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

            return _mode;
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
                    Name = value.Name;
            }
        }

        private float _baseSpeed;
        private static float? cachedTriggerCooldown = null;
        private static float? cachedSwitchCooldown = null;

        public float BaseSpeed
        {
            get => _baseSpeed;
            set
            {
                _baseSpeed = value;
                if (value < 0)
                    DebugLogger.PrintWarning($"BaseSpeed updated to negative value: {value}");
            }
        }

        public new float Speed => BaseSpeed * InputManager.SpeedMultiplier();

        // Current input-driven movement velocity (pixels/sec), built up by the acceleration system.
        public Vector2 MovementVelocity { get; set; }

        // ── Constructor ──────────────────────────────────────────────────────────
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
            Attributes_Body bodyAttributes = default
        )
            : base(id, name, position, rotation, mass, isDestructible, isCollidable, dynamicPhysics, shape, fillColor, outlineColor, outlineWidth)
        {
            TriggerCooldown = 0;
            SwitchCooldown = 0;
            IsCrouching = false;
            IsSprinting = false;
            IsPlayer = isPlayer;
            DrawLayer = 200;
            BaseSpeed = baseSpeed;
            BodyAttributes    = bodyAttributes;
            float maxHp       = AttributeDerived.MaxHealth(bodyAttributes.Mass);
            CurrentHealth     = maxHp;
            MaxHealth         = maxHp;
            CurrentShield     = bodyAttributes.MaxShield;
            MaxShield         = bodyAttributes.MaxShield;
            HealthRegen       = bodyAttributes.HealthRegen;
            HealthRegenDelay  = bodyAttributes.HealthRegenDelay;
            ShieldRegen       = bodyAttributes.ShieldRegen;
            ShieldRegenDelay  = bodyAttributes.ShieldRegenDelay;

            // Seed the first barrel using the passed-in attributes (default = use
            // physics-settings fallbacks at fire time).  Callers may add further
            // barrels via AddBarrel().
            AddBarrel(barrelAttributes);

            DebugLogger.PrintPlayer($"Agent created with TriggerCooldown: {TriggerCooldown}, SwitchCooldown: {SwitchCooldown}");
        }

        // ── Barrel management ────────────────────────────────────────────────────

        /// <summary>
        /// Attaches a new barrel to this agent. The first barrel added is always
        /// the active one; subsequent barrels start in standby (very small height).
        /// </summary>
        public void AddBarrel(Attributes_Barrel attrs)
        {
            float bulletMass = attrs.BulletMass > 0f ? attrs.BulletMass : BulletManager.DefaultBulletMass;
            float bulletSpeed = attrs.BulletSpeed > 0f ? attrs.BulletSpeed : BulletManager.DefaultBulletSpeed;

            int bw = Math.Max(1, (int)MathF.Round(AttributeDerived.BarrelWidth(bulletMass, BulletManager.BulletRadiusScalar)));
            int bl = Math.Max(4, (int)MathF.Round(AttributeDerived.BarrelHeight(bulletSpeed, BulletManager.BarrelHeightScalar)));

            float initialScale = _barrels.Count == 0 ? 1f : StandbyHeightScale;

            var shape = new Shape("Rectangle", bl, bw, 0,
                new Color(192, 192, 192), new Color(64, 64, 64), 1)
            {
                SkipHover = true
            };

            // Load GPU content immediately if the graphics device is already ready
            // (e.g. barrels added at runtime rather than during initialization).
            if (Core.Instance?.GraphicsDevice != null)
                shape.LoadContent(Core.Instance.GraphicsDevice);

            _barrels.Add(new BarrelSlot(attrs, shape, initialScale));
            RefreshTargetScales();
        }

        /// <summary>
        /// Removes all barrels, disposing their shapes, and resets carousel state.
        /// Call before re-applying a build from JSON deserialization.
        /// </summary>
        public void ClearBarrels()
        {
            foreach (var slot in _barrels)
                slot.FullShape?.Dispose();
            _barrels.Clear();
            ActiveBarrelIndex = 0;
            _carouselAngle = 0f;
            _targetCarouselAngle = 0f;
        }

        /// <summary>
        /// Rotates the barrel carousel left (Q key).
        /// All barrels rotate clockwise; the barrel previously counter-clockwise of
        /// the active slot advances to the primary (forward-facing) position.
        /// No-op when fewer than 2 barrels are equipped.
        /// </summary>
        public void SwitchBarrelLeft()
        {
            if (BarrelCount < 2) return;
            ActiveBarrelIndex = (ActiveBarrelIndex - 1 + BarrelCount) % BarrelCount;
            _targetCarouselAngle -= MathF.Tau / BarrelCount;
            RefreshTargetScales();
        }

        /// <summary>
        /// Rotates the barrel carousel right (E key).
        /// All barrels rotate counter-clockwise; the barrel previously clockwise of
        /// the active slot advances to the primary position.
        /// No-op when fewer than 2 barrels are equipped.
        /// </summary>
        public void SwitchBarrelRight()
        {
            if (BarrelCount < 2) return;
            ActiveBarrelIndex = (ActiveBarrelIndex + 1) % BarrelCount;
            _targetCarouselAngle += MathF.Tau / BarrelCount;
            RefreshTargetScales();
        }

        private void RefreshTargetScales()
        {
            for (int i = 0; i < _barrels.Count; i++)
                _barrels[i].TargetHeightScale = (i == ActiveBarrelIndex) ? 1f : StandbyHeightScale;
        }

        // ── Cooldown loading ─────────────────────────────────────────────────────
        public void LoadTriggerCooldown()
        {
            if (!cachedTriggerCooldown.HasValue)
            {
                TriggerCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "TriggerCooldown");
                if (TriggerCooldown == 0)
                    DebugLogger.PrintError("TriggerCooldown is 0 after loading from the database.");
                DebugLogger.PrintPlayer($"TriggerCooldown loaded: {TriggerCooldown}");
                cachedTriggerCooldown = TriggerCooldown;
            }
            else
            {
                TriggerCooldown = cachedTriggerCooldown.Value;
                DebugLogger.PrintPlayer($"Loaded from cache: TriggerCooldown: {TriggerCooldown}");
            }
        }

        public void LoadSwitchCooldown()
        {
            if (!cachedSwitchCooldown.HasValue)
            {
                SwitchCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "SwitchCooldown");
                if (SwitchCooldown == 0)
                    DebugLogger.PrintError("SwitchCooldown is 0 after loading from the database.");
                DebugLogger.PrintPlayer($"SwitchCooldown loaded: {SwitchCooldown}");
                cachedSwitchCooldown = SwitchCooldown;
            }
            else
            {
                SwitchCooldown = cachedSwitchCooldown.Value;
                DebugLogger.PrintPlayer($"Loaded from cache: SwitchCooldown: {SwitchCooldown}");
            }
        }

        // ── Flags ────────────────────────────────────────────────────────────────
        protected override GOFlags ComputeFlags()
        {
            GOFlags f = base.ComputeFlags();
            if (IsPlayer) f |= GOFlags.Player;
            return f;
        }

        // ── Update ───────────────────────────────────────────────────────────────
        public override void Update()
        {
            base.Update(); // applies PhysicsVelocity, HitFlash decay, rotation

            if (TriggerCooldown > 0)
                TriggerCooldown -= Core.DELTATIME;

            if (SwitchCooldown > 0)
                SwitchCooldown -= Core.DELTATIME;

            // Animate carousel angle and barrel height scales toward their targets.
            float t = Math.Min(SwitchAnimSpeed * Core.DELTATIME, 1f);

            _carouselAngle += (_targetCarouselAngle - _carouselAngle) * t;
            if (MathF.Abs(_carouselAngle - _targetCarouselAngle) < 0.001f)
                _carouselAngle = _targetCarouselAngle;

            foreach (var slot in _barrels)
            {
                slot.CurrentHeightScale += (slot.TargetHeightScale - slot.CurrentHeightScale) * t;
                if (MathF.Abs(slot.CurrentHeightScale - slot.TargetHeightScale) < 0.001f)
                    slot.CurrentHeightScale = slot.TargetHeightScale;
            }
        }
    }
}
