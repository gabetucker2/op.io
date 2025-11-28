using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class SystemSpecsProvider
    {
        public static IReadOnlyList<SystemSpec> GetSpecs()
        {
            List<SystemSpec> specs = new();

            double fps = PerformanceTracker.FramesPerSecond;
            specs.Add(new SystemSpec("fps", "FPS", fps > 0d ? fps.ToString("0.0") : "0.0"));

            double frameMs = PerformanceTracker.FrameTimeMilliseconds;
            specs.Add(new SystemSpec("frame_time", "Frame Time", frameMs > 0d ? $"{frameMs:0.0} ms" : "--"));

            Core core = Core.Instance;
            if (core != null)
            {
                specs.Add(new SystemSpec("target_fps", "Target FPS", core.TargetFrameRate.ToString()));
                specs.Add(new SystemSpec("window_mode", "Window Mode", core.WindowMode.ToString()));
                specs.Add(new SystemSpec("vsync", "VSync", core.VSyncEnabled ? "On" : "Off", isBoolean: true, boolValue: core.VSyncEnabled));
                specs.Add(new SystemSpec("fixed_time", "Fixed Time Step", core.UseFixedTimeStep ? "On" : "Off", isBoolean: true, boolValue: core.UseFixedTimeStep));
            }

            var windowBounds = core?.Window?.ClientBounds;
            if (windowBounds != null)
            {
                specs.Add(new SystemSpec("window_size", "Window", $"{windowBounds.Value.Width} x {windowBounds.Value.Height}"));
            }

            GraphicsDevice graphicsDevice = core?.GraphicsDevice;
            PresentationParameters presentation = graphicsDevice?.PresentationParameters;
            if (presentation != null)
            {
                specs.Add(new SystemSpec("backbuffer", "Backbuffer", $"{presentation.BackBufferWidth} x {presentation.BackBufferHeight}"));
                specs.Add(new SystemSpec("surface_format", "Surface", presentation.BackBufferFormat.ToString()));
                specs.Add(new SystemSpec("depth_format", "Depth", presentation.DepthStencilFormat.ToString()));
            }

            if (graphicsDevice != null)
            {
                specs.Add(new SystemSpec("graphics_profile", "Graphics Profile", graphicsDevice.GraphicsProfile.ToString()));
                string adapterLabel = graphicsDevice.Adapter?.Description;
                if (!string.IsNullOrWhiteSpace(adapterLabel))
                {
                    specs.Add(new SystemSpec("adapter", "Adapter", adapterLabel));
                }
            }

            specs.Add(new SystemSpec("cpu_threads", "CPU Threads", Environment.ProcessorCount.ToString()));

            try
            {
                long workingSet = Process.GetCurrentProcess().WorkingSet64;
                specs.Add(new SystemSpec("process_memory", "Process Memory", FormatBytes(workingSet)));
            }
            catch
            {
                // Ignore failures when processes cannot be queried
            }

            long managedBytes = GC.GetTotalMemory(false);
            specs.Add(new SystemSpec("managed_memory", "Managed Heap", FormatBytes(managedBytes)));

            specs.Add(new SystemSpec("os", "OS", Environment.OSVersion.VersionString));

            return specs;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            double kb = bytes / 1024d;
            if (kb < 1024d)
            {
                return $"{kb:0.0} KB";
            }

            double mb = kb / 1024d;
            if (mb < 1024d)
            {
                return $"{mb:0.0} MB";
            }

            double gb = mb / 1024d;
            return $"{gb:0.00} GB";
        }

    }

    public readonly struct SystemSpec
    {
        public SystemSpec(string key, string label, string value, bool isBoolean = false, bool boolValue = false)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            IsBoolean = isBoolean;
            BoolValue = boolValue;
        }

        public string Key { get; }
        public string Label { get; }
        public string Value { get; }
        public bool IsBoolean { get; }
        public bool BoolValue { get; }
    }
}
