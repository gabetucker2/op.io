using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace op.io
{
    /// <summary>
    /// Lightweight per-function CPU profiler. Wrap any code section with
    /// BeginSample / EndSample to accumulate rolling-average timing data
    /// visible in the PerformanceBlock.
    /// </summary>
    public static class FrameProfiler
    {
        private const int RollingWindow = 60;

        private const int PeakWindowSize = 1200; // ~20 seconds at 60 fps

        private sealed class SampleState
        {
            public readonly string ParentScript;
            public long BeginTimestamp;
            public long BeginGCBytes;
            public readonly Queue<double> Durations = new();
            public double SumMs;
            public double AvgMs;
            public double PeakMs;
            public long LastAllocBytes;
            public readonly Queue<long> AllocSamples = new();
            public long AllocSum;
            public long AvgAllocBytes;
            // 20-second rolling peak
            public readonly Queue<long> PeakAllocWindow = new();
            public long PeakAllocBytes;

            public SampleState(string parentScript)
            {
                ParentScript = parentScript ?? string.Empty;
            }
        }

        private static readonly Dictionary<string, SampleState> _states =
            new(StringComparer.OrdinalIgnoreCase);

        // Ordered list of keys so entries appear in registration order by default.
        private static readonly List<string> _keyOrder = new();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Begin timing a named section. parentScript is the owning class name.</summary>
        public static void BeginSample(string key, string parentScript)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (!_states.TryGetValue(key, out SampleState state))
            {
                state = new SampleState(parentScript);
                _states[key] = state;
                _keyOrder.Add(key);
            }

            state.BeginTimestamp = Stopwatch.GetTimestamp();
            state.BeginGCBytes   = GC.GetTotalMemory(false);
        }

        /// <summary>End timing a named section and commit the elapsed ms and GC allocation delta.</summary>
        public static void EndSample(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_states.TryGetValue(key, out SampleState state)) return;

            long end = Stopwatch.GetTimestamp();
            double elapsedMs = (end - state.BeginTimestamp) * 1000.0 / Stopwatch.Frequency;

            state.Durations.Enqueue(elapsedMs);
            state.SumMs += elapsedMs;
            while (state.Durations.Count > RollingWindow)
                state.SumMs -= state.Durations.Dequeue();

            int n = Math.Max(1, state.Durations.Count);
            state.AvgMs = state.SumMs / n;

            // Slow-decay peak so it reflects recent worst-case, not all-time.
            state.PeakMs = Math.Max(state.PeakMs * 0.98, elapsedMs);

            // Track managed heap delta as a proxy for allocations in this section.
            long allocDelta = Math.Max(0L, GC.GetTotalMemory(false) - state.BeginGCBytes);
            state.LastAllocBytes = allocDelta;
            state.AllocSamples.Enqueue(allocDelta);
            state.AllocSum += allocDelta;
            while (state.AllocSamples.Count > RollingWindow)
                state.AllocSum -= state.AllocSamples.Dequeue();
            state.AvgAllocBytes = state.AllocSum / Math.Max(1, state.AllocSamples.Count);

            // 20-second rolling peak allocation
            state.PeakAllocWindow.Enqueue(allocDelta);
            if (allocDelta >= state.PeakAllocBytes)
                state.PeakAllocBytes = allocDelta;
            while (state.PeakAllocWindow.Count > PeakWindowSize)
            {
                long removed = state.PeakAllocWindow.Dequeue();
                if (removed >= state.PeakAllocBytes)
                {
                    state.PeakAllocBytes = 0;
                    foreach (long v in state.PeakAllocWindow)
                        if (v > state.PeakAllocBytes) state.PeakAllocBytes = v;
                }
            }
        }

        /// <summary>Returns an unsorted snapshot of all profiled entries.</summary>
        public static ProfileEntry[] GetEntries()
        {
            double frameMs = Math.Max(0.001, PerformanceTracker.FrameTimeMilliseconds);

            var entries = new ProfileEntry[_keyOrder.Count];
            for (int i = 0; i < _keyOrder.Count; i++)
            {
                string key = _keyOrder[i];
                SampleState s = _states[key];
                entries[i] = new ProfileEntry(
                    functionName:      key,
                    parentScript:      s.ParentScript,
                    avgMs:             s.AvgMs,
                    peakMs:            s.PeakMs,
                    percentOfFrame:    (float)(s.AvgMs / frameMs * 100.0),
                    avgAllocBytes:     s.AvgAllocBytes,
                    peakAllocBytes:    s.PeakAllocBytes,
                    currentAllocBytes: s.LastAllocBytes
                );
            }

            return entries;
        }
    }

    public readonly struct ProfileEntry
    {
        public ProfileEntry(string functionName, string parentScript,
            double avgMs, double peakMs, float percentOfFrame, long avgAllocBytes, long peakAllocBytes,
            long currentAllocBytes = 0)
        {
            FunctionName      = functionName   ?? string.Empty;
            ParentScript      = parentScript   ?? string.Empty;
            AvgMs             = avgMs;
            PeakMs            = peakMs;
            PercentOfFrame    = percentOfFrame;
            AvgAllocBytes     = avgAllocBytes;
            PeakAllocBytes    = peakAllocBytes;
            CurrentAllocBytes = currentAllocBytes;
        }

        public string FunctionName    { get; }
        public string ParentScript    { get; }
        public double AvgMs           { get; }
        public double PeakMs          { get; }
        /// <summary>Percentage of the current frame time this sample consumed (0–100+).</summary>
        public float  PercentOfFrame  { get; }
        /// <summary>Rolling-average managed heap delta per frame (bytes).</summary>
        public long   AvgAllocBytes   { get; }
        /// <summary>Maximum managed heap delta observed in the last ~20 seconds (bytes).</summary>
        public long   PeakAllocBytes  { get; }
        /// <summary>Managed heap delta from the most recent frame sample (bytes).</summary>
        public long   CurrentAllocBytes { get; }
    }
}
