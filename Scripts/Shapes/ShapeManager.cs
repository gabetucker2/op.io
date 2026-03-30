using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public class ShapeManager
    {
        private static ShapeManager _instance;

        public static ShapeManager Instance => _instance ??= new ShapeManager();

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
        }

        // Draws all registered GameObjects by fetching them from GameObjectRegister
        public void DrawShapes(SpriteBatch spriteBatch)
        {
            foreach (GameObject gameObject in GameObjectRegister.GetRegisteredGameObjects())
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

                    // Pass 1: standby barrels
                    for (int i = 0; i < N; i++)
                    {
                        if (i == agent.ActiveBarrelIndex) continue;
                        var slot = agent.Barrels[i];
                        if (slot.FullShape == null) continue;
                        float barrelAngle = agent.Rotation + i * angleStep - agent.CarouselAngle;
                        // Always use the full half-length for positioning so the draw center
                        // stays at the body edge regardless of scale.  The scale only shrinks
                        // the visual length from that anchor point.
                        float halfLength = slot.FullShape.Width / 2f;
                        Vector2 offset = new Vector2(MathF.Cos(barrelAngle), MathF.Sin(barrelAngle)) * halfLength;
                        slot.FullShape.DrawAt(spriteBatch, agent.Position + offset, barrelAngle,
                            new Vector2(slot.CurrentHeightScale, 1f));
                    }

                    // Pass 2: active barrel (drawn last so it appears in front of standby ones)
                    {
                        var active = agent.Barrels[agent.ActiveBarrelIndex];
                        if (active.FullShape != null)
                        {
                            float barrelAngle = agent.Rotation + agent.ActiveBarrelIndex * angleStep - agent.CarouselAngle;
                            float halfLength = active.FullShape.Width / 2f;
                            Vector2 offset = new Vector2(MathF.Cos(barrelAngle), MathF.Sin(barrelAngle)) * halfLength;
                            active.FullShape.DrawAt(spriteBatch, agent.Position + offset, barrelAngle,
                                new Vector2(active.CurrentHeightScale, 1f));
                        }
                    }
                }

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
