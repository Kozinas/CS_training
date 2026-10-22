using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Course.BclLab;
using Course.Import;
using Course.ImportCli;
using Course.IntegrationChecks;
using Course.Ledger.Core;
using CsvHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
void Throws<T>(Action action) where T : Exception
{
    checks++;
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}
async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    checks++;
    try { await action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}
byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
var settings = new ImportSettings();
RawExpense Row(string id = "11111111-1111-4111-8111-111111111111", string date = "2026-01-10",
    string category = "FOOD", string amount = "12.50") => new(id, date, category, amount);
ImportDocument Document(params RawExpense?[] rows) => new() { Version = 1, Records = rows.ToList() };

var queue = new PriorityQueue<string, (int, long)>();
queue.Enqueue("C", (2, 0));
queue.Enqueue("A", (1, 1));
queue.Enqueue("B", (1, 2));
Check(new[] { queue.Dequeue(), queue.Dequeue(), queue.Dequeue() }.SequenceEqual(["A", "B", "C"]),
    "Stable priority tie break");
Check(!queue.TryDequeue(out _, out _), "Empty priority queue");
var sorted = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "food", "FOOD", "travel" };
Check(sorted.Count == 2, "Ordering defines set uniqueness");
var immutable = ImmutableArray.Create(1, 2);
Check(immutable.Add(3).Length == 3 && immutable.Length == 2, "Persistent update");
var list = new List<int> { 1 };
var view = list.AsReadOnly();
list.Add(2);
Check(view.Count == 2, "Read-only view is live");
var frozen = new Dictionary<string, int> { ["food"] = 3 }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
Check(frozen["FOOD"] == 3, "Frozen comparer");
int[] ordered = [1, 3, 8];
Check(Array.BinarySearch(ordered, 3) == 1 && ~Array.BinarySearch(ordered, 4) == 2, "Binary search insertion index");
byte[] header = new byte[4];
System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, 258);
Check(header.SequenceEqual(new byte[] { 0, 0, 1, 2 }) &&
    System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header) == 258, "Explicit binary byte order");
Check(NumberAlgorithms.Sum(new[] { 2m, 3m }) == 5m, "Generic decimal math");
Check(NumberAlgorithms.Sum(new[] { BigInteger.Pow(2, 100), BigInteger.One }) > ulong.MaxValue, "BigInteger");
Throws<OverflowException>(() => NumberAlgorithms.Sum(new[] { int.MaxValue, 1 }));
var concurrent = new ConcurrentDictionary<string, int>();
Check(concurrent.TryAdd("a", 1) && !concurrent.TryAdd("a", 2), "Atomic TryAdd");

Check(TextAndTime.CountText("\U0001F600") == (2, 1, 1), "Surrogate pair");
Check(TextAndTime.CountText("e\u0301") == (2, 2, 1), "Combining mark");
Check("e\u0301".Normalize() == "\u00e9", "NFC");
Throws<DecoderFallbackException>(() => new UTF8Encoding(false, true).GetString([0xff]));
foreach (string value in new[] { "food", "food_1", "a" + new string('b', 31) })
    Check(TextAndTime.CategoryPattern().IsMatch(value), "GeneratedRegex valid");
foreach (string value in new[] { "", "Food", "food\n", "1a", "a" + new string('b', 32) })
    Check(!TextAndTime.CategoryPattern().IsMatch(value), "GeneratedRegex invalid");
