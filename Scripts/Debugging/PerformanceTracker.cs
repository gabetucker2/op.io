using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    /// <summary>
    /// Tracks recent frame timings so UI blocks can show live FPS and frame time.
    /// </summary>
    public static class PerformanceTracker
    {
        private const int SampleSize = 120;
        private static readonly Queue<double> _frameDurations = new();
        private static double _frameDurationSum;
        private static double _framesPerSecond;
        private static double _frameTimeMilliseconds;

        public static double FramesPerSecond => _framesPerSecond;
        public static double FrameTimeMilliseconds => _frameTimeMilliseconds;

        public static void Reset()
        {
            _frameDurations.Clear();
            _frameDurationSum = 0d;
            _framesPerSecond = 0d;
            _frameTimeMilliseconds = 0d;
        }

        public static void Update(GameTime gameTime)
        {
            if (gameTime == null)
            {
                return;
            }

            double frameSeconds = gameTime.ElapsedGameTime.TotalSeconds;
            if (frameSeconds <= 0d || double.IsNaN(frameSeconds))
            {
                return;
            }

            _frameTimeMilliseconds = frameSeconds * 1000d;

            _frameDurations.Enqueue(frameSeconds);
            _frameDurationSum += frameSeconds;

            while (_frameDurations.Count > SampleSize)
            {
                _frameDurationSum -= _frameDurations.Dequeue();
            }

            if (_frameDurationSum <= 0d || _frameDurations.Count == 0)
            {
                _framesPerSecond = 0d;
                return;
            }

            _framesPerSecond = _frameDurations.Count / _frameDurationSum;
        }
    }
}
