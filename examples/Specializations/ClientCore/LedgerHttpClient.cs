using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ClientCore;

// One transport can serve multiple callers: credentials belong to each request.
// The supplied HttpClient must not follow redirects to arbitrary destinations.
public sealed class LedgerHttpClient(HttpClient http) : IReportSource, IImportTransport
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ReportRow>> ReadAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/report", token, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = JsonSerializer.Deserialize<ReportRow[]>(await BodyAsync(response, cancellationToken), Json)
            ?? throw new JsonException("Null report.");
        if (rows.Length > 1000 || rows.Any(r => r is null || string.IsNullOrWhiteSpace(r.Category)
                || r.Count <= 0 || r.AmountMinor <= 0)
            || rows.Select(r => r.Category).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new JsonException("Invalid report.");
        return rows;
    }

    public async Task<ImportOutcome> SendAsync(string token, string payload, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/imports", token, payload, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict) return ImportOutcome.Conflict;
        response.EnsureSuccessStatusCode();
        // Only the L06 contract's 200 response acknowledges this immutable batch.
        if (response.StatusCode != HttpStatusCode.OK) throw new HttpRequestException("Unexpected import status.");
        using var receipt = JsonDocument.Parse(await BodyAsync(response, cancellationToken));
        var root = receipt.RootElement;
        if (!root.TryGetProperty("added", out var added) || !added.TryGetInt32(out var a) || a < 0
            || !root.TryGetProperty("duplicates", out var duplicate) || !duplicate.TryGetInt32(out var d) || d < 0
            || !root.TryGetProperty("replayed", out var replayed)
            || replayed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new JsonException("Invalid import receipt.");
        using var input = JsonDocument.Parse(payload);
        if ((long)a + d != input.RootElement.GetProperty("rows").GetArrayLength())
            throw new JsonException("Incomplete receipt.");
        return ImportOutcome.Accepted;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token,
        string? payload, CancellationToken cancellationToken)
    {
        var address = http.BaseAddress ?? throw new InvalidOperationException("BaseAddress is required.");
        if (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback))
            throw new InvalidOperationException("Use HTTPS, or HTTP on loopback for the local exercise.");
        if (token.Length > 8192 || token.Any(char.IsWhiteSpace) || token.Any(char.IsControl))
            throw new HttpRequestException("Invalid credential format.");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (payload is not null) request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        // ResponseContentRead includes reading the body within HttpClient.Timeout.
        // The injected client's MaxResponseContentBufferSize bounds buffering.
        try { return await http.SendAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("HTTP deadline exceeded."); }
    }

    private static async Task<byte[]> BodyAsync(HttpResponseMessage response, CancellationToken token)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        if (bytes.Length > 64 * 1024) throw new JsonException("Response exceeds 64 KiB.");
        return bytes;
    }

    public static HttpClient CreateHttpClient(Uri address) => new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = address, Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 64 * 1024
    };
}