var linear = new Regex("\\A[a-z]+\\z", RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
Check(linear.IsMatch("food") && !linear.IsMatch("food!"), "Nonbacktracking regex");
var zone = TextAndTime.TrainingZone();
Throws<ArgumentException>(() => TextAndTime.ToInstant(new DateTime(2026, 3, 8, 2, 30, 0), zone));
Throws<ArgumentException>(() => TextAndTime.ToInstant(new DateTime(2026, 11, 1, 1, 30, 0), zone));
Check(TextAndTime.ToInstant(new DateTime(2026, 1, 15, 12, 0, 0), zone).UtcDateTime.Hour == 17, "Winter offset");
Check(TextAndTime.ToInstant(new DateTime(2026, 7, 15, 12, 0, 0), zone).UtcDateTime.Hour == 16, "Summer offset");
var clock = new FixedClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
Check(!TextAndTime.IsExpired(clock.Now, TimeSpan.FromMinutes(1), clock), "Not expired");
Check(TextAndTime.IsExpired(clock.Now.AddMinutes(-1), TimeSpan.FromMinutes(1), clock), "Expiry equality");
var resources = new ResourceManager("BclLab.Messages", typeof(TextAndTime).Assembly);
Check(resources.GetString("Greeting", CultureInfo.GetCultureInfo("ru-RU")) == "Привет", "Resource fallback to ru");
Check(resources.GetString("Greeting", CultureInfo.GetCultureInfo("en-US")) == "Hello", "Invariant resource fallback");

CultureInfo original = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "ru-RU", "en-US", "tr-TR" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        Check(Documents.TryExpense(Row(), out Expense? expense, out _) && expense!.Category == "food",
            "Normalize category");
        byte[] encoded = Documents.Encode([expense!]);
        Check(ImportEngine.ReadState(encoded, 100).Single() == expense, "Round trip");
        foreach (string value in new[] { "0", "-1", "+1", "1,25", "1.234", "1.", ".1", "1e2", "１", "1000000" })
            Check(!Documents.TryExpense(Row(amount: value), out _, out string code) && code == "invalid_amount",
                "Bad amount " + value);
        foreach (string value in new[] { "0.01", "999999.99", "001.50", "1" })
            Check(Documents.TryExpense(Row(amount: value), out _, out _), "Valid amount " + value);
        Check(!Documents.TryExpense(Row(date: "2026-02-29"), out _, out _), "Bad date");
        Check(!Documents.TryExpense(Row(category: "еда"), out _, out _), "Bad category");
        Check(!Documents.TryExpense(Row(id: Guid.Empty.ToString()), out _, out _), "Empty id");
    }
}
finally { CultureInfo.CurrentCulture = original; }

foreach (string json in new[]
{
    "{", "{}", "{\"version\":1}", "{\"version\":1,\"records\":[],\"other\":2}",
    "{\"version\":1,\"version\":1,\"records\":[]}",
    "{\"version\":1,\"records\":[{\"amount\":1}]}",
    "{\"version\":1,\"records\":[{\"id\":\"a\",\"id\":\"b\"}]}"
})
    Throws<JsonException>(() => Documents.Parse(Utf8(json), 10));
Throws<InvalidDataException>(() => Documents.Parse(Utf8("null"), 10));
Throws<InvalidDataException>(() => Documents.Parse(Utf8("{\"version\":2,\"records\":[]}"), 10));
Throws<InvalidDataException>(() => Documents.Parse(Utf8("{\"version\":1,\"records\":null}"), 10));
Throws<InvalidDataException>(() => Documents.Parse(Utf8("{\"version\":1,\"records\":[null,null]}"), 1));
Throws<JsonException>(() => Documents.Parse([0xff], 10));
Check(Documents.Parse([0xef, 0xbb, 0xbf, .. Utf8("{\"version\":1,\"records\":[]}")], 10).Records.Count == 0, "UTF8 BOM");

var converterOptions = new JsonSerializerOptions { Converters = { new MoneyJsonConverter() } };
var converterContext = new ImportJsonContext(converterOptions);
Check(JsonSerializer.Deserialize("\"12.50\"", converterContext.Money) == new Money(12.5m), "Money converter read");
Check(JsonSerializer.Serialize(new Money(12.5m), converterContext.Money) == "\"12.50\"", "Money converter write");
Throws<JsonException>(() => JsonSerializer.Deserialize("12.5", converterContext.Money));

