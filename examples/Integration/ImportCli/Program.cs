using System.CommandLine;
using System.Text.Json;
using Course.ImportCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;
using Serilog;
using Serilog.Events;
using Spectre.Console;

var fileOption = new Option<string?>("--file") { Description = "Version 1 JSON file" };
var urlOption = new Option<string?>("--url") { Description = "HTTPS JSON source, or loopback HTTP" };
var stateOption = new Option<string>("--state") { Required = true, Description = "Checkpoint and accepted records" };
var outputOption = new Option<string>("--output") { Required = true, Description = "Export destination" };
var prettyOption = new Option<bool>("--pretty") { Description = "Display a table instead of the JSON report" };
var import = new Command("import", "Import valid records and report rejected records")
    { fileOption, urlOption, stateOption, prettyOption };
var exportState = new Option<string>("--state") { Required = true };
var export = new Command("export", "Export the validated checkpoint") { exportState, outputOption };
var root = new RootCommand("ExpenseLedger L03") { import, export };
import.SetAction(async (parse, token) => await ExecuteAsync(async (runner, operationToken) =>
{
    string? file = parse.GetValue(fileOption);
    string? url = parse.GetValue(urlOption);
    if ((file is null) == (url is null)) throw new ArgumentException("Specify exactly one of --file and --url.");
    Uri? uri = url is null ? null : new Uri(url, UriKind.Absolute);
    ImportReport report = await runner.RunAsync(file, uri, parse.GetValue(stateOption)!, operationToken);
    if (parse.GetValue(prettyOption))
    {
        var table = new Table().AddColumn("Added").AddColumn("Duplicates").AddColumn("Failed").AddColumn("Total");
        table.AddRow(report.Added.ToString(), report.Duplicates.ToString(), report.Failed.ToString(), report.Total.ToString());
        AnsiConsole.Write(table);
        foreach (var error in report.Errors) AnsiConsole.Write(new Text($"record {error.Record}: {error.Code}\n"));
    }
    else Console.WriteLine(JsonSerializer.Serialize(report, ReportJsonContext.Default.ImportReport));
    return report.Failed == 0 ? 0 : 4;
}, token));
export.SetAction(async (parse, token) => await ExecuteAsync(async (runner, operationToken) =>
{
    await runner.ExportAsync(parse.GetValue(exportState)!, parse.GetValue(outputOption)!, operationToken);
    Console.WriteLine("exported");
    return 0;
}, token));

var parsed = root.Parse(args);
if (parsed.Errors.Count > 0)
{
    foreach (var error in parsed.Errors) Console.Error.WriteLine("usage: " + error.Message);
    return 2;
}
return await parsed.InvokeAsync();

static async Task<int> ExecuteAsync(Func<ImportRunner, CancellationToken, Task<int>> action, CancellationToken token)
{
    try
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [], ContentRootPath = AppContext.BaseDirectory
        });
        builder.Configuration.AddEnvironmentVariables("CS_IMPORT_");
        builder.Services.AddOptions<ImportSettings>().Bind(builder.Configuration.GetSection("Import"))
            .Validate(settings => settings.IsValid(), "Invalid import limits.")
            .ValidateOnStart();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, log) => log
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose,
                outputTemplate: "{Level:u3} [{BatchId}] {Message:lj}{NewLine}"));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddImportHttp();
        builder.Services.AddScoped<ImportRunner>();
        using var host = builder.Build();
        try { await host.StartAsync(token); }
        catch (InvalidOperationException)
        { Console.Error.WriteLine("config: invalid configuration value"); return 2; }
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.ApplicationStopping);
        try
        {
            using var scope = host.Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<ImportRunner>(), linked.Token);
        }
        finally
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stop.Token);
        }
    }
    catch (OperationCanceledException) { Console.Error.WriteLine("cancelled"); return 130; }
    catch (OptionsValidationException) { Console.Error.WriteLine("config: invalid limits"); return 2; }
    catch (ArgumentException) { Console.Error.WriteLine("usage: invalid source or path"); return 2; }
    catch (UriFormatException) { Console.Error.WriteLine("usage: invalid URL"); return 2; }
    catch (Exception error) when (error is HttpRequestException or TimeoutException or TimeoutRejectedException)
    { Console.Error.WriteLine("network: request failed"); return 5; }
    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
    { Console.Error.WriteLine("data: input, state or output failed validation or I/O"); return 3; }
}
