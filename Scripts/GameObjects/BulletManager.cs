using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class BulletManager
    {
        private static readonly List<Bullet> _bullets = new();
        private static readonly List<Bullet> _toRemove = new();

        public static IReadOnlyList<Bullet> GetBullets() => _bullets;
        private static int _nextId = 100000;

        private static float? _cachedAirResistance = null;
        public static float AirResistance
        {
            get
            {
                if (!_cachedAirResistance.HasValue)
                    _cachedAirResistance = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "AirResistance"); // scalar: drag = AirResistance * volume / range
                return _cachedAirResistance.Value;
            }
        }

        private static float? _cachedBounceVelocityLoss = null;
        public static float BounceVelocityLoss
        {
            get
            {
                if (!_cachedBounceVelocityLoss.HasValue)
                    _cachedBounceVelocityLoss = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "BounceVelocityLoss");
                return _cachedBounceVelocityLoss.Value;
            }
        }

        private static float? _cachedHitVelocityLoss = null;
        public static float HitVelocityLoss
        {
            get
            {
                if (!_cachedHitVelocityLoss.HasValue)
                    _cachedHitVelocityLoss = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "HitVelocityLoss");
                return _cachedHitVelocityLoss.Value;
            }
        }

        private static float? _cachedDefaultBulletSpeed = null;
        public static float DefaultBulletSpeed
        {
            get
            {
                if (!_cachedDefaultBulletSpeed.HasValue)
                    _cachedDefaultBulletSpeed = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "DefaultBulletSpeed");
                return _cachedDefaultBulletSpeed.Value;
            }
        }

        private static float? _cachedDefaultBulletLifespan = null;
        public static float DefaultBulletLifespan
        {
            get
            {
                if (!_cachedDefaultBulletLifespan.HasValue)
                    _cachedDefaultBulletLifespan = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "DefaultBulletLifespan");
                return _cachedDefaultBulletLifespan.Value;
            }
        }

        private static float? _cachedDefaultBulletRange = null;
        public static float DefaultBulletRange
        {
            get
            {
                if (!_cachedDefaultBulletRange.HasValue)
                    _cachedDefaultBulletRange = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "DefaultBulletRange");
                return _cachedDefaultBulletRange.Value;
            }
        }

        private static float? _cachedDefaultBulletMass = null;
        public static float DefaultBulletMass
        {
            get
            {
                if (!_cachedDefaultBulletMass.HasValue)
                    _cachedDefaultBulletMass = DatabaseFetch.GetValue<float>("PhysicsSettings", "Value", "SettingKey", "DefaultBulletMass");
                return _cachedDefaultBulletMass.Value;
            }
        }

        private static int? _cachedBulletRadius = null;
        public static int BulletRadius
        {
            get
            {
                if (!_cachedBulletRadius.HasValue)
                    _cachedBulletRadius = DatabaseFetch.GetValue<int>("PhysicsSettings", "Value", "SettingKey", "BulletRadius");
                return _cachedBulletRadius.Value;
            }
        }

        // Spawns a bullet from an agent using its barrel attributes and current rotation.
        // Designed to be called for both the player and NPC agents.
        public static void SpawnBullet(Agent agent)
        {
            if (agent == null) return;

            Attributes_Barrel attrs = agent.BarrelAttributes;
            float speed = attrs.BulletSpeed > 0 ? attrs.BulletSpeed : DefaultBulletSpeed;
            float lifespan = attrs.BulletMaxLifespan > 0 ? attrs.BulletMaxLifespan : DefaultBulletLifespan;
            float range = attrs.BulletRange > 0 ? attrs.BulletRange : DefaultBulletRange;
            float mass = attrs.BulletMass > 0 ? attrs.BulletMass : DefaultBulletMass;

            Vector2 dir = new Vector2(MathF.Cos(agent.Rotation), MathF.Sin(agent.Rotation));
            Vector2 velocity = dir * speed;

            // Spawn at the barrel tip so the bullet starts flush with the end of the barrel
            float barrelLength = agent.BarrelShape?.Width ?? 0f;
            Vector2 spawnPos = agent.Position + dir * barrelLength;

            int bulletRadius = BulletRadius;
            var shape = new Shape("Circle", bulletRadius * 2, bulletRadius * 2, 0,
                Color.Red, new Color(139, 0, 0), 2);
            shape.LoadContent(Core.Instance.GraphicsDevice);

            var bullet = new Bullet(_nextId++, spawnPos, velocity, mass, lifespan, range, shape);
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
                bullet.Update();
            }

            foreach (var bullet in _toRemove)
            {
                _bullets.Remove(bullet);
                bullet.Dispose();
            }
            _toRemove.Clear();
        }
    }
}