var csvRows = OtherFormats.ReadCsv("id,date,category,amount\r\na,2026-01-01,\"tea,\"\"milk\"\"\",1.25\r\n");
Check(csvRows[0].Category == "tea,\"milk\"", "CSV quoting");
var csvNewline = OtherFormats.ReadCsv("id,date,category,amount\na,2026-01-01,\"two\nlines\",1\n");
Check(csvNewline.Count == 1 && csvNewline[0].Category == "two\nlines", "CSV logical record");
Throws<InvalidDataException>(() => OtherFormats.ReadCsv("date,id,category,amount\n"));
Throws<CsvHelperException>(() => OtherFormats.ReadCsv("id,date,category,amount\na,b,c\n"));
Check(OtherFormats.ReadXml("<items><item>A &amp; B</item></items>").Root!.Element("item")!.Value == "A & B", "XML escape");
Throws<System.Xml.XmlException>(() => OtherFormats.ReadXml("<!DOCTYPE x [<!ENTITY e 'text'>]><x>&e;</x>"));
Check(await OtherFormats.ReadZipReportAsync(OtherFormats.ZipSingle("ledger"), 6, default) == "ledger", "ZIP round trip");
await ThrowsAsync<InvalidDataException>(() => OtherFormats.ReadZipReportAsync(OtherFormats.ZipSingle("ledger"), 5, default));
await using (var stream = new TinyReads([1, 2, 3, 4]))
    Check((await BoundedFiles.ReadAsync(stream, 4, default)).SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Short reads");
await using (var stream = new TinyReads([1, 2, 3, 4]))
    await ThrowsAsync<InvalidDataException>(() => BoundedFiles.ReadAsync(stream, 3, default));

var first = ImportEngine.Plan(Document(Row(), Row(category: "food"), Row(amount: "99"), null), [], 100);
Check(first.Report.Added == 1 && first.Report.Duplicates == 1
    && first.Report.Failed == 2 && first.Report.Total == 1, "Mixed import counts");
Check(first.Report.Errors.SequenceEqual([new RowError(3, "id_conflict"), new RowError(4, "invalid_id")]), "Error positions");
var repeated = ImportEngine.Plan(Document(Row()), first.Records, 100);
Check(repeated.Report.Added == 0 && repeated.Report.Duplicates == 1, "Repeated import idempotence");
Throws<InvalidDataException>(() => ImportEngine.Plan(Document(Row(id: "22222222-2222-4222-8222-222222222222")), first.Records, 1));
Throws<InvalidDataException>(() => ImportEngine.ReadState(Documents.Encode([first.Records[0], first.Records[0]]), 10));

// Temporary data is confined to the directory created by this test.
string folder = Path.Combine(Path.GetTempPath(), "csharp-integration-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    string state = Path.Combine(folder, "state.json");
    string input = Path.Combine(folder, "input.json");
    await File.WriteAllBytesAsync(input, Documents.Encode(first.Records));
    using var directClient = new HttpClient();
    var runner = new ImportRunner(new ImportHttpClient(directClient, Options.Create(settings)),
        Options.Create(settings), NullLogger<ImportRunner>.Instance, TimeProvider.System);
    Check((await runner.RunAsync(input, null, state, default)).Added == 1, "Persist first import");
    byte[] checkpoint = await File.ReadAllBytesAsync(state);
    Check((await runner.RunAsync(input, null, state, default)).Duplicates == 1, "Persisted deduplication");
    Check((await File.ReadAllBytesAsync(state)).SequenceEqual(checkpoint), "Duplicate does not rewrite state");
    await File.WriteAllTextAsync(input, "{invalid");
    await ThrowsAsync<JsonException>(() => runner.RunAsync(input, null, state, default));
    Check((await File.ReadAllBytesAsync(state)).SequenceEqual(checkpoint), "Bad document preserves state");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        await ThrowsAsync<OperationCanceledException>(() => BoundedFiles.ReplaceAsync(state, [1], cancelled.Token));
    }
    Check((await File.ReadAllBytesAsync(state)).SequenceEqual(checkpoint), "Cancelled write preserves old state");
    Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "Temporary file cleanup");
    string output = Path.Combine(folder, "export.json");
    await runner.ExportAsync(state, output, default);
    Check(ImportEngine.ReadState(await File.ReadAllBytesAsync(output), 100).SequenceEqual(first.Records), "Export validation");
    await ThrowsAsync<ArgumentException>(() => runner.ExportAsync(state, state, default));
    await File.WriteAllBytesAsync(input, Documents.Encode(first.Records));
    using (var held = new FileStream(state + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        await ThrowsAsync<IOException>(() => runner.RunAsync(input, null, state, default));
    Check((await File.ReadAllBytesAsync(state)).SequenceEqual(checkpoint), "Exclusive lock preserves state");
    await using (var server = new LoopbackServer((_, _) => new Reply(200, "{}", DelayBodyMs: 10_000)))
    using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
    {
        await ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(null, server.Address, state, cancel.Token));
        Check((await File.ReadAllBytesAsync(state)).SequenceEqual(checkpoint), "Cancelled HTTP import preserves state");
    }
}
finally { Directory.Delete(folder, recursive: true); }

