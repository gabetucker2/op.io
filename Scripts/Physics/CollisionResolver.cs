using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class CollisionResolver
    {
        private static HashSet<long> _prevContacts = new();
        private static HashSet<long> _currContacts = new();

        private static float? _cachedKnockbackMassScale;
        public static float KnockbackMassScale
        {
            get
            {
                if (!_cachedKnockbackMassScale.HasValue)
                    _cachedKnockbackMassScale = DatabaseFetch.GetValue<float>(
                        "PhysicsSettings", "Value", "SettingKey", "KnockbackMassScale");
                return _cachedKnockbackMassScale.Value;
            }
        }

        private static float? _cachedBodyRadiusScalar;
        public static float BodyRadiusScalar
        {
            get
            {
                if (!_cachedBodyRadiusScalar.HasValue)
                    _cachedBodyRadiusScalar = DatabaseFetch.GetValue<float>(
                        "PhysicsSettings", "Value", "SettingKey", "BodyRadiusScalar");
                return _cachedBodyRadiusScalar.Value;
            }
        }

        private static long ContactKey(int idA, int idB)
        {
            int lo = idA < idB ? idA : idB;
            int hi = idA < idB ? idB : idA;
            return ((long)lo << 32) | (uint)hi;
        }

        public static void ResolveCollisions(List<GameObject> gameObjects)
        {
            if (gameObjects == null)
            {
                DebugLogger.PrintError("CollisionResolver.ResolveCollisions failed: GameObjects list is null.");
                return;
            }

            _currContacts.Clear();

            // Loop through all game objects to resolve collisions
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var objA = gameObjects[i];

                for (int j = i + 1; j < gameObjects.Count; j++)
                {
                    var objB = gameObjects[j];

                    // Skip if either object is not collidable
                    if (!objA.IsCollidable || !objB.IsCollidable)
                        continue;

                    // Two static objects can never move, so they can't begin overlapping after init.
                    // Skip the expensive collision test entirely for static-static pairs.
                    if (!objA.DynamicPhysics && !objB.DynamicPhysics)
                        continue;

                    // Check if there is a collision between objA and objB
                    if (CollisionManager.TryGetCollision(objA, objB, out Vector2 mtv))
                    {
                        long key          = ContactKey(objA.ID, objB.ID);
                        bool isNewContact = _currContacts.Add(key) && !_prevContacts.Contains(key);

                        // If at least one object is dynamic, apply physics resolution
                        if (objA.DynamicPhysics || objB.DynamicPhysics)
                            HandlePhysicsCollision(objA, objB, mtv, isNewContact);

                        // Apply agent body collision damage to destructible non-agent objects.
                        // Guard health > 0 so already-dead objects don't continue accumulating damage
                        // before the death scan removes them next frame.
                        Agent agentA = objA as Agent;
                        Agent agentB = objB as Agent;

                        if (agentA != null && !(objB is Agent))
                        {
                            // Agent A damages non-agent B (only if destructible). Agent takes
                            // reciprocal self-damage only when B itself deals collision damage.
                            float dmg = agentA.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                            if (objB.IsDestructible && objB.CurrentHealth > 0f)
                            {
                                float dealtToB = objB.ApplyDamage(dmg, agentA.ID);
                                objB.TriggerHitFlash();
                                DamageNumberManager.Notify(objB.ID, objB.Position, dealtToB, sourceId: agentA.ID, isNewHit: isNewContact);
                                objB.DeathImpulse = (agentA.Position - agentA.PreviousPosition) / Core.DELTATIME;
                            }
                            if (dmg > 0f && objB.BodyCollisionDamage > 0f)
                            {
                                float dealtToA = agentA.ApplyDamage(dmg, objB.ID);
                                agentA.TriggerHitFlash();
                                DamageNumberManager.Notify(agentA.ID, agentA.Position, dealtToA, sourceId: objB.ID, isNewHit: isNewContact);
                                agentA.DeathImpulse = (objB.Position - objB.PreviousPosition) / Core.DELTATIME;
                            }
                        }
                        else if (agentB != null && !(objA is Agent))
                        {
                            // Agent B damages non-agent A (only if destructible). Agent takes
                            // reciprocal self-damage only when A itself deals collision damage.
                            float dmg = agentB.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                            if (objA.IsDestructible && objA.CurrentHealth > 0f)
                            {
                                float dealtToA = objA.ApplyDamage(dmg, agentB.ID);
                                objA.TriggerHitFlash();
                                DamageNumberManager.Notify(objA.ID, objA.Position, dealtToA, sourceId: agentB.ID, isNewHit: isNewContact);
                                objA.DeathImpulse = (agentB.Position - agentB.PreviousPosition) / Core.DELTATIME;
                            }
                            if (dmg > 0f && objA.BodyCollisionDamage > 0f)
                            {
                                float dealtToB = agentB.ApplyDamage(dmg, objA.ID);
                                agentB.TriggerHitFlash();
                                DamageNumberManager.Notify(agentB.ID, agentB.Position, dealtToB, sourceId: objA.ID, isNewHit: isNewContact);
                                agentB.DeathImpulse = (objA.Position - objA.PreviousPosition) / Core.DELTATIME;
                            }
                        }
                        else if (agentA != null && agentB != null)
                        {
                            // Agent vs Agent: each deals their own BodyCollisionDamage to the other.
                            float dmgToA = agentB.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                            float dmgToB = agentA.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;

                            if (dmgToA > 0f)
                            {
                                float dealtToA = agentA.ApplyDamage(dmgToA, agentB.ID);
                                agentA.TriggerHitFlash();
                                DamageNumberManager.Notify(agentA.ID, agentA.Position, dealtToA, sourceId: agentB.ID, isNewHit: isNewContact);
                                agentA.DeathImpulse = (agentB.Position - agentB.PreviousPosition) / Core.DELTATIME;
                            }
                            if (dmgToB > 0f)
                            {
                                float dealtToB = agentB.ApplyDamage(dmgToB, agentA.ID);
                                agentB.TriggerHitFlash();
                                DamageNumberManager.Notify(agentB.ID, agentB.Position, dealtToB, sourceId: agentA.ID, isNewHit: isNewContact);
                                agentB.DeathImpulse = (agentA.Position - agentA.PreviousPosition) / Core.DELTATIME;
                            }
                        }
                        else if (agentA == null && agentB == null)
                        {
                            // Non-agent vs non-agent (e.g. two farm objects): mutual collision damage.
                            float dmgA = objA.BodyCollisionDamage * Core.DELTATIME;
                            float dmgB = objB.BodyCollisionDamage * Core.DELTATIME;

                            if (dmgA > 0f && objB.IsDestructible && objB.CurrentHealth > 0f)
                            {
                                float dealtToB = objB.ApplyDamage(dmgA, objA.ID);
                                objB.TriggerHitFlash();
                                DamageNumberManager.Notify(objB.ID, objB.Position, dealtToB, sourceId: objA.ID, isNewHit: isNewContact);
                                objB.DeathImpulse = (objA.Position - objA.PreviousPosition) / Core.DELTATIME;
                            }
                            if (dmgB > 0f && objA.IsDestructible && objA.CurrentHealth > 0f)
                            {
                                float dealtToA = objA.ApplyDamage(dmgB, objB.ID);
                                objA.TriggerHitFlash();
                                DamageNumberManager.Notify(objA.ID, objA.Position, dealtToA, sourceId: objB.ID, isNewHit: isNewContact);
                                objA.DeathImpulse = (objB.Position - objB.PreviousPosition) / Core.DELTATIME;
                            }
                        }
                    }
                }
            }

            (_prevContacts, _currContacts) = (_currContacts, _prevContacts);
        }

        private static void HandlePhysicsCollision(GameObject objA, GameObject objB, Vector2 minimumTranslationVector, bool isNewContact)
        {
            if (minimumTranslationVector == Vector2.Zero)
                return;

            bool aStatic = !objA.DynamicPhysics;
            bool bStatic = !objB.DynamicPhysics;

            float mA = aStatic ? float.PositiveInfinity : Math.Max(objA.Mass, 0.0001f);
            float mB = bStatic ? float.PositiveInfinity : Math.Max(objB.Mass, 0.0001f);

            // Collision normal: points from A toward B.
            // Position correction pushes A in -n and B in +n to separate them.
            Vector2 n = Vector2.Normalize(minimumTranslationVector);

            // === Position separation ===
            if (!aStatic && !bStatic)
            {
                float totalMass = mA + mB;
                objA.Position -= minimumTranslationVector * (mB / totalMass);
                objB.Position += minimumTranslationVector * (mA / totalMass);
            }
            else if (aStatic)
                objB.Position += minimumTranslationVector;
            else if (bStatic)
                objA.Position -= minimumTranslationVector;
            else
            {
                DebugLogger.PrintWarning($"Two static object collision: ID={objA.ID} and ID={objB.ID}");
                return;
            }

            float invMassA   = aStatic ? 0f : 1f / mA;
            float invMassB   = bStatic ? 0f : 1f / mB;
            float invMassSum = invMassA + invMassB;
            if (invMassSum < 1e-6f) return;

            float scale = KnockbackMassScale;
            float kA    = mA * scale;
            float kB    = mB * scale;

            // === Velocity impulse (applied once per contact to prevent runaway accumulation) ===
            // On sustained contact, position correction keeps objects apart each frame;
            // the velocity impulse fires only on the first frame of contact so objects
            // receive a clean "push" rather than accelerating every frame.
            if (isNewContact)
            {
                // Total frame velocity from position delta: captures agent input movement
                // plus any prior-frame PhysicsVelocity, giving true approach speed.
                // PreviousPosition was snapshotted in GameUpdater before all movement this frame.
                Vector2 vA = aStatic ? Vector2.Zero
                                     : (objA.Position - objA.PreviousPosition) / Core.DELTATIME;
                Vector2 vB = bStatic ? Vector2.Zero
                                     : (objB.Position - objB.PreviousPosition) / Core.DELTATIME;

                // Relative approach velocity along the collision normal (positive = A approaching B).
                float vRelN = Vector2.Dot(vA - vB, n);

                // e is effectively always 1 for any normal mass — collisions are fully elastic.
                float e = Math.Min((kA + kB) * 0.5f, 1f);

                // Standard velocity-based impulse — only applies when objects are approaching.
                if (vRelN > 0f)
                {
                    float j = (1f + e) * vRelN / invMassSum;
                    if (!aStatic) objA.PhysicsVelocity -= j * invMassA * n;
                    if (!bStatic) objB.PhysicsVelocity += j * invMassB * n;
                }
            }

            // === Knockback impulse (applied every contact frame) ===
            // Fires on every frame of sustained contact so knockback accumulates
            // throughout the collision rather than only on the first contact frame.
            // Exclude static objects from kEff: their mass is PositiveInfinity and would
            // produce NaN/Infinity velocity, corrupting positions and killing the player.
            float kEff = (aStatic ? 0f : kA) + (bStatic ? 0f : kB);
            if (!aStatic && kEff > 0f) objA.PhysicsVelocity -= kEff * invMassA * n;
            if (!bStatic && kEff > 0f) objB.PhysicsVelocity += kEff * invMassB * n;
        }
    }
}
