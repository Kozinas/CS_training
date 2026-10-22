using Course.Ledger.Core;
using Microsoft.EntityFrameworkCore;

namespace Course.Data;

public sealed record CategoryTotal(string Category, int Count, long AmountMinor);
public sealed record ExpenseSummary(long Sequence, string Category, long AmountMinor);

public sealed class EfLedger(string path)
{
    public async Task<bool> ChangeAmountAsync(Guid id, long expectedVersion, Money amount, CancellationToken token = default)
    {
        if (id == Guid.Empty || expectedVersion < 0 || amount.Amount is <= 0m or > 999_999.99m)
            throw new ArgumentException("invalid_update");
        await using var db = new LedgerDbContext(LedgerDatabase.Options(path));
        var row = await db.Expenses.SingleOrDefaultAsync(e => e.ExternalId == id.ToString("D"), token);
        if (row is null || row.Version != expectedVersion) return false;
        row.AmountMinor = checked((long)(amount.Amount * 100m));
        row.Version = checked(row.Version + 1);
        try { await db.SaveChangesAsync(token); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<IReadOnlyList<CategoryTotal>> ReportAsync(CancellationToken token = default)
    {
        await using var db = new LedgerDbContext(LedgerDatabase.Options(path));
        return await db.Expenses.AsNoTracking().GroupBy(e => e.CategoryName).OrderBy(g => g.Key)
            .Select(g => new CategoryTotal(g.Key, g.Count(), g.Sum(e => e.AmountMinor)))
            .ToListAsync(token);
    }
}
