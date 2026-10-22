using Course.Ledger.Core;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Course.Data.Tests;

public sealed class EfTests
{
    [Fact]
    public async Task Projection_avoids_n_plus_one_and_does_not_track_entities()
    {
        using var fixture = new DatabaseFixture();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(),
            [DatabaseFixture.Expense(category: "food"), DatabaseFixture.Expense(category: "travel"), DatabaseFixture.Expense(category: "books")], TestContext.Current.CancellationToken);
        var counter = new QueryCounter();
        await using (var slow = new LedgerDbContext(LedgerDatabase.Options(fixture.Path, false, counter)))
        {
            var rows = await slow.Expenses.ToListAsync(TestContext.Current.CancellationToken);
            foreach (var row in rows) await slow.Entry(row).Reference(e => e.Category).LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(4, counter.Commands.Count);
        }
        counter.Commands.Clear();
        await using var efficient = new LedgerDbContext(LedgerDatabase.Options(fixture.Path, false, counter));
        var summaries = await efficient.Expenses.AsNoTracking().OrderBy(e => e.Sequence)
            .Select(e => new ExpenseSummary(e.Sequence, e.Category.Name, e.AmountMinor))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, summaries.Count);
        Assert.Single(counter.Commands);
        Assert.Empty(efficient.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Tracking_reuses_instance_but_no_tracking_returns_a_snapshot()
    {
        using var fixture = new DatabaseFixture();
        var expense = DatabaseFixture.Expense();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(), [expense], TestContext.Current.CancellationToken);
        await using var db = new LedgerDbContext(LedgerDatabase.Options(fixture.Path));
        var one = await db.Expenses.SingleAsync(TestContext.Current.CancellationToken);
        var two = await db.Expenses.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Same(one, two);
        Assert.True(await new EfLedger(fixture.Path).ChangeAmountAsync(expense.Id, 0, new Money(20m), TestContext.Current.CancellationToken));
        Assert.Equal(1000, one.AmountMinor);
        var fresh = await db.Expenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotSame(one, fresh);
        Assert.Equal(2000, fresh.AmountMinor);
        Assert.Single(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Competing_contexts_cannot_silently_lose_an_update()
    {
        using var fixture = new DatabaseFixture();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(), [DatabaseFixture.Expense()], TestContext.Current.CancellationToken);
        using var gate = new Barrier(2);
        Task<bool> Start(long amount) => Task.Factory.StartNew(() =>
        {
            using var db = new LedgerDbContext(LedgerDatabase.Options(fixture.Path));
            var row = db.Expenses.Single();
            if (!gate.SignalAndWait(TimeSpan.FromSeconds(5))) throw new TimeoutException("barrier");
            row.AmountMinor = amount;
            row.Version++;
            try { db.SaveChanges(); return true; }
            catch (DbUpdateConcurrencyException) { return false; }
        }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var results = await Task.WhenAll(Start(2000), Start(3000)).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Single(results, r => r);
        await using var verification = new LedgerDbContext(LedgerDatabase.Options(fixture.Path));
        var saved = await verification.Expenses.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, saved.Version);
        Assert.Contains(saved.AmountMinor, new long[] { 2000, 3000 });
    }

    [Fact]
    public async Task Stale_and_missing_updates_do_not_overwrite_current_data()
    {
        using var fixture = new DatabaseFixture();
        var expense = DatabaseFixture.Expense();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(), [expense], TestContext.Current.CancellationToken);
        var ledger = new EfLedger(fixture.Path);
        Assert.True(await ledger.ChangeAmountAsync(expense.Id, 0, new Money(11m), TestContext.Current.CancellationToken));
        Assert.False(await ledger.ChangeAmountAsync(expense.Id, 0, new Money(99m), TestContext.Current.CancellationToken));
        Assert.False(await ledger.ChangeAmountAsync(Guid.NewGuid(), 0, new Money(99m), TestContext.Current.CancellationToken));
        Assert.Equal(1100, new SqlLedger(fixture.Path).TotalByCategoryAdo("food"));
    }

    [Fact]
    public async Task Grouped_report_is_aggregated_by_sql()
    {
        using var fixture = new DatabaseFixture();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(),
            [DatabaseFixture.Expense(12.5m), DatabaseFixture.Expense(7.5m), DatabaseFixture.Expense(30m, "travel")], TestContext.Current.CancellationToken);
        Assert.Equal(new[] { new CategoryTotal("food", 2, 2000), new CategoryTotal("travel", 1, 3000) },
            await new EfLedger(fixture.Path).ReportAsync(TestContext.Current.CancellationToken));
    }
}
