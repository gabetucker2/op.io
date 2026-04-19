using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class GameObject : IDisposable
    {
        // Properties for the GameObject's data
        public int ID { get; set; }
        public string Name { get; set; }
        public bool IsPrototype { get; set; }
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }
        public float Mass { get; set; }
        public bool IsDestructible { get; set; }
        public bool IsCollidable { get; set; }
        public bool DynamicPhysics { get; set; }
        public bool IsInteract { get; set; }
        public bool IsZoneBlock { get; set; }
        public bool IsFarmObject { get; set; }

        /// <summary>
        /// The key identifying which Dynamic block content this ZoneBlock triggers
        /// in the Interact block when the player enters its zone.
        /// </summary>
        public string ZoneBlockDynamicKey { get; set; }

        /// <summary>
        /// Draw layer controlling render order. Higher values draw on top.
        /// Default = 0 (map objects, farms, zone blocks), Bullets = 100, Units = 200.
        /// </summary>
        public int DrawLayer { get; set; }

        public Shape Shape { get; set; }
        public Color FillColor { get; set; }
        public Color OutlineColor { get; set; }
        public int OutlineWidth { get; set; }

        // Parent/child hierarchy (bidirectional)
        public GameObject Parent { get; private set; }
        private readonly List<GameObject> _children = new();
        public IReadOnlyList<GameObject> Children => _children;

        public void AddChild(GameObject child)
        {
            if (child == null || child == this || _children.Contains(child)) return;
            child.Parent?.RemoveChild(child);
            _children.Add(child);
            child.Parent = this;
        }

        public void RemoveChild(GameObject child)
        {
            if (child == null) return;
            if (_children.Remove(child))
                child.Parent = null;
        }

        public float Opacity   { get; set; } = 1f;
        public float HitFlash  { get; set; } = 0f;

        // Death fade animation state
        public bool    IsDying        { get; set; } = false;
        public float   DeathFadeTimer { get; set; } = 0f;
        public float   DeathFadeScale { get; set; } = 1f;
        // Signed angular velocity (radians/second) applied while death fading.
        public float   DeathFadeSpinVelocity { get; set; } = 0f;
        // Velocity of the object that dealt the killing blow — overrides the object's
        // own velocity at the start of the death fade so it drifts in the attacker's direction.
        public Vector2 DeathImpulse   { get; set; } = Vector2.Zero;

        // ── Base identity ────────────────────────────────────────────────────
        public float CurrentXP { get; set; } = 0f;
        public float MaxXP     { get; set; } = 0f;

        // ── Destructible (only meaningful when IsDestructible = true) ────────
        public float CurrentHealth    { get; set; }
        public float MaxHealth        { get; set; }
        public float CurrentShield    { get; set; }
        public float MaxShield        { get; set; }
        public float DeathPointReward { get; set; }

        // ID of the agent whose bullet last dealt damage to this object (for XP award on death).
        public int LastDamagedByAgentID { get; set; } = -1;

        // Game-time timestamps for regen delay tracking. float.NegativeInfinity means "never damaged".
        public float LastHealthDamageTime { get; set; } = float.NegativeInfinity;
        public float LastShieldDamageTime { get; set; } = float.NegativeInfinity;

        /// <summary>Health regenerated per second for non-Agent destructibles. 0 = no regen.</summary>
        public float HealthRegen      { get; set; }
        public float HealthRegenDelay { get; set; }
        public float ShieldRegen      { get; set; }
        public float ShieldRegenDelay { get; set; }

        public float HealthArmor               { get; set; }
        public float ShieldArmor               { get; set; }
        public float BodyPenetration           { get; set; }
        public float BodyCollisionDamage       { get; set; }
        public float CollisionDamageResistance { get; set; }
        public float BulletDamageResistance    { get; set; }
        public float Speed                     { get; set; }
        public float RotationSpeed             { get; set; }
        public float AngularVelocity           { get; set; } = 0f;

        // Physics velocity from collision impulses (knockback, bounces, etc.).
        // Applied each frame and decays toward zero. Separate from input-driven movement.
        public Vector2 PhysicsVelocity  { get; set; } = Vector2.Zero;
        // Snapshotted at the start of each frame (before movement) for collision velocity computation.
        public Vector2 PreviousPosition { get; set; }

        // Farm float animation — null means no float animation
        public FarmAttributes? FarmAttributes  { get; set; }
        /// <summary>Base rotation used as the centre of the sine oscillation.</summary>
        public float FarmFloatBase             { get; set; } = 0f;
        /// <summary>Per-instance phase offset so each farm object oscillates out-of-sync.</summary>
        public float FarmFloatPhase            { get; set; } = 0f;

        private static readonly Random _random = new();

        /// <summary>
        /// Applies damage to this object, absorbing through shields first, then health.
        /// Updates regen delay timestamps. Returns total damage actually dealt.
        /// </summary>
        public float ApplyDamage(float dmg, int sourceAgentId)
        {
            if (dmg <= 0f) return 0f;

            LastDamagedByAgentID = sourceAgentId;

            float shieldDmg = MathF.Min(CurrentShield, dmg);
            if (shieldDmg > 0f)
            {
                CurrentShield -= shieldDmg;
                LastShieldDamageTime = Core.GAMETIME;
                dmg -= shieldDmg;
            }

            float healthDmg = MathF.Min(CurrentHealth, dmg);
            if (healthDmg > 0f)
            {
                CurrentHealth -= healthDmg;
                LastHealthDamageTime = Core.GAMETIME;
            }

            return shieldDmg + healthDmg;
        }

        /// <summary>
        /// Triggers a hit-flash with smooth interruption: if the object is already
        /// partially white from a previous flash, the fade-in is proportionally
        /// shorter so the transition feels instantaneous rather than restarting.
        /// </summary>
        public void TriggerHitFlash()
        {
            float fadeIn  = BulletManager.HitFlashFadeIn;
            float fadeOut = BulletManager.HitFlashFadeOut;
            float currentAlpha;
            if (HitFlash > fadeOut)
                currentAlpha = (fadeIn + fadeOut - HitFlash) / MathF.Max(fadeIn, 0.001f);
            else
                currentAlpha = HitFlash / MathF.Max(fadeOut, 0.001f);
            // Set HitFlash so fade-in duration scales with how far from full-white we are.
            HitFlash = fadeOut + fadeIn * (1f - MathHelper.Clamp(currentAlpha, 0f, 1f));
        }

        public GOProperties GOProperties => new GOProperties
        {
            Id               = ID,
            Flags            = ComputeFlags(),
            CurrentXP        = CurrentXP,
            MaxXP            = MaxXP,
            DeathPointReward = DeathPointReward,
            CurrentHealth    = CurrentHealth,
            CurrentShield    = CurrentShield,
        };

        protected virtual GOFlags ComputeFlags()
        {
            GOFlags f = 0;
            if (DynamicPhysics)  f |= GOFlags.Dynamic;
            if (IsCollidable)    f |= GOFlags.Collidable;
            if (IsDestructible)  f |= GOFlags.Destructible;
            if (IsInteract)      f |= GOFlags.Interact;
            if (IsZoneBlock)     f |= GOFlags.ZoneBlock;
            if (IsPrototype)     f |= GOFlags.Prototype;

            // ZoneBlock implies Interact and forces non-collidable + non-dynamic (static)
            if (f.HasFlag(GOFlags.ZoneBlock))
            {
                f |= GOFlags.Interact;
                f &= ~GOFlags.Collidable;
                f &= ~GOFlags.Dynamic;
            }

            return f;
        }

        // Computed property for BoundingRadius based on Shape size
        public float BoundingRadius =>
            Shape != null ? MathF.Sqrt(Shape.Width * Shape.Width + Shape.Height * Shape.Height) / 2f : 0f;

        // World position of this object's own body center.
        // For root objects (no parent) this equals Position.
        // For children, Position is a local offset, so the parent's world position is used.
        public Vector2 ParentPosition => Parent?.Position ?? Position;

        // World-space centroid of this object's body and all its children.
        // Child positions are local offsets rotated by this object's current orientation.
        public Vector2 ObjectPosition
        {
            get
            {
                if (_children.Count == 0)
                    return Position;

                float cos = MathF.Cos(Rotation);
                float sin = MathF.Sin(Rotation);
                Vector2 sum = Position;
                foreach (GameObject child in _children)
                {
                    sum += new Vector2(
                        Position.X + child.Position.X * cos - child.Position.Y * sin,
                        Position.Y + child.Position.X * sin + child.Position.Y * cos);
                }
                return sum / (_children.Count + 1);
            }
        }

        // Constructor to initialize the GameObject with mandatory fields
        public GameObject(
            int id,
            string name,
            Vector2 position,
            float rotation,
            float mass,
            bool isDestructible,
            bool isCollidable,
            bool dynamicPhysics,
            Shape shape,
            Color fillColor,
            Color outlineColor,
            int outlineWidth,
            bool isPrototype = false
        )
        {
            // Validate that the shape is not null
            if (shape == null)
            {
                DebugLogger.PrintError($"GameObject creation failed: null shape provided for ID {id}.");
                throw new ArgumentNullException(nameof(shape), "Shape cannot be null.");
            }

            // Initialize properties
            ID = id;
            Name = name;
            IsPrototype = isPrototype;
            Position = position;
            PreviousPosition = position;
            Rotation = rotation;
            Mass = mass;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            DynamicPhysics = dynamicPhysics;
            FillColor = fillColor;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
            Shape = shape;

            // Register the GameObject with ShapeManager, if not a prototype
            if (!isPrototype)
            {
                GameObjectRegister.RegisterGameObject(this); // Delegate registration to the new GameObjectRegister class
            }
        }

        // Load content for the Shape
        public virtual void LoadContent(GraphicsDevice graphics)
        {
            Shape?.LoadContent(graphics);  // Load the content for the shape, if it exists
        }

        // Update the GameObject (can be overridden for specific behavior)
        public virtual void Update()
        {
            if (Core.DELTATIME <= 0)
            {
                DebugLogger.PrintWarning($"Skipped update for GameObject ID={ID}: deltaTime is non-positive ({Core.DELTATIME}).");
                return;
            }

            if (IsDying)
            {
                Rotation += DeathFadeSpinVelocity * Core.DELTATIME;
            }
            else if (FarmAttributes.HasValue)
            {
                // Acceleration-based spin: AngularVelocity ramps toward a target direction that
                // periodically reverses. FloatSpeed controls reversal frequency; FarmFloatPhase
                // staggers each instance so they spin out-of-sync.
                FarmAttributes fa = FarmAttributes.Value;
                float freq        = MathF.Max(0.001f, fa.FloatSpeed);
                float targetDir   = MathF.Sin((Core.GAMETIME + FarmFloatPhase) * freq * MathF.Tau) >= 0f ? 1f : -1f;
                float targetVel   = targetDir * RotationSpeed;
                float accel       = RotationSpeed * 4f * Core.DELTATIME;
                AngularVelocity  += MathHelper.Clamp(targetVel - AngularVelocity, -accel, accel);
                Rotation         += AngularVelocity * Core.DELTATIME;
            }
            else if (RotationSpeed != 0f)
            {
                // Non-farm objects: per-object direction changes using golden-ratio phase.
                const float SpinPeriod = 8f;
                float phase     = ID * 1.618f;
                float targetDir = MathF.Sin((Core.GAMETIME + phase) * MathF.Tau / SpinPeriod) >= 0f ? 1f : -1f;
                float targetVel = targetDir * RotationSpeed;
                float accel     = RotationSpeed * 4f * Core.DELTATIME;
                AngularVelocity += MathHelper.Clamp(targetVel - AngularVelocity, -accel, accel);
                Rotation        += AngularVelocity * Core.DELTATIME;
            }

            if (HitFlash > 0f)
                HitFlash = MathHelper.Clamp(HitFlash - Core.DELTATIME, 0f, BulletManager.HitFlashDuration);

            if (PhysicsVelocity != Vector2.Zero && DynamicPhysics)
            {
                Position += PhysicsVelocity * Core.DELTATIME;
                PhysicsVelocity *= MathHelper.Clamp(1f - PhysicsManager.FrictionRate * Core.DELTATIME, 0f, 1f);
                if (PhysicsVelocity.LengthSquared() < 1f)
                    PhysicsVelocity = Vector2.Zero;
            }
        }

        // Explicitly manage resource cleanup
        public void Dispose()
        {
            // Unlink from parent
            Parent?.RemoveChild(this);

            // If the GameObject is registered, unregister it upon disposal
            GameObjectRegister.UnregisterGameObject(this);

            // Dispose of Shape if it has IDisposable functionality
            if (Shape is IDisposable disposableShape)
            {
                disposableShape.Dispose();
            }

            DebugLogger.PrintGO($"Disposed and unregistered GameObject ID={ID}");
        }
    }
}
