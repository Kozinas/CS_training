using Course.Ledger.Core;
using Dapper;

namespace Course.Data.Tests;

internal sealed class DatabaseFixture : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cs-data-" + Guid.NewGuid().ToString("N"));
    public string Path { get; }
    public DatabaseFixture(bool migrate = true)
    {
        Directory.CreateDirectory(directory);
        Path = System.IO.Path.Combine(directory, "ledger.db");
        if (migrate) LedgerDatabase.MigrateAsync(Path).GetAwaiter().GetResult();
    }
    public string File(string name) => System.IO.Path.Combine(directory, name);
    public long Count(string table)
    {
        // Identifiers cannot be SQL parameters. Only fixture-owned constant names are accepted.
        if (table is not ("Expenses" or "Categories" or "ImportReceipts")) throw new ArgumentException(nameof(table));
        using var connection = LedgerDatabase.Open(Path);
        return connection.ExecuteScalar<long>($"SELECT count(*) FROM {table}");
    }
    public static Expense Expense(decimal amount = 10m, string category = "food", Guid? id = null, DateOnly? date = null) =>
        new(id ?? Guid.NewGuid(), date ?? new DateOnly(2026, 1, 10), category, new Money(amount));
    public void Dispose()
    {
        // This unique directory contains only files created by this fixture; no recursive delete.
        foreach (string file in Directory.EnumerateFiles(directory)) System.IO.File.Delete(file);
        Directory.Delete(directory);
    }
}
