using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class ShapeManager
    {
        private static ShapeManager _instance;

        public static ShapeManager Instance => _instance ??= new ShapeManager();

        // Reusable sorted list to avoid per-frame allocations.
        private readonly List<GameObject> _sortedObjects = new();

        private ShapeManager()
        {
            DebugLogger.PrintSystem("ShapeManager initialized.");
        }

        public void DrawFlashes(SpriteBatch spriteBatch)
        {
            foreach (GameObject gameObject in GameObjectRegister.GetRegisteredGameObjects())
            {
                if (gameObject.Shape == null || gameObject.Shape.IsPrototype || gameObject.HitFlash <= 0f) continue;
                gameObject.Shape.DrawFlash(spriteBatch, gameObject);
            }
            // Also flash bullets — they are not in GameObjectRegister
            foreach (var bullet in BulletManager.GetBullets())
            {
                if (bullet == null || bullet.Shape == null || bullet.Shape.IsPrototype || bullet.HitFlash <= 0f) continue;
                bullet.Shape.DrawFlash(spriteBatch, bullet);
            }
        }

        // Draws all registered GameObjects sorted by DrawLayer (lower layers behind, higher on top).
        public void DrawShapes(SpriteBatch spriteBatch)
        {
            _sortedObjects.Clear();
            _sortedObjects.AddRange(GameObjectRegister.GetRegisteredGameObjects());
            _sortedObjects.Sort(static (a, b) => a.DrawLayer.CompareTo(b.DrawLayer));

            foreach (GameObject gameObject in _sortedObjects)
            {
                if (gameObject.Shape == null)
                {
                    DebugLogger.PrintWarning($"GameObject ID={gameObject.ID} has no Shape � skipping draw.");
                    continue;
                }

                if (gameObject.Shape.IsPrototype)
                {
                    DebugLogger.PrintDebug($"Skipping draw for prototype shape ID={gameObject.ID}.");
                    continue;
                }

                // Draw all barrels behind the agent body.
                // Each barrel's world angle is: agentRotation + i*(2π/N) - carouselAngle,
                // placing the active barrel (index 0 of the offset sequence) straight ahead.
                // Standby barrels are drawn first so the active barrel renders on top of them.
                if (gameObject is Agent agent && agent.BarrelCount > 0)
                {
                    int N = agent.BarrelCount;
                    float angleStep = N > 1 ? MathF.Tau / N : 0f;
                    float bodyRadius = agent.Shape != null
                        ? Math.Max(agent.Shape.Width, agent.Shape.Height) / 2f : 0f;

                    // Pass 1: standby barrels
                    for (int i = 0; i < N; i++)
                    {
                        if (i == agent.ActiveBarrelIndex) continue;
                        var slot = agent.Barrels[i];
                        if (slot.FullShape == null) continue;
                        if (!agent.TryGetBarrelWorldTransform(
                            i,
                            out Vector2 barrelCenter,
                            out _,
                            out float barrelAngle,
                            out _,
                            out _))
                        {
                            continue;
                        }

                        slot.FullShape.DrawAt(spriteBatch, barrelCenter, barrelAngle,
                            new Vector2(slot.CurrentHeightScale, 1f), agent.Opacity, applyWorldTint: true);
                    }

                    // Pass 2: active barrel (drawn last so it appears in front of standby ones)
                    {
                        var active = agent.Barrels[agent.ActiveBarrelIndex];
                        if (active.FullShape != null)
                        {
                            if (!agent.TryGetBarrelWorldTransform(
                                agent.ActiveBarrelIndex,
                                out Vector2 barrelCenter,
                                out _,
                                out float barrelAngle,
                                out _,
                                out _))
                            {
                                goto DrawAgentBody;
                            }

                            active.FullShape.DrawAt(spriteBatch, barrelCenter, barrelAngle,
                                new Vector2(active.CurrentHeightScale, 1f), agent.Opacity, applyWorldTint: true);
                        }
                    }
                }

DrawAgentBody:
                gameObject.Shape.Draw(spriteBatch, gameObject);

                if (DebugModeHandler.DEBUGENABLED && gameObject == Core.Instance.Player)
                {
                    // DebugLogger.PrintDebug($"Drawing rotation pointer for Player ID={gameObject.ID} at Pos={gameObject.Position}");
                    DebugRenderer.DrawRotationPointer(spriteBatch, (Agent)gameObject);
                }
            }
        }
    }
}
