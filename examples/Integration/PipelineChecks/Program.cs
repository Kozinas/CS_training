namespace Course.PipelineChecks;

internal static partial class Suite
{
    private static int checks;
    public static async Task Main()
    {
        await CheckAsyncPatterns();
        await CheckSynchronization();
        await CheckPipeline();
        await CheckFileAndHost();
        Console.WriteLine($"PASS PipelineChecks: {checks} assertions");
    }
    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(description);
    }
    private static async Task Throws<T>(Func<Task> action, string description) where T : Exception
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(8)); }
        catch (T) { Check(true, description); return; }
        throw new InvalidOperationException(description + ": expected " + typeof(T).Name);
    }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
