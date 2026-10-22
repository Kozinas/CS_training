using System.Globalization;

namespace Course.ExpenseLedger;

public static class ExpenseParser
{
    public static bool TryParseLine(string line, out Expense? expense, out string error)
    {
        expense = null;
        error = "";
        string[] fields = line.Split(';');
        if (fields.Length != 3)
        {
            error = "expected date;category;amount";
            return false;
        }

        string dateText = fields[0].Trim();
        string category = fields[1].Trim();
        string amountText = fields[2].Trim();
        if (dateText.Length != 10 || !DateOnly.TryParseExact(dateText, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            error = "invalid date";
            return false;
        }
        if (!IsValidCategory(category))
        {
            error = "invalid category";
            return false;
        }
        if (!TryParseAmount(amountText, out decimal amount))
        {
            error = "invalid amount";
            return false;
        }

        expense = new Expense(date, category, amount);
        return true;
    }

    public static bool TryParseAmount(string text, out decimal amount)
    {
        amount = 0m;
        if (text.Length == 0)
        {
            return false;
        }

        int dot = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '.' && dot == -1)
            {
                dot = i;
            }
            else if (c < '0' || c > '9')
            {
                return false;
            }
        }

        if (dot == 0 || (dot >= 0 && (text.Length - dot - 1 < 1 || text.Length - dot - 1 > 2)))
        {
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal parsed)
            || parsed <= 0m || parsed > 999_999.99m)
        {
            return false;
        }

        amount = parsed;
        return true;
    }

    public static bool IsValidCategory(string? category)
    {
        if (string.IsNullOrEmpty(category) || category.Length > 32 || !IsAsciiLetter(category[0]))
        {
            return false;
        }
        foreach (char c in category)
        {
            if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9') && c != '_' && c != '-')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsAsciiLetter(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
}
