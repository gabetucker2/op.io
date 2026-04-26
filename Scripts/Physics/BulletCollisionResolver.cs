using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    /// <summary>
    /// Resolves collisions between bullets and world objects based on each object's physics tag.
    ///
    /// Tag behaviour:
    ///   IsCollidable = false                     → bullet passes through (ignored)
    ///   IsCollidable = true, StaticPhysics = true → bounce: velocity reflected off surface normal;
    ///                                               speed loss scales with object mass fraction
    ///   IsCollidable = true, StaticPhysics = false → hit: mass-ratio depenetration applied
    ///                                               each frame; only approach-velocity component
    ///                                               is drained (inelastic, e=0) so tangential
    ///                                               motion is preserved; bullet penetration HP
    ///                                               decays proportional to overlap depth.
    /// </summary>
    public static class BulletCollisionResolver
    {
        // Tracks active bullet-object contacts from the previous frame.
        // A pair appearing for the first time is a new hit; pairs no longer present have separated.
        private static HashSet<long> _prevContacts = new();
        private static HashSet<long> _currContacts = new();

        private static long ContactKey(int bulletId, int objId) => ((long)bulletId << 32) | (uint)objId;

        public static void ResolveCollisions(IReadOnlyList<Bullet> bullets, List<GameObject> gameObjects)
        {
            if (bullets == null || gameObjects == null) return;

            _currContacts.Clear();

            foreach (var bullet in bullets)
            {
                if (bullet == null || bullet.IsDying || bullet.IsBarrelLocked) continue;

                foreach (var obj in gameObjects)
                {
                    if (obj == null || obj == bullet) continue;

                    // Agents are fully handled by BulletCollisionSystem (damage + depenetration).
                    // Letting them fall through HitBullet would push bullets past enemies before
                    // BulletCollisionSystem runs, defeating the depenetration pass.
                    if (obj is Agent) continue;

                    // Non-collidable: bullet passes through entirely
                    if (!obj.IsCollidable) continue;

                    if (!CollisionManager.TryGetCollision(bullet, obj, out Vector2 mtv)) continue;
                    if (mtv == Vector2.Zero) continue;

                    if (!obj.DynamicPhysics)
                    {
                        ReflectBullet(bullet, obj, mtv);
                    }
                    else
                    {
                        long key          = ContactKey(bullet.ID, obj.ID);
                        bool isNewContact = _currContacts.Add(key) && !_prevContacts.Contains(key);
                        HitBullet(bullet, obj, mtv, isNewContact);
                    }
                }
            }

            (_prevContacts, _currContacts) = (_currContacts, _prevContacts);
        }

        // Reflect bullet off an immovable surface. Object position is unchanged.
        // BounceVelocityLoss is scaled by the object's mass fraction: heavy objects absorb more
        // energy so the bullet loses more speed; light objects absorb less.
        private static void ReflectBullet(Bullet bullet, GameObject obj, Vector2 mtv)
        {
            Vector2 normal = Vector2.Normalize(mtv);
            float vDotN = Vector2.Dot(bullet.Velocity, normal);
            bullet.Velocity -= 2f * vDotN * normal;
            bullet.Position -= mtv; // push bullet clear of the surface

            float mBullet = MathF.Max(bullet.Mass, 0.0001f);
            float mObject = MathF.Max(obj.Mass, 0.0001f);
            float massFraction = mObject / (mBullet + mObject); // 0 → light obj, 1 → infinitely heavy obj
            float speedRetained = MathF.Max(1f - BulletManager.BounceVelocityLoss * massFraction, 0f);
            bullet.Velocity *= speedRetained;
            ClampBulletSpeed(bullet);
        }

        // Bullet hits a non-static, non-agent object.
        // No position correction on the object — that was the source of stiffness (it moved
        // the object away as fast as the bullet moved in, preventing any real embedding).
        // Instead a spring force proportional to penetration depth decelerates the bullet
        // and expels it once inward momentum is exhausted (elastic feel).
        // Damage scales with penetration depth so a grazing frame ≠ a deep-embed frame.
        private static void HitBullet(Bullet bullet, GameObject obj, Vector2 mtv, bool isNewContact)
        {
            float mBullet   = MathF.Max(bullet.Mass, 0.0001f);
            float mObject   = MathF.Max(obj.Mass, 0.0001f);
            float totalMass = mBullet + mObject;
            float mtvLen    = mtv.Length();
            if (mtvLen < 1e-6f) return;

            // separationNormal: direction from object toward bullet (push-out direction).
            // mtv points from bullet toward object, so -mtv points outward for the bullet.
            Vector2 separationNormal = -mtv / mtvLen;

            // Damped spring: spring force stops bullet before it phases through and expels it
            // back out; damping absorbs energy to control elasticity.
            // Mass is excluded from the denominator so embed depth = v/√k regardless of bullet
            // size — without this, light bullets get enormous acceleration and barely embed at all.
            //   PenetrationSpringCoeff  – stiffness: higher = shallower embed (d_max = v/√k)
            //   PenetrationDamping      – energy loss: 0 = elastic; ~√k = critical (no overshoot)
            float vDotN        = Vector2.Dot(bullet.Velocity, separationNormal);
            float springAccel  = BulletManager.PenetrationSpringCoeff * mtvLen;   // outward
            float dampingAccel = -BulletManager.PenetrationDamping * vDotN;       // opposes motion
            bullet.Velocity += separationNormal * (springAccel + dampingAccel) * Core.DELTATIME;
            ClampBulletSpeed(bullet);

            // Drain bullet penetration HP proportional to overlap depth and object resistance.
            float massFraction = mObject / totalMass;
            bullet.CurrentPenetrationHP -= mtvLen * BulletManager.HitVelocityLoss * massFraction * Core.DELTATIME;

            // Deal full BulletDamage once on first contact, matching agent damage behaviour.
            if (obj.IsDestructible && isNewContact)
            {
                float dmg = bullet.BulletDamage;
                float dealtToObj = obj.ApplyDamage(dmg, bullet.OwnerID);
                obj.TriggerHitFlash();
                bullet.TriggerHitFlash();
                DamageNumberManager.Notify(obj.ID, obj.Position, dealtToObj, sourceId: bullet.SourceID, isNewHit: true);
                obj.DeathImpulse = bullet.Velocity;
            }

            // Transfer approach momentum from bullet to the dynamic target, scaled by mass
            // ratio and the shared dynamic-target knockback scalar. As mObject → ∞ or
            // mBullet → 0 the transfer tends to zero; the scalar provides an additional
            // tuneable attenuator so light bullets don't disproportionately shove heavy
            // dynamic objects.
            float vInward = -vDotN; // positive when bullet is moving toward the object
            if (vInward > 0f)
            {
                float transferSpeed = vInward * mBullet / totalMass * BulletManager.BulletDynamicKnockbackScalar;
                obj.PhysicsVelocity += -separationNormal * transferSpeed;
            }
        }

        private static void ClampBulletSpeed(Bullet bullet)
        {
            float maxSpd = bullet.MaxSpeed;
            if (maxSpd <= 0f) return;
            float spdSq = bullet.Velocity.LengthSquared();
            if (spdSq > maxSpd * maxSpd)
                bullet.Velocity = Vector2.Normalize(bullet.Velocity) * maxSpd;
        }
    }
}
