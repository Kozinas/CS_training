using System.Globalization;
using Course.ExpenseLedger;

// Minimal checks runnable offline. Dedicated test frameworks are introduced later in the course.
int checks = 0;
CultureInfo originalCulture = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "ru-RU", "en-US", "tr-TR" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        Check(ExpenseParser.TryParseAmount("12.50", out decimal value) && value == 12.50m, culture);
        Check(!ExpenseParser.TryParseAmount("12,50", out value) && value == 0m, culture);
        Check(ExpenseParser.TryParseLine("2024-02-29;FOOD;12.50", out Expense? item, out string error)
            && item is not null && item.Category == "food" && item.Amount == 12.50m && error == "", culture);
        Check(!ExpenseParser.TryParseLine("2026-02-29;food;1", out item, out error)
            && item is null && error.Length > 0, culture);
        foreach (string invalid in new[] { "0", "-1", "+1", ".5", "1.", "1.234", "1e2", " 1", "1000000" })
        {
            Check(!ExpenseParser.TryParseAmount(invalid, out value) && value == 0m, invalid);
        }
    }
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
}

foreach (decimal invalid in new[] { 0m, -1m, 1.001m, 1_000_000m })
{
    bool thrown = false;
    try
    {
        _ = new Expense(new DateOnly(2026, 1, 1), "food", invalid);
    }
    catch (ArgumentOutOfRangeException)
    {
        thrown = true;
    }
    Check(thrown, "constructor amount invariant");
}
foreach (string invalid in new[] { "", " ", "1food", "еда" })
{
    bool thrown = false;
    try
    {
        _ = new Expense(new DateOnly(2026, 1, 1), invalid, 1m);
    }
    catch (ArgumentException)
    {
        thrown = true;
    }
    Check(thrown, "constructor category invariant");
}
Console.WriteLine($"PASS contract checks: {checks} assertions; ru-RU, en-US, tr-TR and model invariants");
return 0;

void Check(bool condition, string message)
{
    checks++;
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
