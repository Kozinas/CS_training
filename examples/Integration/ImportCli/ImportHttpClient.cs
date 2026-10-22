using Course.Import;
using Microsoft.Extensions.Options;

namespace Course.ImportCli;

public sealed class ImportHttpClient(HttpClient client, IOptions<ImportSettings> options)
{
    public async Task<byte[]> DownloadAsync(Uri uri, CancellationToken token)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttps &&
            !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            throw new ArgumentException("Use HTTPS, or loopback HTTP for local exercises.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) throw new ArgumentException("Credentials in URLs are not supported.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            response.EnsureSuccessStatusCode();
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType,
                    "application/json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("content_type");
            if (response.Content.Headers.ContentLength > options.Value.MaxBytes)
                throw new InvalidDataException("byte_limit");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            return await BoundedFiles.ReadAsync(stream, options.Value.MaxBytes, deadline.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("HTTP deadline exceeded.");
        }
    }
}
