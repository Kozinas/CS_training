using System.CommandLine;
using System.Text.Json;
using Course.Pipeline;
using Course.PipelineWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var file = new Option<string>("--file") { Required = true };
var state = new Option<string>("--state") { Required = true };
var workers = new Option<int>("--workers") { DefaultValueFactory = _ => 4 };
var capacity = new Option<int>("--capacity") { DefaultValueFactory = _ => 8 };
var window = new Option<int>("--window") { DefaultValueFactory = _ => 16 };
var root = new RootCommand("L04: bounded NDJSON import") { file, state, workers, capacity, window };
root.SetAction(async (parse, token) =>
{
    var limits = new PipelineOptions { Workers = parse.GetValue(workers), Capacity = parse.GetValue(capacity),
        Window = parse.GetValue(window) };
    try { limits.Validate(); }
    catch (ArgumentOutOfRangeException) { Console.Error.WriteLine("usage: invalid limits"); return 2; }
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    { Args = [], ContentRootPath = AppContext.BaseDirectory });
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(settings => settings.SingleLine = true);
    builder.Services.Configure<ConsoleLoggerOptions>(settings => settings.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
    builder.Services.Configure<HostOptions>(settings => settings.ShutdownTimeout = TimeSpan.FromSeconds(10));
    builder.Services.AddSingleton(limits);
    builder.Services.AddSingleton(new JobPaths(parse.GetValue(file)!, parse.GetValue(state)!));
    builder.Services.AddSingleton<IRowProcessor, JsonRowProcessor>();
    builder.Services.AddScoped<ImportPipeline>();
    builder.Services.AddScoped<FileImportJob>();
    builder.Services.AddSingleton<JobCompletion>();
    builder.Services.AddHostedService<ImportWorker>();
    var host = builder.Build();
    var completion = host.Services.GetRequiredService<JobCompletion>();
    try { await host.RunAsync(token); }
    catch (OperationCanceledException) { Console.Error.WriteLine("cancelled"); return 130; }
    if (!completion.Task.IsCompleted) { Console.Error.WriteLine("shutdown incomplete"); return 130; }
    JobOutcome outcome = await completion.Task;
    if (outcome.Report is not null)
        Console.WriteLine(JsonSerializer.Serialize(outcome.Report, PipelineJsonContext.Default.PipelineReport));
    return outcome.ExitCode;
});
var parsed = root.Parse(args);
if (parsed.Errors.Count > 0)
{
    foreach (var error in parsed.Errors) Console.Error.WriteLine("usage: " + error.Message);
    return 2;
}
return await parsed.InvokeAsync();
