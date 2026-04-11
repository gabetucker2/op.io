using System;
using Microsoft.Xna.Framework;
using System.Windows.Forms;
using op.io.UI.BlockScripts.Blocks;

namespace op.io
{
    public static class ActionHandler
    {
        private const bool ExitHotkeyEnabled = true;
        private static float _lastExitSuppressLogTime;

        private static float? _cachedRecoilMassScale;
        private static float RecoilMassScale
        {
            get
            {
                if (!_cachedRecoilMassScale.HasValue)
                    _cachedRecoilMassScale = DatabaseFetch.GetValue<float>(
                        "PhysicsSettings", "Value", "SettingKey", "RecoilMassScale");
                return _cachedRecoilMassScale.Value;
            }
        }

        public static void Tickwise_CheckActions()
        {
            // Suppress gameplay actions while the keybind overlay is active so captured keys (e.g., Escape) don't trigger game exits.
            if (ControlsBlock.IsRebindOverlayOpen())
            {
                return;
            }

            // Fire Action — silently ignored when player has no barrels equipped
            if (InputManager.IsInputActive("Fire"))
            {
                Agent player = Core.Instance.Player;
                if (player != null && player.BarrelCount > 0)
                {
                    Fire(player);
                }
            }

            // Barrel carousel — Q rotates left (clockwise), E rotates right; no-op with <2 barrels
            if (InputManager.IsInputActive("BarrelLeft"))
            {
                Agent player = Core.Instance.Player;
                if (player != null && player.BarrelCount >= 2)
                    player.SwitchBarrelLeft();
            }
            if (InputManager.IsInputActive("BarrelRight"))
            {
                Agent player = Core.Instance.Player;
                if (player != null && player.BarrelCount >= 2)
                    player.SwitchBarrelRight();
            }

            // Respawn Action — only activates when the player is dead or dying.
            if (InputManager.IsInputActive(ControlKeyMigrations.RespawnKey))
            {
                Agent player = Core.Instance.Player;
                bool isAlive = player != null && !player.IsDying && player.CurrentHealth > 0f;
                DebugLogger.PrintPlayer($"[Respawn] Shift+R detected. player={player?.ID.ToString() ?? "null"}, IsDying={player?.IsDying}, HP={player?.CurrentHealth:F1}, isAlive={isAlive}");
                if (!isAlive)
                    RespawnPlayer();
            }

            // Exit Action
            if (ExitHotkeyEnabled && InputManager.IsInputActive("Exit"))
            {
                DebugLogger.PrintUI("Exit hotkey triggered. Closing game.");
                Exit.CloseGame();
            }
            else if (!ExitHotkeyEnabled && InputManager.IsInputActive("Exit"))
            {
                if (Core.GAMETIME - _lastExitSuppressLogTime > 1.0f)
                {
                    _lastExitSuppressLogTime = Core.GAMETIME;
                    DebugLogger.PrintUI("Exit hotkey suppressed to keep the session running.");
                }
            }

            // ReturnCursorToPlayer Handling
            if (InputManager.IsInputActive("ReturnCursorToPlayer"))
            {
                Vector2 playerPosition = GameObjectFunctions.GetGOGlobalScreenPosition(Core.Instance.Player);
                Cursor.Position = TypeConversionFunctions.Vector2ToPoint(playerPosition);
                DebugLogger.PrintUI($"Cursor returned to player position: {playerPosition}");
            }

            // CameraSnapToPlayer (Shift+Space)
            // Free: snap camera to player once.  Scout: no-op (always centered).  Locked: reset offset.
            if (InputManager.IsInputActive(ControlKeyMigrations.CameraSnapToPlayerKey))
            {
                string camMode = ControlStateManager.ContainsEnumState(ControlKeyMigrations.CameraLockModeKey)
                    ? ControlStateManager.GetEnumValue(ControlKeyMigrations.CameraLockModeKey)
                    : "Locked";

                if (string.Equals(camMode, "Free", StringComparison.OrdinalIgnoreCase))
                    BlockManager.SnapCameraToPlayer();
                else if (string.Equals(camMode, "Locked", StringComparison.OrdinalIgnoreCase))
                    BlockManager.ResetLockedCameraOffset();
                // Scout: no-op — camera auto-follows and is already centered.
            }

            // Camera scroll zoom — ScrollIn brings the camera closer, ScrollOut moves it farther
            if (InputManager.IsInputActive(ControlKeyMigrations.ScrollInKey))
            {
                BlockManager.ApplyCameraZoom(1);
            }
            if (InputManager.IsInputActive(ControlKeyMigrations.ScrollOutKey))
            {
                BlockManager.ApplyCameraZoom(-1);
            }

            if (InputManager.IsInputActive(ControlKeyMigrations.PreviousConfigurationKey))
            {
                if (!ControlSetupsBlock.TryApplyPreviousSetup(allowWhileLocked: true))
                {
                    DockingSetupsBlock.TryApplyPreviousSetup(allowWhileLocked: true);
                }
            }
            else if (InputManager.IsInputActive(ControlKeyMigrations.NextConfigurationKey))
            {
                if (!ControlSetupsBlock.TryApplyNextSetup(allowWhileLocked: true))
                {
                    DockingSetupsBlock.TryApplyNextSetup(allowWhileLocked: true);
                }
            }
        }

