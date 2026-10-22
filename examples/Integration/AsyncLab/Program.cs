using Course.AsyncLab;

if (args is ["31"])
{
    Func<CancellationToken, Task<int>>[] operations =
    [ _ => Task.FromResult(7), _ => throw new FormatException("bad"),
        async _ => { await Task.Yield(); throw new IOException("later"); } ];
    var outcomes = await AsyncPatterns.ObserveAll(operations);
    Console.WriteLine($"success={outcomes.Count(x => x.Error is null)};errors={outcomes.Count(x => x.Error is not null)}");
    bool disposed = false;
    await foreach (int value in AsyncPatterns.Numbers(3, () => disposed = true))
    { Console.WriteLine(value); break; }
    Console.WriteLine($"disposed={disposed}");
}
else if (args is ["32"])
{
    Console.WriteLine($"lost={SharedState.LostUpdate()}");
    Console.WriteLine($"atomic={await SharedState.AtomicCount(4, 1000)}");
    var counter = new LockedCounter();
    await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
    { for (int i = 0; i < 1000; i++) counter.Increment(); })));
    Console.WriteLine($"locked={counter.Value}");
}
else if (args is ["33"])
{
    int[] values = new int[4];
    await Parallel.ForEachAsync(Enumerable.Range(0, 4),
        new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (i, token) =>
        { await Task.Delay(1, token); values[i] = i * i; });
    Console.WriteLine(string.Join(",", values));
    Console.WriteLine(string.Join(",", Enumerable.Range(0, 4).AsParallel().AsOrdered().Select(i => i * i)));
}
else if (args is ["34", "split" or "span", var secondsText]
    && int.TryParse(secondsText, out int seconds) && seconds is >= 1 and <= 60)
{
    string[] rows = Enumerable.Repeat("2026-01-10;food;12.50", 1000).ToArray();
    Console.Error.WriteLine($"pid={Environment.ProcessId};mode={args[1]}");
    var elapsed = System.Diagnostics.Stopwatch.StartNew();
    int batches = 0;
    decimal checksum = 0;
    while (elapsed.Elapsed < TimeSpan.FromSeconds(seconds))
    {
        checksum += args[1] == "split" ? AmountAlgorithms.SplitSum(rows) : AmountAlgorithms.SpanSum(rows);
        batches++;
    }
    Console.WriteLine($"batches={batches};valid={checksum == batches * 12500m}");
}
else { Console.Error.WriteLine("usage: 31 | 32 | 33 | 34 split|span seconds(1..60)"); return 2; }
return 0;
