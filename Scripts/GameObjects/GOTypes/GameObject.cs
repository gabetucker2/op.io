using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class GameObject : IDisposable
    {
        // Properties for the GameObject's data
        public int ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }
        public float Mass { get; set; }
        public bool IsDestructible { get; set; }
        public bool IsCollidable { get; set; }
        public bool StaticPhysics { get; set; }
        public Shape Shape { get; set; }
        public Color FillColor { get; set; }
        public Color OutlineColor { get; set; }
        public int OutlineWidth { get; set; }

        // Computed property for BoundingRadius based on Shape size
        public float BoundingRadius =>
            Shape != null ? MathF.Sqrt(Shape.Width * Shape.Width + Shape.Height * Shape.Height) / 2f : 0f;

        // Constructor to initialize the GameObject with mandatory fields
        public GameObject(
            int id,
            string name,
            string type,
            Vector2 position,
            float rotation,
            float mass,
            bool isDestructible,
            bool isCollidable,
            bool staticPhysics,
            Shape shape,
            Color fillColor,
            Color outlineColor,
            int outlineWidth,
            bool isPrototype = false // Flag to determine if this object is a prototype and shouldn't be registered
        )
        {
            // Validate that the shape is not null
            if (shape == null)
            {
                DebugLogger.PrintError($"GameObject creation failed: null shape provided for ID {id}.");
                throw new ArgumentNullException(nameof(shape), "Shape cannot be null.");
            }

            // Initialize properties
            ID = id;
            Name = name;
            Type = type;
            Position = position;
            Rotation = rotation;
            Mass = mass;
            IsDestructible = isDestructible;
            IsCollidable = isCollidable;
            StaticPhysics = staticPhysics;
            FillColor = fillColor;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
            Shape = shape;

            // Register the GameObject with ShapeManager, if not a prototype
            if (!isPrototype)
            {
                GameObjectRegister.RegisterGameObject(this); // Delegate registration to the new GameObjectRegister class
            }
        }

        // Load content for the Shape
        public virtual void LoadContent(GraphicsDevice graphics)
        {
            Shape?.LoadContent(graphics);  // Load the content for the shape, if it exists
        }

        // Update the GameObject (can be overridden for specific behavior)
        public virtual void Update()
        {
            if (Core.DELTATIME <= 0)
            {
                DebugLogger.PrintWarning($"Skipped update for GameObject ID={ID}: deltaTime is non-positive ({Core.DELTATIME}).");
                return;
            }

            // General update behavior (can be extended for specific objects)
        }

        // Explicitly manage resource cleanup
        public void Dispose()
        {
            // If the GameObject is registered, unregister it upon disposal
            GameObjectRegister.UnregisterGameObject(this);

            // Dispose of Shape if it has IDisposable functionality
            if (Shape is IDisposable disposableShape)
            {
                disposableShape.Dispose();
            }

            DebugLogger.PrintGO($"Disposed and unregistered GameObject ID={ID}");
        }
    }
}
