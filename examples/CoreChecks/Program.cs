using System.Globalization;
using Course.Ledger.Core;
using Course.Ledger.Memory;

int checks = 0;
Check(default(Money) == new Money(0m), "default must be a valid zero");
Check(new Money(1.20m) + new Money(2.30m) == new Money(3.50m), "cent arithmetic");
Check(new Money(1.2300m) == new Money(1.23m), "numeric precision, not decimal scale");
Check(new HashSet<Money> { new(1.20m), new(1.2m) }.Count == 1, "consistent value equality and hash");
Expect<ArgumentOutOfRangeException>(() => new Money(-1m), "negative amount");
Expect<ArgumentOutOfRangeException>(() => new Money(1.001m), "fractional cent");
Expect<OverflowException>(() => { _ = new Money(decimal.MaxValue) + new Money(1m); }, "overflow");
Expect<ArgumentException>(() => new Expense(Guid.Empty, new(2026, 1, 1), "food", new(1m)), "empty Id");
Expect<ArgumentOutOfRangeException>(() => Make(1, 2026, 1, "food", 0m), "zero expense");
Expect<ArgumentOutOfRangeException>(() => Make(1, 2026, 1, "food", 1_000_000m), "expense cap");
foreach (string category in new[] { "", " ", "1food", "еда", "a/b", new string('a', 33) })
{
    Expect<ArgumentException>(() => Make(1, 2026, 1, category, 1m), "category " + category);
}
Check(Make(1, 2026, 1, " FOOD ", 1m).Category == "food", "normalization");
Check(Make(1, 2026, 1, "a_1-b", 1m).Category == "a_1-b", "allowed category characters");
Check(Make(1, 2026, 1, "food", 1m) == Make(1, 2026, 1, "FOOD", 1m), "record equality");
Check(Make(1, 2026, 1, "food", 1m) != Make(1, 2026, 1, "food", 2m), "data differs despite same Id");

Result<int> zero = Result<int>.Success(0);
Check(zero.IsSuccess && zero.Value == 0 && zero.Error is null, "zero success");
Result<int> failure = Result<int>.Failure("duplicate_id");
Check(!failure.IsSuccess && failure.Error == "duplicate_id", "failure code");
Expect<InvalidOperationException>(() => { _ = failure.Value; }, "failure has no Value");
Expect<ArgumentException>(() => Result<int>.Failure(" "), "blank error code");
// Intentional null-forgiving only in negative tests: model a caller without nullable analysis.
Expect<ArgumentNullException>(() => Result<string>.Success(null!), "runtime null success");
Expect<ArgumentNullException>(() => new LedgerService(null!), "runtime null store");
Expect<ArgumentNullException>(() => Make(1, 2026, 1, null!, 1m), "runtime null category");

