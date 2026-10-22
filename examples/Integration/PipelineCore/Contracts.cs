using Course.Ledger.Core;

namespace Course.Pipeline;

public sealed record PipelineOptions
{
    public int Workers { get; init; } = 4;
    public int Capacity { get; init; } = 8;
    public int Window { get; init; } = 16;
    public int MaxLineBytes { get; init; } = 4096;
    public long MaxInputBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxLines { get; init; } = 100_000;
    public int MaxRecords { get; init; } = 10_000;
    public int MaxErrors { get; init; } = 20;
    public int Attempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    public void Validate()
    {
        if (Workers is < 1 or > 32 || Capacity is < 1 or > 1024 || Window is < 1 or > 4096
            || MaxLineBytes is < 1 or > 65536 || MaxInputBytes is < 1 or > 67_108_864
            || MaxLines is < 1 or > 1_000_000 || MaxRecords is < 1 or > 100_000
            || MaxErrors is < 0 or > 1000 || Attempts is < 1 or > 10
            || RetryDelay < TimeSpan.Zero || RetryDelay > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(PipelineOptions));
    }
}

public sealed record ParsedExpense(Expense? Expense, string? Error);
public sealed record RowError(int Line, string Code);
public sealed record PipelineReport(int Processed, int Added, int Duplicates, int Failed,
    int Total, int Retried, RowError[] Errors, bool ErrorsTruncated);
public sealed record PipelineResult(Expense[] Records, PipelineReport Report);
public sealed class TransientWorkException(string message) : Exception(message);

// Implementations may run concurrently. They must be safe to retry and honor cancellation.
public interface IRowProcessor
{
    ValueTask<ParsedExpense> ProcessAsync(ReadOnlyMemory<byte> line, CancellationToken token);
}
