using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    /// <summary>
    /// Tracks key state for text inputs so held keys repeat after an initial delay.
    /// </summary>
    internal sealed class KeyRepeatTracker
    {
        private readonly Dictionary<Keys, double> _repeatTimers = new();
        private readonly double _initialDelaySeconds;
        private readonly double _repeatIntervalSeconds;
        private readonly int _maxRepeatsPerFrame;

        public KeyRepeatTracker(double initialDelaySeconds = 0.4, double repeatIntervalSeconds = 0.05, int maxRepeatsPerFrame = 6)
        {
            _initialDelaySeconds = Math.Max(0.01, initialDelaySeconds);
            _repeatIntervalSeconds = Math.Max(0.01, repeatIntervalSeconds);
            _maxRepeatsPerFrame = Math.Max(1, maxRepeatsPerFrame);
        }

        public IEnumerable<Keys> GetKeysWithRepeat(KeyboardState current, KeyboardState previous, double elapsedSeconds)
        {
            elapsedSeconds = Math.Max(0d, elapsedSeconds);
            Keys[] pressed = current.GetPressedKeys();
            HashSet<Keys> pressedSet = new(pressed.Length);
            foreach (Keys key in pressed)
            {
                pressedSet.Add(key);
            }

            foreach (Keys key in pressed)
            {
                if (!previous.IsKeyDown(key))
                {
                    _repeatTimers[key] = _initialDelaySeconds;
                    yield return key;
                    continue;
                }

                double timer = _repeatTimers.TryGetValue(key, out double stored) ? stored : _initialDelaySeconds;
                timer -= elapsedSeconds;
                int repeats = 0;
                while (timer <= 0d && repeats < _maxRepeatsPerFrame)
                {
                    yield return key;
                    timer += _repeatIntervalSeconds;
                    repeats++;
                }

                _repeatTimers[key] = Math.Min(timer, _initialDelaySeconds);
            }

            if (_repeatTimers.Count == 0)
            {
                yield break;
            }

            List<Keys> toRemove = null;
            foreach (KeyValuePair<Keys, double> pair in _repeatTimers)
            {
                if (!pressedSet.Contains(pair.Key))
                {
                    toRemove ??= new List<Keys>();
                    toRemove.Add(pair.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (Keys key in toRemove)
                {
                    _repeatTimers.Remove(key);
                }
            }
        }

        public void Reset()
        {
            _repeatTimers.Clear();
        }
    }
}
