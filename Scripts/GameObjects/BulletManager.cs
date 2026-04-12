using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class BulletManager
    {
        private static readonly List<Bullet> _bullets = new();
        private static readonly HashSet<Bullet> _toRemove = new();

        public static IReadOnlyList<Bullet> GetBullets() => _bullets;
        private static int _nextId = 100000;

        private static float? _cachedAirResistanceScalar = null;
        public static float AirResistanceScalar
        {
            get
            {
                if (!_cachedAirResistanceScalar.HasValue)
                    _cachedAirResistanceScalar = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "AirResistanceScalar");
                return _cachedAirResistanceScalar.Value;
            }
        }

        private static float? _cachedBounceVelocityLoss = null;
        public static float BounceVelocityLoss
        {
            get
            {
                if (!_cachedBounceVelocityLoss.HasValue)
                    _cachedBounceVelocityLoss = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "BounceVelocityLoss");
                return _cachedBounceVelocityLoss.Value;
            }
        }

        private static float? _cachedHitVelocityLoss = null;
        public static float HitVelocityLoss
        {
            get
            {
                if (!_cachedHitVelocityLoss.HasValue)
                    _cachedHitVelocityLoss = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "HitVelocityLoss");
                return _cachedHitVelocityLoss.Value;
            }
        }

        private static float? _cachedDefaultBulletSpeed = null;
        public static float DefaultBulletSpeed
        {
            get
            {
                if (!_cachedDefaultBulletSpeed.HasValue)
                    _cachedDefaultBulletSpeed = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletSpeed");
                return _cachedDefaultBulletSpeed.Value;
            }
        }

        private static float? _cachedDefaultBulletLifespan = null;
        public static float DefaultBulletLifespan
        {
            get
            {
                if (!_cachedDefaultBulletLifespan.HasValue)
                    _cachedDefaultBulletLifespan = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletLifespan");
                return _cachedDefaultBulletLifespan.Value;
            }
        }

        private static float? _cachedDefaultBulletDragFactor = null;
        public static float DefaultBulletDragFactor
        {
            get
            {
                if (!_cachedDefaultBulletDragFactor.HasValue)
                    _cachedDefaultBulletDragFactor = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletDragFactor");
                return _cachedDefaultBulletDragFactor.Value;
            }
        }

        private static float? _cachedDefaultBulletMass = null;
        public static float DefaultBulletMass
        {
            get
            {
                if (!_cachedDefaultBulletMass.HasValue)
                    _cachedDefaultBulletMass = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletMass");
                return _cachedDefaultBulletMass.Value;
            }
        }

        private static float? _cachedDefaultBulletHealth = null;
        public static float DefaultBulletHealth
        {
            get
            {
                if (!_cachedDefaultBulletHealth.HasValue)
                    _cachedDefaultBulletHealth = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletHealth");
                return _cachedDefaultBulletHealth.Value;
            }
        }

        private static float? _cachedPenetrationSpringCoeff = null;
        public static float PenetrationSpringCoeff
        {
            get
            {
                if (!_cachedPenetrationSpringCoeff.HasValue)
                    _cachedPenetrationSpringCoeff = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "PenetrationSpringCoeff");
                return _cachedPenetrationSpringCoeff.Value;
            }
        }

        private static float? _cachedPenetrationDamping = null;
        public static float PenetrationDamping
        {
            get
            {
                if (!_cachedPenetrationDamping.HasValue)
                    _cachedPenetrationDamping = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "PenetrationDamping");
                return _cachedPenetrationDamping.Value;
            }
        }

        private static float? _cachedDefaultBulletDamage = null;
        public static float DefaultBulletDamage
        {
            get
            {
                if (!_cachedDefaultBulletDamage.HasValue)
                    _cachedDefaultBulletDamage = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletDamage");
                return _cachedDefaultBulletDamage.Value;
            }
        }

        private static float? _cachedDefaultBulletPenetration = null;
        public static float DefaultBulletPenetration
        {
            get
            {
                if (!_cachedDefaultBulletPenetration.HasValue)
                    _cachedDefaultBulletPenetration = DatabaseFetch.GetValue<float>("BulletDefaults", "Value", "SettingKey", "DefaultBulletPenetration");
                return _cachedDefaultBulletPenetration.Value;
            }
        }

        private static float? _cachedBulletRadiusScalar = null;
        public static float BulletRadiusScalar
        {
            get
            {
                if (!_cachedBulletRadiusScalar.HasValue)
                    _cachedBulletRadiusScalar = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "BulletRadiusScalar");
                return _cachedBulletRadiusScalar.Value;
            }
        }

        private static float? _cachedBarrelHeightScalar = null;
        public static float BarrelHeightScalar
        {
            get
            {
                if (!_cachedBarrelHeightScalar.HasValue)
                    _cachedBarrelHeightScalar = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "BarrelHeightScalar");
                return _cachedBarrelHeightScalar.Value;
            }
        }

        private static float? _cachedBulletKnockbackScalar = null;
        public static float BulletKnockbackScalar
        {
            get
            {
                if (!_cachedBulletKnockbackScalar.HasValue)
                    _cachedBulletKnockbackScalar = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "BulletKnockbackScalar");
                return _cachedBulletKnockbackScalar.Value;
            }
        }

        private static float? _cachedBulletRecoilScalar = null;
        public static float BulletRecoilScalar
        {
            get
            {
                if (!_cachedBulletRecoilScalar.HasValue)
                    _cachedBulletRecoilScalar = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "BulletRecoilScalar");
                return _cachedBulletRecoilScalar.Value;
            }
        }

        private static float? _cachedBulletFarmKnockbackScalar = null;
        public static float BulletFarmKnockbackScalar
        {
            get
            {
                if (!_cachedBulletFarmKnockbackScalar.HasValue)
                    _cachedBulletFarmKnockbackScalar = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "BulletFarmKnockbackScalar");
                return _cachedBulletFarmKnockbackScalar.Value;
            }
        }

        private static float? _cachedOwnerImmunityDuration = null;
        public static float OwnerImmunityDuration
        {
            get
            {
                if (!_cachedOwnerImmunityDuration.HasValue)
                    _cachedOwnerImmunityDuration = DatabaseFetch.GetValue<float>("BulletPhysics", "Value", "SettingKey", "OwnerImmunityDuration");
                return _cachedOwnerImmunityDuration.Value;
            }
        }

        private static (float FadeIn, float Hold, float FadeOut)? _cachedHitFlashAnim;
        private static (float FadeIn, float Hold, float FadeOut) HitFlashAnim =>
            _cachedHitFlashAnim ??= DatabaseFetch.GetAnimSetting("HitFlashAnim", 0.05f, 0f, 0.2f);
        public static float HitFlashFadeIn  => HitFlashAnim.FadeIn;
        public static float HitFlashFadeOut => HitFlashAnim.FadeOut;
        public static float HitFlashDuration => HitFlashFadeIn + HitFlashFadeOut;

        private static (float FadeIn, float Hold, float FadeOut)? _cachedDespawnAnim;
        private static (float FadeIn, float Hold, float FadeOut) DespawnAnim =>
            _cachedDespawnAnim ??= DatabaseFetch.GetAnimSetting("DespawnAnim", 0f, 0f, 0.2f);
        public static float DespawnFadeIn  => DespawnAnim.FadeIn;
        public static float DespawnFadeOut => DespawnAnim.FadeOut;

        private static Color? _cachedDefaultBulletFillColor = null;
        public static Color DefaultBulletFillColor
        {
            get
            {
                if (!_cachedDefaultBulletFillColor.HasValue)
                    _cachedDefaultBulletFillColor = new Color(
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletFillR",    255),
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletFillG",    0),
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletFillB",    0),
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletFillA",    255));
                return _cachedDefaultBulletFillColor.Value;
            }
        }

        private static Color? _cachedDefaultBulletOutlineColor = null;
        public static Color DefaultBulletOutlineColor
        {
            get
            {
                if (!_cachedDefaultBulletOutlineColor.HasValue)
                    _cachedDefaultBulletOutlineColor = new Color(
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletOutlineR", 139),
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletOutlineG", 0),
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletOutlineB", 0),
                        DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletOutlineA", 255));
                return _cachedDefaultBulletOutlineColor.Value;
            }
        }

        private static int? _cachedDefaultBulletOutlineWidth = null;
        public static int DefaultBulletOutlineWidth
        {
            get
            {
                if (!_cachedDefaultBulletOutlineWidth.HasValue)
                    _cachedDefaultBulletOutlineWidth = DatabaseFetch.GetSetting<int>("BulletDefaults", "Value", "SettingKey", "DefaultBulletOutlineWidth", 2);
                return _cachedDefaultBulletOutlineWidth.Value;
            }
        }

        // Computes bullet radius from mass: radius = sqrt(mass) * BulletRadiusScalar
        public static float ComputeBulletRadius(float mass)
        {
            return MathF.Sqrt(MathF.Max(mass, 0.01f)) * BulletRadiusScalar;
        }

        // Spawns a bullet from an agent using its active barrel attributes and current rotation.
        public static void SpawnBullet(Agent agent)
        {
            if (agent == null || agent.BarrelCount == 0) return;

            Attributes_Barrel attrs = agent.BarrelAttributes;
            float speed = attrs.BulletSpeed >= 0 ? attrs.BulletSpeed : DefaultBulletSpeed;
            float lifespan = attrs.BulletMaxLifespan >= 0 ? attrs.BulletMaxLifespan : DefaultBulletLifespan;
            float dragFactor = DefaultBulletDragFactor; // hidden: derived from radius (airR * pi * r^2 / dragFactor)
            float mass = attrs.BulletMass >= 0 ? attrs.BulletMass : DefaultBulletMass;

            Vector2 dir = new Vector2(MathF.Cos(agent.Rotation), MathF.Sin(agent.Rotation));
            Vector2 inheritedVelocity = Core.DELTATIME > 0f
                ? (agent.Position - agent.PreviousPosition) / Core.DELTATIME
                : Vector2.Zero;
            Vector2 velocity = inheritedVelocity + dir * speed;

            // Spawn at the center of the player so the bullet visually emerges from the body.
            Vector2 spawnPos = agent.Position;

            float radius = ComputeBulletRadius(mass);
            int diameter = Math.Max(1, (int)MathF.Round(radius * 2));
            Color fill    = attrs.BulletFillAlphaRaw    >= 0 ? attrs.BulletFillColor    : DefaultBulletFillColor;
            Color outline = attrs.BulletOutlineAlphaRaw >= 0 ? attrs.BulletOutlineColor : DefaultBulletOutlineColor;
            int   outlineW = attrs.BulletOutlineWidth   >= 0 ? attrs.BulletOutlineWidth : DefaultBulletOutlineWidth;
            var shape = new Shape("Circle", diameter, diameter, 0, fill, outline, outlineW);
            shape.LoadContent(Core.Instance.GraphicsDevice);

            float bulletHealth      = attrs.BulletHealth >= 0 ? attrs.BulletHealth : AttributeDerived.BulletHealth(mass);
            float bulletDamage      = attrs.BulletDamage      >= 0 ? attrs.BulletDamage      : DefaultBulletDamage;
            float bulletPenetration = attrs.BulletPenetration >= 0 ? attrs.BulletPenetration : DefaultBulletPenetration;
            float bulletKnockback   = AttributeDerived.BulletKnockback(bulletPenetration, BulletKnockbackScalar); // hidden: derived from BulletPenetration
            float bulletMaxSpeed    = speed + agent.BaseSpeed; // hidden: ceiling = bulletSpeed + body speed
            var bullet = new Bullet(_nextId++, spawnPos, velocity, mass, lifespan, dragFactor, shape, bulletHealth, bulletDamage, bulletPenetration, bulletKnockback, bulletMaxSpeed, fill, outline, outlineW);
            bullet.OwnerID  = agent.ID;
            bullet.SourceID = HashCode.Combine(agent.ID, agent.ActiveBarrelIndex);
            _bullets.Add(bullet);
        }

        public static void MarkForRemoval(Bullet bullet)
        {
            _toRemove.Add(bullet);
        }

        public static void Update()
        {
            foreach (var bullet in _bullets)
            {
                bullet.PreviousPosition = bullet.Position;
                bullet.Update();
            }

            foreach (var bullet in _toRemove)
                bullet.Dispose();
            _bullets.RemoveAll(b => _toRemove.Contains(b));
            _toRemove.Clear();
        }
    }
}
