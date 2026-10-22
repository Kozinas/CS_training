using Course.ImportCli;
using Course.IntegrationChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Course.EngineeringIntegration;

public sealed class HttpTests
{
    [Fact(Timeout = 15000)]
    public async Task Transient_get_is_retried_and_body_is_returned()
    {
        await using var server = new LoopbackServer((number, _) => number == 1 ? new Reply(503, "busy") : new Reply(200, "{\"ok\":true}"));
        using var services = Build();
        byte[] body = await services.GetRequiredService<ImportHttpClient>().DownloadAsync(server.Address, TestContext.Current.CancellationToken);
        Assert.Equal("{\"ok\":true}", System.Text.Encoding.UTF8.GetString(body));
        Assert.Equal(2, server.Requests);
    }

    [Fact(Timeout = 15000)]
    public async Task Missing_resource_is_not_retried()
    {
        await using var server = new LoopbackServer((_, _) => new Reply(404, "missing"));
        using var services = Build();
        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<ImportHttpClient>()
            .DownloadAsync(server.Address, TestContext.Current.CancellationToken));
        Assert.Equal(1, server.Requests);
    }

    [Theory(Timeout = 15000)]
    [InlineData("text/html", "<html/>")]
    [InlineData("application/json", "a body exceeding the byte limit")]
    public async Task Content_contract_is_checked(string mediaType, string body)
    {
        await using var server = new LoopbackServer((_, _) => new Reply(200, body, mediaType));
        using var services = Build(8);
        await Assert.ThrowsAsync<InvalidDataException>(() => services.GetRequiredService<ImportHttpClient>()
            .DownloadAsync(server.Address, TestContext.Current.CancellationToken));
    }

    private static ServiceProvider Build(int maxBytes = 1024)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<ImportSettings>>(Options.Create(new ImportSettings { MaxBytes = maxBytes }));
        services.AddImportHttp();
        return services.BuildServiceProvider();
    }
}
