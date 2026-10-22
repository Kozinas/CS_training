using System.Collections.Concurrent;
using System.Globalization;
using Course.AsyncLab;

namespace Course.PipelineChecks;

internal static partial class Suite
{
    private static async Task CheckAsyncPatterns()
    {
        var signal = Signal();
        int before = 0, after = 0;
        async Task<int> Operation() { before++; await signal.Task; after++; return 9; }
        Task<int> task = Operation();
        Check(before == 1 && after == 0 && !task.IsCompleted, "synchronous prefix, incomplete await");
        signal.SetResult();
        Check(await task == 9 && after == 1, "continuation result");
        async Task<int> EarlyFailure() { if (before > 0) throw new FormatException(); await Task.Yield(); return 0; }
        Task<int> early = EarlyFailure();
        Check(early.IsFaulted, "async prefix exception captured in Task");
        await Throws<FormatException>(async () => await early, "early async error");
        var observed = await AsyncPatterns.ObserveAll<int>([
            _ => Task.FromResult(7), _ => throw new ArgumentException(),
            async _ => { await Task.Yield(); throw new IOException(); }]);
        Check(observed[0].Value == 7 && observed[1].Error is ArgumentException
            && observed[2].Error is IOException, "all errors retained in input order");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var outcomes = await AsyncPatterns.ObserveAll<int>([ct => Task.FromCanceled<int>(ct)], cancelled.Token);
        Check(outcomes[0].Cancelled, "cooperative cancellation classified");
        var all = Task.WhenAll(Task.FromException(new FormatException()), Task.FromException(new IOException()));
        try { await all; } catch (Exception) { }
        Check(all.Exception!.InnerExceptions.Count == 2, "WhenAll retains both exceptions");
        var other = Signal();
        Task winner = await Task.WhenAny(Task.FromException(new FormatException()), other.Task);
        await Throws<FormatException>(async () => await winner, "WhenAny winner must be awaited");
        Check(!other.Task.IsCompleted, "WhenAny does not cancel loser");
        other.SetResult(); await other.Task;
        var underlying = Signal();
        await Throws<OperationCanceledException>(async () => await underlying.Task.WaitAsync(cancelled.Token), "cancel wait");
        Check(!underlying.Task.IsCompleted, "cancel wait does not cancel work");
        underlying.SetResult(); await underlying.Task;
        bool disposed = false;
        await foreach (int i in AsyncPatterns.Numbers(10, () => disposed = true)) { Check(i == 0, "first value"); break; }
        Check(disposed, "early break disposes async iterator");
        disposed = false;
        await Throws<OperationCanceledException>(async () =>
        { await foreach (int _ in AsyncPatterns.Numbers(10, () => disposed = true).WithCancellation(cancelled.Token)) { } },
            "WithCancellation reaches iterator");
        Check(disposed, "cancelled iterator finally");
        bool asyncDisposed = false;
        await using (var resource = new AsyncResource(() => asyncDisposed = true)) { }
        Check(asyncDisposed, "await using waits for cleanup");
        ValueTask<int> value = ValueTask.FromResult(42);
        Task<int> shareable = value.AsTask();
        Check(await shareable == 42 && await shareable == 42, "convert ValueTask once, share Task");

        var context = new QueueContext();
        var gate = Signal();
        var old = SynchronizationContext.Current;
        Task captured;
        SynchronizationContext.SetSynchronizationContext(context);
        try { captured = Capture(); }
        finally { SynchronizationContext.SetSynchronizationContext(old); }
        gate.SetResult();
        await context.Posted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Check(!captured.IsCompleted, "captured continuation awaits context pump");
        context.Pump(); await captured;
        async Task Capture() { await gate.Task; }
        var gate2 = Signal();
        Task free;
        SynchronizationContext.SetSynchronizationContext(context);
        try { free = NoCapture(); }
        finally { SynchronizationContext.SetSynchronizationContext(old); }
        gate2.SetResult(); await free.WaitAsync(TimeSpan.FromSeconds(8));
        Check(context.Posts == 1, "ConfigureAwait(false) bypasses this context");
        async Task NoCapture() { await gate2.Task.ConfigureAwait(false); }
        using (var timer = new PeriodicTimer(TimeSpan.FromHours(1)))
        {
            Task<bool> tick = timer.WaitForNextTickAsync().AsTask();
            timer.Dispose();
            Check(!await tick, "disposing timer ends pending wait without a tick");
        }
        using (var timer = new PeriodicTimer(TimeSpan.FromHours(1)))
            await Throws<OperationCanceledException>(async () => await timer.WaitForNextTickAsync(cancelled.Token),
                "periodic wait accepts cancellation");
        var ambient = new AsyncLocal<string?> { Value = "parent" };
        string? child = await Task.Run(async () =>
        { Check(ambient.Value == "parent", "ExecutionContext flows"); ambient.Value = "child"; await Task.Yield(); return ambient.Value; });
        Check(child == "child" && ambient.Value == "parent", "AsyncLocal logical flow isolation");
    }

    private static async Task CheckSynchronization()
    {
        Check(SharedState.LostUpdate() == 1, "controlled lost update");
        Check(await SharedState.AtomicCount(8, 2000) == 16000, "Interlocked updates");
        var counter = new LockedCounter();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        { for (int i = 0; i < 2000; i++) counter.Increment(); })));
        Check(counter.Value == 16000, "System.Threading.Lock updates");
        using var semaphore = new SemaphoreSlim(1, 1);
        await semaphore.WaitAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Throws<OperationCanceledException>(() => semaphore.WaitAsync(cancellation.Token), "cancel before acquisition");
        Check(semaphore.CurrentCount == 0, "cancel did not acquire/release permit");
        semaphore.Release();
        await semaphore.WaitAsync();
        try { throw new FormatException(); } catch (FormatException) { } finally { semaphore.Release(); }
        Check(semaphore.CurrentCount == 1, "release in finally");
        var cache = new ConcurrentDictionary<int, int>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => cache.TryAdd(1, i))));
        Check(cache.Count == 1, "atomic TryAdd");
        foreach (string culture in new[] { "ru-RU", "en-US", "tr-TR" })
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                string[] rows = ["2026-01-01;a;0.01", "2026-01-01;b;999999.99", "2026-01-01;c;12.50"];
                Check(AmountAlgorithms.SplitSum(rows) == 1_000_012.50m, "baseline invariant");
                Check(AmountAlgorithms.SpanSum(rows) == AmountAlgorithms.SplitSum(rows), "optimization preserves sum");
                Check(AmountAlgorithms.SpanSum([]) == 0 && AmountAlgorithms.SplitSum([]) == 0, "empty sum");
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }

    private sealed class AsyncResource(Action disposed) : IAsyncDisposable
    { public async ValueTask DisposeAsync() { await Task.Yield(); disposed(); } }
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
        public TaskCompletionSource Posted { get; } = Signal();
        public int Posts;
        public override void Post(SendOrPostCallback callback, object? state)
        { queue.Enqueue((callback, state)); Interlocked.Increment(ref Posts); Posted.TrySetResult(); }
        public void Pump() { while (queue.TryDequeue(out var work)) work.Callback(work.State); }
    }
}
