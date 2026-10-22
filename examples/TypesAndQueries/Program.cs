using System.Globalization;
using System.Numerics;
using Course.Ledger.Core;
using Course.Ledger.Memory;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: TypesAndQueries <11..18>");
    return 2;
}

switch (args[0])
{
    case "11":
        Report report = new DetailedReport();
        Console.WriteLine(report.Title);
        IText view = new View();
        Console.WriteLine(view.Render());
        break;
    case "12":
        Console.WriteLine(new Point(1, 2) == new Point(1, 2));
        Console.WriteLine(default(Money) == new Money(0m));
        Console.WriteLine((new Money(1.20m) + new Money(2.30m)).Amount
            .ToString("F2", CultureInfo.InvariantCulture));
        break;
    case "13":
        Console.WriteLine(Min(3, 5));
        Console.WriteLine(Add(2, 3));
        Console.WriteLine(Result<int>.Success(0).Value);
        Console.WriteLine(Result<int>.Failure("duplicate_id").Error);
        break;
    case "14":
        List<Action> actions = [];
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            actions.Add(() => Console.WriteLine(captured));
        }
        foreach (Action action in actions)
        {
            action();
        }
        break;
    case "15":
        foreach (int value in Countdown(3))
        {
            Console.WriteLine(value);
            if (value == 2)
            {
                break;
            }
        }
        break;
    case "16":
        List<int> values = [1, 2];
        var query = values.Where(static x => x > 1);
        values.Add(3);
        Console.WriteLine(string.Join(",", query));
        int[] snapshot = query.ToArray();
        values.Add(4);
        Console.WriteLine(string.Join(",", snapshot));
        break;
    case "17":
        Money money = new(1500m);
        Console.WriteLine(money switch { { Amount: >= 1000m } => "large", _ => "small" });
        Console.WriteLine(Money.Zero.IsZero);
        Console.WriteLine(money.Display());
        break;
    case "18":
        LedgerService service = new(new InMemoryExpenseStore());
        int notifications = 0;
        EventHandler<ExpenseAddedEventArgs> handler = (_, _) => notifications++;
        service.Added += handler;
        Expense first = MakeExpense(1, 1, "food", 12.50m);
        service.Add(first);
        service.Add(MakeExpense(2, 1, "travel", 30m));
        service.Add(MakeExpense(3, 1, "FOOD", 7.50m));
        service.Add(MakeExpense(4, 2, "food", 1m));
        Console.WriteLine(service.Add(first).Error);
        service.Added -= handler;
        foreach (CategoryTotal row in service.ByCategory(2026, 1))
        {
            Console.WriteLine($"{row.Category}={row.Total.Display()} ({row.Count})");
        }
        Console.WriteLine($"notifications={notifications}");
        break;
    default:
        Console.Error.WriteLine("unknown chapter");
        return 2;
}
return 0;

static T Min<T>(T left, T right) where T : IComparable<T> =>
    left.CompareTo(right) <= 0 ? left : right;

static T Add<T>(T left, T right) where T : INumber<T> => left + right;

static Expense MakeExpense(int id, int month, string category, decimal amount) =>
    new(new Guid(id, 0, 0, new byte[8]), new DateOnly(2026, month, 1), category, new Money(amount));

static IEnumerable<int> Countdown(int start)
{
    try
    {
        for (int value = start; value > 0; value--)
        {
            yield return value;
        }
    }
    finally
    {
        Console.WriteLine("disposed");
    }
}

abstract class Report
{
    public abstract string Title { get; }
}
sealed class DetailedReport : Report
{
    public override string Title => "Detailed";
}
interface IText { string Render(); }
sealed class View : IText
{
    string IText.Render() => "text";
}
readonly record struct Point(int X, int Y);

public static class MoneyPresentation
{
    public static string Display(this Money money) =>
        money.Amount.ToString("F2", CultureInfo.InvariantCulture);

    extension(Money money)
    {
        public bool IsZero => money.Amount == 0m;
    }
}
