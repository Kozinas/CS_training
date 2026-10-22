using Dapper;
using Microsoft.Data.Sqlite;

namespace Course.Data.Tests;

public sealed class SqlTests
{
    [Fact]
    public void Import_normalizes_and_deduplicates_then_replays_receipt()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path);
        Guid id = Guid.NewGuid(), batch = Guid.NewGuid();
        var rows = new[] { DatabaseFixture.Expense(12.5m, " FOOD ", id), DatabaseFixture.Expense(12.50m, "food", id) };
        Assert.Equal(new ImportOutcome(1, 1, false), ledger.Import(batch, rows, TestContext.Current.CancellationToken));
        Assert.Equal(new ImportOutcome(1, 1, true), ledger.Import(batch, rows, TestContext.Current.CancellationToken));
        Assert.Equal(new ImportOutcome(0, 2, false), ledger.Import(Guid.NewGuid(), rows, TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Count("Expenses"));
        Assert.Equal(2, fixture.Count("ImportReceipts"));
    }

    [Fact]
    public void Reusing_batch_id_for_other_content_is_a_conflict()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path);
        Guid batch = Guid.NewGuid();
        ledger.Import(batch, [DatabaseFixture.Expense()], TestContext.Current.CancellationToken);
        var error = Assert.Throws<ImportConflictException>(() => ledger.Import(batch, [DatabaseFixture.Expense(20m)], TestContext.Current.CancellationToken));
        Assert.Equal("batch_id_conflict", error.Message);
        Assert.Equal(1, fixture.Count("Expenses"));
    }

    [Fact]
    public void Conflicting_expense_rolls_back_other_rows_and_categories()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path);
        var old = DatabaseFixture.Expense();
        ledger.Import(Guid.NewGuid(), [old], TestContext.Current.CancellationToken);
        var error = Assert.Throws<ImportConflictException>(() => ledger.Import(Guid.NewGuid(),
            [DatabaseFixture.Expense(category: "travel"), DatabaseFixture.Expense(99m, id: old.Id)], TestContext.Current.CancellationToken));
        Assert.Equal("expense_id_conflict", error.Message);
        Assert.Equal(1, fixture.Count("Expenses"));
        Assert.Equal(1, fixture.Count("Categories"));
        Assert.Equal(1, fixture.Count("ImportReceipts"));
    }

    [Fact]
    public void Exception_after_insert_rolls_back_expense_and_receipt()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path) { AfterInsert = _ => throw new IOException("injected") };
        Assert.Throws<IOException>(() => ledger.Import(Guid.NewGuid(), [DatabaseFixture.Expense()], TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Count("Expenses"));
        Assert.Equal(0, fixture.Count("Categories"));
        Assert.Equal(0, fixture.Count("ImportReceipts"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Cancellation_before_commit_rolls_back_everything(int cancelAfter)
    {
        using var fixture = new DatabaseFixture();
        using var stop = new CancellationTokenSource();
        var ledger = new SqlLedger(fixture.Path) { AfterInsert = count => { if (count == cancelAfter) stop.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => ledger.Import(Guid.NewGuid(),
            [DatabaseFixture.Expense(), DatabaseFixture.Expense()], stop.Token));
        Assert.Equal(0, fixture.Count("Expenses"));
        Assert.Equal(0, fixture.Count("ImportReceipts"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Competing_imports_do_not_create_duplicates(bool sameBatch)
    {
        using var fixture = new DatabaseFixture();
        using var gate = new Barrier(2);
        var expense = DatabaseFixture.Expense();
        Guid batch = Guid.NewGuid();
        Task<ImportOutcome> Start(Guid key) => Task.Factory.StartNew(() =>
        {
            if (!gate.SignalAndWait(TimeSpan.FromSeconds(5))) throw new TimeoutException("barrier");
            return new SqlLedger(fixture.Path).Import(key, [expense], TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var results = await Task.WhenAll(Start(batch), Start(sameBatch ? batch : Guid.NewGuid()))
            .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Equal(1, fixture.Count("Expenses"));
        Assert.Equal(sameBatch ? 1 : 2, fixture.Count("ImportReceipts"));
        Assert.Equal(sameBatch ? 1 : 0, results.Count(r => r.Replayed));
        Assert.Equal(sameBatch ? 2 : 1, results.Sum(r => r.Added));
        Assert.Equal(sameBatch ? 0 : 1, results.Sum(r => r.Duplicates));
    }

    [Theory]
    [InlineData("UPDATE Expenses SET AmountMinor = 0", 275)]
    [InlineData("UPDATE Expenses SET AmountMinor = 1.25", 275)]
    [InlineData("UPDATE Expenses SET Day = 'bad'", 275)]
    [InlineData("UPDATE Expenses SET CategoryName = 'absent'", 787)]
    [InlineData("UPDATE Expenses SET Version = -1", 275)]
    [InlineData("INSERT INTO Categories(Name) VALUES ('UPPER')", 275)]
    [InlineData("INSERT INTO Expenses(ExternalId,Day,CategoryName,AmountMinor,Version) SELECT ExternalId,Day,CategoryName,AmountMinor,Version FROM Expenses", 2067)]
    public void Database_itself_rejects_invalid_writes(string sql, int extendedCode)
    {
        using var fixture = new DatabaseFixture();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(), [DatabaseFixture.Expense()], TestContext.Current.CancellationToken);
        using var connection = LedgerDatabase.Open(fixture.Path);
        var error = Assert.Throws<SqliteException>(() => connection.Execute(sql));
        Assert.Equal(extendedCode, error.SqliteExtendedErrorCode);
        Assert.Equal(1, fixture.Count("Expenses"));
    }

    [Fact]
    public void Parameter_is_data_not_sql_and_reader_stream_can_stop_early()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path);
        ledger.Import(Guid.NewGuid(), [DatabaseFixture.Expense(12.50m), DatabaseFixture.Expense(7.50m)], TestContext.Current.CancellationToken);
        Assert.Equal(2000, ledger.TotalByCategoryAdo("food"));
        Assert.Equal(0, ledger.TotalByCategoryAdo("food' OR 1=1; DROP TABLE Expenses; --"));
        Assert.Single(ledger.StreamByDate(TestContext.Current.CancellationToken).Take(1));
        Assert.Equal(2, ledger.StreamByDate(TestContext.Current.CancellationToken).Count());
    }

    [Fact]
    public void Keyset_uses_unique_tie_breaker_and_does_not_shift_after_earlier_insert()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path);
        var rows = Enumerable.Range(1, 5).Select(i => DatabaseFixture.Expense(i)).ToArray();
        ledger.Import(Guid.NewGuid(), rows, TestContext.Current.CancellationToken);
        var first = ledger.Page(null, 2);
        var cursor = new PageCursor(first[^1].Day, first[^1].Sequence);
        ledger.Import(Guid.NewGuid(), [DatabaseFixture.Expense(date: new DateOnly(2025, 1, 1))], TestContext.Current.CancellationToken);
        var rest = ledger.Page(cursor, 100);
        Assert.Equal(3, rest.Count);
        Assert.Equal(rows.Select(r => r.Id.ToString("D")).Order(), first.Concat(rest).Select(r => r.ExternalId).Order());
        Assert.Contains(ledger.ExplainPage(), p => p.Contains("IX_Expenses_Day_Sequence", StringComparison.Ordinal));
        Assert.Empty(ledger.Page(new PageCursor(rest[^1].Day, rest[^1].Sequence), 100));
    }

    [Fact]
    public void Empty_batch_is_receipted_and_invalid_limits_are_rejected()
    {
        using var fixture = new DatabaseFixture();
        var ledger = new SqlLedger(fixture.Path);
        Guid batch = Guid.NewGuid();
        Assert.Equal(new ImportOutcome(0, 0, false), ledger.Import(batch, [], TestContext.Current.CancellationToken));
        Assert.Equal(new ImportOutcome(0, 0, true), ledger.Import(batch, [], TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ledger.Import(Guid.Empty, [], TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Page(null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Page(null, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Import(Guid.NewGuid(),
            Enumerable.Repeat(DatabaseFixture.Expense(), SqlLedger.MaxBatchSize + 1).ToArray(), TestContext.Current.CancellationToken));
    }
}
