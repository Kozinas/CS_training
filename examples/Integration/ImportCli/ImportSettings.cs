using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Course.ImportCli;

public sealed class ImportSettings
{
    public int MaxBytes { get; set; } = 1_048_576;
    public int MaxRecords { get; set; } = 10_000;
    public int TimeoutSeconds { get; set; } = 15;

    public bool IsValid() => MaxBytes is >= 128 and <= 16_777_216
        && MaxRecords is >= 1 and <= 100_000 && TimeoutSeconds is >= 1 and <= 120;
}

public static class ImportServices
{
    public static IHttpClientBuilder AddImportHttp(this IServiceCollection services)
    {
        var builder = services.AddHttpClient("import", client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan; // Whole-operation deadline is in the client.
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false
        }).AddTypedClient<ImportHttpClient>();

        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 1; // At most two GET attempts.
            options.Retry.Delay = TimeSpan.FromMilliseconds(100);
            options.Retry.UseJitter = true;
            options.Retry.DisableForUnsafeHttpMethods();
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        });
        return builder;
    }
}
