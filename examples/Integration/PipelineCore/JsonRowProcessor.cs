using System.Text.Json;
using System.Text.Json.Serialization;
using Course.Import;

namespace Course.Pipeline;

[JsonSerializable(typeof(RawExpense))]
[JsonSerializable(typeof(PipelineReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class PipelineJsonContext : JsonSerializerContext;

public sealed class JsonRowProcessor : IRowProcessor
{
    private static readonly PipelineJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 16
    });

    public ValueTask<ParsedExpense> ProcessAsync(ReadOnlyMemory<byte> line, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            RawExpense? row = JsonSerializer.Deserialize(line.Span, Context.RawExpense);
            return ValueTask.FromResult(Documents.TryExpense(row, out var expense, out string code)
                ? new ParsedExpense(expense, null) : new ParsedExpense(null, code));
        }
        catch (JsonException) { return ValueTask.FromResult(new ParsedExpense(null, "invalid_json")); }
    }
}
