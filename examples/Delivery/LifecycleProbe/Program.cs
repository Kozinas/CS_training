using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSingleton<DrainQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DrainQueue>());
using var host = builder.Build();
var queue = host.Services.GetRequiredService<DrainQueue>();
await host.StartAsync();
if (!queue.Accept(1)) throw new InvalidOperationException("First item was rejected.");
await queue.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
bool second = queue.Accept(2), third = queue.Accept(3), overloadRejected = !queue.Accept(4);
var stop = host.StopAsync();
await queue.Closing.Task.WaitAsync(TimeSpan.FromSeconds(5));
bool closedRejected = !queue.Accept(5);
queue.Release.TrySetResult();
await stop.WaitAsync(TimeSpan.FromSeconds(5));
int[] processed = queue.Processed.ToArray();
bool drained = processed.SequenceEqual([1, 2, 3]);
Console.WriteLine(JsonSerializer.Serialize(new { Accepted = second && third ? 3 : 1,
    OverloadRejected = overloadRejected, ClosedRejected = closedRejected, Processed = processed, Drained = drained }));
return overloadRejected && closedRejected && drained ? 0 : 1;

internal sealed class DrainQueue : BackgroundService
{
    private readonly object admission = new();
    private readonly Channel<int> channel = Channel.CreateBounded<int>(new BoundedChannelOptions(2)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private bool closing;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Closing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<int> Processed { get; } = new();
    public bool Accept(int value)
    {
        lock (admission) return !closing && channel.Writer.TryWrite(value);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Closing the writer defines drain. Host cancellation alone must not drop accepted entries.
        await foreach (int value in channel.Reader.ReadAllAsync())
        {
            Started.TrySetResult();
            await Release.Task; // Controlled dependency used by the exercise, not a production wait.
            Processed.Enqueue(value);
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (admission) { closing = true; channel.Writer.TryComplete(); }
        Closing.TrySetResult();
        await base.StopAsync(cancellationToken);
    }
}
