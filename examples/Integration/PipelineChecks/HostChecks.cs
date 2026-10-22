using Course.Import;
using Course.Pipeline;
using Course.PipelineWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Course.PipelineChecks;

internal static partial class Suite
{
    private static async Task CheckFileAndHost()
    {
        string folder = Path.Combine(Path.GetTempPath(), "csharp-pipeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string input = Path.Combine(folder, "input.ndjson"), state = Path.Combine(folder, "state.json");
            await File.WriteAllBytesAsync(input, Rows(Row(1), Row(2), Row(1)).ToArray());
            var limits = new PipelineOptions();
            var paths = new JobPaths(input, state);
            FileImportJob Job(IRowProcessor? processor = null, PipelineOptions? options = null) =>
                new(new ImportPipeline(processor ?? new JsonRowProcessor(), options ?? limits), options ?? limits, paths);
            var initial = await Job().ExecuteAsync(default);
            byte[] saved = await File.ReadAllBytesAsync(state);
            Check(initial is { Added: 2, Duplicates: 1, Processed: 3 }, "file import");
            var repeated = await Job().ExecuteAsync(default);
            Check(repeated is { Added: 0, Duplicates: 3 } && (await File.ReadAllBytesAsync(state)).SequenceEqual(saved), "idempotent bytes");
            Check(Documents.Parse(saved, 10).Records.Count == 2, "L03 checkpoint format");
            await Throws<InvalidDataException>(() => Job(options: limits with { MaxLineBytes = 8 }).ExecuteAsync(default), "fatal before commit");
            Check((await File.ReadAllBytesAsync(state)).SequenceEqual(saved), "fatal preserves state");
            using (var held = new FileStream(state + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await Throws<IOException>(() => Job().ExecuteAsync(default), "exclusive checkpoint lock");
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Throws<OperationCanceledException>(() => Job().ExecuteAsync(cancel.Token), "file cancellation");
            Check((await File.ReadAllBytesAsync(state)).SequenceEqual(saved), "cancel preserves state");
            Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "no leftover temporary state");

            var started = Signal(); int active = 0;
            var blocking = new DelegateProcessor(async (_, token) =>
            {
                Interlocked.Increment(ref active); started.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, token); return new(null, "unreachable"); }
                finally { Interlocked.Decrement(ref active); }
            });
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], ContentRootPath = folder });
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(limits);
            builder.Services.AddSingleton(paths);
            builder.Services.AddSingleton<IRowProcessor>(blocking);
            builder.Services.AddScoped<ImportPipeline>();
            builder.Services.AddScoped<FileImportJob>();
            builder.Services.AddSingleton<JobCompletion>();
            builder.Services.AddHostedService<ImportWorker>();
            using var host = builder.Build();
            await host.StartAsync();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(8));
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await host.StopAsync(shutdown.Token);
            var outcome = await host.Services.GetRequiredService<JobCompletion>().Task.WaitAsync(TimeSpan.FromSeconds(8));
            Check(outcome.ExitCode == 130 && outcome.Report is null && active == 0, "BackgroundService StopAsync joins cancelled pipeline");
            Check((await File.ReadAllBytesAsync(state)).SequenceEqual(saved), "host stop preserves checkpoint");

            await File.WriteAllTextAsync(state, "corrupt");
            await Throws<System.Text.Json.JsonException>(() => Job().ExecuteAsync(default), "corrupt checkpoint");
            Check(await File.ReadAllTextAsync(state) == "corrupt", "corrupt checkpoint not overwritten");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
