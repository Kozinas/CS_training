namespace Course.Foundations;

public sealed class Expense
{
    public string Category { get; }
    public decimal Amount { get; }

    public Expense(string category, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Категория обязательна.", nameof(category));
        }
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Сумма должна быть положительной.");
        }
        Category = category.Trim();
        Amount = amount;
    }
}