CultureInfo originalCulture = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "ru-RU", "en-US", "tr-TR" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        foreach (Func<IExpenseStore> create in new Func<IExpenseStore>[]
                 { static () => new InMemoryExpenseStore(), static () => new ListStore() })
        {
            IExpenseStore store = create();
            LedgerService service = new(store);
            int notifications = 0;
            EventHandler<ExpenseAddedEventArgs> handler = (sender, e) =>
            {
                Check(ReferenceEquals(sender, service), "event sender");
                Check(store.Snapshot().Any(item => item.Id == e.Expense.Id), "event follows persistence");
                notifications++;
            };
            service.Added += handler;
            Check(service.ByCategory(2026, 1).Count == 0, "empty report");
            Expect<ArgumentOutOfRangeException>(() => service.ByCategory(2026, 0), "month before reading");
            Expect<ArgumentOutOfRangeException>(() => service.ByCategory(0, 1), "year before reading");
            Expect<ArgumentNullException>(() => service.Add(null!), "null expense");

            Expense first = Make(1, 2026, 1, "food", 12.50m);
            Check(service.Add(first).Value == first, "first add");
            IReadOnlyList<Expense> snapshot = store.Snapshot();
            Check(service.Add(Make(2, 2026, 1, "travel", 30m)).IsSuccess, "second add");
            Check(service.Add(Make(3, 2026, 1, "FOOD", 7.50m)).IsSuccess, "same category");
            Check(service.Add(Make(4, 2026, 2, "food", 1m)).IsSuccess, "other month");
            Check(service.Add(Make(5, 2025, 1, "food", 9m)).IsSuccess, "other year");
            Result<Expense> duplicate = service.Add(Make(1, 2026, 1, "food", 99m));
            Check(!duplicate.IsSuccess && duplicate.Error == "duplicate_id", "duplicate rejected");
            Check(store.Snapshot().Single(e => e.Id == first.Id).Amount == new Money(12.5m),
                "duplicate cannot overwrite");
            Check(snapshot.Count == 1 && snapshot[0] == first, "stable snapshot");
            if (snapshot is IList<Expense> mutableView)
            {
                Expect<NotSupportedException>(() => mutableView[0] = Make(9, 2026, 1, "bad", 1m),
                    "snapshot mutation is rejected");
            }
            Check(notifications == 5, "no event on duplicate");

            IReadOnlyList<CategoryTotal> report = service.ByCategory(2026, 1);
            Check(report.Count == 2, "two groups");
            Check(report[0] == new CategoryTotal("food", new Money(20m), 2), "first row");
            Check(report[1] == new CategoryTotal("travel", new Money(30m), 1), "second row");
            Check(report[0].Total.Amount.ToString("F2", CultureInfo.InvariantCulture) == "20.00",
                "invariant output");
            Check(service.ByCategory(2026, 2).Single().Total == new Money(1m), "month filter");
            Check(service.ByCategory(2026, 3).Count == 0, "empty month");
            service.Add(Make(6, 2026, 1, "food", 2m));
            Check(report[0].Total == new Money(20m), "old report unchanged");
            Check(service.ByCategory(2026, 1)[0].Total == new Money(22m), "new report updated");
            if (report is IList<CategoryTotal> mutableReport)
            {
                Expect<NotSupportedException>(() => mutableReport.Clear(), "report is read-only");
            }
            service.Added -= handler;
            service.Add(Make(7, 2026, 3, "food", 1m));
            Check(notifications == 6, "unsubscribe");

            // The service must not silently turn an observer failure into a rollback.
            LedgerService observed = new(create());
            Expense recorded = Make(10, 2026, 1, "food", 1m);
            bool laterCalled = false;
            observed.Added += (_, _) => throw new InvalidOperationException("observer failed");
            observed.Added += (_, _) => laterCalled = true;
            Expect<InvalidOperationException>(() => observed.Add(recorded), "observer failure propagates");
            Check(!laterCalled, "multicast stops on exception");
            Check(observed.ByCategory(2026, 1).Single().Count == 1, "record remains committed");
            Check(observed.Add(recorded).Error == "duplicate_id", "retry does not duplicate");
        }
    }
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
}

Check(!typeof(Money).Assembly.GetReferencedAssemblies()
    .Any(static assembly => assembly.Name is "ExpenseLedger.Memory" or "TypesAndQueries"), "dependency direction");
LanguageChecks.Run(Check);
Console.WriteLine($"PASS CoreChecks: {checks} assertions; two stores; three cultures; value and iteration semantics");
return 0;

void Check(bool condition, string message)
{
    checks++;
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

void Expect<T>(Action action, string message) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        Check(true, message);
        return;
    }
    throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + message);
}

static Expense Make(int id, int year, int month, string category, decimal amount) =>
    new(new Guid(id, 0, 0, new byte[8]), new DateOnly(year, month, 1), category, new Money(amount));

sealed class ListStore : IExpenseStore
{
    private readonly List<Expense> _items = [];

    public bool TryAdd(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        if (_items.Any(item => item.Id == expense.Id))
        {
            return false;
        }
        _items.Add(expense);
        return true;
    }

    public IReadOnlyList<Expense> Snapshot() => Array.AsReadOnly(_items.ToArray());
}
