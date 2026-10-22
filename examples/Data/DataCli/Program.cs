using System.Globalization;
using System.Text.Json;
using Course.Data;
using Course.Import;
using Course.Ledger.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
try
{
    if (args.Length < 2) throw new ArgumentException("usage");
    string database = Path.GetFullPath(args[1]);
    object result;
    switch (args[0])
    {
        case "init" when args.Length == 2:
            await LedgerDatabase.MigrateAsync(database, stop.Token);
            result = new { initialized = true }; break;
        case "import" when args.Length == 4:
            if (!Guid.TryParseExact(args[2], "D", out Guid batch) || batch == Guid.Empty) throw new ArgumentException("batch_id");
            // A bounded read prevents a small declared file length from bypassing the input limit.
            byte[] bytes = await BoundedFiles.ReadPathAsync(args[3], 1048576, stop.Token);
            var document = Documents.Parse(bytes, SqlLedger.MaxBatchSize);
            var expenses = new List<Expense>();
            foreach (var row in document.Records)
            {
                if (!Documents.TryExpense(row, out Expense? expense, out string error))
                    throw new InvalidDataException($"record {expenses.Count + 1}: {error}");
                expenses.Add(expense!);
            }
            result = new SqlLedger(database).Import(batch, expenses, stop.Token); break;
        case "page" when args.Length is 2 or 5:
            PageCursor? after = null;
            int size = 20;
            if (args.Length == 5)
            {
                if (!DateOnly.TryParseExact(args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || !long.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
                    || !int.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out size))
                    throw new ArgumentException("cursor");
                after = new PageCursor(date.DayNumber, sequence);
            }
            var page = new SqlLedger(database).Page(after, size);
            result = new
            {
                rows = page.Select(e => new { e.Sequence, id = e.ExternalId,
                    date = DateOnly.FromDayNumber(e.Day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    category = e.CategoryName, amount = (e.AmountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture), e.Version }),
                next = page.Count == size ? new PageCursor(page[^1].Day, page[^1].Sequence) : null
            }; break;
        case "report" when args.Length == 2:
            result = await new EfLedger(database).ReportAsync(stop.Token); break;
        case "plan" when args.Length == 2:
            result = new SqlLedger(database).ExplainPage(); break;
        case "update" when args.Length == 5:
            if (!Guid.TryParseExact(args[2], "D", out Guid id)
                || !long.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out long version)
                || !DecimalText.TryParse(args[4], out decimal amount)) throw new ArgumentException("update");
            if (!await new EfLedger(database).ChangeAmountAsync(id, version, new Money(amount), stop.Token))
                throw new ImportConflictException("stale_version_or_missing");
            result = new { updated = true, version = checked(version + 1) }; break;
        case "backup" when args.Length == 3:
            LedgerDatabase.Backup(database, Path.GetFullPath(args[2]));
            result = new { backedUp = true }; break;
        case "integrity" when args.Length == 2:
            using (var connection = LedgerDatabase.Open(database))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check";
                if ((string?)command.ExecuteScalar() != "ok") throw new InvalidDataException("integrity_failed");
                command.CommandText = "PRAGMA foreign_key_check";
                using var reader = command.ExecuteReader();
                if (reader.Read()) throw new InvalidDataException("foreign_key_failed");
            }
            result = new { integrity = "ok" }; break;
        default: throw new ArgumentException("usage");
    }
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("cancelled"); return 130; }
catch (ImportConflictException e) { Console.Error.WriteLine(e.Message); return 4; }
catch (ArgumentException e) { Console.Error.WriteLine("usage: " + e.Message); return 2; }
catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or SqliteException or DbUpdateException)
{ Console.Error.WriteLine("data: " + e.Message); return 3; }
