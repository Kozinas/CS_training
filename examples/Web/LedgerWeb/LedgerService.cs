using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;
using Course.Ledger.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Course.Web;

public sealed record ExpenseInput(Guid Id, DateOnly Date, string? Category, decimal Amount, string? Note = null);
public sealed record ImportInput(Guid BatchId, ExpenseInput?[]? Rows);
public sealed record UpdateInput(long ExpectedVersion, decimal Amount);
public sealed record ImportReply(int Added, int Duplicates, bool Replayed);
public sealed record ExpenseView(Guid Id, DateOnly Date, string Category, long AmountMinor, long Version, long Sequence, string? Note);
public sealed record ReportItem(string Category, int Count, long AmountMinor);
public sealed record PageReply(ExpenseView[] Rows, long? Next);
public sealed class ContractException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
}
public static class WebTelemetry
{
    public const string Name = "Course.LedgerWeb";
    public static readonly ActivitySource Activities = new(Name);
    public static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Imports = Meter.CreateCounter<long>("ledger.imports.committed");
}

public sealed class LedgerService(WebDb db, IDbContextFactory<WebDb> factory, HybridCache cache, ILogger<LedgerService> logger)
{
    public async Task<ImportReply> Import(string owner, ImportInput input, CancellationToken token)
    {
        if (input.BatchId == Guid.Empty || input.Rows is null || input.Rows.Length > 100)
            throw new ContractException(400, "invalid_batch");
        // Domain validation finishes before any write. Owner always comes from authenticated claims.
        var rows = input.Rows.Select(row =>
        {
            if (row is null || row.Note?.Length > 200) throw new ContractException(400, "invalid_row");
            try
            {
                var expense = new Expense(row.Id, row.Date, row.Category!, new Money(row.Amount));
                return new ExpenseInput(expense.Id, expense.Date, expense.Category, expense.Amount.Amount, row.Note);
            }
            catch (ArgumentException) { throw new ContractException(400, "invalid_row"); }
        }).ToArray();
        var canonical = new { Version = 1, Rows = rows.Select(r => new
            { Id = r.Id.ToString("D"), Day = r.Date.DayNumber, r.Category, AmountMinor = checked((long)(r.Amount * 100m)), r.Note }) };
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
        string key = input.BatchId.ToString("D");
        using var activity = WebTelemetry.Activities.StartActivity("ledger.import");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var receipt = await db.Receipts.FindAsync([owner, key], token);
        if (receipt is not null)
        {
            if (receipt.Fingerprint != hash) throw new ContractException(409, "batch_id_conflict");
            return new(receipt.Added, receipt.Duplicates, true);
        }
        int added = 0, duplicates = 0;
        long delta = 0;
        foreach (var row in rows)
        {
            string id = row.Id.ToString("D");
            long minor = checked((long)(row.Amount * 100m));
            // Save each insert inside the transaction so duplicate IDs within the same batch are visible.
            var old = await db.Expenses.SingleOrDefaultAsync(e => e.Owner == owner && e.ExternalId == id, token);
            if (old is not null)
            {
                if (old.Day != row.Date.DayNumber || old.Category != row.Category || old.AmountMinor != minor || old.Note != row.Note)
                    throw new ContractException(409, "expense_id_conflict");
                duplicates++;
                continue;
            }
            db.Expenses.Add(new() { Owner = owner, ExternalId = id, Day = row.Date.DayNumber,
                Category = row.Category!, AmountMinor = minor, Note = row.Note });
            await db.SaveChangesAsync(token);
            added++;
            delta += minor;
        }
        db.Receipts.Add(new() { Owner = owner, BatchId = key, Fingerprint = hash, Added = added, Duplicates = duplicates });
        if (added > 0) await Changed(owner, added, delta, token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        WebTelemetry.Imports.Add(1);
        logger.LogInformation("Import committed: {Added} added, {Duplicates} duplicates", added, duplicates);
        return new(added, duplicates, false);
    }

    public async Task Update(string owner, Guid id, UpdateInput input, CancellationToken token)
    {
        try { _ = new Expense(id, DateOnly.MinValue, "value", new Money(input.Amount)); }
        catch (ArgumentException) { throw new ContractException(400, "invalid_amount_or_id"); }
        if (input.ExpectedVersion is < 0 or long.MaxValue) throw new ContractException(400, "invalid_version");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var row = await db.Expenses.SingleOrDefaultAsync(e => e.Owner == owner && e.ExternalId == id.ToString(), token)
            ?? throw new ContractException(404, "expense_missing");
        if (row.Version != input.ExpectedVersion) throw new ContractException(409, "stale_version");
        long minor = checked((long)(input.Amount * 100m));
        long delta = minor - row.AmountMinor;
        row.AmountMinor = minor;
        row.Version++;
        await Changed(owner, 0, delta, token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private async Task Changed(string owner, int count, long delta, CancellationToken token)
    {
        db.Outbox.Add(new() { MessageId = Guid.NewGuid().ToString("D"), Owner = owner, CountDelta = count, AmountDelta = delta });
        await db.Users.Where(u => u.Id == owner).ExecuteUpdateAsync(s => s.SetProperty(u => u.Revision, u => u.Revision + 1), token);
    }
    public async Task<PageReply> Page(string owner, long after, int size, CancellationToken token)
    {
        if (after < 0 || size is < 1 or > 100) throw new ContractException(400, "invalid_page");
        var rows = await db.Expenses.AsNoTracking().Where(e => e.Owner == owner && e.Sequence > after)
            .OrderBy(e => e.Sequence).Take(size + 1).ToArrayAsync(token);
        return new(rows.Take(size).Select(View).ToArray(), rows.Length > size ? rows[size - 1].Sequence : null);
    }
    public async Task<ExpenseView> Find(string owner, Guid id, CancellationToken token) => View(
        await db.Expenses.AsNoTracking().SingleOrDefaultAsync(e => e.Owner == owner && e.ExternalId == id.ToString(), token)
        ?? throw new ContractException(404, "expense_missing"));
    public async Task<ReportItem[]> Report(string owner, CancellationToken token)
    {
        long revision = await db.Users.Where(u => u.Id == owner).Select(u => u.Revision).SingleAsync(token);
        return await cache.GetOrCreateAsync($"report:{owner}:{revision}", async sharedToken =>
        {
            // The shared factory owns its context, independently of any one request's lifetime.
            await using var query = await factory.CreateDbContextAsync(sharedToken);
            return await query.Expenses.AsNoTracking().Where(e => e.Owner == owner).GroupBy(e => e.Category)
                .OrderBy(g => g.Key).Select(g => new ReportItem(g.Key, g.Count(), g.Sum(e => e.AmountMinor)))
                .ToArrayAsync(sharedToken);
        }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(30), LocalCacheExpiration = TimeSpan.FromSeconds(30) },
        cancellationToken: token);
    }
    private static ExpenseView View(WebExpense row) => new(Guid.Parse(row.ExternalId), DateOnly.FromDayNumber(row.Day),
        row.Category, row.AmountMinor, row.Version, row.Sequence, row.Note);
}
