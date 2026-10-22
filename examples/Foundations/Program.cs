using System.Globalization;
using System.Text;
using Course.Foundations;

// The dispatcher lets each chapter run separately from one small project.
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: Foundations <01..09>");
    return 2;
}

switch (args[0])
{
    case "01":
        Console.WriteLine("Hello, C#!");
        Console.WriteLine(7 + 5);
        break;
    case "02":
        int price = 25;
        int count = 4;
        Console.WriteLine($"total={price * count}");
        break;
    case "03":
        Console.WriteLine(5 / 2);
        Console.WriteLine((5 / 2.0).ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(0.1 + 0.2 == 0.3);
        Console.WriteLine(0.1m + 0.2m == 0.3m);
        Console.WriteLine((19.90m * 3 * 0.90m).ToString("F2", CultureInfo.InvariantCulture));
        break;
    case "04":
        long total = 0;
        for (int i = 1; i <= 5; i++)
        {
            total += i;
        }
        Console.WriteLine(total);
        break;
    case "05":
        Console.WriteLine(CalculateTotal(125.50m, 4, 15m).ToString("F2", CultureInfo.InvariantCulture));
        int number = 10;
        Increase(ref number);
        Console.WriteLine(number);
        Console.WriteLine(TryReadCount("0", out _));
        break;
    case "06":
        Console.WriteLine("\U0001F600".Length);
        bool ok = decimal.TryParse("12.50", NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal amount);
        Console.WriteLine(ok);
        Console.WriteLine(amount.ToString("F2", CultureInfo.InvariantCulture));
        string? comment = null;
        Console.WriteLine(comment?.Length ?? 0);
        break;
    case "07":
        int[] original = [1, 2];
        int[] alias = original;
        alias[0] = 9;
        Console.WriteLine(original[0]);
        int[] copy = original[..];
        copy[0] = 7;
        Console.WriteLine(original[0]);
        Dictionary<string, decimal> totals = new(StringComparer.OrdinalIgnoreCase);
        string[] categories = ["food", "travel", "FOOD"];
        decimal[] amounts = [12.50m, 30m, 7.50m];
        for (int i = 0; i < categories.Length; i++)
        {
            totals.TryGetValue(categories[i], out decimal previous);
            totals[categories[i]] = previous + amounts[i];
        }
        Console.WriteLine(totals["food"].ToString("F2", CultureInfo.InvariantCulture));
        break;
    case "08":
        Expense expense = new("food", 12.50m);
        Console.WriteLine(expense.Category);
        Console.WriteLine(expense.Amount.ToString("F2", CultureInfo.InvariantCulture));
        Console.WriteLine(ReferenceEquals(expense, expense));
        break;
    case "09":
        // A private file for this run; never overwrite the learner's files.
        string path = Path.Combine(Path.GetTempPath(), $"csharp-course-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "food\ntravel\n", new UTF8Encoding(false, true));
            int lines = 0;
            using (StreamReader reader = new(path, new UTF8Encoding(false, true), false))
            {
                while (reader.ReadLine() is not null)
                {
                    lines++;
                }
            }
            Console.WriteLine(lines);
        }
        finally
        {
            File.Delete(path);
        }
        break;
    default:
        Console.Error.WriteLine("unknown chapter");
        return 2;
}
return 0;

static decimal CalculateTotal(decimal price, int count, decimal discountPercent)
{
    return Math.Round(price * count * (1 - discountPercent / 100m), 2, MidpointRounding.ToEven);
}

static void Increase(ref int value) => value++;

static bool TryReadCount(string text, out int count)
{
    count = 0;
    if (!int.TryParse(text, out int parsed) || parsed < 1 || parsed > 100)
    {
        return false;
    }
    count = parsed;
    return true;
}
