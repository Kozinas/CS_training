using System.Diagnostics;
using System.Text.Json;
using Course.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Course.PipelineWorker;

public sealed record JobOutcome(int ExitCode, PipelineReport? Report);
public sealed class JobCompletion
{
    private readonly TaskCompletionSource<JobOutcome> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<JobOutcome> Task => source.Task;
    public void Complete(JobOutcome result) => source.TrySetResult(result);
}

public sealed class ImportWorker(IServiceScopeFactory scopes, JobCompletion completion,
    IHostApplicationLifetime lifetime, ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long started = Stopwatch.GetTimestamp();
        JobOutcome outcome;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<FileImportJob>().ExecuteAsync(stoppingToken);
            logger.LogInformation("Processed {Processed}, rejected {Failed}, retries {Retried}, elapsed {ElapsedMs} ms",
                report.Processed, report.Failed, report.Retried, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            outcome = new JobOutcome(report.Failed == 0 ? 0 : 4, report);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        { logger.LogWarning("Import cancelled before completion"); outcome = new JobOutcome(130, null); }
        catch (ArgumentException)
        { logger.LogError("Invalid arguments or limits"); outcome = new JobOutcome(2, null); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { logger.LogError("Input, state or output failed"); outcome = new JobOutcome(3, null); }
        catch (Exception error)
        { logger.LogError("Unexpected worker failure: {Kind}", error.GetType().Name); outcome = new JobOutcome(1, null); }
        completion.Complete(outcome);
        lifetime.StopApplication();
    }
}
