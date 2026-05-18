using System;
using System.Text;

namespace op.io
{
    internal static class MainThreadStallLogger
    {
        private const double StallThresholdMilliseconds = 75d;
        private const double LogCooldownSeconds = 1d;
        private const int MaxSampleCount = 8;

        private static double _lastLogGameTime = double.NegativeInfinity;
        private static double _lastLoggedMilliseconds;

        public static int StallCount { get; private set; }
        public static string LastStallStage { get; private set; } = "none";
        public static double LastStallMilliseconds { get; private set; }
        public static double StallThresholdMs => StallThresholdMilliseconds;
        public static string LastStallSourceSummary { get; private set; } = "none";

        public static void ReportIfStalled(string stage, double elapsedMilliseconds)
        {
            if (!double.IsFinite(elapsedMilliseconds) || elapsedMilliseconds < StallThresholdMilliseconds)
            {
                return;
            }

            StallCount++;
            LastStallStage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage.Trim();
            LastStallMilliseconds = elapsedMilliseconds;
            LastStallSourceSummary = BuildSourceSummary();

            double now = Core.GAMETIME;
            bool cooldownActive = double.IsFinite(_lastLogGameTime) && now - _lastLogGameTime < LogCooldownSeconds;
            bool materiallyWorse = elapsedMilliseconds >= Math.Max(StallThresholdMilliseconds, _lastLoggedMilliseconds * 1.35d);
            if (cooldownActive && !materiallyWorse)
            {
                return;
            }

            _lastLogGameTime = now;
            _lastLoggedMilliseconds = elapsedMilliseconds;

            DebugLogger.PrintWarning(
                $"Main thread stall detected: stage={LastStallStage} elapsed={elapsedMilliseconds:0.0}ms " +
                $"frame={PerformanceTracker.FrameTimeMilliseconds:0.0}ms fps={PerformanceTracker.FramesPerSecond:0.0} " +
                $"sources=[{LastStallSourceSummary}] " +
                $"terrain={GameBlockTerrainBackground.TerrainVisibleCoverageStatus} " +
                $"chunks={GameBlockTerrainBackground.TerrainAppliedVisualChunkWindow}");
        }

        private static string BuildSourceSummary()
        {
            ProfileEntry[] entries = FrameProfiler.GetEntries();
            if (entries == null || entries.Length == 0)
            {
                return "no profiler samples";
            }

            Array.Sort(entries, (a, b) => b.LastMs.CompareTo(a.LastMs));

            StringBuilder builder = new();
            int appended = 0;
            for (int i = 0; i < entries.Length && appended < MaxSampleCount; i++)
            {
                ProfileEntry entry = entries[i];
                if (entry.LastMs <= 0.01d)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                string owner = string.IsNullOrWhiteSpace(entry.ParentScript) ? "unknown" : entry.ParentScript;
                builder.Append(owner);
                builder.Append('.');
                builder.Append(entry.FunctionName);
                builder.Append('=');
                builder.Append(entry.LastMs.ToString("0.0"));
                builder.Append("ms");
                appended++;
            }

            return builder.Length > 0 ? builder.ToString() : "no current-frame profiler samples";
        }
    }
}
