using System.Globalization;

namespace Course.RuntimeLab;

public static class PairParser
{
    // Exactly two ASCII nonnegative Int32 numbers; no spaces or signs.
    // Failure always leaves sum = 0, including overflow of the total.
    public static bool TrySum(ReadOnlySpan<char> text, out int sum)
    {
        int separator = text.IndexOf(';');
        sum = 0;
        return separator >= 0 &&
            Combine(text[..separator], text[(separator + 1)..], out sum);
    }

    public static bool TrySumWithSplit(string text, out int sum)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] parts = text.Split(';');
        sum = 0;
        return parts.Length == 2 && Combine(parts[0], parts[1], out sum);
    }

    private static bool Combine(ReadOnlySpan<char> left, ReadOnlySpan<char> right, out int sum)
    {
        sum = 0;
        if (!Number(left, out int a) || !Number(right, out int b)) return false;
        long total = (long)a + b;
        if (total > int.MaxValue) return false;
        sum = (int)total;
        return true;
    }

    private static bool Number(ReadOnlySpan<char> text, out int number)
    {
        number = 0;
        if (text.IsEmpty) return false;
        foreach (char c in text)
            if (c is < '0' or > '9') return false;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
