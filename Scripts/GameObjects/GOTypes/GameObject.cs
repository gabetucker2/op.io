using System;
using System.Collections.Generic;
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

        // Parent/child hierarchy (bidirectional)
        public GameObject Parent { get; private set; }
        private readonly List<GameObject> _children = new();
        public IReadOnlyList<GameObject> Children => _children;

        public void AddChild(GameObject child)
        {
            if (child == null || child == this || _children.Contains(child)) return;
            child.Parent?.RemoveChild(child);
            _children.Add(child);
            child.Parent = this;
        }

        public void RemoveChild(GameObject child)
        {
            if (child == null) return;
            if (_children.Remove(child))
                child.Parent = null;
        }

        public float Opacity   { get; set; } = 1f;
        public float HitFlash  { get; set; } = 0f;

        // ── Base identity ────────────────────────────────────────────────────
        public float CurrentXP { get; set; } = 0f;

        // ── Destructible (only meaningful when IsDestructible = true) ────────
        public float CurrentHealth    { get; set; }
        public float MaxHealth        { get; set; }
        public float CurrentShield    { get; set; }
        public float MaxShield        { get; set; }
        public float DeathPointReward { get; set; }

        // ID of the agent whose bullet last dealt damage to this object (for XP award on death).
        public int LastDamagedByAgentID { get; set; } = -1;

        /// <summary>
        /// Triggers a hit-flash with smooth interruption: if the object is already
        /// partially white from a previous flash, the fade-in is proportionally
        /// shorter so the transition feels instantaneous rather than restarting.
        /// </summary>
        public void TriggerHitFlash()
        {
            float fadeIn  = BulletManager.HitFlashFadeIn;
            float fadeOut = BulletManager.HitFlashFadeOut;
            float currentAlpha;
            if (HitFlash > fadeOut)
                currentAlpha = (fadeIn + fadeOut - HitFlash) / MathF.Max(fadeIn, 0.001f);
            else
                currentAlpha = HitFlash / MathF.Max(fadeOut, 0.001f);
            // Set HitFlash so fade-in duration scales with how far from full-white we are.
            HitFlash = fadeOut + fadeIn * (1f - MathHelper.Clamp(currentAlpha, 0f, 1f));
        }

        public GOProperties GOProperties => new GOProperties
        {
            Id               = ID,
            Type             = Type,
            Flags            = ComputeFlags(),
            CurrentXP        = CurrentXP,
            DeathPointReward = DeathPointReward,
            CurrentHealth    = CurrentHealth,
            CurrentShield    = CurrentShield,
        };

        protected virtual GOFlags ComputeFlags()
        {
            GOFlags f = GOFlags.None;
            if (StaticPhysics)  f |= GOFlags.Static;
            if (IsCollidable)   f |= GOFlags.Collidable;
            if (IsDestructible) f |= GOFlags.Destructible;
            return f;
        }

        // Computed property for BoundingRadius based on Shape size
        public float BoundingRadius =>
            Shape != null ? MathF.Sqrt(Shape.Width * Shape.Width + Shape.Height * Shape.Height) / 2f : 0f;

        // World position of this object's own body center.
        // For root objects (no parent) this equals Position.
        // For children, Position is a local offset, so the parent's world position is used.
        public Vector2 ParentPosition => Parent?.Position ?? Position;

        // World-space centroid of this object's body and all its children.
        // Child positions are local offsets rotated by this object's current orientation.
        public Vector2 ObjectPosition
        {
            get
            {
                if (_children.Count == 0)
                    return Position;

                float cos = MathF.Cos(Rotation);
                float sin = MathF.Sin(Rotation);
                Vector2 sum = Position;
                foreach (GameObject child in _children)
                {
                    sum += new Vector2(
                        Position.X + child.Position.X * cos - child.Position.Y * sin,
                        Position.Y + child.Position.X * sin + child.Position.Y * cos);
                }
                return sum / (_children.Count + 1);
            }
        }

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

            if (HitFlash > 0f)
                HitFlash = MathHelper.Clamp(HitFlash - Core.DELTATIME, 0f, BulletManager.HitFlashDuration);
        }

        // Explicitly manage resource cleanup
        public void Dispose()
        {
            // Unlink from parent
            Parent?.RemoveChild(this);

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
