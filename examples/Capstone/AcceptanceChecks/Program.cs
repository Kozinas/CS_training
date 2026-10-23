using System.Collections;
using System.Globalization;
using System.Text.Json;
using Course.Capstone;

if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] is not ("strict-boundary" or "double-enumeration"))
{ Console.Error.WriteLine("usage: AcceptanceChecks CASES_JSON [strict-boundary|double-enumeration]"); return 2; }
int checks = 0;
void Require(bool value, string message)
{ checks++; if (!value) throw new InvalidOperationException(message); }
void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { Require(true, message); return; }
    throw new InvalidOperationException(message);
}
BudgetDecision Evaluate(long limit, long spent, IEnumerable<long> incoming)
{
    // Deliberate defects are confined to this test driver, never to the reference policy.
    if (args.Length == 2 && args[1] == "double-enumeration") _ = incoming.Count();
    var result = BudgetPolicy.Preview(limit, spent, incoming);
    return args.Length == 2 && args[1] == "strict-boundary"
        ? result with { Allowed = result.ProposedMinor < limit } : result;
}

try
{
    var cases = JsonSerializer.Deserialize<BudgetCase[]>(File.ReadAllText(args[0]),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Missing cases.");
    Require(cases.Length == 12, "Expected twelve independent fixture cases.");
    foreach (string culture in new[] { "en-US", "ru-RU", "tr-TR" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        foreach (var item in cases)
        {
            try
            {
                var result = Evaluate(item.LimitMinor, item.SpentMinor, item.IncomingMinor);
                Require(item.Error is null && result.Allowed == item.Allowed &&
                    result.ProposedMinor == item.ProposedMinor && result.RemainingMinor == item.RemainingMinor,
                    $"fixture: {item.Name}");
            }
            catch (Exception ex) when (ex is ArgumentException or OverflowException)
            { Require(ex.GetType().Name == item.Error, $"fixture error: {item.Name}: {ex.GetType().Name}"); }
        }
    }
    var stream = new OnePass([250, 500]);
    Require(Evaluate(2000, 1250, stream) == new BudgetDecision(true, 2000, 0), "one-pass exact boundary");
    Require(stream.Disposed, "enumerator disposed after success");
    var broken = new OnePass([1, 0]);
    Throws<ArgumentOutOfRangeException>(() => Evaluate(2000, 0, broken), "invalid second item");
    Require(broken.Disposed, "enumerator disposed after validation failure");
    Throws<ArgumentException>(() => Evaluate(2000, 0, Enumerable.Repeat(1L, 101)), "bounded input");
    Throws<ArgumentNullException>(() => BudgetPolicy.Preview(2000, 0, null!), "null input");
    Require(Evaluate(2000, 0, Enumerable.Repeat(1L, 100)).ProposedMinor == 100, "100 rows accepted");
    long[] original = [250, 500];
    _ = Evaluate(2000, 1250, original);
    Require(original.SequenceEqual([250L, 500L]), "input is unchanged");

    // Two operations observe the same snapshot before either commits. No sleeps or timing guesses.
    long stored = 600;
    int arrived = 0;
    var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    async Task NaiveImport()
    {
        long snapshot = Interlocked.Read(ref stored);
        if (Interlocked.Increment(ref arrived) == 2) bothRead.TrySetResult();
        await bothRead.Task;
        if (BudgetPolicy.Preview(1000, snapshot, [300]).Allowed) Interlocked.Add(ref stored, 300);
    }
    await Task.WhenAll(Task.Run(NaiveImport), Task.Run(NaiveImport)).WaitAsync(TimeSpan.FromSeconds(10));
    Require(stored == 1200, "reproduce snapshot/commit race");

    stored = 600;
    int accepted = 0;
    object gate = new();
    void LocalImport()
    {
        lock (gate)
        {
            if (!BudgetPolicy.Preview(1000, stored, [300]).Allowed) return;
            stored += 300;
            accepted++;
        }
    }
    await Task.WhenAll(Task.Run(LocalImport), Task.Run(LocalImport)).WaitAsync(TimeSpan.FromSeconds(10));
    Require(stored == 900 && accepted == 1, "single-process check and commit share one critical section");
    Console.WriteLine($"PASS Capstone: {checks} checks; fixtures, one-pass disposal, bounded input and deterministic race");
    Console.WriteLine("INFO: local lock is not a multi-process database transaction; student L08 remains an independent implementation.");
    return 0;
}
catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException or TimeoutException)
{ Console.Error.WriteLine($"FAIL: {ex.Message}"); return 1; }

internal sealed record BudgetCase(string Name, long LimitMinor, long SpentMinor, long[] IncomingMinor,
    bool? Allowed, long? ProposedMinor, long? RemainingMinor, string? Error);

internal sealed class OnePass(long[] values) : IEnumerable<long>
{
    private bool started;
    public bool Disposed { get; private set; }
    public IEnumerator<long> GetEnumerator()
    {
        if (started) throw new InvalidOperationException("Sequence enumerated twice.");
        started = true;
        return Iterate().GetEnumerator();
    }
    private IEnumerable<long> Iterate()
    {
        try { foreach (long value in values) yield return value; }
        finally { Disposed = true; }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
