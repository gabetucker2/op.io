using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class Bullet : GameObject
    {
        public Vector2 Velocity { get; set; }

        private float _age;
        private readonly float _maxLifespan;
        private readonly float _range;
        private readonly float _volume;
        private readonly Vector2 _spawnPosition;

        public Bullet(int id, Vector2 position, Vector2 velocity, float mass, float maxLifespan, float range, Shape shape)
            : base(id, "Bullet", "Bullet", position, 0f, mass,
                   false, false, false,
                   shape,
                   Color.Red, new Color(139, 0, 0), 2)
        {
            Velocity = velocity;
            _maxLifespan = maxLifespan > 0 ? maxLifespan : BulletManager.DefaultBulletLifespan;
            _range = range > 0 ? range : BulletManager.DefaultBulletRange;
            _volume = ComputeVolume(shape);
            _spawnPosition = position;
        }

        // Computes 2D cross-sectional area (px²) used as a proxy for volume in drag calculations.
        // Larger bullets face more air resistance; shape type determines the formula.
        private static float ComputeVolume(Shape shape)
        {
            if (shape == null) return 1f;
            return shape.ShapeType switch
            {
                "Circle"  => MathF.PI * (shape.Width / 2f) * (shape.Width / 2f),
                "Rectangle" => shape.Width * shape.Height,
                _ => MathF.PI * (shape.Width / 2f) * (shape.Width / 2f) // polygon: approximate as circle
            };
        }

        public override void Update()
        {
            if (Core.DELTATIME <= 0) return;

            _age += Core.DELTATIME;
            float drag = BulletManager.AirResistance * _volume / MathF.Max(_range, 0.0001f);
            Velocity *= MathF.Max(1f - drag * Core.DELTATIME, 0f);
            Position += Velocity * Core.DELTATIME;

            bool expired = _age >= _maxLifespan;
            bool outOfRange = _range > 0 && Vector2.DistanceSquared(Position, _spawnPosition) >= _range * _range;

            if (expired || outOfRange)
            {
                BulletManager.MarkForRemoval(this);
            }
        }
    }
}
