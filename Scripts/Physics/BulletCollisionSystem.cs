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

        // Cached lists — reused every frame to avoid per-frame heap allocations.
        private static readonly List<Agent>   _agents        = new();
        private static readonly List<Contact> _allContacts   = new();
        private static readonly List<Contact> _bulletContacts = new();

        // Tracks same-owner bullet pairs that were overlapping when friendly-fire immunity
        // expired (spawn overlaps). Value = velocity cap for that interaction, set to the
        // faster bullet's speed at the moment the overlap was first detected.
        // These pairs get normal elastic collision physics but with a speed ceiling so the
        // depenetration impulse can't launch bullets faster than either was already moving.
        private static readonly Dictionary<long, float> _spawnOverlapCaps = new();
        private static readonly HashSet<long> _activeSpawnOverlaps = new();
        private static readonly List<long> _staleKeys = new();

        private static long BulletPairKey(int idA, int idB)
        {
            int lo = Math.Min(idA, idB);
            int hi = Math.Max(idA, idB);
            return ((long)lo << 32) | (uint)hi;
        }

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
            _agents.Clear();
            foreach (var go in gameObjects)
                if (go is Agent a) _agents.Add(a);

            _allContacts.Clear();

            // ── Steps 3 & 4: Swept detection + damage ───────────────────────────
            if (_agents.Count > 0)
            {
                foreach (var bullet in bullets)
                {
                    if (bullet == null || bullet.IsDying || bullet.CurrentPenetrationHP <= 0f) continue;

                    float br = bullet.Shape != null ? bullet.Shape.Width * 0.5f : BulletRadius;

                    Vector2 sweepDelta = bullet.Position - bullet.PreviousPosition;
                    float   a          = Vector2.Dot(sweepDelta, sweepDelta);

                    _bulletContacts.Clear();

                    foreach (var enemy in _agents)
                    {
                        if (enemy == null) continue;

                        // Skip owner during immunity period so the bullet can leave the body.
                        if (bullet.IsOwnerImmune && enemy.ID == bullet.OwnerID) continue;

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
                        _bulletContacts.Add(contact);
                        _allContacts.Add(contact);
                    }

                    if (_bulletContacts.Count == 0) continue;

                    _bulletContacts.Sort(static (x, y) => x.TEnter.CompareTo(y.TEnter));

                    foreach (var contact in _bulletContacts)
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
                            float totalDealt = contact.Enemy.ApplyDamage(dmg, bullet.OwnerID);
                            contact.Enemy.TriggerHitFlash();
                            bullet.TriggerHitFlash();
                            DamageNumberManager.Notify(contact.Enemy.ID, contact.Enemy.Position, totalDealt, sourceId: bullet.SourceID, isNewHit: true);
                            contact.Enemy.DeathImpulse = bullet.Velocity;
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
            // Position correction uses BulletKnockback as the bullet's effective push
            // mass so bullets are very light in collisions by default; only high-
            // penetration bullets meaningfully shove enemies.  The bullet itself is
            // still corrected using its full mass (so enemies push bullets away).
            foreach (var contact in _allContacts)
            {
                Bullet bullet = contact.Bullet;
                Agent  enemy  = contact.Enemy;
                if (bullet == null || enemy == null) continue;
                if (bullet.IsOwnerImmune && enemy.ID == bullet.OwnerID) continue;

                float   br    = bullet.Shape != null ? bullet.Shape.Width * 0.5f : BulletRadius;
                float   er    = enemy.Shape  != null ? enemy.Shape.Width  * 0.5f : EnemyRadius;
                float   sumR  = br + er;
                Vector2 diff  = bullet.Position - enemy.Position;
                float   dist  = diff.Length();
                float overlap = sumR - dist;
                if (overlap <= 0f) continue;

                Vector2 normal = dist > 1e-6f ? diff / dist : Vector2.UnitX;

                // BulletKnockback determines how much the bullet pushes the enemy;
                // the bullet's real mass still governs how much the enemy pushes the bullet.
                float mPush     = MathF.Max(bullet.BulletKnockback, 0.0001f);
                float mBullet   = MathF.Max(bullet.Mass, 0.0001f);
                float mEnemy    = MathF.Max(enemy.Mass,  0.0001f);

                // Position correction: bullet pushed by enemy using real mass ratio,
                // enemy pushed by bullet using knockback as effective mass.
                float pushTotal = mPush + mEnemy;
                bullet.Position += normal * (overlap * mEnemy / (mBullet + mEnemy));
                enemy.Position  -= normal * (overlap * mPush  / pushTotal);

                // Inelastic velocity drain on bullet (still uses real mass)
                float vDotN = Vector2.Dot(bullet.Velocity, normal);
                if (vDotN < 0f)
                    bullet.Velocity -= (vDotN * mEnemy / (mBullet + mEnemy)) * normal;
                ClampBulletSpeed(bullet);
            }

            // ── Step 7: Bullet-bullet elastic collision ───────────────────────────
            // Bullets are not registered in GameObjects so no other system handles
            // this. Process each unique pair once (i < j).
            _activeSpawnOverlaps.Clear();
            for (int i = 0; i < bullets.Count; i++)
            {
                Bullet ba = bullets[i];
                if (ba == null || ba.IsDying || IsBulletExpired(ba)) continue;

                float ra = ba.Shape != null ? ba.Shape.Width * 0.5f : BulletRadius;

                for (int j = i + 1; j < bullets.Count; j++)
                {
                    Bullet bb = bullets[j];
                    if (bb == null || bb.IsDying || IsBulletExpired(bb)) continue;

                    // Skip collision between same-owner bullets while either is still immune.
                    if (ba.OwnerID == bb.OwnerID && (ba.IsOwnerImmune || bb.IsOwnerImmune)) continue;

                    float   rb   = bb.Shape != null ? bb.Shape.Width * 0.5f : BulletRadius;
                    float   sumR = ra + rb;
                    Vector2 diff = ba.Position - bb.Position;
                    float   dist = diff.Length();
                    if (dist >= sumR) continue;

                    float overlap = sumR - dist;
                    Vector2 normal = dist > 1e-6f ? diff / dist : Vector2.UnitX;

                    // Same-owner bullets that overlap once immunity expires were spawned
                    // on top of each other. Instead of killing the newer one, track the
                    // pair with a velocity cap so the depenetration impulse can't launch
                    // them faster than either was already moving.
                    bool isSpawnOverlap = false;
                    float spawnCap = 0f;
                    if (ba.OwnerID == bb.OwnerID)
                    {
                        long pairKey = BulletPairKey(ba.ID, bb.ID);
                        if (!_spawnOverlapCaps.TryGetValue(pairKey, out spawnCap))
                        {
                            // First detection — cap = speed of whichever bullet is faster right now.
                            spawnCap = MathF.Max(ba.Velocity.Length(), bb.Velocity.Length());
                            _spawnOverlapCaps[pairKey] = spawnCap;
                        }
                        _activeSpawnOverlaps.Add(pairKey);
                        isSpawnOverlap = true;
                    }

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

                    // Spawn-overlap pairs use the recorded cap; normal collisions use MaxSpeed.
                    if (isSpawnOverlap)
                    {
                        ClampToSpeed(ba, spawnCap);
                        ClampToSpeed(bb, spawnCap);
                    }
                    else
                    {
                        ClampBulletSpeed(ba);
                        ClampBulletSpeed(bb);
                    }

                    // Bullet-vs-bullet damage: each bullet deals its BulletDamage to the other's health.
                    float dmgToB = ba.BulletDamage;
                    float dmgToA = bb.BulletDamage;
                    if (dmgToB > 0f)
                    {
                        float dealt = MathF.Min(dmgToB, bb.CurrentPenetrationHP);
                        bb.CurrentPenetrationHP -= dealt;
                        bb.TriggerHitFlash();
                        DamageNumberManager.Notify(bb.ID, bb.Position, dealt, sourceId: ba.SourceID, isNewHit: true, isBulletDamage: true);
                    }
                    if (dmgToA > 0f)
                    {
                        float dealt = MathF.Min(dmgToA, ba.CurrentPenetrationHP);
                        ba.CurrentPenetrationHP -= dealt;
                        ba.TriggerHitFlash();
                        DamageNumberManager.Notify(ba.ID, ba.Position, dealt, sourceId: bb.SourceID, isNewHit: true, isBulletDamage: true);
                    }
                }
            }

            // Prune spawn-overlap entries for pairs that have separated or whose bullets died.
            if (_spawnOverlapCaps.Count > 0)
            {
                _staleKeys.Clear();
                foreach (long key in _spawnOverlapCaps.Keys)
                {
                    if (!_activeSpawnOverlaps.Contains(key))
                        _staleKeys.Add(key);
                }
                foreach (long key in _staleKeys)
                    _spawnOverlapCaps.Remove(key);
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

        private static void ClampToSpeed(Bullet bullet, float cap)
        {
            if (cap <= 0f) return;
            float spdSq = bullet.Velocity.LengthSquared();
            if (spdSq > cap * cap)
                bullet.Velocity = Vector2.Normalize(bullet.Velocity) * cap;
        }

        private static bool IsBulletExpired(Bullet bullet)
        {
            return bullet.CurrentPenetrationHP <= 0f
                || bullet.LifetimeElapsed >= bullet.MaxLifespan;
        }
    }
}
