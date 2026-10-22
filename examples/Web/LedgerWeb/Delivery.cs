using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Course.Web;

[Authorize(Policy = "api")]
public sealed class LedgerHub : Hub
{
    public string Ping() => "ready";
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Context.UserIdentifier!);
        await base.OnConnectedAsync();
    }
}

// Local durable delivery demonstrator: producer and consumer commit separately.
// There is no external broker, distributed transaction or exactly-once transport.
public sealed class OutboxDispatcher(IDbContextFactory<WebDb> factory, IHubContext<LedgerHub> hub,
    ILogger<OutboxDispatcher> logger)
{
    internal Action? AfterConsumerCommit { get; set; }
    public async Task<int> Drain(CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var batch = await db.Outbox.AsNoTracking().Where(x => !x.Delivered).OrderBy(x => x.Sequence).Take(20).ToArrayAsync(token);
        foreach (var row in batch)
        {
            await Consume(row, token);
            AfterConsumerCommit?.Invoke(); // Tests reproduce a crash before delivery acknowledgement.
            await db.Outbox.Where(x => x.Sequence == row.Sequence).ExecuteUpdateAsync(s => s.SetProperty(x => x.Delivered, true), token);
            try { await hub.Clients.Group(row.Owner).SendAsync("changed", row.MessageId, token); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogWarning("Live notification failed: {ErrorType}", ex.GetType().Name); }
        }
        return batch.Length;
    }
    public async Task<bool> Consume(OutboxRow row, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // Inbox and the durable consumer effect belong to one transaction.
        int added = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Inbox (MessageId) VALUES ({row.MessageId}) ON CONFLICT(MessageId) DO NOTHING", token);
        if (added == 0) return false;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Projections (Owner, Count, AmountMinor) VALUES ({row.Owner}, {row.CountDelta}, {row.AmountDelta})
            ON CONFLICT(Owner) DO UPDATE SET Count = Count + excluded.Count, AmountMinor = AmountMinor + excluded.AmountMinor
            """, token);
        await transaction.CommitAsync(token);
        return true;
    }
}
public sealed class DeliveryWorker(IServiceScopeFactory scopes, ILogger<DeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await using var scope = scopes.CreateAsyncScope();
                try { await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().Drain(stoppingToken); }
                catch (SqliteException ex) { logger.LogWarning("Delivery deferred: SQLite {Code}", ex.SqliteErrorCode); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
public sealed class LedgerHealth(IDbContextFactory<WebDb> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            _ = await db.Outbox.Select(e => e.Sequence).Take(1).ToArrayAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (SqliteException) { return HealthCheckResult.Unhealthy("storage_unavailable"); }
    }
}
