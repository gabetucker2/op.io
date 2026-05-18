using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace op.io
{
    internal static class BlockAsyncLoadManager
    {
        private sealed class BlockWorker
        {
            public readonly DockBlockKind Kind;
            public readonly object Sync = new();
            public readonly AutoResetEvent Signal = new(false);
            public Thread Thread;
            public bool HasPending;
            public long PendingId;
            public string PendingKey = string.Empty;
            public string PendingReason = string.Empty;
            public Func<CancellationToken, object> PendingWork;
            public bool IsRunning;
            public long RunningId;
            public string RunningKey = string.Empty;
            public string RunningReason = string.Empty;
            public CancellationTokenSource RunningCancellation;
            public bool HasCompleted;
            public long CompletedId;
            public string CompletedKey = string.Empty;
            public object CompletedResult;
            public Exception CompletedException;
            public double LastDurationMilliseconds;
            public string LatestStatusLine = "idle";
            public string LastPrintedStatusLine = string.Empty;
            public bool ExternalLoading;
            public string ExternalStatusLine = string.Empty;

            public BlockWorker(DockBlockKind kind)
            {
                Kind = kind;
            }
        }

        private static readonly int MaxConcurrentBlockLoadCount = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
        private static readonly SemaphoreSlim CapacitySemaphore = new(MaxConcurrentBlockLoadCount, MaxConcurrentBlockLoadCount);
        private static readonly object WorkersSync = new();
        private static readonly Dictionary<DockBlockKind, BlockWorker> Workers = new();
        private static long _nextRequestId;

        public static int MaxConcurrentBlockLoads => MaxConcurrentBlockLoadCount;
        public static int ActiveBlockLoadCount => CountWorkers(worker => worker.IsRunning);
        public static int PendingBlockLoadCount => CountWorkers(worker => worker.HasPending);
        public static string StatusSummary => BuildStatusSummary();

        public static long QueueLatest(
            DockBlockKind kind,
            string workKey,
            string reason,
            Func<CancellationToken, object> work)
        {
            if (work == null)
            {
                return 0;
            }

            BlockWorker worker = GetOrCreateWorker(kind);
            long requestId = Interlocked.Increment(ref _nextRequestId);
            lock (worker.Sync)
            {
                worker.PendingId = requestId;
                worker.PendingKey = string.IsNullOrWhiteSpace(workKey) ? "default" : workKey.Trim();
                worker.PendingReason = string.IsNullOrWhiteSpace(reason) ? "loading" : reason.Trim();
                worker.PendingWork = work;
                worker.HasPending = true;
                worker.RunningCancellation?.Cancel();
                RecordStatusLocked(worker, $"queued {worker.PendingKey}: {worker.PendingReason}");
            }

            worker.Signal.Set();
            return requestId;
        }

        public static bool TryTakeCompleted<T>(
            DockBlockKind kind,
            string workKey,
            out T result,
            out Exception error,
            out long requestId)
        {
            result = default;
            error = null;
            requestId = 0;

            BlockWorker worker = GetExistingWorker(kind);
            if (worker == null)
            {
                return false;
            }

            string normalizedKey = string.IsNullOrWhiteSpace(workKey) ? "default" : workKey.Trim();
            lock (worker.Sync)
            {
                if (!worker.HasCompleted ||
                    !string.Equals(worker.CompletedKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                requestId = worker.CompletedId;
                error = worker.CompletedException;
                if (error == null && worker.CompletedResult is T typed)
                {
                    result = typed;
                }

                worker.HasCompleted = false;
                worker.CompletedId = 0;
                worker.CompletedKey = string.Empty;
                worker.CompletedResult = null;
                worker.CompletedException = null;
                return true;
            }
        }

        public static bool IsBlockLoading(DockBlockKind kind)
        {
            BlockWorker worker = GetExistingWorker(kind);
            if (worker == null)
            {
                return false;
            }

            lock (worker.Sync)
            {
                return worker.HasPending || worker.IsRunning || worker.ExternalLoading;
            }
        }

        public static string GetBlockStatus(DockBlockKind kind)
        {
            BlockWorker worker = GetExistingWorker(kind);
            if (worker == null)
            {
                return "idle";
            }

            lock (worker.Sync)
            {
                if (worker.IsRunning)
                {
                    return $"running {worker.RunningKey}: {worker.RunningReason}";
                }

                if (worker.HasPending)
                {
                    return $"pending {worker.PendingKey}: {worker.PendingReason}";
                }

                if (worker.ExternalLoading)
                {
                    return worker.ExternalStatusLine;
                }

                return worker.LastDurationMilliseconds > 0d
                    ? $"idle last={worker.LastDurationMilliseconds:0.0}ms"
                    : "idle";
            }
        }

        public static void SetExternalLoading(DockBlockKind kind, bool isLoading, string statusLine)
        {
            BlockWorker worker = GetOrCreateWorker(kind);
            lock (worker.Sync)
            {
                worker.ExternalLoading = isLoading;
                worker.ExternalStatusLine = isLoading
                    ? (string.IsNullOrWhiteSpace(statusLine) ? "loading" : statusLine.Trim())
                    : string.Empty;
                if (isLoading)
                {
                    RecordStatusLocked(worker, worker.ExternalStatusLine);
                }
            }
        }

        public static string GetBlockLoadingLine(DockBlockKind kind)
        {
            BlockWorker worker = GetExistingWorker(kind);
            if (worker == null)
            {
                return string.Empty;
            }

            lock (worker.Sync)
            {
                if (worker.ExternalLoading)
                {
                    return worker.ExternalStatusLine;
                }

                return worker.HasPending || worker.IsRunning
                    ? worker.LatestStatusLine
                    : string.Empty;
            }
        }

        private static BlockWorker GetOrCreateWorker(DockBlockKind kind)
        {
            lock (WorkersSync)
            {
                if (Workers.TryGetValue(kind, out BlockWorker existing))
                {
                    return existing;
                }

                BlockWorker worker = new(kind);
                worker.Thread = new Thread(() => WorkerLoop(worker))
                {
                    IsBackground = true,
                    Name = $"BlockLoad.{kind}"
                };
                Workers[kind] = worker;
                worker.Thread.Start();
                return worker;
            }
        }

        private static BlockWorker GetExistingWorker(DockBlockKind kind)
        {
            lock (WorkersSync)
            {
                Workers.TryGetValue(kind, out BlockWorker worker);
                return worker;
            }
        }

        private static void WorkerLoop(BlockWorker worker)
        {
            while (true)
            {
                worker.Signal.WaitOne();

                while (TryTakePendingWork(
                    worker,
                    out long requestId,
                    out string workKey,
                    out string reason,
                    out Func<CancellationToken, object> work,
                    out CancellationTokenSource cancellation))
                {
                    bool enteredCapacity = false;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    object result = null;
                    Exception error = null;

                    try
                    {
                        RecordStatus(worker, $"waiting for capacity {workKey}: {reason}");
                        CapacitySemaphore.Wait(cancellation.Token);
                        enteredCapacity = true;
                        RecordStatus(worker, $"loading {workKey}: {reason}");
                        result = work(cancellation.Token);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        error = null;
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        stopwatch.Stop();
                        if (enteredCapacity)
                        {
                            CapacitySemaphore.Release();
                        }

                        CompleteWork(worker, requestId, workKey, result, error, stopwatch.Elapsed.TotalMilliseconds, cancellation);
                        cancellation.Dispose();
                    }
                }
            }
        }

        private static bool TryTakePendingWork(
            BlockWorker worker,
            out long requestId,
            out string workKey,
            out string reason,
            out Func<CancellationToken, object> work,
            out CancellationTokenSource cancellation)
        {
            requestId = 0;
            workKey = string.Empty;
            reason = string.Empty;
            work = null;
            cancellation = null;

            lock (worker.Sync)
            {
                if (!worker.HasPending || worker.PendingWork == null)
                {
                    return false;
                }

                requestId = worker.PendingId;
                workKey = worker.PendingKey;
                reason = worker.PendingReason;
                work = worker.PendingWork;
                worker.PendingWork = null;
                worker.HasPending = false;

                cancellation = new CancellationTokenSource();
                worker.RunningCancellation = cancellation;
                worker.RunningId = requestId;
                worker.RunningKey = workKey;
                worker.RunningReason = reason;
                worker.IsRunning = true;
                RecordStatusLocked(worker, $"starting {workKey}: {reason}");
                return true;
            }
        }

        private static void CompleteWork(
            BlockWorker worker,
            long requestId,
            string workKey,
            object result,
            Exception error,
            double elapsedMilliseconds,
            CancellationTokenSource cancellation)
        {
            lock (worker.Sync)
            {
                bool canceled = cancellation.IsCancellationRequested;
                if (!canceled)
                {
                    worker.HasCompleted = true;
                    worker.CompletedId = requestId;
                    worker.CompletedKey = workKey;
                    worker.CompletedResult = result;
                    worker.CompletedException = error;
                }

                worker.LastDurationMilliseconds = elapsedMilliseconds;
                if (canceled)
                {
                    RecordStatusLocked(worker, $"canceled {workKey}: superseded by newer block load");
                }
                else if (error != null)
                {
                    RecordStatusLocked(worker, $"failed {workKey}: {error.GetType().Name}");
                }
                else
                {
                    RecordStatusLocked(worker, $"loaded {workKey}: {elapsedMilliseconds:0.0}ms");
                }

                if (worker.RunningId == requestId)
                {
                    worker.IsRunning = false;
                    worker.RunningId = 0;
                    worker.RunningKey = string.Empty;
                    worker.RunningReason = string.Empty;
                }

                if (ReferenceEquals(worker.RunningCancellation, cancellation))
                {
                    worker.RunningCancellation = null;
                }
            }
        }

        private static void RecordStatus(BlockWorker worker, string statusLine)
        {
            lock (worker.Sync)
            {
                RecordStatusLocked(worker, statusLine);
            }
        }

        private static void RecordStatusLocked(BlockWorker worker, string statusLine)
        {
            if (worker == null)
            {
                return;
            }

            string normalized = string.IsNullOrWhiteSpace(statusLine) ? "loading" : statusLine.Trim();
            worker.LatestStatusLine = normalized;
            if (string.Equals(worker.LastPrintedStatusLine, normalized, StringComparison.Ordinal))
            {
                return;
            }

            worker.LastPrintedStatusLine = normalized;
            DebugLogger.PrintUI($"BlockLoad {worker.Kind}: {normalized}");
        }

        private static int CountWorkers(Func<BlockWorker, bool> predicate)
        {
            int count = 0;
            lock (WorkersSync)
            {
                foreach (BlockWorker worker in Workers.Values)
                {
                    lock (worker.Sync)
                    {
                        if (predicate(worker))
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private static string BuildStatusSummary()
        {
            StringBuilder builder = new();
            lock (WorkersSync)
            {
                foreach (BlockWorker worker in Workers.Values)
                {
                    string status;
                    lock (worker.Sync)
                    {
                        if (!worker.IsRunning && !worker.HasPending)
                        {
                            if (!worker.ExternalLoading)
                            {
                                continue;
                            }

                            status = $"{worker.Kind}:{worker.ExternalStatusLine}";
                        }
                        else
                        {
                            status = worker.IsRunning
                                ? $"{worker.Kind}:{worker.RunningKey}"
                                : $"{worker.Kind}:{worker.PendingKey} pending";
                        }
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append("; ");
                    }
                    builder.Append(status);
                }
            }

            return builder.Length > 0 ? builder.ToString() : "idle";
        }
    }
}