        // Fires a bullet from the given agent. Can be called for both the player and NPC agents.
        public static void Fire(Agent agent)
        {
            if (agent == null) return;
            if (agent.BarrelCount == 0) return;
            if (agent.TriggerCooldown > 0) return;

            BulletManager.SpawnBullet(agent);

            float bulletMass = agent.BarrelAttributes.BulletMass > 0f ? agent.BarrelAttributes.BulletMass : BulletManager.DefaultBulletMass;
            float recoilMass = AttributeDerived.RecoilMass(bulletMass);
            float recoilSpeed = recoilMass * RecoilMassScale * BulletManager.BulletKnockbackScalar;
            Vector2 recoilDir = new Vector2(MathF.Cos(agent.Rotation + MathF.PI), MathF.Sin(agent.Rotation + MathF.PI));
            agent.PhysicsVelocity += recoilSpeed * recoilDir;

            float rs = agent.BarrelAttributes.ReloadSpeed;
            float reloadTime;
            if (rs < 0)
                reloadTime = 1.0f / 3.0f;   // -1 → use default (3 shots/sec)
            else if (rs == 0)
                reloadTime = float.MaxValue; // 0 → never fire again
            else
                reloadTime = 1.0f / rs;      // shots/sec → seconds per shot
            agent.TriggerCooldown = reloadTime;

            DebugLogger.PrintPlayer($"Agent {agent.ID} fired. Cooldown: {reloadTime:F2}s");
        }

        private static void RespawnPlayer()
        {
            Agent dead = Core.Instance.Player;
            if (dead == null)
            {
                DebugLogger.PrintError("Respawn failed: no player reference to respawn.");
                return;
            }

            Agent newPlayer = AgentLoader.LoadAgent(dead.ID);
            if (newPlayer == null)
            {
                DebugLogger.PrintError("Respawn failed: could not load player agent from database.");
                return;
            }

            var barrels = BarrelLoader.LoadBarrelsForAgent(newPlayer.ID);
            if (barrels.Count > 0)
            {
                newPlayer.ClearBarrels();
                foreach (var barrel in barrels)
                    newPlayer.AddBarrel(barrel);
            }

            Core.Instance.GameObjects.Add(newPlayer);
            Core.Instance.Player = newPlayer;

            // LoadGraphics only runs at startup, so load content for the new player's shapes now.
            newPlayer.LoadContent(Core.Instance.GraphicsDevice);
            foreach (var slot in newPlayer.Barrels)
                slot.FullShape?.LoadContent(Core.Instance.GraphicsDevice);

            DebugLogger.PrintPlayer($"Player respawned at {newPlayer.Position}.");
        }

        public static void Move(GameObject gameObject, Vector2 direction, float speed)
        {
            if (gameObject == null)
            {
                DebugLogger.PrintError("Move failed: GameObject is null.");
                return;
            }

            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                DebugLogger.PrintWarning($"Move aborted: Direction contains NaN values: {direction}");
                return;
            }

            if (direction == Vector2.Zero)
            {
                DebugLogger.PrintWarning("Move aborted: Direction is zero.");
                return;
            }

            if (speed <= 0)
            {
                DebugLogger.PrintWarning($"Move aborted: Speed must be positive (received {speed})");
                return;
            }

            if (Core.DELTATIME <= 0)
            {
                DebugLogger.PrintWarning($"Move skipped: DeltaTime must be positive (received {Core.DELTATIME})");
                return;
            }

            // Normalize direction and apply speed for frame rate independence
            Vector2 normalizedDirection = Vector2.Normalize(direction);
            Vector2 velocity = normalizedDirection * speed * Core.DELTATIME;  // Using deltaTime for consistent speed

            gameObject.Position += velocity;  // Update position based on velocity
            //DebugLogger.PrintUI($"Moved GameObject (ID={gameObject.ID}) by {velocity}, new position: {gameObject.Position}");
        }
    }
}
