using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class CollisionResolver
    {
        private static HashSet<long> _prevContacts = new();
        private static HashSet<long> _currContacts = new();

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

                    // Check if there is a collision between objA and objB
                    if (CollisionManager.TryGetCollision(objA, objB, out Vector2 mtv))
                    {
                        // If neither object is static, apply physics resolution
                        if (!(objA.StaticPhysics && objB.StaticPhysics))
                            HandlePhysicsCollision(objA, objB, mtv);

                        // Apply agent body collision damage to destructible non-agent objects.
                        // Guard health > 0 so already-dead objects don't continue accumulating damage
                        // before the death scan removes them next frame.
                        Agent agentA = objA as Agent;
                        Agent agentB = objB as Agent;

                        long key          = ContactKey(objA.ID, objB.ID);
                        bool isNewContact = _currContacts.Add(key) && !_prevContacts.Contains(key);

                        if (agentA != null && !(objB is Agent))
                        {
                            // Agent A damages non-agent B, and takes reciprocal collision damage.
                            if (objB.IsDestructible && objB.CurrentHealth > 0f)
                            {
                                float dmg = agentA.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                                objB.CurrentHealth -= dmg;
                                objB.TriggerHitFlash();
                                objB.LastDamagedByAgentID = agentA.ID;
                                agentA.CurrentHealth -= dmg;
                                agentA.TriggerHitFlash();
                                DamageNumberManager.Notify(objB.ID,   objB.Position,   dmg, sourceId: agentA.ID, isNewHit: isNewContact);
                                DamageNumberManager.Notify(agentA.ID, agentA.Position, dmg, sourceId: objB.ID,   isNewHit: isNewContact);
                            }
                        }
                        else if (agentB != null && !(objA is Agent))
                        {
                            // Agent B damages non-agent A, and takes reciprocal collision damage.
                            if (objA.IsDestructible && objA.CurrentHealth > 0f)
                            {
                                float dmg = agentB.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                                objA.CurrentHealth -= dmg;
                                objA.TriggerHitFlash();
                                objA.LastDamagedByAgentID = agentB.ID;
                                agentB.CurrentHealth -= dmg;
                                agentB.TriggerHitFlash();
                                DamageNumberManager.Notify(objA.ID,   objA.Position,   dmg, sourceId: agentB.ID, isNewHit: isNewContact);
                                DamageNumberManager.Notify(agentB.ID, agentB.Position, dmg, sourceId: objA.ID,   isNewHit: isNewContact);
                            }
                        }
                        else if (agentA != null && agentB != null)
                        {
                            // Agent vs Agent: each deals their own BodyCollisionDamage to the other.
                            float dmgToA = agentB.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                            float dmgToB = agentA.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;

                            if (dmgToA > 0f)
                            {
                                agentA.CurrentHealth -= dmgToA;
                                agentA.TriggerHitFlash();
                                agentA.LastDamagedByAgentID = agentB.ID;
                                DamageNumberManager.Notify(agentA.ID, agentA.Position, dmgToA, sourceId: agentB.ID, isNewHit: isNewContact);
                            }
                            if (dmgToB > 0f)
                            {
                                agentB.CurrentHealth -= dmgToB;
                                agentB.TriggerHitFlash();
                                agentB.LastDamagedByAgentID = agentA.ID;
                                DamageNumberManager.Notify(agentB.ID, agentB.Position, dmgToB, sourceId: agentA.ID, isNewHit: isNewContact);
                            }
                        }
                    }
                }
            }

            (_prevContacts, _currContacts) = (_currContacts, _prevContacts);
        }

        private static void HandlePhysicsCollision(GameObject objA, GameObject objB, Vector2 minimumTranslationVector)
        {
            if (minimumTranslationVector == Vector2.Zero)
                return;

            bool objAStatic = objA.StaticPhysics;
            bool objBStatic = objB.StaticPhysics;

            // Calculate mass for each object
            float massA = objAStatic ? float.PositiveInfinity : Math.Max(objA.Mass, 0.0001f);
            float massB = objBStatic ? float.PositiveInfinity : Math.Max(objB.Mass, 0.0001f);

            // If both objects are dynamic
            if (!objAStatic && !objBStatic)
            {
                // Both objects move based on their relative masses
                float totalMass = massA + massB;
                Vector2 moveA = minimumTranslationVector * (massB / totalMass);
                Vector2 moveB = minimumTranslationVector * (massA / totalMass);

                objA.Position -= moveA;
                objB.Position += moveB;
            }
            else if (objAStatic && !objBStatic)
            {
                objB.Position += minimumTranslationVector;
            }
            else if (!objAStatic && objBStatic)
            {
                objA.Position -= minimumTranslationVector;
            }
            else
            {
                DebugLogger.PrintWarning($"Two static object collision: ID={objA.ID} and ID={objB.ID}");
            }
        }
    }
}
