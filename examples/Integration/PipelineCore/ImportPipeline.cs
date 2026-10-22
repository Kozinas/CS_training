using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Course.Ledger.Core;

namespace Course.Pipeline;

public sealed class ImportPipeline(IRowProcessor processor, PipelineOptions options)
{
    private sealed record Work(int Number, byte[] Bytes);
    private sealed record Done(int Number, ParsedExpense Parsed);

    public async Task<PipelineResult> RunAsync(Stream input, IEnumerable<Expense> existing,
        CancellationToken token = default)
    {
        options.Validate();
        token.ThrowIfCancellationRequested();
        var state = new Dictionary<Guid, Expense>();
        foreach (var expense in existing)
        {
            if (state.Count >= options.MaxRecords || !state.TryAdd(expense.Id, expense))
                throw new InvalidDataException("invalid_existing_state");
        }
        var jobs = Channel.CreateBounded<Work>(new BoundedChannelOptions(options.Capacity)
        { SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
        var results = Channel.CreateBounded<Done>(new BoundedChannelOptions(options.Capacity)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var window = new SemaphoreSlim(options.Window, options.Window);
        ExceptionDispatchInfo? failure = null;
        int processed = 0, added = 0, duplicates = 0, failed = 0, retried = 0;
        var errors = new List<RowError>();

        async Task Guard(Func<Task> action)
        {
            try { await action().ConfigureAwait(false); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(error), null);
                stop.Cancel(); // Wake blocked readers, writers, and the admission window.
                throw;
            }
        }

        async Task Produce()
        {
            try
            {
                int number = 0;
                await foreach (byte[] line in BoundedLines.ReadAsync(input, options, stop.Token))
                {
                    await window.WaitAsync(stop.Token).ConfigureAwait(false);
                    await jobs.Writer.WriteAsync(new Work(++number, line), stop.Token).ConfigureAwait(false);
                }
            }
            finally { jobs.Writer.TryComplete(); }
        }

        async Task WorkAsync()
        {
            await foreach (var job in jobs.Reader.ReadAllAsync(stop.Token))
            {
                ParsedExpense parsed;
                for (int attempt = 1; ; attempt++)
                {
                    try { parsed = await processor.ProcessAsync(job.Bytes, stop.Token).ConfigureAwait(false); break; }
                    catch (TransientWorkException)
                    {
                        if (attempt == options.Attempts)
                        { parsed = new ParsedExpense(null, "retry_exhausted"); break; }
                        Interlocked.Increment(ref retried);
                        await Task.Delay(options.RetryDelay, stop.Token).ConfigureAwait(false);
                    }
                }
                await results.Writer.WriteAsync(new Done(job.Number, parsed), stop.Token).ConfigureAwait(false);
            }
        }

        async Task Collect()
        {
            var pending = new Dictionary<int, ParsedExpense>();
            int next = 1;
            await foreach (var result in results.Reader.ReadAllAsync(stop.Token))
            {
                pending.Add(result.Number, result.Parsed);
                while (pending.Remove(next, out var parsed))
                {
                    stop.Token.ThrowIfCancellationRequested();
                    string? error = parsed.Error;
                    if (error is null)
                    {
                        var expense = parsed.Expense ?? throw new InvalidOperationException("processor_contract");
                        if (state.TryGetValue(expense.Id, out var previous))
                        {
                            if (previous == expense) duplicates++;
                            else error = "id_conflict";
                        }
                        else
                        {
                            if (state.Count == options.MaxRecords) throw new InvalidDataException("state_record_limit");
                            state.Add(expense.Id, expense);
                            added++;
                        }
                    }
                    if (error is not null)
                    {
                        failed++;
                        if (errors.Count < options.MaxErrors) errors.Add(new RowError(next, error));
                    }
                    processed++;
                    next++;
                    window.Release(); // Only ordered acceptance admits another line.
                }
            }
            if (pending.Count != 0) throw new InvalidOperationException("missing_result");
        }

        Task producer = Guard(Produce);
        // Fixed worker count also bounds CPU work; no Task.Run per record.
        Task[] workers = Enumerable.Range(0, options.Workers)
            .Select(_ => Task.Run(() => Guard(WorkAsync))).ToArray();
        async Task FinishWorkers()
        {
            try { await Task.WhenAll(workers).ConfigureAwait(false); }
            finally { results.Writer.TryComplete(); }
        }
        Task completedWorkers = FinishWorkers();
        Task collector = Guard(Collect);
        try { await Task.WhenAll(producer, completedWorkers, collector).ConfigureAwait(false); }
        catch
        {
            // WhenAll has joined every participant before anything is disposed.
            failure?.Throw();
            token.ThrowIfCancellationRequested();
            throw;
        }
        token.ThrowIfCancellationRequested();
        return new PipelineResult(state.Values.OrderBy(e => e.Id).ToArray(),
            new PipelineReport(processed, added, duplicates, failed, state.Count, retried,
                errors.ToArray(), failed > errors.Count));
    }
}
