using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class GameObject
    {
        // Fields
        private Vector2 _position;
        private float _rotation;

        // Properties
        public Vector2 Position
        {
            get => _position;
            set
            {
                if (float.IsNaN(value.X) || float.IsNaN(value.Y))
                    throw new ArgumentException("Position must contain valid numeric values.", nameof(value));

                _position = value;
                OnTransformChanged?.Invoke(this);
            }
        }

        public float Rotation
        {
            get => _rotation;
            set
            {
                if (float.IsNaN(value))
                    throw new ArgumentException("Rotation must be a valid numeric value.", nameof(value));

                _rotation = value % 360f; // Normalize rotation to [0, 360)
                if (_rotation < 0) _rotation += 360f; // Handle negative rotations
                OnTransformChanged?.Invoke(this);
            }
        }

        public float Mass { get; private set; } // Mass of the object, used for physics calculations
        public float BoundingRadius { get; private set; } // Bounding radius for collision purposes
        public bool IsPlayer { get; private set; } // Indicates if this object is a player
        public bool IsDestructible { get; private set; } // Indicates if this object is destructible
        public bool IsCollidable { get; private set; } // Indicates if this object can collide with others
        public Shape Shape { get; private set; } // The rendering shape associated with the object

        // Events
        public event Action<GameObject> OnTransformChanged;

        // Constructor
        public GameObject(
            Vector2 initialPosition = default,
            float initialRotation = 0f,
            float mass = 1f,
            float boundingRadius = 0f,
            bool isPlayer = false,
            bool isDestructible = false,
            bool isCollidable = true,
            Shape shape = null
        )
        {
            if (float.IsNaN(initialPosition.X) || float.IsNaN(initialPosition.Y))
                throw new ArgumentException("Initial position must contain valid numeric values.", nameof(initialPosition));

            if (float.IsNaN(initialRotation))
                throw new ArgumentException("Initial rotation must be a valid numeric value.", nameof(initialRotation));

            if (mass <= 0)
                throw new ArgumentException("Mass must be greater than 0.", nameof(mass));

            if (boundingRadius < 0)
                throw new ArgumentException("Bounding radius cannot be negative.", nameof(boundingRadius));

            Position = initialPosition == default ? Vector2.Zero : initialPosition;
            Rotation = initialRotation;
            Mass = mass;
            BoundingRadius = boundingRadius;
            IsPlayer = isPlayer;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            Shape = shape;
        }

        // Method to update object properties (used for adjustments after creation)
        public void SetProperties(bool isPlayer, bool isDestructible, bool isCollidable)
        {
            IsPlayer = isPlayer;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
        }

        // Physics Integration
        public virtual void ApplyForce(Vector2 force)
        {
            if (float.IsNaN(force.X) || float.IsNaN(force.Y))
                throw new ArgumentException("Force vector must contain valid numeric values.", nameof(force));

            Position += force / Mass; // Force application considers mass for realistic movement
        }

        public virtual void Update(float deltaTime)
        {
            if (deltaTime <= 0)
                throw new ArgumentException("DeltaTime must be greater than 0.", nameof(deltaTime));

            // Update position or other state as needed
        }

        public virtual void LoadContent(GraphicsDevice graphicsDevice)
        {
            Shape?.LoadContent(graphicsDevice);
        }

        public virtual void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            Shape?.Draw(spriteBatch, debugEnabled);
        }

        // Validation for ensuring only one player exists in the game
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
    }
}
