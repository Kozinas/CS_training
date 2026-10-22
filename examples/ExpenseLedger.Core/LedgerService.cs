namespace Course.Ledger.Core;

public sealed record CategoryTotal(string Category, Money Total, int Count);

public sealed class ExpenseAddedEventArgs(Expense expense) : EventArgs
{
    public Expense Expense { get; } = expense;
}

/// <summary>Single-threaded teaching service. Subscriber errors propagate after the expense is stored.</summary>
public sealed class LedgerService
{
    private readonly IExpenseStore _store;
    public event EventHandler<ExpenseAddedEventArgs>? Added;

    public LedgerService(IExpenseStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public Result<Expense> Add(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        if (!_store.TryAdd(expense))
        {
            return Result<Expense>.Failure("duplicate_id");
        }
        Added?.Invoke(this, new ExpenseAddedEventArgs(expense));
        return Result<Expense>.Success(expense);
    }

    public IReadOnlyList<CategoryTotal> ByCategory(int year, int month)
    {
        // DateOnly validates year and month even when the store is empty.
        _ = new DateOnly(year, month, 1);
        CategoryTotal[] rows = _store.Snapshot()
            .Where(e => e.Date.Year == year && e.Date.Month == month)
            .GroupBy(static e => e.Category, StringComparer.Ordinal)
            .Select(static group => new CategoryTotal(
                group.Key,
                group.Aggregate(Money.Zero, static (sum, expense) => sum + expense.Amount),
                group.Count()))
            .OrderBy(static row => row.Category, StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(rows);
    }
}
