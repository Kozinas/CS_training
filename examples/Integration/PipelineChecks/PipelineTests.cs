using System.Text;
using System.Text.Json;
using Course.Import;
using Course.Pipeline;

namespace Course.PipelineChecks;

internal static partial class Suite
{
    private static byte[] Row(int id, string amount = "1.00") => JsonSerializer.SerializeToUtf8Bytes(
        new RawExpense(new Guid(id, 0, 0, new byte[8]).ToString("D"), "2026-01-10", "food", amount),
        PipelineJsonContext.Default.RawExpense);
    private static MemoryStream Rows(params byte[][] rows) => new(rows.SelectMany(row => row.Append((byte)10)).ToArray());
    private static async Task<PipelineResult> RunRows(byte[][] rows, PipelineOptions? options = null,
        IRowProcessor? processor = null)
    {
        using var input = Rows(rows);
        return await new ImportPipeline(processor ?? new JsonRowProcessor(), options ?? new()).RunAsync(input, [])
            .WaitAsync(TimeSpan.FromSeconds(8));
    }

    private static async Task CheckPipeline()
    {
        byte[][] mixed = [Row(1), Row(2), Row(1), Row(1, "2.00"), Encoding.UTF8.GetBytes("bad"), Row(3, "1.234")];
        for (int workers = 1; workers <= 4; workers++)
        foreach (int capacity in new[] { 1, 8 })
        foreach (int window in new[] { 1, 3, 16 })
        {
            var result = await RunRows(mixed, new() { Workers = workers, Capacity = capacity, Window = window });
            Check(result.Report is { Processed: 6, Added: 2, Duplicates: 1, Failed: 3, Total: 2, Retried: 0 }, "invariant report");
            Check(result.Report.Errors.Select(e => e.Code).SequenceEqual(new[] { "id_conflict", "invalid_json", "invalid_amount" }), "ordered errors");
            Check(result.Records[0].Amount.Amount == 1m, "first input wins conflict");
        }
        var truncated = await RunRows(Enumerable.Repeat(Encoding.UTF8.GetBytes("bad"), 100).ToArray(), new() { MaxErrors = 3 });
        Check(truncated.Report is { Failed: 100, ErrorsTruncated: true } && truncated.Report.Errors.Length == 3, "bounded error sample");
        Check((await RunRows([], new())).Report.Processed == 0, "empty input");
        var parser = new JsonRowProcessor();
        foreach (byte[] invalid in new byte[][] { [255], "[]"u8.ToArray(), "{\"id\":\"a\",\"id\":\"b\"}"u8.ToArray(),
            "{\"extra\":1}"u8.ToArray() })
            Check((await parser.ProcessAsync(invalid, default)).Error == "invalid_json", "strict row JSON");
        Check((await parser.ProcessAsync("null"u8.ToArray(), default)).Error == "invalid_id", "null row");
        using (var raw = new ShortReadStream([0xef, 0xbb, 0xbf, 97, 13, 10, 98]))
        {
            var lines = new List<string>();
            await foreach (var line in BoundedLines.ReadAsync(raw, new())) lines.Add(Encoding.UTF8.GetString(line));
            Check(lines.SequenceEqual(new[] { "a", "b" }), "short reads BOM CRLF and last line");
            Check(raw.CanRead, "reader leaves caller stream open");
        }
        Check((await RunRows([[]])).Report.Failed == 1, "blank line is rejected, not skipped");
        await Throws<InvalidDataException>(() => RunRows([Row(1)], new() { MaxLineBytes = 8 }), "line bound");
        await Throws<InvalidDataException>(() => RunRows([Row(1)], new() { MaxInputBytes = 8 }), "input byte bound");
        await Throws<InvalidDataException>(() => RunRows([Row(1), Row(2)], new() { MaxLines = 1 }), "line count bound");
        await Throws<InvalidDataException>(() => RunRows([Row(1), Row(2)], new() { MaxRecords = 1 }), "state bound");
        await Throws<ArgumentOutOfRangeException>(() => RunRows([], new() { Workers = 0 }), "options validated");

        // Four admitted lines. Hold line 1; the other three finish before it.
        var firstStarted = Signal(); var releaseFirst = Signal(); var laterFinished = Signal();
        int started = 0, later = 0, active = 0, peak = 0;
        var controlled = new DelegateProcessor(async (bytes, token) =>
        {
            Interlocked.Increment(ref started);
            int running = Interlocked.Increment(ref active);
            int old;
            do { old = Volatile.Read(ref peak); if (running <= old) break; }
            while (Interlocked.CompareExchange(ref peak, running, old) != old);
            try
            {
                if (bytes.Span.SequenceEqual(Row(1)))
                { firstStarted.SetResult(); await releaseFirst.Task.WaitAsync(token); }
                else if (Interlocked.Increment(ref later) == 3) laterFinished.SetResult();
                return await parser.ProcessAsync(bytes, token);
            }
            finally { Interlocked.Decrement(ref active); }
        });
        using (var input = Rows(new[] { Row(1), Row(1, "2.00") }
            .Concat(Enumerable.Range(2, 28).Select(i => Row(i))).ToArray()))
        {
            Task<PipelineResult> work = new ImportPipeline(controlled, new() { Workers = 4, Capacity = 1, Window = 4 }).RunAsync(input, []);
            try
            {
                await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(8));
                await laterFinished.Task.WaitAsync(TimeSpan.FromSeconds(8));
                Check(Volatile.Read(ref started) == 4 && !work.IsCompleted, "admission window blocks later input");
            }
            finally { releaseFirst.TrySetResult(); }
            var result = await work.WaitAsync(TimeSpan.FromSeconds(8));
            Check(result.Report is { Processed: 30, Added: 29, Failed: 1 } && peak <= 4 && active == 0,
                "bounded workers and complete join");
            Check(result.Records[0].Amount.Amount == 1m && result.Report.Errors[0] == new RowError(2, "id_conflict"),
                "slow first row still wins over faster conflicting row");
        }
        int attempts = 0;
        var transient = new DelegateProcessor((bytes, token) =>
        {
            if (Interlocked.Increment(ref attempts) <= 2) throw new TransientWorkException("retry");
            return parser.ProcessAsync(bytes, token);
        });
        var retry = await RunRows([Row(1)], new() { RetryDelay = TimeSpan.Zero }, transient);
        Check(retry.Report is { Added: 1, Retried: 2 } && attempts == 3, "bounded transient retry");
        attempts = 0;
        var poison = new DelegateProcessor((_, _) => { Interlocked.Increment(ref attempts); throw new TransientWorkException("poison"); });
        var exhausted = await RunRows([Row(1)], new() { RetryDelay = TimeSpan.Zero }, poison);
        Check(exhausted.Report is { Added: 0, Retried: 2, Failed: 1 } && attempts == 3
            && exhausted.Report.Errors[0].Code == "retry_exhausted", "poison cannot loop forever");

