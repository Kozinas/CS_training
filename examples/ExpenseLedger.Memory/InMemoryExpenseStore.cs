using Course.Ledger.Core;

namespace Course.Ledger.Memory;

/// <summary>Single-threaded store for the L02 lab; it does not persist across process restarts.</summary>
public sealed class InMemoryExpenseStore : IExpenseStore
{
    private readonly Dictionary<Guid, Expense> _items = [];

    public bool TryAdd(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        return _items.TryAdd(expense.Id, expense);
    }

    public IReadOnlyList<Expense> Snapshot() => Array.AsReadOnly(_items.Values.ToArray());
}
