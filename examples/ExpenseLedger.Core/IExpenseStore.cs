namespace Course.Ledger.Core;

public interface IExpenseStore
{
    /// <summary>Adds once by Id; on duplicate returns false and leaves the previous value unchanged.</summary>
    bool TryAdd(Expense expense);

    /// <summary>Returns an independent read-only snapshot; enumeration order is unspecified.</summary>
    IReadOnlyList<Expense> Snapshot();
}
