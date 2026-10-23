using Analysis;
using ClientCore;
using GameRules;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;

try
{
    if (args is ["analyze", var file])
    {
        if (new FileInfo(file).Length > 2 * 1024 * 1024) throw new ArgumentException("Input exceeds 2 MiB.");
        var amounts = JsonSerializer.Deserialize<long[]>(File.ReadAllBytes(file))
            ?? throw new JsonException("Expected JSON array of integer minor units.");
        Console.WriteLine(JsonSerializer.Serialize(ExpenseAnalysis.Analyze(amounts)));
    }
    else if (args is ["measure"])
    {
        var data = Enumerable.Range(0, 100_003).Select(i => (double)(i % 100 + 1)).ToArray();
        if (!ExpenseAnalysis.NearlyEqual(ExpenseAnalysis.ScalarSum(data), ExpenseAnalysis.VectorSum(data)))
            throw new InvalidOperationException("SIMD result differs from scalar reference.");
        // Warm both paths. This small observation is not a BenchmarkDotNet benchmark.
        for (int i = 0; i < 50; i++) { ExpenseAnalysis.ScalarSum(data); ExpenseAnalysis.VectorSum(data); }
        var scalar = Measure(() => ExpenseAnalysis.ScalarSum(data));
        var vector = Measure(() => ExpenseAnalysis.VectorSum(data));
        Console.WriteLine(JsonSerializer.Serialize(new
        { Runtime = Environment.Version.ToString(), Vector.IsHardwareAccelerated, Width = Vector<double>.Count, scalar, vector }));
    }
    else if (args is ["game", var savePath])
    {
        var game = new Inventory();
        if (File.Exists(savePath))
        {
            if (new FileInfo(savePath).Length > 128 * 1024) throw new ArgumentException("Save too large.");
            game.Restore(JsonSerializer.Deserialize<SaveData>(File.ReadAllBytes(savePath))
                ?? throw new JsonException("Null save."));
        }
        bool added = game.PickUp("level-1:coin-1", 10);
        string temporary = savePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(game.Snapshot()));
            File.Move(temporary, savePath, overwrite: true); // Same-directory rename; one writer is assumed.
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine(JsonSerializer.Serialize(new { game.Coins, Added = added }));
    }
    else if (args is ["offline", var db])
    {
        const string account = "demo-api|alice";
        var queue = new OfflineQueue(db, account);
        queue.Enqueue(new(Guid.Parse("fe3386e7-a561-4071-a42d-b1ee718fa1aa"),
            [new(Guid.Parse("fe3386e7-a561-4071-a42d-b1ee718fa1ab"), "2026-01-10", "food", 12.50m)]));
        var remote = new ReceiptServer();
        try
        {
            await queue.SyncAsync(account, "demo", remote, CancellationToken.None,
                () => throw new IOException("Simulated stop after remote commit."));
        }
        catch (IOException) { }
        int retained = queue.Counts().Pending;
        var reopened = new OfflineQueue(db, account);
        int sent = await reopened.SyncAsync(account, "demo", remote, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { Retained = retained, Sent = sent,
            Pending = reopened.Counts().Pending, remote.Attempts, remote.UniqueBatches }));
    }
    else if (args is ["report"])
    {
        var url = Environment.GetEnvironmentVariable("LEDGER_API") ?? throw new ArgumentException("Set LEDGER_API.");
        var token = Environment.GetEnvironmentVariable("LEDGER_TOKEN") ?? throw new ArgumentException("Set LEDGER_TOKEN.");
        using var http = LedgerHttpClient.CreateHttpClient(new Uri(url));
        Console.WriteLine(JsonSerializer.Serialize(await new LedgerHttpClient(http).ReadAsync(token, CancellationToken.None)));
    }
    else { Console.Error.WriteLine("usage: analyze FILE | measure | game SAVE | offline DB | report"); return 2; }
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or JsonException or IOException or UnauthorizedAccessException
    or HttpRequestException or TimeoutException)
{ Console.Error.WriteLine($"input/io: {ex.GetType().Name}"); return 3; }

static object Measure(Func<double> action)
{
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    double result = 0;
    for (int i = 0; i < 200; i++) result = action();
    watch.Stop();
    return new { Result = result, Milliseconds = watch.Elapsed.TotalMilliseconds,
        AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated, Iterations = 200 };
}

sealed class ReceiptServer : IImportTransport
{
    private readonly HashSet<Guid> receipts = [];
    public int Attempts { get; private set; }
    public int UniqueBatches => receipts.Count;
    public Task<ImportOutcome> SendAsync(string token, string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Attempts++;
        using var doc = JsonDocument.Parse(payload);
        receipts.Add(doc.RootElement.GetProperty("batchId").GetGuid());
        return Task.FromResult(ImportOutcome.Accepted);
    }
}
