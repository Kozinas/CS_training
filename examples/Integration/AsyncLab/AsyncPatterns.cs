using System.Runtime.CompilerServices;

namespace Course.AsyncLab;

public sealed record Outcome<T>(T? Value, Exception? Error, bool Cancelled);
public static class AsyncPatterns
{
    // Small bounded batch only. Each factory, even a synchronously throwing one, is observed.
    public static Task<Outcome<T>[]> ObserveAll<T>(IReadOnlyList<Func<CancellationToken, Task<T>>> operations,
        CancellationToken token = default) => Task.WhenAll(operations.Select(operation => Observe(operation, token)));

    private static async Task<Outcome<T>> Observe<T>(Func<CancellationToken, Task<T>> operation,
        CancellationToken token)
    {
        try { return new(await operation(token).ConfigureAwait(false), null, false); }
        catch (OperationCanceledException error) when (token.IsCancellationRequested)
        { return new(default, error, true); }
        catch (Exception error) { return new(default, error, false); }
    }

    public static async IAsyncEnumerable<int> Numbers(int count, Action onDispose,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return i;
            }
        }
        finally { onDispose(); }
    }
}
