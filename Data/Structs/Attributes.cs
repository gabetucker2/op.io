using Microsoft.Xna.Framework;

namespace op.io
{
    public struct Attributes_Barrel
    {
        // Normal attributes — stored in DB, have defaults.
        public float BulletDamage       { get; set; }
        public float BulletPenetration  { get; set; }
        public float ReloadSpeed        { get; set; }
        public float BulletMass         { get; set; }
        public float BulletSpeed        { get; set; }
        public float BulletMaxLifespan  { get; set; }
        public float BulletHealth       { get; set; }  // -1 → derived from BulletMass via AttributeDerived
        public Color BulletFillColor    { get; set; }
        public Color BulletOutlineColor { get; set; }
        public int   BulletOutlineWidth { get; set; }
        public int   BulletFillAlphaRaw    { get; set; }  // raw DB int; -1 → use default fill color
        public int   BulletOutlineAlphaRaw { get; set; }  // raw DB int; -1 → use default outline color

        // Bullet effectors — body-equivalent stats applied to bullets.
        // Hidden (derived from BulletMass): BulletHealthRegen, BulletHealthRegenDelay,
        //   BulletHealthArmor, BulletCollisionDamageResistance, BulletDamageResistance.
        // Access via AttributeDerived.*
        // Movement
        public float BulletControl             { get; set; }

        // Hidden: RecoilMass (from BulletMass),
        //         BulletRadius (from BulletMass), BulletDrag (from BulletRadius),
        //         BulletHealthRegen (from BulletMass), BulletHealthRegenDelay (from BulletMass),
        //         BulletHealthArmor (from BulletMass), BulletCollisionDamageResistance (from BulletMass),
        //         BulletDamageResistance (from BulletMass),
        //         BulletKnockback (from BulletPenetration).
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
        /// Recoil mass derived from bullet mass.
        /// Formula: bulletMass × RecoilMassPerBulletMass
        /// </summary>
        public const float RecoilMassPerBulletMass = 1f / 3f;
        public static float RecoilMass(float bulletMass)
            => bulletMass * RecoilMassPerBulletMass;

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

        // ── Barrel: hidden attributes derived from BulletPenetration ─────────────

        /// <summary>
        /// Bullet knockback impulse magnitude for collision physics.
        /// Formula: bulletPenetration × BulletKnockbackScalar (from PhysicsSettings).
        /// Higher penetration → more push on collided targets.
        /// </summary>
        public static float BulletKnockback(float bulletPenetration, float bulletKnockbackScalar)
            => bulletPenetration * bulletKnockbackScalar;

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

        // ── Barrel: hidden effectors derived from BulletMass ──────────────────

        /// <summary>Bullet health regen per second. Formula: bulletMass × scale.</summary>
        public const float BulletHealthRegenPerMass = 0f;
        public static float BulletHealthRegen(float bulletMass)
            => bulletMass * BulletHealthRegenPerMass;

        /// <summary>Delay before bullet health regen starts. Formula: bulletMass × scale.</summary>
        public const float BulletHealthRegenDelayPerMass = 0f;
        public static float BulletHealthRegenDelay(float bulletMass)
            => bulletMass * BulletHealthRegenDelayPerMass;

        /// <summary>Bullet health armor (flat damage reduction). Formula: bulletMass × scale.</summary>
        public const float BulletHealthArmorPerMass = 0f;
        public static float BulletHealthArmor(float bulletMass)
            => bulletMass * BulletHealthArmorPerMass;

        /// <summary>Bullet collision damage resistance. Formula: bulletMass × scale.</summary>
        public const float BulletCollisionDamageResistancePerMass = 0f;
        public static float BulletCollisionDamageResistance(float bulletMass)
            => bulletMass * BulletCollisionDamageResistancePerMass;

        /// <summary>Bullet damage resistance. Formula: bulletMass × scale.</summary>
        public const float BulletDamageResistancePerMass = 0f;
        public static float BulletBarrelDamageResistance(float bulletMass)
            => bulletMass * BulletDamageResistancePerMass;

        public static readonly string[] AffectsBulletHealthRegen              = ["Bullet HP/s"];
        public static readonly string[] AffectsBulletHealthRegenDelay         = ["Bullet regen delay"];
        public static readonly string[] AffectsBulletHealthArmor              = ["Bullet flat DR"];
        public static readonly string[] AffectsBulletCollisionDamageResistance = ["Bullet coll. resist"];
        public static readonly string[] AffectsBulletDamageResistance         = ["Bullet dmg resist"];
        public static readonly string[] AffectsBulletKnockback               = ["Collision push"];

        // ── Barrel: hidden dimensions derived from bullet attributes ─────────

        /// <summary>
        /// Barrel width (narrow dimension) in pixels, matching bullet diameter.
        /// Formula: 2 × BulletRadius(bulletMass, bulletRadiusScalar)
        /// </summary>
        public static float BarrelWidth(float bulletMass, float bulletRadiusScalar)
            => 2f * BulletRadius(bulletMass, bulletRadiusScalar);

        /// <summary>
        /// Barrel height (long dimension) in pixels, scaled from bullet speed.
        /// Formula: bulletSpeed × barrelHeightScalar
        /// </summary>
        public static float BarrelHeight(float bulletSpeed, float barrelHeightScalar)
            => System.MathF.Max(4f, bulletSpeed * barrelHeightScalar);

        // ── Display labels for the Affects column ────────────────────────────────

        public static readonly string[] AffectsMaxHealth         = ["Health cap", "Health bar size"];
        public static readonly string[] AffectsBodyKnockback      = ["Collision impulse"];
        public static readonly string[] AffectsSpeed              = ["Movement speed"];
        public static readonly string[] AffectsRotationSpeed      = ["Turn rate"];
        public static readonly string[] AffectsAccelerationSpeed  = ["Movement ramp-up"];
        public static readonly string[] AffectsRecoilMass           = ["Recoil force"];
        public static readonly string[] AffectsBulletHealth       = ["Penetration HP"];
        public static readonly string[] AffectsBulletRadius       = ["Visual size", "Hitbox radius"];
        public static readonly string[] AffectsBulletDrag         = ["Decel rate"];
        public static readonly string[] AffectsBarrelWidth        = ["Barrel visual width"];
        public static readonly string[] AffectsBarrelHeight       = ["Barrel visual length"];
    }
}
