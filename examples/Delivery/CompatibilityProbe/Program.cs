using System;
using System.Globalization;
using PortableRules;

internal static class Program
{
    private static int Main()
    {
        decimal[] input = { 0m, 0.01m, 10.05m, 10.15m, 1000000m };
        decimal[] expected = { 0m, 0.01m, 11.06m, 11.16m, 1100000m };
        int checks = 0;
        foreach (string culture in new[] { "en-US", "ru-RU", "tr-TR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            for (int i = 0; i < input.Length; i++)
            {
                Require(Quote.Total(input[i], 0.10m) == expected[i]);
                checks++;
#if NETFRAMEWORK
                Require(LegacyRules.LegacyQuote.Total(input[i]) == expected[i]);
                checks++;
#endif
            }
        }
        foreach (decimal amount in new[] { -1m, 1000000.01m })
        {
            Throws(() => Quote.Total(amount, 0.1m)); checks++;
#if NETFRAMEWORK
            Throws(() => LegacyRules.LegacyQuote.Total(amount)); checks++;
#endif
        }
        Throws(() => Quote.Total(10m, -0.1m)); checks++;
        Throws(() => Quote.Total(10m, 1.1m)); checks++;
        Console.WriteLine("PASS Compatibility: " + checks + " checks; three cultures; midpoint-to-even");
        return 0;
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Contract changed."); }
    private static void Throws(Action action)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException("Invalid input was accepted.");
    }
}
