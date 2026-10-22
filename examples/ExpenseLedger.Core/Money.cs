namespace Course.Ledger.Core;

/// <summary>A nonnegative amount in the single accounting unit used by L02.</summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public static Money Zero => default;

    public Money(decimal amount)
    {
        if (amount < 0m || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Use nonnegative amounts with cent precision.");
        }
        Amount = amount;
    }

    public static Money operator +(Money left, Money right) =>
        new(checked(left.Amount + right.Amount));
}
