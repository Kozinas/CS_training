using Course.Ledger.Core;
using Course.Ledger.Memory;

namespace Course.EngineeringIntegration;

public sealed class StoreContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Snapshot_and_duplicate_contract_is_independent_of_storage(bool useList)
    {
        IExpenseStore store = useList ? new ListStore() : new InMemoryExpenseStore();
        var first = new Expense(Guid.NewGuid(), new DateOnly(2026, 1, 10), "food", new Money(1m));
        Assert.True(store.TryAdd(first));
        var snapshot = store.Snapshot();
        Assert.False(store.TryAdd(new Expense(first.Id, first.Date, "travel", new Money(2m))));
        Assert.Equal(first, Assert.Single(store.Snapshot()));
        Assert.True(store.TryAdd(new Expense(Guid.NewGuid(), first.Date, "food", new Money(3m))));
        Assert.Single(snapshot);
        Assert.Equal(2, store.Snapshot().Count);
    }

    private sealed class ListStore : IExpenseStore
    {
        private readonly List<Expense> items = [];
        public bool TryAdd(Expense expense)
        {
            ArgumentNullException.ThrowIfNull(expense);
            if (items.Any(item => item.Id == expense.Id)) return false;
            items.Add(expense);
            return true;
        }
        public IReadOnlyList<Expense> Snapshot() => Array.AsReadOnly(items.ToArray());
    }
}
