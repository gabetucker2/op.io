using System;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class GameObject
    {
        // Properties
        public GameObject InstanceGO { get; set; }
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }
        public float Mass { get; set; }
        public bool IsPlayer { get; set; }
        public bool IsDestructible { get; set; }
        public bool IsCollidable { get; set; }
        public bool StaticPhysics { get; set; }
        public Shape Shape { get; set; }
        public int Count { get; set; } = 1;

        // Computed property
        public float BoundingRadius =>
            Shape != null
                ? MathF.Sqrt(Shape.Width * Shape.Width + Shape.Height * Shape.Height) / 2f
                : 0f;

        // Parameterless Constructor
        public GameObject()
        {
            InstanceGO = this;
        }

        // Main Constructor
        public GameObject(Vector2 initialPosition, float initialRotation, float mass, bool isPlayer, bool isDestructible, bool isCollidable, bool staticPhysics, Shape shape)
        {
            if (shape == null)
            {
                DebugLogger.PrintError("Attempted to construct GameObject with null Shape.");
                return;
            }

            Position = initialPosition;
            Rotation = initialRotation;
            Mass = mass > 0 ? mass : 1f; // Default mass to 1 if invalid
            IsPlayer = isPlayer;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            StaticPhysics = staticPhysics;
            Shape = shape;

            DebugLogger.PrintObject($"Created GameObject at {Position}, Shape: {Shape.Type}");
        }

        public virtual void LoadContent(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                DebugLogger.PrintError("LoadContent called with null GraphicsDevice.");
                return;
            }

            Shape?.LoadContent(graphicsDevice);
        }

        public virtual void Update()
        {
            DebugHelperFunctions.DeltaTimeZeroWarning();

            if (Shape != null)
                Shape.Position = Position;  // <- This line is crucial

            // Add additional per-object update logic here as needed
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (Shape == null)
            {
                DebugLogger.PrintError($"GameObject at {Position} has no Shape. Skipping draw.");
                return;
            }

            if (spriteBatch == null)
            {
                DebugLogger.PrintError("Draw called with null SpriteBatch.");
                return;
            }

            float appliedRotation = Shape.Type == "Circle" ? 0f : Rotation;
            Shape.Draw(spriteBatch, appliedRotation);
        }

        public virtual void ApplyForce(Vector2 force)
        {
            if (force == Vector2.Zero || Core.deltaTime <= 0f)
                return;

            Vector2 acceleration = force / Mass;
            Position += acceleration * Core.deltaTime;

            //DebugLogger.PrintObject($"Applied force: {force}, acceleration: {acceleration}, deltaTime: {deltaTime}, new position: {Position}");
        }

        public static void ValidateSinglePlayer(GameObject[] objects)
        {
            if (objects == null)
            {
                DebugLogger.PrintError("ValidateSinglePlayer called with null object array.");
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
                        DebugLogger.PrintError("Multiple GameObjects have IsPlayer set to true. Only one is allowed.");
                        return;
                    }
                }
            }
        }
    }
}
