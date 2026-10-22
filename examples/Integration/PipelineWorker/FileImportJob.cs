using Course.Import;
using Course.Ledger.Core;
using Course.Pipeline;

namespace Course.PipelineWorker;

public sealed record JobPaths(string Input, string State);

public sealed class FileImportJob(ImportPipeline pipeline, PipelineOptions options, JobPaths paths)
{
    public const int MaxStateBytes = 16 * 1024 * 1024;

    public async Task<PipelineReport> ExecuteAsync(CancellationToken token)
    {
        string source = Path.GetFullPath(paths.Input), target = Path.GetFullPath(paths.State);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(source, target, comparison) || string.Equals(source, target + ".lock", comparison))
            throw new ArgumentException("Input and state paths must differ.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var exclusive = new FileStream(target + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        Expense[] existing = [];
        if (File.Exists(target))
        {
            var data = Documents.Parse(await BoundedFiles.ReadPathAsync(target, MaxStateBytes, token), options.MaxRecords);
            var records = new List<Expense>();
            var ids = new HashSet<Guid>();
            foreach (var raw in data.Records)
            {
                if (!Documents.TryExpense(raw, out var expense, out _) || !ids.Add(expense!.Id))
                    throw new InvalidDataException("invalid_state");
                records.Add(expense);
            }
            existing = records.ToArray();
        }
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        PipelineResult result = await pipeline.RunAsync(input, existing, token);
        token.ThrowIfCancellationRequested();
        if (result.Report.Added > 0)
        {
            byte[] bytes = Documents.Encode(result.Records);
            if (bytes.Length > MaxStateBytes) throw new InvalidDataException("state_byte_limit");
            await BoundedFiles.ReplaceAsync(target, bytes, token);
        }
        // Cancellation after commit cannot undo the file replacement.
        return result.Report;
    }
}
