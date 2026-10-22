namespace Course.AsyncLab;

public sealed class LockedCounter
{
    private readonly Lock gate = new();
    private int value;
    public void Increment() { lock (gate) value++; }
    public int Value { get { lock (gate) return value; } }
}

public static class SharedState
{
    // Deliberately broken read-modify-write, with a controlled interleaving.
    public static int LostUpdate()
    {
        int value = 0;
        using var bothRead = new Barrier(2);
        void Increment()
        {
            int local = value;
            bothRead.SignalAndWait();
            value = local + 1;
        }
        var first = new Thread(Increment);
        var second = new Thread(Increment);
        first.Start(); second.Start();
        first.Join(); second.Join();
        return value;
    }

    public static async Task<int> AtomicCount(int workers, int iterations)
    {
        int value = 0;
        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++) Interlocked.Increment(ref value);
        })));
        return value;
    }
}
