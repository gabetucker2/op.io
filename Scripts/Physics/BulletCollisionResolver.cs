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
    ///   IsCollidable = true, StaticPhysics = false → hit: forward momentum maintained, speed
    ///                                               attenuated by same mass-fraction formula as bounce
    /// </summary>
    public static class BulletCollisionResolver
    {
        public static void ResolveCollisions(IReadOnlyList<Bullet> bullets, List<GameObject> gameObjects)
        {
            if (bullets == null || gameObjects == null) return;

            foreach (var bullet in bullets)
            {
                if (bullet == null) continue;

                foreach (var obj in gameObjects)
                {
                    if (obj == null || obj == bullet) continue;

                    // Non-collidable: bullet passes through entirely
                    if (!obj.IsCollidable) continue;

                    if (!CollisionManager.TryGetCollision(bullet, obj, out Vector2 mtv)) continue;
                    if (mtv == Vector2.Zero) continue;

                    if (obj.StaticPhysics)
                        ReflectBullet(bullet, obj, mtv);
                    else
                        HitBullet(bullet, obj, mtv);
                }
            }
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
        }

        // Bullet hits a non-static object and passes through it.
        // Forward momentum is maintained (no direction change); speed is attenuated by the same
        // mass-fraction formula as bounce. The bullet is pushed forward by the overlap distance
        // so it exits past the surface and doesn't re-collide on the next frame.
        private static void HitBullet(Bullet bullet, GameObject obj, Vector2 mtv)
        {
            // Push bullet forward through the surface by the overlap length
            float overlap = mtv.Length();
            if (overlap > 0f)
            {
                Vector2 forward = Vector2.Normalize(bullet.Velocity);
                bullet.Position += forward * overlap;
            }

            float mBullet = MathF.Max(bullet.Mass, 0.0001f);
            float mObject = MathF.Max(obj.Mass, 0.0001f);
            float massFraction = mObject / (mBullet + mObject); // 0 → light obj, 1 → infinitely heavy obj
            float speedRetained = MathF.Max(1f - BulletManager.HitVelocityLoss * massFraction, 0f);
            bullet.Velocity *= speedRetained;
        }
    }
}
