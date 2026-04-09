using Microsoft.Xna.Framework;

namespace op.io
{
    public struct Attributes_Barrel
    {
        // Normal attributes — stored in DB, have defaults.
        public float BarrelMass         { get; set; }
        public float BulletDamage       { get; set; }
        public float BulletPenetration  { get; set; }
        public float ReloadSpeed        { get; set; }
        public float BulletMass         { get; set; }
        public float BulletSpeed        { get; set; }
        public float BulletMaxLifespan  { get; set; }
        public Color BulletFillColor    { get; set; }
        public Color BulletOutlineColor { get; set; }
        public int   BulletOutlineWidth { get; set; }
        public int   BulletFillAlphaRaw    { get; set; }  // raw DB int; -1 → use default fill color
        public int   BulletOutlineAlphaRaw { get; set; }  // raw DB int; -1 → use default outline color
        // Hidden: BulletHealth (from BulletMass), BulletRadius (from BulletMass), BulletDrag (from BulletRadius).
        // Access via AttributeDerived.*
    }

    public struct Attributes_Body
    {
        // Normal attributes — stored in DB, have defaults.
        public float Mass { get; set; }

        // Health group
        public float HealthRegen         { get; set; }
        public float HealthRegenDelay    { get; set; }
        public float HealthArmor         { get; set; }

        // Shield group
        public float MaxShield           { get; set; }
        public float ShieldRegen         { get; set; }
        public float ShieldRegenDelay    { get; set; }
        public float ShieldArmor         { get; set; }

        // Combat group
        public float BodyCollisionDamage { get; set; }
        public float BodyPenetration     { get; set; }

        // Resistance group
        public float CollisionDamageResistance { get; set; }
        public float BulletDamageResistance    { get; set; }

        // Movement group
        public float Speed               { get; set; }  // Movement speed multiplier
        public float Control             { get; set; }  // Controls rotation and acceleration responsiveness

        // Action buff
        public float BodyActionBuff      { get; set; }

        // Hidden: MaxHealth (from Mass), BodyKnockback (from Mass),
        //         RotationSpeed (from Control), BodyAccelerationSpeed (from Control).
        // Access via AttributeDerived.*
    }

    public struct Attributes_Unit
    {
        public string Name { get; set; }
        public float DeathPointReward { get; set; }
        public float BodySwitchSpeed { get; set; }
        public float BarrelSwitchSpeed { get; set; }
    }

    /// <summary>
    /// Controls the back-and-forth sine-wave float animation applied to farm objects.
    /// Visible in the Properties block under the Destructible section.
    /// </summary>
    public struct FarmAttributes
    {
        /// <summary>Reserved (unused by current rotation model).</summary>
        public float FloatAmplitude { get; set; }
        /// <summary>Direction-reversal frequency in cycles per second.</summary>
        public float FloatSpeed     { get; set; }
    }

    /// <summary>
    /// Computes hidden attribute values from normal (stored) attributes.
    /// Hidden attributes are derived — not stored in DB — and accessed only through these functions.
    /// </summary>
    public static class AttributeDerived
    {
        // ── Body: hidden attributes derived from Mass ────────────────────────────

        /// <summary>
        /// Max health capacity. Formula: mass × 33.33 (so mass=3 → HP=100).
        /// </summary>
        public const float MaxHealthPerMass = 100f / 3f;
        public static float MaxHealth(float mass) => mass * MaxHealthPerMass;

        /// <summary>
        /// Body knockback impulse magnitude per collision frame.
        /// Formula: mass × KnockbackMassScale (loaded from PhysicsSettings).
        /// </summary>
        public static float BodyKnockback(float mass, float knockbackMassScale)
            => mass * knockbackMassScale;

        // ── Body: hidden attributes derived from Control ─────────────────────────

        /// <summary>Base acceleration ramp time when Control = 1.</summary>
        public const float BaseAccelerationDelay = 0.2f;
        /// <summary>Base rotation delay (seconds to turn 180°) when Control = 1.</summary>
        public const float BaseRotationDelay = 0.15f;

        /// <summary>
        /// Seconds to ramp to full movement speed. Lower = snappier acceleration.
        /// Formula: BaseAccelDelay / control
        /// </summary>
        public static float AccelerationDelay(float control)
            => control > 0f ? BaseAccelerationDelay / control : BaseAccelerationDelay;

        /// <summary>
        /// Seconds to turn 180°. Lower = faster rotation.
        /// Formula: BaseRotDelay / control
        /// </summary>
        public static float RotationDelay(float control)
            => control > 0f ? BaseRotationDelay / control : BaseRotationDelay;

        /// <summary>
        /// Effective movement speed in px/s.
        /// Formula: speed × baseSpeed
        /// </summary>
        public static float BodySpeed(float speed, float baseSpeed)
            => speed * baseSpeed;

        // ── Barrel: hidden attributes derived from BulletMass ───────────────────

        /// <summary>
        /// Bullet penetration HP. Formula: mass × (10/3) so default mass=3 → HP=10.
        /// </summary>
        public const float BulletHealthPerMass = 10f / 3f;
        public static float BulletHealth(float bulletMass)
            => bulletMass * BulletHealthPerMass;

        /// <summary>
        /// Bullet radius in pixels. Formula: sqrt(mass) × BulletRadiusScalar.
        /// </summary>
        public static float BulletRadius(float bulletMass, float bulletRadiusScalar)
            => System.MathF.Sqrt(System.MathF.Max(bulletMass, 0.01f)) * bulletRadiusScalar;

        // ── Barrel: hidden attributes derived from BulletRadius ──────────────────

        /// <summary>
        /// Air drag coefficient (per second). Larger radius = more drag.
        /// Formula: airR × π × r² / defaultDragFactor
        /// </summary>
        public static float BulletDrag(float bulletRadius, float airResistanceScalar, float defaultDragFactor)
        {
            if (defaultDragFactor <= 0f) return 0f;
            return airResistanceScalar * System.MathF.PI * bulletRadius * bulletRadius / defaultDragFactor;
        }

        // ── Display labels for the Affects column ────────────────────────────────

        public static readonly string[] AffectsMaxHealth         = ["Health cap", "Health bar size"];
        public static readonly string[] AffectsBodyKnockback      = ["Collision impulse"];
        public static readonly string[] AffectsSpeed              = ["Movement speed"];
        public static readonly string[] AffectsRotationSpeed      = ["Turn rate"];
        public static readonly string[] AffectsAccelerationSpeed  = ["Movement ramp-up"];
        public static readonly string[] AffectsBulletHealth       = ["Penetration HP"];
        public static readonly string[] AffectsBulletRadius       = ["Visual size", "Hitbox radius"];
        public static readonly string[] AffectsBulletDrag         = ["Decel rate"];
    }
}
