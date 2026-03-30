using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace op.io
{
    public static class RuntimeErrorLogger
    {
        private static bool _initialized;

        private const string Tag = "[CRASHLOGS]";
        private const string Separator = "============================================================";

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _initialized = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception ??
                new Exception($"Unhandled exception object: {args.ExceptionObject}");

            LogException("UnhandledException", exception, args.IsTerminating);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            LogException("UnobservedTaskException", args.Exception, isTerminating: false);
            args.SetObserved();
        }

        private static void LogException(string source, Exception exception, bool isTerminating)
        {
            string payload = BuildPayload(source, exception, isTerminating);

            try
            {
                LogFileHandler.AppendLog(payload);
            }
            catch
            {
                // Do not let logging failures hide the original crash.
            }

            try
            {
                Console.Error.WriteLine(payload);
            }
            catch
            {
                // If console output is unavailable, we still attempted to persist the error to disk.
            }
        }

        private static string BuildPayload(string source, Exception exception, bool isTerminating)
        {
            StringBuilder b = new();
            string ts = DateTime.Now.ToString("O");

            b.AppendLine($"{Tag} {Separator}");
            b.AppendLine($"{Tag} CRASH DETECTED at {ts}");
            b.AppendLine($"{Tag} Source: {source} | Terminating: {isTerminating}");

            if (exception == null)
            {
                b.AppendLine($"{Tag} No exception information available.");
                b.Append($"{Tag} {Separator}");
                return b.ToString();
            }

            AppendExceptionChain(b, exception, depth: 0);
            AppendThreadInfo(b);
            AppendRuntimeInfo(b);
            AppendGameState(b);
            AppendLoadedAssemblies(b);

            b.Append($"{Tag} {Separator}");
            return b.ToString();
        }

        private static void AppendExceptionChain(StringBuilder b, Exception ex, int depth)
        {
            string indent = depth == 0 ? "" : $"  (inner x{depth}) ";

            b.AppendLine($"{Tag} --- {(depth == 0 ? "Exception" : "Inner Exception")} ---");
            b.AppendLine($"{Tag} {indent}Type:    {ex.GetType().FullName}");
            b.AppendLine($"{Tag} {indent}Message: {ex.Message}");

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                b.AppendLine($"{Tag} {indent}Stack Trace:");
                foreach (string line in ex.StackTrace.Split('\n'))
                {
                    string trimmed = line.TrimEnd('\r');
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        b.AppendLine($"{Tag} {indent}  {trimmed.TrimStart()}");
                    }
                }
            }
            else
            {
                b.AppendLine($"{Tag} {indent}Stack Trace: (unavailable)");
            }

            if (ex.InnerException != null)
            {
                AppendExceptionChain(b, ex.InnerException, depth + 1);
            }
            else if (ex is AggregateException agg)
            {
                foreach (Exception inner in agg.InnerExceptions)
                {
                    AppendExceptionChain(b, inner, depth + 1);
                }
            }
        }

        private static void AppendThreadInfo(StringBuilder b)
        {
            try
            {
                Thread t = Thread.CurrentThread;
                b.AppendLine($"{Tag} --- Thread ---");
                b.AppendLine($"{Tag} Managed Thread ID: {t.ManagedThreadId}");
                b.AppendLine($"{Tag} Thread Name:       {(string.IsNullOrEmpty(t.Name) ? "(unnamed)" : t.Name)}");
                b.AppendLine($"{Tag} Is Background:     {t.IsBackground}");
                b.AppendLine($"{Tag} Thread State:      {t.ThreadState}");
                b.AppendLine($"{Tag} Apartment State:   {t.GetApartmentState()}");
            }
            catch
            {
                b.AppendLine($"{Tag} --- Thread --- (unavailable)");
            }
        }

        private static void AppendRuntimeInfo(StringBuilder b)
        {
            try
            {
                b.AppendLine($"{Tag} --- Runtime & Process ---");
                b.AppendLine($"{Tag} CLR Version:    {Environment.Version}");
                b.AppendLine($"{Tag} OS:             {Environment.OSVersion}");
                b.AppendLine($"{Tag} CPU Threads:    {Environment.ProcessorCount}");
                b.AppendLine($"{Tag} 64-bit Process: {Environment.Is64BitProcess}");
                b.AppendLine($"{Tag} Machine Name:   {Environment.MachineName}");

                try
                {
                    Process proc = Process.GetCurrentProcess();
                    b.AppendLine($"{Tag} Process Memory: {FormatBytes(proc.WorkingSet64)}");
                    b.AppendLine($"{Tag} Private Memory: {FormatBytes(proc.PrivateMemorySize64)}");
                    b.AppendLine($"{Tag} GC Heap:        {FormatBytes(GC.GetTotalMemory(false))}");
                    b.AppendLine($"{Tag} Thread Count:   {proc.Threads.Count}");
                    b.AppendLine($"{Tag} Process Uptime: {(DateTime.Now - proc.StartTime):hh\\:mm\\:ss}");
                }
                catch
                {
                    b.AppendLine($"{Tag} Process info unavailable.");
                }
            }
            catch
            {
                b.AppendLine($"{Tag} --- Runtime & Process --- (unavailable)");
            }
        }

        private static void AppendGameState(StringBuilder b)
        {
            try
            {
                b.AppendLine($"{Tag} --- Game State ---");

                double fps = PerformanceTracker.FramesPerSecond;
                double frameMs = PerformanceTracker.FrameTimeMilliseconds;
                b.AppendLine($"{Tag} FPS:        {(fps > 0 ? fps.ToString("0.0") : "0.0")}");
                b.AppendLine($"{Tag} Frame Time: {(frameMs > 0 ? $"{frameMs:0.0} ms" : "--")}");

                Core core = Core.Instance;
                if (core != null)
                {
                    b.AppendLine($"{Tag} Target FPS: {core.TargetFrameRate}");
                    b.AppendLine($"{Tag} Window:     {core.WindowMode}");
                    b.AppendLine($"{Tag} VSync:      {(core.VSyncEnabled ? "On" : "Off")}");
                }
                else
                {
                    b.AppendLine($"{Tag} Core instance unavailable (crash occurred before/after game lifecycle).");
                }
            }
            catch
            {
                b.AppendLine($"{Tag} --- Game State --- (unavailable)");
            }
        }

        private static void AppendLoadedAssemblies(StringBuilder b)
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                b.AppendLine($"{Tag} --- Loaded Assemblies ({assemblies.Length}) ---");
                foreach (Assembly asm in assemblies)
                {
                    try
                    {
                        b.AppendLine($"{Tag}   {asm.FullName}");
                    }
                    catch
                    {
                        b.AppendLine($"{Tag}   (assembly name unavailable)");
                    }
                }
            }
            catch
            {
                b.AppendLine($"{Tag} --- Loaded Assemblies --- (unavailable)");
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024d;
            if (kb < 1024d) return $"{kb:0.0} KB";
            double mb = kb / 1024d;
            if (mb < 1024d) return $"{mb:0.0} MB";
            return $"{mb / 1024d:0.00} GB";
        }
    }
}
