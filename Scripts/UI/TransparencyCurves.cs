using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    /// <summary>
    /// Shared easing helpers for opacity fades so transparency behaves consistently.
    /// </summary>
    internal static class TransparencyCurves
    {
        public static float FadeIn(float progress)
        {
            return EaseOutCubic(progress);
        }

        public static float FadeOut(float progress)
        {
            return EaseInCubic(progress);
        }

        public static float Apply(float progress, bool isFadeIn)
        {
            return isFadeIn ? FadeIn(progress) : FadeOut(progress);
        }

        private static float EaseOutCubic(float progress)
        {
            progress = MathHelper.Clamp(progress, 0f, 1f);
            float inverse = 1f - progress;
            return 1f - (inverse * inverse * inverse);
        }

        private static float EaseInCubic(float progress)
        {
            progress = MathHelper.Clamp(progress, 0f, 1f);
            return progress * progress * progress;
        }
    }

    internal struct OpacityAnimation
    {
        public float CurrentOpacity;
        public float StartOpacity;
        public float TargetOpacity;
        public double DurationSeconds;
        public double ElapsedSeconds;
        public bool IsFadeIn;

        public void Begin(float currentOpacity, float targetOpacity, double durationSeconds, bool isFadeIn)
        {
            CurrentOpacity = MathHelper.Clamp(currentOpacity, 0f, 1f);
            StartOpacity = CurrentOpacity;
            TargetOpacity = MathHelper.Clamp(targetOpacity, 0f, 1f);
            DurationSeconds = Math.Max(0d, durationSeconds);
            ElapsedSeconds = 0d;
            IsFadeIn = isFadeIn;

            if (DurationSeconds <= 0d)
            {
                CurrentOpacity = TargetOpacity;
            }
        }

        public void Update(double deltaSeconds)
        {
            if (DurationSeconds <= 0d)
            {
                CurrentOpacity = TargetOpacity;
                return;
            }

            deltaSeconds = Math.Max(0d, deltaSeconds);
            ElapsedSeconds = Math.Min(DurationSeconds, ElapsedSeconds + deltaSeconds);

            float progress = DurationSeconds <= 0d ? 1f : (float)(ElapsedSeconds / DurationSeconds);
            float curved = TransparencyCurves.Apply(progress, IsFadeIn);
            CurrentOpacity = MathHelper.Lerp(StartOpacity, TargetOpacity, curved);

            if (ElapsedSeconds >= DurationSeconds)
            {
                CurrentOpacity = TargetOpacity;
            }
        }
    }
}
