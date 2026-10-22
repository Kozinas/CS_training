using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Course.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Course.Web.Tests;

internal sealed class WebFactory(int permits = 1000, bool migrate = true, HttpMessageHandler? upstream = null) : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-web-" + Guid.NewGuid().ToString("N"));
    public const string Password = "Course-Only-Passw0rd!";
    public static CancellationToken Token => TestContext.Current.CancellationToken;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(directory);
        builder.UseEnvironment("Testing");
        builder.UseSetting("Ledger:Path", Path.Combine(directory, "web.db"));
        builder.UseSetting("Worker:Enabled", "false");
        builder.UseSetting("Limits:ApiPermits", permits.ToString());
        builder.ConfigureLogging(l => l.ClearProviders());
        builder.ConfigureServices(s =>
        {
            s.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (upstream is not null) s.AddHttpClient("rates").ConfigurePrimaryHttpMessageHandler(() => upstream);
        });
    }
    public HttpClient Client()
    {
        var client = CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (migrate)
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<WebDb>().Database.Migrate();
        }
        return client;
    }
    public async Task<(HttpClient Client, string AccessToken, string Email)> User()
    {
        var client = Client();
        string email = $"{Guid.NewGuid():N}@example.test";
        var registered = await client.PostAsJsonAsync("/auth/register", new { email, password = Password }, Token);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var logged = await client.PostAsJsonAsync("/auth/token", new { email, password = Password }, Token);
        Assert.Equal(HttpStatusCode.OK, logged.StatusCode);
        var json = await logged.Content.ReadFromJsonAsync<JsonElement>(Token);
        string access = json.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return (client, access, email);
    }
    public async Task<T> InDb<T>(Func<WebDb, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<WebDb>());
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory); // Nonrecursive, fixture-owned files only.
        }
    }
    public static ExpenseInput Row(Guid? id = null, decimal amount = 12.50m, string? note = null) =>
        new(id ?? Guid.NewGuid(), new DateOnly(2026, 1, 10), "food", amount, note);
    public static async Task<ImportReply> Import(HttpClient client, ImportInput input)
    {
        var response = await client.PostAsJsonAsync("/api/v1/imports", input, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportReply>(Token))!;
    }
}
