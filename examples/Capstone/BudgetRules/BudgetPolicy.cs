namespace Course.Capstone;

public readonly record struct BudgetDecision(bool Allowed, long ProposedMinor, long RemainingMinor);

/// <summary>Preview new, deduplicated expenses for one owner/month/category. Does not reserve funds.</summary>
public static class BudgetPolicy
{
    public const int MaxBatchSize = 100;
    public const long MaxExpenseMinor = 99_999_999;

    public static BudgetDecision Preview(long limitMinor, long spentMinor, IEnumerable<long> incomingMinor)
    {
        ArgumentNullException.ThrowIfNull(incomingMinor);
        ArgumentOutOfRangeException.ThrowIfNegative(limitMinor);
        if (spentMinor < 0 || spentMinor > limitMinor)
            throw new ArgumentOutOfRangeException(nameof(spentMinor));

        long proposed = spentMinor;
        int count = 0;
        foreach (long amount in incomingMinor)
        {
            if (++count > MaxBatchSize)
                throw new ArgumentException("A batch can contain at most 100 new expenses.", nameof(incomingMinor));
            if (amount is <= 0 or > MaxExpenseMinor)
                throw new ArgumentOutOfRangeException(nameof(incomingMinor));
            proposed = checked(proposed + amount);
        }
        // Validate the entire bounded input before returning, even when the limit is already exceeded.
        return new(proposed <= limitMinor, proposed, Math.Max(0, limitMinor - proposed));
    }
}
