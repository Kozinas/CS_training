namespace Course.Review;

public enum ApprovalDecision { AutoApproved, ManualReview, Expired, FutureSubmission }

public sealed class ApprovalPolicy
{
    private readonly IApprovalLimit limits;
    public TimeSpan MaximumAge { get; }

    public ApprovalPolicy(IApprovalLimit limits, TimeSpan maximumAge)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (maximumAge <= TimeSpan.Zero || maximumAge > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(maximumAge));
        this.limits = limits;
        MaximumAge = maximumAge;
    }

    public static ApprovalPolicy Standard() => new(new FixedApprovalLimit(1000m), TimeSpan.FromMinutes(30));

    public ApprovalDecision Evaluate(decimal amount, string category, DateTimeOffset submittedAt, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (submittedAt > now) return ApprovalDecision.FutureSubmission;
        if (now - submittedAt >= MaximumAge) return ApprovalDecision.Expired;
        return amount <= limits.ForCategory(category) ? ApprovalDecision.AutoApproved : ApprovalDecision.ManualReview;
    }

    [Obsolete("Use Evaluate to distinguish rejection reasons.", DiagnosticId = "CRV001")]
    public bool CanAutoApprove(decimal amount, string category, DateTimeOffset submittedAt, DateTimeOffset now) =>
        Evaluate(amount, category, submittedAt, now) == ApprovalDecision.AutoApproved;
}
