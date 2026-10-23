using ClientCore;

namespace Specializations.Tests;

public sealed class QueueTests : IDisposable
{
    // Keep even temporary DBs under the repository's ignored artifact directory.
    private readonly string path;
    public QueueTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "book.json"))) root = root.Parent;
        var dir = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Repository not found."), ".artifacts");
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, "queue-test-" + Guid.NewGuid().ToString("N") + ".db");
    }
    public void Dispose() { if (File.Exists(path)) File.Delete(path); }
    private static ImportBatch Batch() => new(Guid.NewGuid(), [new(Guid.NewGuid(), "2026-01-10", "food", 12.50m)]);

    [Fact]
    public async Task LostLocalAckReplaysSameSnapshotAfterReopen()
    {
        var queue = new OfflineQueue(path, "alice");
        var batch = Batch();
        queue.Enqueue(batch);
        batch.Rows[0] = batch.Rows[0] with { Amount = 99 }; // Caller changes cannot alter the queued payload.
        var remote = new FakeRemote();
        await Assert.ThrowsAsync<IOException>(() => queue.SyncAsync("alice", "token", remote,
            TestContext.Current.CancellationToken, () => throw new IOException("crash")));
        Assert.Equal((1, 0), queue.Counts());
        Assert.Equal(1, await new OfflineQueue(path, "alice").SyncAsync("alice", "token", remote, TestContext.Current.CancellationToken));
        Assert.Equal(2, remote.Payloads.Count);
        Assert.Equal(remote.Payloads[0], remote.Payloads[1]);
        Assert.Contains("12.50", remote.Payloads[0]);
        Assert.Equal((0, 0), queue.Counts());
    }

    [Fact]
    public void ReusedBatchIdCannotChangePayload()
    {
        var queue = new OfflineQueue(path, "alice");
        var batch = Batch();
        queue.Enqueue(batch);
        queue.Enqueue(batch);
        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(batch with { Rows = [batch.Rows[0] with { Amount = 1 }] }));
        Assert.Equal((1, 0), queue.Counts());
    }

    [Fact]
    public async Task ConflictRemainsBlockedWithoutAutomaticRetries()
    {
        var queue = new OfflineQueue(path, "alice");
        queue.Enqueue(Batch());
        var remote = new FakeRemote { Result = ImportOutcome.Conflict };
        Assert.Equal(0, await queue.SyncAsync("alice", "t", remote, TestContext.Current.CancellationToken));
        Assert.Equal((1, 1), queue.Counts());
        await queue.SyncAsync("alice", "t", remote, TestContext.Current.CancellationToken);
        Assert.Single(remote.Payloads);
    }

    [Fact]
    public async Task NetworkFailureAndCancellationRetainPendingData()
    {
        var queue = new OfflineQueue(path, "alice");
        queue.Enqueue(Batch());
        var remote = new FakeRemote { Failure = new HttpRequestException("offline") };
        await Assert.ThrowsAsync<HttpRequestException>(() => queue.SyncAsync("alice", "t", remote, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.SyncAsync("alice", "t", remote, cancellation.Token));
        Assert.Equal((1, 0), queue.Counts());
    }

    [Fact]
    public async Task QueuePartitionsAccountsAndRefusesChangedIdentity()
    {
        var alice = new OfflineQueue(path, "alice");
        var bob = new OfflineQueue(path, "bob");
        alice.Enqueue(Batch());
        Assert.Equal((0, 0), bob.Counts());
        var remote = new FakeRemote();
        await Assert.ThrowsAsync<InvalidOperationException>(() => alice.SyncAsync("bob", "t", remote, TestContext.Current.CancellationToken));
        Assert.Empty(remote.Payloads);
        Assert.Equal((1, 0), alice.Counts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000000)]
    public void InvalidBatchLeavesQueueEmpty(int amount)
    {
        var queue = new OfflineQueue(path, "alice");
        var batch = Batch();
        Assert.Throws<ArgumentException>(() => queue.Enqueue(batch with { Rows = [batch.Rows[0] with { Amount = amount }] }));
        Assert.Equal((0, 0), queue.Counts());
    }

    private sealed class FakeRemote : IImportTransport
    {
        public List<string> Payloads { get; } = [];
        public ImportOutcome Result { get; init; } = ImportOutcome.Accepted;
        public Exception? Failure { get; init; }
        public Task<ImportOutcome> SendAsync(string token, string payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Payloads.Add(payload);
            return Failure is null ? Task.FromResult(Result) : Task.FromException<ImportOutcome>(Failure);
        }
    }
}
