namespace Course.Ledger.Core;

/// <summary>An immutable expense snapshot; the store enforces uniqueness by Id.</summary>
public sealed record Expense
{
    public Guid Id { get; }
    public DateOnly Date { get; }
    public string Category { get; }
    public Money Amount { get; }

    public Expense(Guid id, DateOnly date, string category, Money amount)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must be nonempty.", nameof(id));
        }
        ArgumentNullException.ThrowIfNull(category);
        category = category.Trim();
        if (category.Length is < 1 or > 32 || !IsLetter(category[0]))
        {
            throw new ArgumentException("Invalid category.", nameof(category));
        }
        foreach (char c in category)
        {
            if (!IsLetter(c) && c is not (>= '0' and <= '9') && c is not ('_' or '-'))
            {
                throw new ArgumentException("Invalid category.", nameof(category));
            }
        }
        if (amount.Amount is <= 0m or > 999_999.99m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
        Id = id;
        Date = date;
        Category = category.ToLowerInvariant();
        Amount = amount;
    }

    private static bool IsLetter(char value) => value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
}
