using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameUpdater
    {
        private static readonly List<GameObject> _dyingObjects = new();
        private static readonly Random _deathFadeRandom = new();

        private static float? _deathFadeOut;
        private static float DeathFadeOut =>
            _deathFadeOut ??= DatabaseFetch.GetAnimSetting("DeathFadeAnim", 0f, 0f, 0.5f).FadeOut;

        private static float? _deathFadeScaleMultiplier;
        private static float DeathFadeScaleMultiplier =>
            _deathFadeScaleMultiplier ??= DatabaseFetch.GetSetting<float>(
                "FXSettings", "Value", "SettingKey", "DeathFadeScaleMultiplier", 1.5f);

        private static float? _deathFadeSpinMinDegPerSecond;
        private static float DeathFadeSpinMinDegPerSecond =>
            _deathFadeSpinMinDegPerSecond ??= DatabaseFetch.GetSetting<float>(
                "FXSettings", "Value", "SettingKey", "DeathFadeSpinMinDegPerSecond", 90f);

        private static float? _deathFadeSpinMaxDegPerSecond;
        private static float DeathFadeSpinMaxDegPerSecond =>
            _deathFadeSpinMaxDegPerSecond ??= DatabaseFetch.GetSetting<float>(
                "FXSettings", "Value", "SettingKey", "DeathFadeSpinMaxDegPerSecond", 240f);

        public static void Update(GameTime gameTime)
        {
            try
            {
                UpdateInternal(gameTime);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"[GameUpdater] Exception in Update: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void ProcessDeaths()
        {
            var gameObjects = Core.Instance.GameObjects;

            // Collect dead objects first to avoid mutating the list while iterating.
            List<GameObject> dead = null;
            foreach (var go in gameObjects)
            {
                if (go.IsDestructible && go.CurrentHealth <= 0f && !go.IsDying)
                {
                    dead ??= new List<GameObject>();
                    dead.Add(go);
                }
            }

            if (dead == null) return;

            foreach (var go in dead)
            {
                // Destructible kills spill into XP drops (free clumps) during death fade,
                // including units. Unit drops prefer CurrentXP so unstable/death behavior
                // matches farms, regardless of what delivered the killing blow.
                float rewardXP = XPClumpManager.ResolveDropRewardXP(go);

                if (rewardXP > 0f)
                {
                    XPClumpManager.BeginDeathDrop(go, rewardXP, DeathFadeOut);
                }

                gameObjects.Remove(go);
                Core.Instance.StaticObjects?.Remove(go);

                float fadeOut = DeathFadeOut;
                if (fadeOut > 0f)
                {
                    go.IsDying = true;
                    go.DeathFadeTimer = fadeOut;
                    go.DeathFadeScale = 1f;
                    go.DeathFadeSpinVelocity = GetRandomDeathFadeSpinVelocity();
                    go.Opacity = 1f;
                    if (go.DeathImpulse != Vector2.Zero)
                        go.PhysicsVelocity = go.DeathImpulse;
                    _dyingObjects.Add(go);
                    DebugLogger.PrintGO($"Death fade started for GameObject ID={go.ID} (Name={go.Name}, Reward={go.DeathPointReward} XP, FadeOut={fadeOut}s).");
                }
                else
                {
                    XPClumpManager.CompleteDeathDrop(go);
                    go.Dispose();
                    DebugLogger.PrintGO($"Destroyed GameObject ID={go.ID} (Name={go.Name}, Reward={go.DeathPointReward} XP).");
                }
            }
        }

        private static void UpdateDyingObjects()
        {
            if (_dyingObjects.Count == 0) return;
            float fadeOut = DeathFadeOut;
            float targetScale = MathF.Max(0f, DeathFadeScaleMultiplier);
            for (int i = _dyingObjects.Count - 1; i >= 0; i--)
            {
                GameObject go = _dyingObjects[i];
                float previousTimer = go.DeathFadeTimer;
                go.Update(); // keep physics velocity applied during fade
                go.DeathFadeTimer -= Core.DELTATIME;
                float elapsedBefore = MathF.Max(0f, fadeOut - previousTimer);
                float elapsedAfter = MathF.Max(0f, fadeOut - go.DeathFadeTimer);
                XPClumpManager.UpdateDeathDrop(go, elapsedBefore, elapsedAfter);
                float remainingRatio = MathHelper.Clamp(go.DeathFadeTimer / MathF.Max(fadeOut, 0.001f), 0f, 1f);
                go.Opacity = remainingRatio;
                float fadeProgress = 1f - remainingRatio;
                go.DeathFadeScale = MathHelper.Lerp(1f, targetScale, fadeProgress);
                if (go.DeathFadeTimer <= 0f)
                {
                    _dyingObjects.RemoveAt(i);
                    XPClumpManager.CompleteDeathDrop(go);
                    go.Dispose();
                    DebugLogger.PrintGO($"Death fade complete, disposed GameObject ID={go.ID}.");
                }
            }
        }

        private static float GetRandomDeathFadeSpinVelocity()
        {
            float minDeg = MathF.Abs(DeathFadeSpinMinDegPerSecond);
            float maxDeg = MathF.Abs(DeathFadeSpinMaxDegPerSecond);
            if (maxDeg < minDeg)
            {
                (minDeg, maxDeg) = (maxDeg, minDeg);
            }

            float magnitudeDeg = minDeg;
            if (maxDeg > minDeg)
            {
                magnitudeDeg = MathHelper.Lerp(minDeg, maxDeg, (float)_deathFadeRandom.NextDouble());
            }

            if (magnitudeDeg <= 0f)
            {
                return 0f;
            }

            float sign = _deathFadeRandom.NextDouble() < 0.5d ? -1f : 1f;
            return MathHelper.ToRadians(magnitudeDeg) * sign;
        }

        private static void RegenerateStats(float dt)
        {
            float now = Core.GAMETIME;
            foreach (var go in Core.Instance.GameObjects)
            {
                if (go is Agent agent)
                {
                    Attributes_Body body = agent.BodyAttributes;

                    if (body.HealthRegen > 0f && agent.CurrentHealth > 0f && agent.CurrentHealth < agent.MaxHealth &&
                        now - agent.LastHealthDamageTime >= body.HealthRegenDelay)
                    {
                        agent.CurrentHealth = MathF.Min(agent.CurrentHealth + body.HealthRegen * dt, agent.MaxHealth);
                    }

                    if (agent.MaxShield > 0f && body.ShieldRegen > 0f && agent.CurrentShield < agent.MaxShield &&
                        now - agent.LastShieldDamageTime >= body.ShieldRegenDelay)
                    {
                        agent.CurrentShield = MathF.Min(agent.CurrentShield + body.ShieldRegen * dt, agent.MaxShield);
                    }
                }
                else if (go.IsDestructible)
                {
                    if (go.HealthRegen > 0f && go.CurrentHealth < go.MaxHealth &&
                        now - go.LastHealthDamageTime >= go.HealthRegenDelay)
                    {
                        go.CurrentHealth = MathF.Min(go.CurrentHealth + go.HealthRegen * dt, go.MaxHealth);
                    }

                    if (go.MaxShield > 0f && go.ShieldRegen > 0f && go.CurrentShield < go.MaxShield &&
                        now - go.LastShieldDamageTime >= go.ShieldRegenDelay)
                    {
                        go.CurrentShield = MathF.Min(go.CurrentShield + go.ShieldRegen * dt, go.MaxShield);
                    }
                }
            }
        }

        private static void UpdateInternal(GameTime gameTime)
        {
            Core.GAMETIME = (float)gameTime.TotalGameTime.TotalSeconds;
            Core.DELTATIME = (float)gameTime.ElapsedGameTime.TotalSeconds;

            PerformanceTracker.Update(gameTime);

            DebugHelperFunctions.DeltaTimeZeroWarning();

            // Update sensitivity-adjusted virtual cursor (must run before rotation logic)
            MouseFunctions.Tick();

            // Prime previous input snapshots so the first frame doesn't register phantom releases.
            InputTypeManager.BeginFrame();
            InputManager.TickWindowFocusState();

            // Centralized switch polling
            SwitchStateScanner.Tick();

            // Process general actions (toggles, switches, etc.)
            FrameProfiler.BeginSample("ActionHandler.Tickwise_CheckActions", "ActionHandler");
            ActionHandler.Tickwise_CheckActions();
            FrameProfiler.EndSample("ActionHandler.Tickwise_CheckActions");

            // Snapshot positions before any movement so CollisionResolver can compute approach velocity.
            foreach (var go in Core.Instance.GameObjects)
                go.PreviousPosition = go.Position;

            // Resolve which UI elements own the left click BEFORE movement is evaluated so that
            // IsAnyGuiInteracting returns the current-frame answer instead of last frame's cached state.
            BlockManager.PreUpdateInteractionStates();

            // Handle player transform — suppress when the UI is actively consuming mouse input
            // (dragging a slider, resize handle, block, etc.) so UI interactions don't move the player.
            bool uiConsumingMouse = BlockManager.IsConsumingMouseInput();

            FrameProfiler.BeginSample("Player.Movement", "GameUpdater");
            Agent player = Core.Instance.Player;
            if (player != null)
            {
                Vector2 direction = uiConsumingMouse ? Vector2.Zero : InputManager.GetMoveVector();
                float accelDelay = AttributeDerived.AccelerationDelay(player.BodyAttributes.Control);
                if (accelDelay <= 0f)
                {
                    // Instant movement — snap directly to full speed with no ramp-up.
                    player.MovementVelocity = Vector2.Zero;
                    if (direction != Vector2.Zero)
                        ActionHandler.Move(player, direction, player.Speed);
                }
                else
                {
                    // Delay-based acceleration — higher delay means slower ramp-up.
                    Vector2 targetVelocity = direction != Vector2.Zero
                        ? Vector2.Normalize(direction) * player.Speed
                        : Vector2.Zero;
                    float t = MathHelper.Clamp(Core.DELTATIME / accelDelay, 0f, 1f);
                    player.MovementVelocity += (targetVelocity - player.MovementVelocity) * t;
                    if (player.MovementVelocity.LengthSquared() < 1f)
                        player.MovementVelocity = Vector2.Zero;
                    player.Position += player.MovementVelocity * Core.DELTATIME;
                }
                if (InputManager.TryGetHoldLatchRotation(out float lockedRotation))
                {
                    player.Rotation = lockedRotation;
                }
                else if (!uiConsumingMouse)
                {
                    Vector2 cursorPos = MouseFunctions.GetMousePositionWithSensitivity();
                    Vector2 playerPos = player.Position;
                    if (Vector2.DistanceSquared(cursorPos, playerPos) > 0.25f)
                    {
                        Vector2 aimDelta = cursorPos - playerPos;
                        float targetAngle = MathF.Atan2(aimDelta.Y, aimDelta.X);
                        float rotDelay = AttributeDerived.RotationDelay(player.BodyAttributes.Control);
                        if (rotDelay <= 0f)
                        {
                            player.Rotation = targetAngle;
                        }
                        else
                        {
                            // rotDelay = seconds to turn 180°; higher = slower rotation.
                            float current = player.Rotation;
                            float diff = targetAngle - current;
                            while (diff >  MathF.PI) diff -= MathF.Tau;
                            while (diff < -MathF.PI) diff += MathF.Tau;
                            float maxDelta = MathF.PI / rotDelay * Core.DELTATIME;
                            player.Rotation = current + MathHelper.Clamp(diff, -maxDelta, maxDelta);
                        }
                    }
                }
            }
            FrameProfiler.EndSample("Player.Movement");

            // Update all GameObjects
            FrameProfiler.BeginSample("GameObjects.Update", "GameUpdater");
            foreach (var gameObject in Core.Instance.GameObjects)
            {
                gameObject.Update();
            }
            FrameProfiler.EndSample("GameObjects.Update");

            if (Core.Instance.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects exist in the scene.");
            }

            // Update all active bullets
            FrameProfiler.BeginSample("BulletManager.Update", "BulletManager");
            BulletManager.Update();
            FrameProfiler.EndSample("BulletManager.Update");

            // Resolve bullet collisions against world objects
            FrameProfiler.BeginSample("BulletCollisionResolver.ResolveCollisions", "BulletCollisionResolver");
            BulletCollisionResolver.ResolveCollisions(BulletManager.GetBullets(), Core.Instance.GameObjects);
            FrameProfiler.EndSample("BulletCollisionResolver.ResolveCollisions");

            // Resolve bullet-enemy damage, penetration, expiry, and depenetration
            FrameProfiler.BeginSample("BulletCollisionSystem.Update", "BulletCollisionSystem");
            BulletCollisionSystem.Update(Core.DELTATIME);
            FrameProfiler.EndSample("BulletCollisionSystem.Update");

            // Regenerate health and shields for agents with regen stats
            FrameProfiler.BeginSample("RegenerateStats", "GameUpdater");
            RegenerateStats(Core.DELTATIME);
            FrameProfiler.EndSample("RegenerateStats");

            // Apply physics step
            FrameProfiler.BeginSample("PhysicsManager.Update", "PhysicsManager");
            PhysicsManager.Update(Core.Instance.GameObjects);
            FrameProfiler.EndSample("PhysicsManager.Update");

            // Update unstable-clump low-health previews before death processing so
            // death handling can smoothly transition those previews into free clumps.
            FrameProfiler.BeginSample("XPClumpManager.UpdateUnstableDropPreviews", "XPClumpManager");
            XPClumpManager.UpdateUnstableDropPreviews(Core.Instance.GameObjects, Core.DELTATIME);
            FrameProfiler.EndSample("XPClumpManager.UpdateUnstableDropPreviews");

            // Remove dead destructible objects. Non-agent destructibles now spill XP drops
            // as free clumps during death fade; agent rewards still grant directly.
            // CollisionResolver no longer removes objects inline, so this is the single death authority.
            FrameProfiler.BeginSample("ProcessDeaths", "GameUpdater");
            ProcessDeaths();
            UpdateDyingObjects();
            FrameProfiler.EndSample("ProcessDeaths");

            FrameProfiler.BeginSample("XPClumpManager.Update", "XPClumpManager");
            XPClumpManager.Update(Core.DELTATIME);
            FrameProfiler.EndSample("XPClumpManager.Update");

            // Flush accumulated damage notifications and advance active damage numbers
            FrameProfiler.BeginSample("DamageNumberManager", "DamageNumberManager");
            DamageNumberManager.Flush();
            DamageNumberManager.Update(Core.DELTATIME);
            FrameProfiler.EndSample("DamageNumberManager");

            // Advance health bar fade state
            FrameProfiler.BeginSample("HealthBarManager.Update", "HealthBarManager");
            HealthBarManager.Update(Core.DELTATIME);
            FrameProfiler.EndSample("HealthBarManager.Update");

            // Detect player overlap with ZoneBlock GOs and activate/deactivate
            // the corresponding Dynamic content in the Interact block.
            ZoneBlockDetector.Update();

            FrameProfiler.BeginSample("BlockManager.Update", "BlockManager");
            BlockManager.Update(gameTime);
            FrameProfiler.EndSample("BlockManager.Update");

            // Reset triggers
            TriggerManager.Tickwise_TriggerReset();

            // Assess "prev" switch state management mechanism
            ControlStateManager.Tickwise_PrevSwitchTrackUpdate();

            // Capture current input states for next-frame comparisons (trigger/switch edge detection)
            InputTypeManager.Update();
        }
    }
}
