using Course.Import;
using Course.Ledger.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Course.ImportCli;

public sealed class ImportRunner(ImportHttpClient http, IOptions<ImportSettings> options,
    ILogger<ImportRunner> logger, TimeProvider clock)
{
    public async Task<ImportReport> RunAsync(string? file, Uri? uri, string statePath, CancellationToken token)
    {
        if ((file is null) == (uri is null)) throw new ArgumentException("Specify exactly one source.");
        if (file is not null && SamePath(file, statePath))
            throw new ArgumentException("Input and state must be different files.");
        var settings = options.Value;
        long started = clock.GetTimestamp();
        using var scope = logger.BeginScope("Import {BatchId}", Guid.NewGuid());
        byte[] bytes = file is not null
            ? await BoundedFiles.ReadPathAsync(file, settings.MaxBytes, token)
            : await http.DownloadAsync(uri!, token);
        var input = Documents.Parse(bytes, settings.MaxRecords);
        using var stateLock = LockState(statePath);
        Expense[] existing = File.Exists(statePath)
            ? ImportEngine.ReadState(await BoundedFiles.ReadPathAsync(statePath, settings.MaxBytes, token),
                settings.MaxRecords)
            : [];
        var plan = ImportEngine.Plan(input, existing, settings.MaxRecords);
        if (plan.Report.Added > 0)
        {
            byte[] saved = Documents.Encode(plan.Records);
            if (saved.Length > settings.MaxBytes) throw new InvalidDataException("state_byte_limit");
            await BoundedFiles.ReplaceAsync(statePath, saved, token);
        }
        logger.LogInformation("Import finished: {Added} added, {Duplicates} duplicates, {Failed} rejected, {ElapsedMs} ms",
            plan.Report.Added, plan.Report.Duplicates, plan.Report.Failed, clock.GetElapsedTime(started).TotalMilliseconds);
        return plan.Report;
    }

    public async Task ExportAsync(string statePath, string output, CancellationToken token)
    {
        if (SamePath(statePath, output)) throw new ArgumentException("State and export must differ.");
        using var stateLock = LockState(statePath);
        var records = ImportEngine.ReadState(
            await BoundedFiles.ReadPathAsync(statePath, options.Value.MaxBytes, token), options.Value.MaxRecords);
        byte[] bytes = Documents.Encode(records);
        await BoundedFiles.ReplaceAsync(output, bytes, token);
    }

    private static FileStream LockState(string path)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        // Keep the lock file: deleting it after release would allow a replacement-file race.
        return new FileStream(full + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private static bool SamePath(string left, string right) => string.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
