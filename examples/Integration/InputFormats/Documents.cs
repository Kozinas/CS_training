using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Course.Ledger.Core;

namespace Course.Import;

public sealed record RawExpense(string? Id, string? Date, string? Category, string? Amount);

public sealed class ImportDocument
{
    public required int Version { get; init; }
    public required List<RawExpense?> Records { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ImportDocument))]
[JsonSerializable(typeof(Money))]
public partial class ImportJsonContext : JsonSerializerContext;

public static class Documents
{
    private static readonly ImportJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 16
    });

    public static ImportDocument Parse(ReadOnlySpan<byte> utf8, int maxRecords)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRecords);
        // Optional UTF-8 BOM, never guess UTF-16.
        if (utf8.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) utf8 = utf8[3..];
        var document = JsonSerializer.Deserialize(utf8, Context.ImportDocument)
            ?? throw new InvalidDataException("document_null");
        if (document.Version != 1) throw new InvalidDataException("unsupported_version");
        if (document.Records is null || document.Records.Count > maxRecords)
            throw new InvalidDataException("record_limit_or_null");
        return document;
    }

    public static byte[] Encode(IEnumerable<Expense> expenses) =>
        JsonSerializer.SerializeToUtf8Bytes(new ImportDocument
        {
            Version = 1,
            Records = expenses.OrderBy(e => e.Id).Select(e => (RawExpense?)new RawExpense(
                e.Id.ToString("D"), e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.Category, e.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture))).ToList()
        }, Context.ImportDocument);

    public static bool TryExpense(RawExpense? row, out Expense? expense, out string code)
    {
        expense = null;
        code = "invalid_id";
        if (row is null || !Guid.TryParseExact(row.Id, "D", out Guid id) || id == Guid.Empty) return false;
        code = "invalid_date";
        if (!DateOnly.TryParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly date)) return false;
        code = "invalid_amount";
        if (!DecimalText.TryParse(row.Amount, out decimal amount) || amount <= 0m || amount > 999_999.99m)
            return false;
        code = "invalid_category";
        if (row.Category is null) return false;
        try { expense = new Expense(id, date, row.Category, new Money(amount)); }
        catch (ArgumentException) { return false; }
        code = "";
        return true;
    }
}

public static class DecimalText
{
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (text is null || text.Length is < 1 or > 32) return false;
        int dot = text.IndexOf('.');
        if (dot == 0 || (dot >= 0 && text.Length - dot - 1 is < 1 or > 2)) return false;
        for (int i = 0; i < text.Length; i++)
            if (text[i] is < '0' or > '9' && i != dot) return false;
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}

// Separate converter demonstration; the input DTO intentionally keeps raw strings.
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !DecimalText.TryParse(reader.GetString(), out decimal value))
            throw new JsonException("Expected a nonnegative decimal string with cent precision.");
        return new Money(value);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Amount.ToString("0.00", CultureInfo.InvariantCulture));
}
