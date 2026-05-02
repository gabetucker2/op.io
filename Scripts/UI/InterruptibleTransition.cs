using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    internal static class InterruptibleTransitionCurves
    {
        public static float SmoothStep(float progress)
        {
            progress = MathHelper.Clamp(progress, 0f, 1f);
            return progress * progress * (3f - (2f * progress));
        }
    }

    internal struct InterruptibleVector4Transition
    {
        public Vector4 Current { get; private set; }
        public Vector4 Target { get; private set; }
        public float Progress => _durationSeconds <= 0f ? 1f : MathHelper.Clamp(_elapsedSeconds / _durationSeconds, 0f, 1f);
        public bool IsActive => Progress < 1f;

        private Vector4 _start;
        private float _durationSeconds;
        private float _elapsedSeconds;

        public void Reset(Vector4 value)
        {
            Current = value;
            Target = value;
            _start = value;
            _durationSeconds = 0f;
            _elapsedSeconds = 0f;
        }

        public void Retarget(Vector4 target, float durationSeconds)
        {
            _start = Current;
            Target = target;
            _durationSeconds = Math.Max(0f, durationSeconds);
            _elapsedSeconds = 0f;

            if (_durationSeconds <= 0f)
            {
                Current = Target;
            }
        }

        public void Update(float deltaSeconds)
        {
            if (_durationSeconds <= 0f)
            {
                Current = Target;
                return;
            }

            _elapsedSeconds = Math.Min(_durationSeconds, _elapsedSeconds + Math.Max(0f, deltaSeconds));
            float eased = InterruptibleTransitionCurves.SmoothStep(Progress);
            Current = Vector4.Lerp(_start, Target, eased);

            if (_elapsedSeconds >= _durationSeconds)
            {
                Current = Target;
            }
        }
    }
}
