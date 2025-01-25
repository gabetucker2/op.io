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
                _position = value;
                OnTransformChanged?.Invoke(this);
            }
        }

        public float Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value % 360f; // Keep rotation within 0-360
                OnTransformChanged?.Invoke(this);
            }
        }

        // Event to notify when position or rotation changes
        public event System.Action<GameObject> OnTransformChanged;

        public GameObject(Vector2 initialPosition = default, float initialRotation = 0f)
        {
            Position = initialPosition == default ? Vector2.Zero : initialPosition;
            Rotation = initialRotation;
        }
    }
}
