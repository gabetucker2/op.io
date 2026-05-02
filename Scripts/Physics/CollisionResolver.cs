using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class CollisionResolver
    {
        private const float BroadPhaseCellSizeWorldUnits = 256f;
        private const float MinCollisionVectorLengthSq = 1e-8f;
        private const float DefaultCollisionBounceMomentumTransfer = 0.35f;
        private static HashSet<long> _prevContacts = new();
        private static HashSet<long> _currContacts = new();
        private static readonly List<GameObject> _dynamicColliders = new();
        private static readonly Dictionary<long, List<GameObject>> _spatialHashCells = new();
        private static readonly Stack<List<GameObject>> _availableCellObjectLists = new();
        private static readonly List<long> _activeCellKeys = new();
        private static readonly HashSet<long> _evaluatedPairKeys = new();

        public static int BroadPhaseActiveCollidableCount { get; private set; }
        public static int BroadPhaseCandidatePairCount { get; private set; }
        public static int StartupOverlapResolvedPairCount { get; private set; }
        public static int StartupOverlapIterationCount { get; private set; }

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

        private static float? _cachedCollisionBounceMomentumTransfer;
        public static float CollisionBounceMomentumTransfer
        {
            get
            {
                if (!_cachedCollisionBounceMomentumTransfer.HasValue)
                {
                    float configured = DatabaseFetch.GetSetting(
                        "PhysicsSettings",
                        "Value",
                        "SettingKey",
                        "CollisionBounceMomentumTransfer",
                        DefaultCollisionBounceMomentumTransfer);
                    _cachedCollisionBounceMomentumTransfer = MathHelper.Clamp(configured, 0f, 2f);
                }

                return _cachedCollisionBounceMomentumTransfer.Value;
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
            BuildBroadPhase(gameObjects);

            for (int i = 0; i < _dynamicColliders.Count; i++)
            {
                GameObject objA = _dynamicColliders[i];
                if (!TryGetOccupiedCellRange(objA, out int minCellX, out int maxCellX, out int minCellY, out int maxCellY))
                {
                    continue;
                }

                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    {
                        long cellKey = ComposeCellKey(cellX, cellY);
                        if (!_spatialHashCells.TryGetValue(cellKey, out List<GameObject> cellObjects))
                        {
                            continue;
                        }

                        for (int objectIndex = 0; objectIndex < cellObjects.Count; objectIndex++)
                        {
                            GameObject objB = cellObjects[objectIndex];
                            if (objB == null || objB == objA || !objB.IsCollidable)
                            {
                                continue;
                            }

                            if (!objA.DynamicPhysics && !objB.DynamicPhysics)
                            {
                                continue;
                            }

                            long pairKey = ContactKey(objA.ID, objB.ID);
                            if (!_evaluatedPairKeys.Add(pairKey))
                            {
                                continue;
                            }

                            if (!BroadPhaseOverlaps(objA, objB))
                            {
                                continue;
                            }

                            BroadPhaseCandidatePairCount++;
                            if (CollisionManager.TryGetCollision(objA, objB, out Vector2 mtv))
                            {
                                bool isNewContact = _currContacts.Add(pairKey) && !_prevContacts.Contains(pairKey);

                                if (objA.DynamicPhysics || objB.DynamicPhysics)
                                {
                                    HandlePhysicsCollision(objA, objB, mtv, isNewContact);
                                }

                                ApplyCollisionDamage(objA, objB, isNewContact);
                            }
                        }
                    }
                }
            }

            (_prevContacts, _currContacts) = (_currContacts, _prevContacts);
        }

        public static void ResolveStartupOverlaps(List<GameObject> gameObjects, int maxIterations = 32)
        {
            StartupOverlapResolvedPairCount = 0;
            StartupOverlapIterationCount = 0;

            if (gameObjects == null || gameObjects.Count == 0 || maxIterations <= 0)
            {
                return;
            }

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool separatedAnyPair = false;
                BuildBroadPhase(gameObjects);

                for (int i = 0; i < _dynamicColliders.Count; i++)
                {
                    GameObject objA = _dynamicColliders[i];
                    if (!TryGetOccupiedCellRange(objA, out int minCellX, out int maxCellX, out int minCellY, out int maxCellY))
                    {
                        continue;
                    }

                    for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                    {
                        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                        {
                            long cellKey = ComposeCellKey(cellX, cellY);
                            if (!_spatialHashCells.TryGetValue(cellKey, out List<GameObject> cellObjects))
                            {
                                continue;
                            }

                            for (int objectIndex = 0; objectIndex < cellObjects.Count; objectIndex++)
                            {
                                GameObject objB = cellObjects[objectIndex];
                                if (objB == null || objB == objA || !objB.IsCollidable)
                                {
                                    continue;
                                }

                                long pairKey = ContactKey(objA.ID, objB.ID);
                                if (!_evaluatedPairKeys.Add(pairKey))
                                {
                                    continue;
                                }

                                if (!BroadPhaseOverlaps(objA, objB))
                                {
                                    continue;
                                }

                                if (!CollisionManager.TryGetCollision(objA, objB, out Vector2 mtv))
                                {
                                    continue;
                                }

                                if (!SeparateOverlapWithoutImpulse(objA, objB, mtv))
                                {
                                    continue;
                                }

                                separatedAnyPair = true;
                                StartupOverlapResolvedPairCount++;
                            }
                        }
                    }
                }

                if (!separatedAnyPair)
                {
                    return;
                }

                StartupOverlapIterationCount = iteration + 1;
            }

            StartupOverlapIterationCount = maxIterations;
        }

        private static void BuildBroadPhase(List<GameObject> gameObjects)
        {
            _dynamicColliders.Clear();
            _evaluatedPairKeys.Clear();
            BroadPhaseActiveCollidableCount = 0;
            BroadPhaseCandidatePairCount = 0;

            for (int i = 0; i < _activeCellKeys.Count; i++)
            {
                long cellKey = _activeCellKeys[i];
                if (!_spatialHashCells.TryGetValue(cellKey, out List<GameObject> cellObjects))
                {
                    continue;
                }

                cellObjects.Clear();
                _spatialHashCells.Remove(cellKey);
                _availableCellObjectLists.Push(cellObjects);
            }

            _activeCellKeys.Clear();

            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject == null || !gameObject.IsCollidable || gameObject.Shape == null)
                {
                    continue;
                }

                BroadPhaseActiveCollidableCount++;
                if (gameObject.DynamicPhysics)
                {
                    _dynamicColliders.Add(gameObject);
                }

                if (!TryGetOccupiedCellRange(gameObject, out int minCellX, out int maxCellX, out int minCellY, out int maxCellY))
                {
                    continue;
                }

                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    {
                        long cellKey = ComposeCellKey(cellX, cellY);
                        if (!_spatialHashCells.TryGetValue(cellKey, out List<GameObject> cellObjects))
                        {
                            cellObjects = _availableCellObjectLists.Count > 0
                                ? _availableCellObjectLists.Pop()
                                : new List<GameObject>();
                            _spatialHashCells[cellKey] = cellObjects;
                            _activeCellKeys.Add(cellKey);
                        }

                        cellObjects.Add(gameObject);
                    }
                }
            }
        }

        private static bool TryGetOccupiedCellRange(GameObject gameObject, out int minCellX, out int maxCellX, out int minCellY, out int maxCellY)
        {
            minCellX = 0;
            maxCellX = 0;
            minCellY = 0;
            maxCellY = 0;

            if (gameObject == null)
            {
                return false;
            }

            SanitizeDynamicPhysicsState(gameObject);
            if (!IsFiniteVector(gameObject.Position))
            {
                return false;
            }

            float radius = MathF.Max(gameObject?.BoundingRadius ?? 0f, 2f);
            if (!float.IsFinite(radius))
            {
                return false;
            }

            minCellX = (int)MathF.Floor((gameObject.Position.X - radius) / BroadPhaseCellSizeWorldUnits);
            maxCellX = (int)MathF.Floor((gameObject.Position.X + radius) / BroadPhaseCellSizeWorldUnits);
            minCellY = (int)MathF.Floor((gameObject.Position.Y - radius) / BroadPhaseCellSizeWorldUnits);
            maxCellY = (int)MathF.Floor((gameObject.Position.Y + radius) / BroadPhaseCellSizeWorldUnits);
            return true;
        }

        private static long ComposeCellKey(int cellX, int cellY)
        {
            return ((long)cellX << 32) | (uint)cellY;
        }

        private static bool BroadPhaseOverlaps(GameObject objA, GameObject objB)
        {
            float radiusA = MathF.Max(objA.BoundingRadius, 2f);
            float radiusB = MathF.Max(objB.BoundingRadius, 2f);
            float combinedRadius = radiusA + radiusB;
            return Vector2.DistanceSquared(objA.Position, objB.Position) <= combinedRadius * combinedRadius;
        }

        private static void ApplyCollisionDamage(GameObject objA, GameObject objB, bool isNewContact)
        {
            Agent agentA = objA as Agent;
            Agent agentB = objB as Agent;

            if (agentA != null && !(objB is Agent))
            {
                float dmgToB = agentA.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                if (objB.IsDestructible && objB.CurrentHealth > 0f)
                {
                    float dealtToB = objB.ApplyDamage(dmgToB, agentA.ID);
                    objB.TriggerHitFlash();
                    DamageNumberManager.Notify(objB.ID, objB.Position, dealtToB, sourceId: agentA.ID, isNewHit: isNewContact);
                    objB.DeathImpulse = (agentA.Position - agentA.PreviousPosition) / Core.DELTATIME;
                }

                float dmgToA = objB.BodyCollisionDamage * Core.DELTATIME;
                if (dmgToA > 0f && objB.BodyCollisionDamage > 0f)
                {
                    float dealtToA = agentA.ApplyDamage(dmgToA, objB.ID);
                    agentA.TriggerHitFlash();
                    DamageNumberManager.Notify(agentA.ID, agentA.Position, dealtToA, sourceId: objB.ID, isNewHit: isNewContact);
                    agentA.DeathImpulse = (objB.Position - objB.PreviousPosition) / Core.DELTATIME;
                }
            }
            else if (agentB != null && !(objA is Agent))
            {
                float dmgToA = agentB.BodyAttributes.BodyCollisionDamage * Core.DELTATIME;
                if (objA.IsDestructible && objA.CurrentHealth > 0f)
                {
                    float dealtToA = objA.ApplyDamage(dmgToA, agentB.ID);
                    objA.TriggerHitFlash();
                    DamageNumberManager.Notify(objA.ID, objA.Position, dealtToA, sourceId: agentB.ID, isNewHit: isNewContact);
                    objA.DeathImpulse = (agentB.Position - agentB.PreviousPosition) / Core.DELTATIME;
                }

                float dmgToB = objA.BodyCollisionDamage * Core.DELTATIME;
                if (dmgToB > 0f && objA.BodyCollisionDamage > 0f)
                {
                    float dealtToB = agentB.ApplyDamage(dmgToB, objA.ID);
                    agentB.TriggerHitFlash();
                    DamageNumberManager.Notify(agentB.ID, agentB.Position, dealtToB, sourceId: objA.ID, isNewHit: isNewContact);
                    agentB.DeathImpulse = (objA.Position - objA.PreviousPosition) / Core.DELTATIME;
                }
            }
            else if (agentA != null && agentB != null)
            {
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

        private static void HandlePhysicsCollision(GameObject objA, GameObject objB, Vector2 minimumTranslationVector, bool isNewContact)
        {
            if (!TryResolveCollisionVector(minimumTranslationVector, out Vector2 n, out float overlap))
                return;

            bool aStatic = !objA.DynamicPhysics;
            bool bStatic = !objB.DynamicPhysics;

            float mA = aStatic ? float.PositiveInfinity : Math.Max(objA.Mass, 0.0001f);
            float mB = bStatic ? float.PositiveInfinity : Math.Max(objB.Mass, 0.0001f);
            Vector2 preResolveVelocityA = ComputeFrameVelocity(objA, aStatic);
            Vector2 preResolveVelocityB = ComputeFrameVelocity(objB, bStatic);
            Vector2 separationVector = n * overlap;

            // === Position separation ===
            if (!aStatic && !bStatic)
            {
                float totalMass = mA + mB;
                objA.Position -= separationVector * (mB / totalMass);
                objB.Position += separationVector * (mA / totalMass);
                RemoveInwardAgentMovementVelocity(objA, -n);
                RemoveInwardAgentMovementVelocity(objB, n);
            }
            else if (aStatic)
            {
                objB.Position += separationVector;
                RemoveInwardAgentMovementVelocity(objB, n);
            }
            else if (bStatic)
            {
                objA.Position -= separationVector;
                RemoveInwardAgentMovementVelocity(objA, -n);
            }
            else
            {
                DebugLogger.PrintWarning($"Two static object collision: ID={objA.ID} and ID={objB.ID}");
                return;
            }

            float invMassA   = aStatic ? 0f : 1f / mA;
            float invMassB   = bStatic ? 0f : 1f / mB;
            float invMassSum = invMassA + invMassB;
            if (invMassSum < 1e-6f) return;

            // === Velocity impulse (applied once per contact to prevent runaway accumulation) ===
            // On sustained contact, position correction keeps objects apart each frame;
            // the velocity impulse fires only on the first frame of contact so objects
            // receive a clean "push" rather than accelerating every frame.
            if (isNewContact)
            {
                // Use the pre-depenetration frame velocities. Spawn overlaps and resting
                // contacts should not manufacture approach speed from the resolver's own
                // position correction.
                Vector2 vA = preResolveVelocityA;
                Vector2 vB = preResolveVelocityB;

                // Relative approach velocity along the collision normal (positive = A approaching B).
                float vRelN = Vector2.Dot(vA - vB, n);

                // CollisionBounceMomentumTransfer is a momentum fraction, not elastic restitution.
                float momentumTransfer = CollisionBounceMomentumTransfer;

                // Standard velocity-based impulse — only applies when objects are approaching.
                if (vRelN > 0f)
                {
                    float j = momentumTransfer * vRelN / invMassSum;
                    if (!aStatic) objA.PhysicsVelocity -= j * invMassA * n;
                    if (!bStatic) objB.PhysicsVelocity += j * invMassB * n;
                }
            }

            // Resting/startup overlaps should only depenetrate. Continuous contact knockback
            // creates energy from a static overlap and can cascade into invalid launch-state
            // physics when several non-destructible colliders spawn intersecting.
            SanitizeDynamicPhysicsState(objA);
            SanitizeDynamicPhysicsState(objB);
        }

        private static void RemoveInwardAgentMovementVelocity(GameObject gameObject, Vector2 escapeDirection)
        {
            if (gameObject is not Agent agent ||
                !gameObject.DynamicPhysics ||
                !IsFiniteVector(escapeDirection) ||
                !IsFiniteVector(agent.MovementVelocity))
            {
                return;
            }

            float escapeLengthSquared = escapeDirection.LengthSquared();
            if (!float.IsFinite(escapeLengthSquared) || escapeLengthSquared <= MinCollisionVectorLengthSq)
            {
                return;
            }

            Vector2 escapeNormal = escapeDirection / MathF.Sqrt(escapeLengthSquared);
            float outwardSpeed = Vector2.Dot(agent.MovementVelocity, escapeNormal);
            if (outwardSpeed < 0f)
            {
                agent.MovementVelocity -= escapeNormal * outwardSpeed;
                if (!IsFiniteVector(agent.MovementVelocity) || agent.MovementVelocity.LengthSquared() < 1f)
                {
                    agent.MovementVelocity = Vector2.Zero;
                }
            }
        }

        private static bool SeparateOverlapWithoutImpulse(GameObject objA, GameObject objB, Vector2 minimumTranslationVector)
        {
            if (!TryResolveCollisionVector(minimumTranslationVector, out Vector2 n, out float overlap))
            {
                return false;
            }

            bool aStatic = objA == null || !objA.DynamicPhysics;
            bool bStatic = objB == null || !objB.DynamicPhysics;
            if (aStatic && bStatic)
            {
                return false;
            }

            float mA = aStatic ? float.PositiveInfinity : Math.Max(objA.Mass, 0.0001f);
            float mB = bStatic ? float.PositiveInfinity : Math.Max(objB.Mass, 0.0001f);
            Vector2 separationVector = n * overlap;

            if (!aStatic && !bStatic)
            {
                float totalMass = mA + mB;
                objA.Position -= separationVector * (mB / totalMass);
                objB.Position += separationVector * (mA / totalMass);
            }
            else if (aStatic)
            {
                objB.Position += separationVector;
            }
            else
            {
                objA.Position -= separationVector;
            }

            SanitizePostStartupOverlapState(objA);
            SanitizePostStartupOverlapState(objB);
            return true;
        }

        private static Vector2 ComputeFrameVelocity(GameObject gameObject, bool isStatic)
        {
            if (isStatic || gameObject == null || Core.DELTATIME <= 0f)
            {
                return Vector2.Zero;
            }

            Vector2 frameVelocity = (gameObject.Position - gameObject.PreviousPosition) / Core.DELTATIME;
            return IsFiniteVector(frameVelocity) ? frameVelocity : Vector2.Zero;
        }

        private static bool TryResolveCollisionVector(Vector2 collisionVector, out Vector2 normal, out float overlap)
        {
            normal = Vector2.Zero;
            overlap = 0f;

            if (!IsFiniteVector(collisionVector))
            {
                return false;
            }

            float lengthSquared = collisionVector.LengthSquared();
            if (!float.IsFinite(lengthSquared) || lengthSquared <= MinCollisionVectorLengthSq)
            {
                return false;
            }

            overlap = MathF.Sqrt(lengthSquared);
            normal = collisionVector / overlap;
            return IsFiniteVector(normal);
        }

        private static void SanitizeDynamicPhysicsState(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.DynamicPhysics)
            {
                return;
            }

            if (!IsFiniteVector(gameObject.Position))
            {
                gameObject.Position = IsFiniteVector(gameObject.PreviousPosition)
                    ? gameObject.PreviousPosition
                    : Vector2.Zero;
            }

            if (!IsFiniteVector(gameObject.PreviousPosition))
            {
                gameObject.PreviousPosition = gameObject.Position;
            }

            if (!IsFiniteVector(gameObject.PhysicsVelocity))
            {
                gameObject.PhysicsVelocity = Vector2.Zero;
            }
        }

        private static void SanitizePostStartupOverlapState(GameObject gameObject)
        {
            SanitizeDynamicPhysicsState(gameObject);
            if (gameObject == null || !gameObject.DynamicPhysics)
            {
                return;
            }

            gameObject.PreviousPosition = gameObject.Position;
            gameObject.PhysicsVelocity = Vector2.Zero;
            if (gameObject is Agent agent)
            {
                agent.MovementVelocity = Vector2.Zero;
            }
        }

        private static bool IsFiniteVector(Vector2 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y);
        }
    }
}
