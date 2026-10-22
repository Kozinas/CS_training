using Course.Ledger.Core;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Course.Data.Tests;

public sealed class MigrationBackupTests
{
    [Fact]
    public async Task Upgrading_previous_schema_preserves_rows_and_sets_optional_note_to_null()
    {
        using var fixture = new DatabaseFixture(migrate: false);
        await using (var old = new LedgerDbContext(LedgerDatabase.Options(fixture.Path, create: true)))
        {
            var migrations = old.Database.GetMigrations().ToArray();
            Assert.Equal(2, migrations.Length);
            await old.GetService<IMigrator>().MigrateAsync(migrations[0], TestContext.Current.CancellationToken);
        }
        var expense = DatabaseFixture.Expense(12.50m);
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(), [expense], TestContext.Current.CancellationToken);
        await LedgerDatabase.MigrateAsync(fixture.Path, TestContext.Current.CancellationToken);
        await LedgerDatabase.MigrateAsync(fixture.Path, TestContext.Current.CancellationToken);
        await using var upgraded = new LedgerDbContext(LedgerDatabase.Options(fixture.Path));
        Assert.False(upgraded.Database.HasPendingModelChanges());
        Assert.Equal(2, (await upgraded.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).Count());
        var row = await upgraded.Expenses.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expense.Id.ToString("D"), row.ExternalId);
        Assert.Equal(1250, row.AmountMinor);
        Assert.Null(row.Note);
        using var connection = LedgerDatabase.Open(fixture.Path);
        Assert.Equal(0, connection.ExecuteScalar<long>("SELECT count(*) FROM Expenses WHERE Note = NULL"));
        Assert.Equal(1, connection.ExecuteScalar<long>("SELECT count(*) FROM Expenses WHERE Note IS NULL"));
    }

    [Fact]
    public async Task Bulk_update_requires_explicit_version_filter_and_invalidates_tracked_snapshot()
    {
        using var fixture = new DatabaseFixture();
        new SqlLedger(fixture.Path).Import(Guid.NewGuid(), [DatabaseFixture.Expense()], TestContext.Current.CancellationToken);
        await using var db = new LedgerDbContext(LedgerDatabase.Options(fixture.Path));
        var tracked = await db.Expenses.SingleAsync(TestContext.Current.CancellationToken);
        int changed = await db.Expenses.Where(e => e.Sequence == tracked.Sequence && e.Version == 0)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.Note, "reviewed").SetProperty(e => e.Version, e => e.Version + 1),
                TestContext.Current.CancellationToken);
        Assert.Equal(1, changed);
        Assert.Null(tracked.Note);
        Assert.Equal(0, await db.Expenses.Where(e => e.Sequence == tracked.Sequence && e.Version == 0)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.Note, "stale"), TestContext.Current.CancellationToken));
        tracked.AmountMinor = 999;
        tracked.Version = 1;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Backup_restores_data_versions_receipts_and_schema_independently()
    {
        using var fixture = new DatabaseFixture();
        var expense = DatabaseFixture.Expense();
        Guid batch = Guid.NewGuid();
        new SqlLedger(fixture.Path).Import(batch, [expense], TestContext.Current.CancellationToken);
        Assert.True(await new EfLedger(fixture.Path).ChangeAmountAsync(expense.Id, 0, new Money(20m), TestContext.Current.CancellationToken));
        string backup = fixture.File("backup.db"), restored = fixture.File("restored.db");
        LedgerDatabase.Backup(fixture.Path, backup);
        Assert.Throws<IOException>(() => LedgerDatabase.Backup(fixture.Path, backup));
        Assert.Throws<IOException>(() => LedgerDatabase.Backup(fixture.Path, fixture.Path));
        Assert.True(await new EfLedger(fixture.Path).ChangeAmountAsync(expense.Id, 1, new Money(30m), TestContext.Current.CancellationToken));
        File.Copy(backup, restored);
        Assert.Equal(2000, new SqlLedger(restored).TotalByCategoryAdo("food"));
        Assert.Equal(3000, new SqlLedger(fixture.Path).TotalByCategoryAdo("food"));
        // The receipt replays the ORIGINAL outcome even after the expense was edited.
        Assert.Equal(new ImportOutcome(1, 0, true), new SqlLedger(restored).Import(batch, [expense], TestContext.Current.CancellationToken));
        using var connection = LedgerDatabase.Open(restored);
        Assert.Equal("ok", connection.ExecuteScalar<string>("PRAGMA integrity_check"));
        Assert.Empty(connection.Query("PRAGMA foreign_key_check"));
        Assert.Equal(2, connection.ExecuteScalar<long>("SELECT count(*) FROM __EFMigrationsHistory"));
        Assert.Equal(1, connection.ExecuteScalar<long>("SELECT Version FROM Expenses"));
    }
}
