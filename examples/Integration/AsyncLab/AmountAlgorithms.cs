using System.Globalization;

namespace Course.AsyncLab;

// Inputs are already validated semicolon rows. This is a measured hot path, not the L01 parser.
public static class AmountAlgorithms
{
    public static decimal SplitSum(string[] rows)
    {
        decimal total = 0;
        foreach (string row in rows)
            total += decimal.Parse(row.Split(';')[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return total;
    }

    public static decimal SpanSum(string[] rows)
    {
        decimal total = 0;
        foreach (string row in rows)
            total += decimal.Parse(row.AsSpan(row.LastIndexOf(';') + 1),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return total;
    }
}
