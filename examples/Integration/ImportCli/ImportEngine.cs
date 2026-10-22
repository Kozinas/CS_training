using System.Text.Json.Serialization;
using Course.Import;
using Course.Ledger.Core;

namespace Course.ImportCli;

public sealed record RowError(int Record, string Code);
public sealed record ImportReport(int Added, int Duplicates, int Failed, int Total, RowError[] Errors);
public sealed record ImportPlan(ImportReport Report, Expense[] Records);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ImportReport))]
public partial class ReportJsonContext : JsonSerializerContext;

public static class ImportEngine
{
    public static Expense[] ReadState(byte[] bytes, int maxRecords)
    {
        var document = Documents.Parse(bytes, maxRecords);
        var seen = new HashSet<Guid>();
        var records = new List<Expense>();
        foreach (var row in document.Records)
        {
            if (!Documents.TryExpense(row, out Expense? expense, out _) || !seen.Add(expense!.Id))
                throw new InvalidDataException("invalid_state");
            records.Add(expense);
        }
        return records.ToArray();
    }

    public static ImportPlan Plan(ImportDocument input, IEnumerable<Expense> existing, int maxRecords)
    {
        var records = existing.ToDictionary(e => e.Id);
        int added = 0, duplicates = 0;
        var errors = new List<RowError>();
        for (int i = 0; i < input.Records.Count; i++)
        {
            if (!Documents.TryExpense(input.Records[i], out Expense? expense, out string code))
            {
                errors.Add(new RowError(i + 1, code));
                continue;
            }
            if (records.TryGetValue(expense!.Id, out Expense? previous))
            {
                if (previous == expense) duplicates++;
                else errors.Add(new RowError(i + 1, "id_conflict"));
            }
            else
            {
                if (records.Count >= maxRecords) throw new InvalidDataException("state_record_limit");
                records.Add(expense.Id, expense);
                added++;
            }
        }
        return new ImportPlan(new ImportReport(added, duplicates, errors.Count, records.Count, errors.ToArray()),
            records.Values.OrderBy(e => e.Id).ToArray());
    }
}
