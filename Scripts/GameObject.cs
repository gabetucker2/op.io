using System;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

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

        // Computed property (not serialized, calculated from Shape)
        public float BoundingRadius => Shape != null ? MathF.Sqrt(Shape.Width * Shape.Width + Shape.Height * Shape.Height) / 2 : 0f;

        // Parameterless Constructor for JSON Deserialization
        public GameObject() { }

        // Main Constructor
        public GameObject(Vector2 initialPosition, float initialRotation, float mass, bool isPlayer, bool isDestructible, bool isCollidable, Shape shape)
        {
            Position = initialPosition;
            Rotation = initialRotation;
            Mass = mass;
            IsPlayer = isPlayer;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            Shape = shape;

            DebugManager.PrintDebug($"Created GameObject at {Position}, Shape: {Shape?.Type ?? "None"}");
        }

        public virtual void LoadContent(GraphicsDevice graphicsDevice)
        {
            Shape?.LoadContent(graphicsDevice);
        }

        public virtual void Update(float deltaTime)
        {
            if (deltaTime <= 0)
                throw new ArgumentException("DeltaTime must be greater than 0.", nameof(deltaTime));
        }

        public virtual void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            if (Shape == null)
            {
                DebugManager.PrintError($"GameObject at {Position} has no Shape. Skipping draw.");
                return;
            }

            Shape.Draw(spriteBatch, debugEnabled);
        }

        // Adds ApplyForce for physics movement
        public virtual void ApplyForce(Vector2 force)
        {
            if (float.IsNaN(force.X) || float.IsNaN(force.Y))
                throw new ArgumentException("Force vector must contain valid numeric values.", nameof(force));

            Position += force / Mass;
            OnTransformChanged?.Invoke(this);
        }

        // Ensure only one player exists
        public static void ValidateSinglePlayer(GameObject[] objects)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects), "Object list cannot be null.");
            int playerCount = 0;
            foreach (var obj in objects)
            {
                if (obj.IsPlayer)
                {
                    playerCount++;
                    if (playerCount > 1)
                        throw new InvalidOperationException("Multiple GameObjects cannot have IsPlayer set to true.");
                }
            }
        }

        // Event for transform changes
        public event Action<GameObject> OnTransformChanged;
    }
}
