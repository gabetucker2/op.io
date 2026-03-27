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

                // Draw barrel behind body for agents
                // Offset by half the barrel length so the back sits at the unit center and the front extends outward
                if (gameObject is Agent agent && agent.BarrelShape != null)
                {
                    float halfLength = agent.BarrelShape.Width / 2f;
                    Vector2 barrelOffset = new Vector2(MathF.Cos(agent.Rotation), MathF.Sin(agent.Rotation)) * halfLength;
                    agent.BarrelShape.DrawAt(spriteBatch, agent.Position + barrelOffset, agent.Rotation);
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
