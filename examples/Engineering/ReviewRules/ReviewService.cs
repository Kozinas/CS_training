namespace Course.Review;

public sealed record ReviewRequest(Guid Id, decimal Amount, string Category, DateTimeOffset SubmittedAt);
public sealed record ReviewRecord(Guid Id, ApprovalDecision Decision, DateTimeOffset EvaluatedAt);

public interface IReviewSink
{
    ValueTask SaveAsync(ReviewRecord record, CancellationToken token);
}

public sealed class ReviewService(ApprovalPolicy policy, IReviewSink sink, TimeProvider clock)
{
    public async Task<ReviewRecord> ReviewAsync(ReviewRequest request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id == Guid.Empty) throw new ArgumentException("Id must be nonempty.", nameof(request));
        token.ThrowIfCancellationRequested();
        DateTimeOffset now = clock.GetUtcNow();
        var decision = policy.Evaluate(request.Amount, request.Category, request.SubmittedAt, now);
        var record = new ReviewRecord(request.Id, decision, now);
        await sink.SaveAsync(record, token).ConfigureAwait(false);
        return record;
    }
}

public sealed class CountingReviewSink(IReviewSink inner) : IReviewSink
{
    private long saved;
    public long Saved => Interlocked.Read(ref saved);
    public async ValueTask SaveAsync(ReviewRecord record, CancellationToken token)
    {
        await inner.SaveAsync(record, token).ConfigureAwait(false);
        Interlocked.Increment(ref saved);
    }
}
