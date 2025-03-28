using System;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class GameObject
    {
        // Properties
        [JsonInclude] public Vector2 Position { get; set; }
        [JsonInclude] public float Rotation { get; set; }
        [JsonInclude] public float Mass { get; set; }
        [JsonInclude] public bool IsPlayer { get; set; }
        [JsonInclude] public bool IsDestructible { get; set; }
        [JsonInclude] public bool IsCollidable { get; set; }
        [JsonInclude] public Shape Shape { get; set; }
        public int Count { get; set; } = 1;

        // Computed property
        public float BoundingRadius =>
            Shape != null
                ? MathF.Sqrt(Shape.Width * Shape.Width + Shape.Height * Shape.Height) / 2f
                : 0f;

        // Parameterless Constructor
        public GameObject() { }

        // Main Constructor
        public GameObject(Vector2 initialPosition, float initialRotation, float mass, bool isPlayer, bool isDestructible, bool isCollidable, Shape shape)
        {
            if (shape == null)
            {
                DebugManager.PrintError("Attempted to construct GameObject with null Shape.");
                return;
            }

            Position = initialPosition;
            Rotation = initialRotation;
            Mass = mass > 0 ? mass : 1f; // Default mass to 1 if invalid
            IsPlayer = isPlayer;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            Shape = shape;

            DebugManager.PrintDebug($"Created GameObject at {Position}, Shape: {Shape.Type}");
        }

        public virtual void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                DebugManager.PrintError("LoadContent called with null GraphicsDevice.");
                return;
            }

            Shape?.LoadContent(graphicsDevice);
        }

        public virtual void Update(float deltaTime)
        {
            if (deltaTime <= 0)
            {
                DebugManager.PrintWarning($"GameObject skipped update due to non-positive deltaTime: {deltaTime}");
                return;
            }

            // Add per-object behavior here as needed
        }

        public virtual void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (Shape == null)
            {
                DebugManager.PrintError($"GameObject at {Position} has no Shape. Skipping draw.");
                return;
            }

            if (spriteBatch == null)
            {
                DebugManager.PrintError("Draw called with null SpriteBatch.");
                return;
            }

            float appliedRotation = Shape.Type == "Circle" ? 0f : Rotation;
            Shape.Draw(spriteBatch, debugEnabled, appliedRotation);
        }

        public virtual void ApplyForce(Vector2 force, float deltaTime)
        {
            if (force == Vector2.Zero || deltaTime <= 0f)
                return;

            Vector2 acceleration = force / Mass;
            Position += acceleration * deltaTime;

            //DebugManager.PrintDebug($"Applied force: {force}, acceleration: {acceleration}, deltaTime: {deltaTime}, new position: {Position}");
        }

        public static void ValidateSinglePlayer(GameObject[] objects)
        {
            if (objects == null)
            {
                DebugManager.PrintError("ValidateSinglePlayer called with null object array.");
                return;
            }

            int playerCount = 0;
            foreach (var obj in objects)
            {
                if (obj.IsPlayer)
                {
                    playerCount++;
                    if (playerCount > 1)
                    {
                        DebugManager.PrintError("Multiple GameObjects have IsPlayer set to true. Only one is allowed.");
                        return;
                    }
                }
            }
        }
    }
}
