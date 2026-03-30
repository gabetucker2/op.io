using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    /// <summary>
    /// Swept circle-circle collision system for bullet interactions.
    /// Covers bullet-enemy damage/penetration and bullet-bullet deflection.
    /// Called from GameUpdater.Update() after BulletCollisionResolver.
    ///
    /// BulletCollisionResolver skips Agent objects so this system is the sole
    /// authority for all bullet-enemy physics.
    ///
    /// Per-frame order:
    ///   Steps 1-2  – snapshot + advance positions  (BulletManager / Bullet.Update)
    ///   Steps 3-4  – sweep bullet vs enemy; apply damage sorted by t_enter
    ///   Step  5    – despawn expired bullets
    ///   Step  6    – bullet-enemy depenetration (BOTH sides position-corrected so
    ///                enemies can push bullets and bullets stop at enemy surfaces)
    ///   Step  7    – bullet-bullet elastic collision
    /// </summary>
    public static class BulletCollisionSystem
    {
        // Fallback radii when a shape is unavailable.
        public static float BulletMass   = 1f;
        public static float BulletRadius = 6f;
        public static float EnemyRadius  = 30f;

        // Persistent contact tracking: a pair present last frame is a continuing contact,
        // not a new hit — even if depenetration pushed the bullet geometrically outside.
        // Without this, a bounced bullet oscillating against the player wall-side re-triggers
        // full BulletDamage every frame (depenetration pushes bullet to wall → wall reflects
        // it back → c>0 again → IsNewContact=true again → damage loop).
        private static HashSet<long> _prevContacts = new();
        private static HashSet<long> _currContacts = new();

        private static long ContactKey(int bulletId, int agentId) => ((long)bulletId << 32) | (uint)agentId;

        private struct Contact
        {
            public Bullet Bullet;
            public Agent  Enemy;
            public float  TEnter;
            public float  TExit;
            public bool   IsNewContact;
        }

        public static void Update(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            var bullets = BulletManager.GetBullets();
            _currContacts.Clear();
            if (bullets.Count == 0)
            {
                (_prevContacts, _currContacts) = (_currContacts, _prevContacts);
                return;
            }

            var gameObjects = Core.Instance.GameObjects;
            var agents = new List<Agent>(gameObjects.Count);
            foreach (var go in gameObjects)
                if (go is Agent a) agents.Add(a);

            var allContacts    = new List<Contact>();
            var bulletContacts = new List<Contact>();

            // ── Steps 3 & 4: Swept detection + damage ───────────────────────────
            if (agents.Count > 0)
            {
                foreach (var bullet in bullets)
                {
                    if (bullet == null || bullet.IsDying || bullet.CurrentPenetrationHP <= 0f) continue;

                    float br = bullet.Shape != null ? bullet.Shape.Width * 0.5f : BulletRadius;

                    Vector2 sweepDelta = bullet.Position - bullet.PreviousPosition;
                    float   a          = Vector2.Dot(sweepDelta, sweepDelta);

                    bulletContacts.Clear();

                    foreach (var enemy in agents)
                    {
                        if (enemy == null) continue;

                        float   er   = enemy.Shape != null ? enemy.Shape.Width * 0.5f : EnemyRadius;
                        float   sumR = br + er;
                        Vector2 w    = bullet.PreviousPosition - enemy.Position;
                        float   c    = Vector2.Dot(w, w) - sumR * sumR;

                        float tEnter, tExit;

                        if (a < 1e-10f)
                        {
                            // Bullet stationary this frame — static overlap only
                            if (c > 0f) continue;
                            tEnter = 0f;
                            tExit  = 1f;
                        }
                        else
                        {
                            float b    = 2f * Vector2.Dot(w, sweepDelta);
                            float disc = b * b - 4f * a * c;
                            if (disc < 0f) continue;

                            float sqrtDisc = MathF.Sqrt(disc);
                            float inv2a   = 0.5f / a;
                            tEnter = (-b - sqrtDisc) * inv2a;
                            tExit  = (-b + sqrtDisc) * inv2a;

                            if (tExit < 0f || tEnter > 1f) continue;
                            tEnter = MathF.Max(tEnter, 0f);
                            tExit  = MathF.Min(tExit,  1f);
                        }

                        long key         = ContactKey(bullet.ID, enemy.ID);
                        bool isNewContact = _currContacts.Add(key) && !_prevContacts.Contains(key);
                        var contact = new Contact
                        {
                            Bullet       = bullet,
                            Enemy        = enemy,
                            TEnter       = tEnter,
                            TExit        = tExit,
                            IsNewContact = isNewContact
                        };
                        bulletContacts.Add(contact);
                        allContacts.Add(contact);
                    }

                    if (bulletContacts.Count == 0) continue;

                    bulletContacts.Sort(static (x, y) => x.TEnter.CompareTo(y.TEnter));

                    foreach (var contact in bulletContacts)
                    {
                        if (bullet.CurrentPenetrationHP <= 0f) break;

                        float           overlapDuration = contact.TExit - contact.TEnter;
                        Attributes_Body body            = contact.Enemy.BodyAttributes;

                        // Only deal health damage on first contact — the bullet was outside
                        // at frame start, so this is a genuine new penetration event.
                        // Bullets already embedded from a prior frame skip the health damage
                        // to prevent multi-frame accumulation that made hits inconsistent.
                        if (contact.IsNewContact)
                        {
                            float resistance = MathF.Max(body.BulletDamageResistance - bullet.BulletPenetration, 0f);
                            float dmg = bullet.BulletDamage * (1f - resistance);
                            contact.Enemy.CurrentHealth -= dmg;
                            contact.Enemy.TriggerHitFlash();
                            bullet.TriggerHitFlash();
                            contact.Enemy.LastDamagedByAgentID = bullet.OwnerID;
                            DamageNumberManager.Notify(contact.Enemy.ID, contact.Enemy.Position, dmg, sourceId: bullet.SourceID, isNewHit: true);
                        }

                        // Penetration HP drains regardless so embedded bullets still expire.
                        bullet.CurrentPenetrationHP -= body.BodyCollisionDamage
                                                       * (1f - body.CollisionDamageResistance)
                                                       * overlapDuration;
                    }
                }
            }

            (_prevContacts, _currContacts) = (_currContacts, _prevContacts);

            // ── Step 5: Begin fade-out for expired bullets ────────────────────────
            foreach (var bullet in bullets)
            {
                if (bullet != null && !bullet.IsDying && IsBulletExpired(bullet))
                    bullet.StartDying();
            }

            // ── Step 6: Bullet-enemy depenetration ────────────────────────────────
            // Both bullet and enemy positions are corrected proportionally to inverse mass.
            // Correcting the bullet side is required so that:
            //   (a) Bullets stop at enemy surfaces rather than drifting through.
            //   (b) Enemies running into bullets push the bullet away.
            // The inelastic impulse (e = 0) drains the bullet's approach velocity so
            // subsequent frames see a smaller overlap and smaller corrections.
            foreach (var contact in allContacts)
            {
                Bullet bullet = contact.Bullet;
                Agent  enemy  = contact.Enemy;
                if (bullet == null || enemy == null) continue;

                float   br    = bullet.Shape != null ? bullet.Shape.Width * 0.5f : BulletRadius;
                float   er    = enemy.Shape  != null ? enemy.Shape.Width  * 0.5f : EnemyRadius;
                float   sumR  = br + er;
                Vector2 diff  = bullet.Position - enemy.Position;
                float   dist  = diff.Length();
                float overlap = sumR - dist;
                if (overlap <= 0f) continue;

                Vector2 normal = dist > 1e-6f ? diff / dist : Vector2.UnitX;

                float mBullet   = MathF.Max(BulletMass,  0.0001f);
                float mEnemy    = MathF.Max(enemy.Mass,   0.0001f);
                float totalMass = mBullet + mEnemy;

                bullet.Position += normal * (overlap * mEnemy  / totalMass);
                enemy.Position  -= normal * (overlap * mBullet / totalMass);

                float vDotN = Vector2.Dot(bullet.Velocity, normal);
                if (vDotN < 0f)
                    bullet.Velocity -= (vDotN * mEnemy / totalMass) * normal;
            }

            // ── Step 7: Bullet-bullet elastic collision ───────────────────────────
            // Bullets are not registered in GameObjects so no other system handles
            // this. Process each unique pair once (i < j).
            for (int i = 0; i < bullets.Count; i++)
            {
                Bullet ba = bullets[i];
                if (ba == null || ba.IsDying || IsBulletExpired(ba)) continue;

                float ra = ba.Shape != null ? ba.Shape.Width * 0.5f : BulletRadius;

                for (int j = i + 1; j < bullets.Count; j++)
                {
                    Bullet bb = bullets[j];
                    if (bb == null || bb.IsDying || IsBulletExpired(bb)) continue;

                    float   rb   = bb.Shape != null ? bb.Shape.Width * 0.5f : BulletRadius;
                    float   sumR = ra + rb;
                    Vector2 diff = ba.Position - bb.Position;
                    float   dist = diff.Length();
                    if (dist >= sumR) continue;

                    float overlap = sumR - dist;
                    Vector2 normal = dist > 1e-6f ? diff / dist : Vector2.UnitX;

                    float mA        = MathF.Max(ba.Mass, 0.0001f);
                    float mB        = MathF.Max(bb.Mass, 0.0001f);
                    float totalMass = mA + mB;

                    // Position correction
                    ba.Position += normal * (overlap * mB / totalMass);
                    bb.Position -= normal * (overlap * mA / totalMass);

                    // Elastic impulse (e = 1) — bullets deflect off each other
                    Vector2 relVel = ba.Velocity - bb.Velocity;
                    float   vRelN  = Vector2.Dot(relVel, normal);
                    if (vRelN >= 0f) continue; // already separating

                    float impulse = -2f * vRelN / (1f / mA + 1f / mB);
                    ba.Velocity += (impulse / mA) * normal;
                    bb.Velocity -= (impulse / mB) * normal;
                }
            }
        }

        private static bool IsBulletExpired(Bullet bullet)
        {
            return bullet.CurrentPenetrationHP <= 0f
                || bullet.LifetimeElapsed >= bullet.MaxLifespan;
        }
    }
}