        var attempted = Signal(); int retryCalls = 0;
        var waitRetry = new DelegateProcessor((_, _) =>
        { Interlocked.Increment(ref retryCalls); attempted.TrySetResult(); throw new TransientWorkException("wait"); });
        using (var stopRetry = new CancellationTokenSource())
        using (var retryInput = Rows(Row(1)))
        {
            var pendingRetry = new ImportPipeline(waitRetry, new() { RetryDelay = TimeSpan.FromSeconds(5) })
                .RunAsync(retryInput, [], stopRetry.Token);
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(8)); stopRetry.Cancel();
            await Throws<OperationCanceledException>(async () => await pendingRetry, "cancel retry delay");
            Check(retryCalls == 1, "no retry after cancellation");
        }

        var entered = Signal(); int inFlight = 0;
        var blocked = new DelegateProcessor(async (_, token) =>
        {
            Interlocked.Increment(ref inFlight); entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); return new(null, "unreachable"); }
            finally { Interlocked.Decrement(ref inFlight); }
        });
        using (var cancel = new CancellationTokenSource())
        using (var input = Rows(Enumerable.Range(1, 30).Select(i => Row(i)).ToArray()))
        {
            Task<PipelineResult> running = new ImportPipeline(blocked, new() { Workers = 2, Capacity = 1, Window = 3 })
                .RunAsync(input, [], cancel.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(8)); cancel.Cancel();
            await Throws<OperationCanceledException>(async () => await running, "cancel blocked queues");
            Check(inFlight == 0, "all processors exited before cancellation returned");
        }
        var failureGate = Signal(); int survivors = 0;
        var crashing = new DelegateProcessor(async (bytes, token) =>
        {
            if (bytes.Span.SequenceEqual(Row(1)))
            { await failureGate.Task.WaitAsync(token); throw new IOException("injected"); }
            Interlocked.Increment(ref survivors); failureGate.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); return new(null, "unreachable"); }
            finally { Interlocked.Decrement(ref survivors); }
        });
        await Throws<IOException>(() => RunRows([Row(1), Row(2), Row(3)], new() { Workers = 2, Capacity = 1, Window = 2 }, crashing), "fatal worker cancels peers");
        Check(survivors == 0, "peer finally before fatal error propagated");
        using var broken = new BrokenReadStream();
        await Throws<IOException>(() => new ImportPipeline(parser, new()).RunAsync(broken, []), "producer failure joins workers");
    }

    private sealed class DelegateProcessor(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<ParsedExpense>> action) : IRowProcessor
    { public ValueTask<ParsedExpense> ProcessAsync(ReadOnlyMemory<byte> line, CancellationToken token) => action(line, token); }
    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
    private sealed class BrokenReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("injected read failure"));
    }
}
