using System.Globalization;
using System.Text;
using Course.ExpenseLedger;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: ExpenseLedger <file>");
    return 2;
}

List<Expense> expenses = [];
try
{
    // Decode only UTF-8, reject malformed bytes. An optional UTF-8 BOM is stripped below.
    using StreamReader reader = new(args[0], new UTF8Encoding(false, true), false);
    int lineNumber = 0;
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        lineNumber++;
        if (lineNumber == 1)
        {
            line = line.TrimStart('\uFEFF');
        }
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }
        if (expenses.Count >= 100_000)
        {
            Console.Error.WriteLine($"line {lineNumber}: too many records");
            return 4;
        }
        if (!ExpenseParser.TryParseLine(line, out Expense? expense, out string error))
        {
            Console.Error.WriteLine($"line {lineNumber}: {error}");
            return 4;
        }
        // TryParseLine promises a non-null value on success. Keep an explicit check at this boundary.
        if (expense is null)
        {
            throw new InvalidOperationException("Parser returned success without a value.");
        }
        expenses.Add(expense);
    }
}
catch (DecoderFallbackException)
{
    Console.Error.WriteLine("encoding: expected valid UTF-8");
    return 4;
}
catch (IOException)
{
    Console.Error.WriteLine("io: cannot read file");
    return 3;
}
catch (UnauthorizedAccessException)
{
    Console.Error.WriteLine("io: cannot read file");
    return 3;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("io: invalid path");
    return 3;
}
catch (NotSupportedException)
{
    Console.Error.WriteLine("io: invalid path");
    return 3;
}

// No successful output is written until the entire input has been validated.
decimal total = 0m;
Dictionary<string, decimal> totals = new(StringComparer.Ordinal);
foreach (Expense expense in expenses)
{
    total += expense.Amount;
    totals.TryGetValue(expense.Category, out decimal previous);
    totals[expense.Category] = previous + expense.Amount;
}

Console.WriteLine($"count={expenses.Count.ToString(CultureInfo.InvariantCulture)}");
Console.WriteLine($"total={total.ToString("F2", CultureInfo.InvariantCulture)}");
List<string> categories = new(totals.Keys);
categories.Sort(StringComparer.Ordinal);
foreach (string category in categories)
{
    Console.WriteLine($"{category}={totals[category].ToString("F2", CultureInfo.InvariantCulture)}");
}
return 0;
