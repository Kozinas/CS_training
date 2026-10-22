namespace Course.ExpenseLedger;

public sealed class Expense
{
    public DateOnly Date { get; }
    public string Category { get; }
    public decimal Amount { get; }

    public Expense(DateOnly date, string category, decimal amount)
    {
        if (!ExpenseParser.IsValidCategory(category))
        {
            throw new ArgumentException("Invalid category.", nameof(category));
        }
        if (amount <= 0m || amount > 999_999.99m || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
        Date = date;
        Category = category.ToLowerInvariant();
        Amount = amount;
    }
}
