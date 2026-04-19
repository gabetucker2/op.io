using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class Bullet : GameObject
    {
        public Vector2 Velocity { get; set; }
        public new Vector2 PreviousPosition { get; set; }
        public int OwnerID  { get; set; } = -1;
        // Unique identifier for the (owner, barrel) pair that fired this bullet.
        // Computed at spawn as HashCode.Combine(ownerID, barrelIndex) so bullets
        // from different barrels of the same agent get distinct combat-text routines.
        public int SourceID { get; set; } = 0;
        public int SourceBarrelIndex { get; set; } = -1;
        public string SourceBarrelName { get; set; }
        public float CurrentPenetrationHP { get; set; }
        public float MaxPenetrationHP { get; }
        public float LifetimeElapsed { get; private set; }
        public float BulletDamage { get; }
        public float BulletPenetration { get; }
        public float BulletKnockback { get; }
        public float MaxSpeed { get; }
        public float MaxLifespan { get; }
        public float DragFactor { get; }
        public new bool IsDying { get; private set; } = false;

        private float _dyingTimer = 0f;
        private readonly float _volume;

        public Bullet(int id, Vector2 position, Vector2 velocity, float mass,
                      float maxLifespan, float dragFactor, Shape shape,
                      float bulletHealth, float bulletDamage, float bulletPenetration,
                      float bulletKnockback, float maxSpeed,
                      Color fillColor, Color outlineColor, int outlineWidth)
            : base(id, "Bullet", position, 0f, mass,
                   false, false, false,
                   shape,
                   fillColor, outlineColor, outlineWidth)
        {
            Velocity = velocity;
            PreviousPosition = position;
            MaxLifespan = maxLifespan > 0 ? maxLifespan : BulletManager.DefaultBulletLifespan;
            DragFactor = dragFactor > 0 ? dragFactor : BulletManager.DefaultBulletDragFactor;
            MaxPenetrationHP = bulletHealth > 0 ? bulletHealth : 1f;
            CurrentPenetrationHP = MaxPenetrationHP;
            BulletDamage = bulletDamage;
            BulletPenetration = bulletPenetration;
            BulletKnockback = bulletKnockback;
            MaxSpeed = maxSpeed;
            DrawLayer = 100;
            _volume = ComputeVolume(shape);
        }

        // Computes 2D cross-sectional area (px²) used as a proxy for volume in drag calculations.
        // Larger bullets face more air resistance; shape type determines the formula.
        private static float ComputeVolume(Shape shape)
        {
            if (shape == null) return 1f;
            return shape.ShapeType switch
            {
                "Circle"    => MathF.PI * (shape.Width / 2f) * (shape.Width / 2f),
                "Rectangle" => (float)(shape.Width * shape.Height),
                _           => MathF.PI * (shape.Width / 2f) * (shape.Width / 2f)
            };
        }

        /// <summary>True while the bullet cannot collide with or damage its owner.</summary>
        public bool IsOwnerImmune => LifetimeElapsed < BulletManager.OwnerImmunityDuration;

        public void StartDying()
        {
            if (IsDying) return;
            IsDying = true;
            _dyingTimer = 0f;
        }

        public override void Update()
        {
            if (Core.DELTATIME <= 0) return;

            LifetimeElapsed += Core.DELTATIME;

            if (HitFlash > 0f)
                HitFlash = MathHelper.Clamp(HitFlash - Core.DELTATIME, 0f, BulletManager.HitFlashDuration);

            // During owner immunity, fade from fully transparent to fully opaque.
            float immunityDuration = BulletManager.OwnerImmunityDuration;
            if (immunityDuration > 0f && LifetimeElapsed < immunityDuration)
                Opacity = MathHelper.Clamp(LifetimeElapsed / immunityDuration, 0f, 1f);
            else if (!IsDying)
                Opacity = 1f;

            if (IsDying)
            {
                _dyingTimer += Core.DELTATIME;
                float fadeIn  = BulletManager.DespawnFadeIn;
                float fadeOut = BulletManager.DespawnFadeOut;
                Opacity = _dyingTimer < fadeIn
                    ? 1f
                    : MathHelper.Clamp(1f - (_dyingTimer - fadeIn) / MathF.Max(fadeOut, 0.001f), 0f, 1f);
                if (_dyingTimer >= fadeIn + fadeOut)
                    BulletManager.MarkForRemoval(this);
                return;
            }

            float drag = BulletManager.AirResistanceScalar * _volume / MathF.Max(DragFactor, 0.0001f);
            Velocity *= MathF.Max(1f - drag * Core.DELTATIME, 0f);

            // Hard ceiling — no collision resolver can push velocity past MaxSpeed.
            if (MaxSpeed > 0f)
            {
                float spdSq = Velocity.LengthSquared();
                if (spdSq > MaxSpeed * MaxSpeed)
                    Velocity = Vector2.Normalize(Velocity) * MaxSpeed;
            }

            Vector2 movement = Velocity * Core.DELTATIME;
            Position += movement;
        }
    }
}
