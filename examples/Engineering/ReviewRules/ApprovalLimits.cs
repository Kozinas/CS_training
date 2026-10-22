using System.Collections.Frozen;

namespace Course.Review;

public interface IApprovalLimit
{
    decimal ForCategory(string category);
}

public sealed class FixedApprovalLimit : IApprovalLimit
{
    private readonly decimal amount;
    public FixedApprovalLimit(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        this.amount = amount;
    }
    public decimal ForCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return amount;
    }
}

public sealed class CategoryApprovalLimit : IApprovalLimit
{
    private readonly FrozenDictionary<string, decimal> overrides;
    private readonly IApprovalLimit fallback;
    public CategoryApprovalLimit(IReadOnlyDictionary<string, decimal> overrides, IApprovalLimit fallback)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(fallback);
        var copy = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrides)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            ArgumentOutOfRangeException.ThrowIfNegative(pair.Value);
            copy.Add(pair.Key.Trim(), pair.Value);
        }
        this.overrides = copy.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        this.fallback = fallback;
    }
    public decimal ForCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return overrides.TryGetValue(category.Trim(), out decimal amount) ? amount : fallback.ForCategory(category);
    }
}
