using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class GameUpdater
    {
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
                if (go.IsDestructible && go.CurrentHealth <= 0f)
                {
                    dead ??= new List<GameObject>();
                    dead.Add(go);
                }
            }

            if (dead == null) return;

            foreach (var go in dead)
            {
                // Award XP to the agent who landed the killing blow.
                if (go.LastDamagedByAgentID >= 0)
                {
                    foreach (var obj in gameObjects)
                    {
                        if (obj is Agent agent && agent.ID == go.LastDamagedByAgentID)
                        {
                            agent.CurrentXP += go.DeathPointReward;
                            break;
                        }
                    }
                }

                gameObjects.Remove(go);
                Core.Instance.StaticObjects?.Remove(go);
                go.Dispose();
                DebugLogger.PrintGO($"Destroyed GameObject ID={go.ID} (Name={go.Name}, Reward={go.DeathPointReward} XP).");
            }
        }

        private static void UpdateInternal(GameTime gameTime)
        {
            Core.GAMETIME = (float)gameTime.TotalGameTime.TotalSeconds;
            Core.DELTATIME = (float)gameTime.ElapsedGameTime.TotalSeconds;

            PerformanceTracker.Update(gameTime);

            DebugHelperFunctions.DeltaTimeZeroWarning();

            // Prime previous input snapshots so the first frame doesn't register phantom releases.
            InputTypeManager.BeginFrame();

            // Centralized switch polling
            SwitchStateScanner.Tick();

            // Process general actions (toggles, switches, etc.)
            ActionHandler.Tickwise_CheckActions();

            // Handle player transform
            Vector2 direction = InputManager.GetMoveVector();
            if (direction != Vector2.Zero)
            {
                ActionHandler.Move(Core.Instance.Player, direction, Core.Instance.Player.Speed);
            }
            if (InputManager.TryGetHoldLatchRotation(out float lockedRotation))
            {
                Core.Instance.Player.Rotation = lockedRotation;
            }
            else
            {
                Core.Instance.Player.Rotation = MouseFunctions.GetAngleToMouse(Core.Instance.Player.Position);
            }

            // Update all GameObjects
            foreach (var gameObject in Core.Instance.GameObjects)
            {
                gameObject.Update();
            }

            if (Core.Instance.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects exist in the scene.");
            }

            // Update all active bullets
            BulletManager.Update();

            // Resolve bullet collisions against world objects
            BulletCollisionResolver.ResolveCollisions(BulletManager.GetBullets(), Core.Instance.GameObjects);

            // Resolve bullet-enemy damage, penetration, expiry, and depenetration
            BulletCollisionSystem.Update(Core.DELTATIME);

            // Apply physics step
            PhysicsManager.Update(Core.Instance.GameObjects);

            // Remove dead destructible objects and award XP to the last bullet-owner who dealt damage.
            // CollisionResolver no longer removes objects inline, so this is the single death authority.
            ProcessDeaths();

            // Flush accumulated damage notifications and advance active damage numbers
            DamageNumberManager.Flush();
            DamageNumberManager.Update(Core.DELTATIME);

            // Advance health bar fade state
            HealthBarManager.Update(Core.DELTATIME);

            BlockManager.Update(gameTime);

            // Reset triggers
            TriggerManager.Tickwise_TriggerReset();

            // Assess "prev" switch state management mechanism
            ControlStateManager.Tickwise_PrevSwitchTrackUpdate();

            // Capture current input states for next-frame comparisons (trigger/switch edge detection)
            InputTypeManager.Update();
        }
    }
}
