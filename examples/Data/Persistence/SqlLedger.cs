using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Course.Ledger.Core;
using Dapper;

namespace Course.Data;

public sealed record ImportOutcome(int Added, int Duplicates, bool Replayed);
public sealed class ImportConflictException(string code) : Exception(code);
public sealed record PageCursor(int Day, long Sequence);
public sealed class StoredExpense
{
    public long Sequence { get; set; }
    public string ExternalId { get; set; } = "";
    public int Day { get; set; }
    public string CategoryName { get; set; } = "";
    public long AmountMinor { get; set; }
    public long Version { get; set; }
}

public sealed class SqlLedger(string path)
{
    internal Action<int>? AfterInsert { get; init; }
    public const int MaxBatchSize = 1000;

    public ImportOutcome Import(Guid batchId, IReadOnlyList<Expense> rows, CancellationToken token = default)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("batch_id_empty", nameof(batchId));
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > MaxBatchSize) throw new ArgumentOutOfRangeException(nameof(rows));
        token.ThrowIfCancellationRequested();
        // Copy the collection before hashing/using it; Expense itself is immutable.
        Expense[] snapshot = rows.ToArray();
        if (snapshot.Any(e => e is null)) throw new ArgumentException("null_expense", nameof(rows));
        string fingerprint = Fingerprint(snapshot);
        string key = batchId.ToString("D");
        using var connection = LedgerDatabase.Open(path);
        // SQLite-specific: acquire the write reservation before the read/decide/write sequence.
        using var transaction = connection.BeginTransaction(deferred: false);
        var receipt = connection.QuerySingleOrDefault<ImportReceiptRow>(
            "SELECT * FROM ImportReceipts WHERE BatchId = @key", new { key }, transaction);
        if (receipt is not null)
        {
            if (receipt.Fingerprint != fingerprint) throw new ImportConflictException("batch_id_conflict");
            return new(receipt.Added, receipt.Duplicates, true);
        }
        int added = 0, duplicates = 0;
        foreach (Expense expense in snapshot)
        {
            token.ThrowIfCancellationRequested();
            var values = new
            {
                Id = expense.Id.ToString("D"), Day = expense.Date.DayNumber,
                Category = expense.Category, Minor = checked((long)(expense.Amount.Amount * 100m))
            };
            var existing = connection.QuerySingleOrDefault<StoredExpense>(
                "SELECT * FROM Expenses WHERE ExternalId = @Id", values, transaction);
            if (existing is not null)
            {
                if (existing.Day != values.Day || existing.CategoryName != values.Category || existing.AmountMinor != values.Minor)
                    throw new ImportConflictException("expense_id_conflict");
                duplicates++;
                continue;
            }
            connection.Execute("INSERT INTO Categories(Name) VALUES (@Category) ON CONFLICT(Name) DO NOTHING", values, transaction);
            connection.Execute("""
                INSERT INTO Expenses(ExternalId, Day, CategoryName, AmountMinor, Version)
                VALUES (@Id, @Day, @Category, @Minor, 0)
                """, values, transaction);
            added++;
            AfterInsert?.Invoke(added);
        }
        connection.Execute("""
            INSERT INTO ImportReceipts(BatchId, Fingerprint, Added, Duplicates)
            VALUES (@key, @fingerprint, @added, @duplicates)
            """, new { key, fingerprint, added, duplicates }, transaction);
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        return new(added, duplicates, false);
    }

    public IReadOnlyList<StoredExpense> Page(PageCursor? after, int size)
    {
        if (size is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(size));
        if (after is { Day: < 0 or > 3652058 } or { Sequence: < 0 }) throw new ArgumentOutOfRangeException(nameof(after));
        using var connection = LedgerDatabase.Open(path);
        var cursor = after ?? new PageCursor(0, 0);
        return connection.Query<StoredExpense>("""
            SELECT Sequence, ExternalId, Day, CategoryName, AmountMinor, Version
            FROM Expenses
            WHERE Day >= @Day AND (Day > @Day OR Sequence > @Sequence)
            ORDER BY Day, Sequence LIMIT @size
            """, new { cursor.Day, cursor.Sequence, size }).AsList();
    }

    public long TotalByCategoryAdo(string category)
    {
        using var connection = LedgerDatabase.Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(AmountMinor), 0) FROM Expenses WHERE CategoryName = $category";
        command.Parameters.AddWithValue("$category", category);
        return (long)command.ExecuteScalar()!;
    }

    public IEnumerable<StoredExpense> StreamByDate(CancellationToken token = default)
    {
        using var connection = LedgerDatabase.Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Sequence, ExternalId, Day, CategoryName, AmountMinor, Version FROM Expenses ORDER BY Day, Sequence";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            yield return new StoredExpense { Sequence = reader.GetInt64(0), ExternalId = reader.GetString(1),
                Day = reader.GetInt32(2), CategoryName = reader.GetString(3), AmountMinor = reader.GetInt64(4), Version = reader.GetInt64(5) };
        }
    }

    public IReadOnlyList<string> ExplainPage()
    {
        using var connection = LedgerDatabase.Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = """
            EXPLAIN QUERY PLAN SELECT Sequence, ExternalId, Day, CategoryName, AmountMinor, Version
            FROM Expenses WHERE Day >= $day AND (Day > $day OR Sequence > $sequence)
            ORDER BY Day, Sequence LIMIT 20
            """;
        command.Parameters.AddWithValue("$day", new DateOnly(2026, 1, 10).DayNumber);
        command.Parameters.AddWithValue("$sequence", 0);
        using var reader = command.ExecuteReader();
        var details = new List<string>();
        while (reader.Read()) details.Add(reader.GetString(3));
        return details;
    }

    private static string Fingerprint(IEnumerable<Expense> rows)
    {
        var canonical = new StringBuilder("batch-v1\n");
        foreach (Expense e in rows)
            canonical.Append(e.Id.ToString("D")).Append('|')
                .Append(e.Date.DayNumber.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(e.Category).Append('|')
                .Append(e.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
