using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class GameObject
    {
        private Vector2 _position;
        private float _rotation;

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
                OnTransformChanged?.Invoke(this);
            }
        }

        public event Action<GameObject> OnTransformChanged;

        public GameObject(Vector2 initialPosition = default, float initialRotation = 0f)
        {
            if (float.IsNaN(initialPosition.X) || float.IsNaN(initialPosition.Y))
                throw new ArgumentException("Initial position must contain valid numeric values.", nameof(initialPosition));

            if (float.IsNaN(initialRotation))
                throw new ArgumentException("Initial rotation must be a valid numeric value.", nameof(initialRotation));

            Position = initialPosition == default ? Vector2.Zero : initialPosition;
            Rotation = initialRotation;
        }
    }
}