using (var services = new ServiceCollection().AddScoped<ScopedToken>().AddTransient<TransientToken>()
    .AddSingleton<SingletonToken>().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }))
{
    using var scope = services.CreateScope();
    var local = scope.ServiceProvider;
    Check(ReferenceEquals(local.GetRequiredService<ScopedToken>(), local.GetRequiredService<ScopedToken>()), "Scoped identity");
    Check(!ReferenceEquals(local.GetRequiredService<TransientToken>(), local.GetRequiredService<TransientToken>()), "Transient identity");
    Check(ReferenceEquals(services.GetRequiredService<SingletonToken>(), local.GetRequiredService<SingletonToken>()), "Singleton identity");
    Throws<InvalidOperationException>(() => services.GetRequiredService<ScopedToken>());
}
Throws<AggregateException>(() => new ServiceCollection().AddScoped<ScopedToken>().AddSingleton<Captive>()
    .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
Check(settings.IsValid() && !new ImportSettings { MaxBytes = 0 }.IsValid(), "Options limits");

ServiceProvider HttpServices(int maxBytes = 1024, int timeout = 5)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddOptions<ImportSettings>().Configure(o => { o.MaxBytes = maxBytes; o.TimeoutSeconds = timeout; });
    services.AddImportHttp();
    return services.BuildServiceProvider();
}

await using (var server = new LoopbackServer((attempt, _) => new Reply(attempt == 1 ? 503 : 200, "{}")))
using (var services = HttpServices())
{
    var client = services.GetRequiredService<ImportHttpClient>();
    Check(Encoding.UTF8.GetString(await client.DownloadAsync(server.Address, default)) == "{}", "GET after transient failure");
    Check(server.Requests == 2, "One retry");
}
await using (var server = new LoopbackServer((_, _) => new Reply(404, "{}")))
using (var services = HttpServices())
{
    await ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, default));
    Check(server.Requests == 1, "404 is not retried");
}
await using (var server = new LoopbackServer((_, _) => new Reply(503, "{}")))
using (var services = HttpServices())
{
    using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("import");
    using var response = await client.PostAsync(server.Address, new StringContent("{}"));
    Check((int)response.StatusCode == 503 && server.Requests == 1, "Unsafe POST isn't retried");
}
foreach (bool omitLength in new[] { false, true })
{
    await using var server = new LoopbackServer((_, _) => new Reply(200, new string('x', 200), OmitLength: omitLength));
    using var services = HttpServices(maxBytes: 128);
    await ThrowsAsync<InvalidDataException>(() => services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, default));
    Check(server.Requests == 1, "Size failure isn't retried");
}
await using (var server = new LoopbackServer((_, _) => new Reply(200, "{}", Type: "text/html")))
using (var services = HttpServices())
    await ThrowsAsync<InvalidDataException>(() => services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, default));
await using (var server = new LoopbackServer((_, _) => new Reply(200, "{}", Type: "Application/JSON")))
using (var services = HttpServices())
    Check((await services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, default)).Length == 2,
        "Media type is case insensitive");
await using (var server = new LoopbackServer((_, _) => new Reply(302, "{}")))
using (var services = HttpServices())
    await ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, default));
await using (var server = new LoopbackServer((_, _) => new Reply(200, "{}", DelayBodyMs: 10_000)))
using (var services = HttpServices(timeout: 1))
    await ThrowsAsync<TimeoutException>(() => services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, default));
await using (var server = new LoopbackServer((_, _) => new Reply(200, "{}", DelayBodyMs: 10_000)))
using (var services = HttpServices(timeout: 5))
using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
    await ThrowsAsync<OperationCanceledException>(() => services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, cancel.Token));

Console.WriteLine($"PASS IntegrationChecks: {checks} assertions; formats; state; DI; loopback HTTP; cancellation");
return 0;

file sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
file sealed class TinyReads(byte[] bytes) : MemoryStream(bytes)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
}
file sealed class ScopedToken;
file sealed class TransientToken;
file sealed class SingletonToken;
file sealed class Captive(ScopedToken token) { public ScopedToken Token { get; } = token; }
