using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Course.Web.Tests.WebFactory;

namespace Course.Web.Tests;

public sealed class ApiTests
{
    [Fact]
    public async Task Authentication_and_policy_have_distinct_401_403()
    {
        using var f = new WebFactory();
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/report", Token)).StatusCode);
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/report", Token)).StatusCode);
        var user = await f.User();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/v1/admin", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/auth/token",
            new { user.Email, password = "wrong" }, Token)).StatusCode);
    }

    [Fact]
    public async Task Identity_hashes_password_and_locks_after_five_failures()
    {
        using var f = new WebFactory();
        var user = await f.User();
        string? hash = await f.InDb(db => db.Users.Select(u => u.PasswordHash).SingleAsync(Token));
        Assert.NotNull(hash);
        Assert.DoesNotContain(Password, hash);
        for (int i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.PostAsJsonAsync("/auth/token",
                new { user.Email, password = "wrong" }, Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.PostAsJsonAsync("/auth/token",
            new { user.Email, password = Password }, Token)).StatusCode);
        Assert.True(await f.InDb(db => db.Users.AnyAsync(u => u.LockoutEnd != null, Token)));
    }

    [Fact]
    public async Task Ownership_applies_to_read_write_lists_reports_and_receipts()
    {
        using var f = new WebFactory();
        var alice = await f.User(); var bob = await f.User();
        var row = Row(); var batch = new ImportInput(Guid.NewGuid(), [row]);
        Assert.Equal(new ImportReply(1, 0, false), await Import(alice.Client, batch));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.GetAsync($"/api/v1/expenses/{row.Id}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.PutAsJsonAsync($"/api/v1/expenses/{row.Id}", new UpdateInput(0, 99), Token)).StatusCode);
        Assert.Empty((await bob.Client.GetFromJsonAsync<PageReply>("/api/v1/expenses", Token))!.Rows);
        Assert.Empty((await bob.Client.GetFromJsonAsync<ReportItem[]>("/api/v1/report", Token))!);
        Assert.Equal(new ImportReply(1, 0, false), await Import(bob.Client, batch));
        Assert.Equal(2, await f.InDb(db => db.Receipts.CountAsync(Token)));
        Assert.Equal(2, await f.InDb(db => db.Expenses.CountAsync(Token)));
        Assert.Equal(1250, (await alice.Client.GetFromJsonAsync<ExpenseView>($"/api/v1/expenses/{row.Id}", Token))!.AmountMinor);
    }

    [Fact]
    public async Task Replay_conflict_and_partial_rollback_preserve_atomic_contract()
    {
        using var f = new WebFactory(); var user = await f.User(); var row = Row();
        var batch = new ImportInput(Guid.NewGuid(), [row, row with { Category = " FOOD " }]);
        Assert.Equal(new ImportReply(1, 1, false), await Import(user.Client, batch));
        Assert.Equal(new ImportReply(1, 1, true), await Import(user.Client, batch));
        Assert.Equal(new ImportReply(1, 1, true), await Import(user.Client,
            batch with { Rows = [row with { Amount = 12.5m }, row with { Category = " FOOD ", Amount = 12.500m }] }));
        var conflict = await user.Client.PostAsJsonAsync("/api/v1/imports", batch with { Rows = [row] }, Token);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var partial = await user.Client.PostAsJsonAsync("/api/v1/imports", new ImportInput(Guid.NewGuid(), [Row(), row with { Amount = 99 }]), Token);
        Assert.Equal(HttpStatusCode.Conflict, partial.StatusCode);
        Assert.Equal(1, await f.InDb(db => db.Expenses.CountAsync(Token)));
        Assert.Equal(1, await f.InDb(db => db.Receipts.CountAsync(Token)));
        Assert.Equal(1, await f.InDb(db => db.Outbox.CountAsync(Token)));
    }

    [Theory]
    [InlineData("{\"batchId\":\"00000000-0000-0000-0000-000000000000\",\"rows\":[]}")]
    [InlineData("{\"batchId\":\"10000000-0000-0000-0000-000000000001\",\"rows\":[null]}")]
    [InlineData("{\"batchId\":\"10000000-0000-0000-0000-000000000001\",\"rows\":null}")]
    [InlineData("{\"owner\":\"victim\",\"rows\":[]}")]
    [InlineData("{")]
    public async Task Invalid_json_or_contract_is_400_with_no_writes(string body)
    {
        using var f = new WebFactory(); var user = await f.User();
        var response = await user.Client.PostAsync("/api/v1/imports", new StringContent(body, Encoding.UTF8, "application/json"), Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.Equal(0, await f.InDb(db => db.Expenses.CountAsync(Token)));
    }

    [Fact]
    public async Task Row_limits_precision_media_type_and_body_size_are_enforced()
    {
        using var f = new WebFactory(); var user = await f.User();
        foreach (var rows in new ExpenseInput?[][] { [Row(amount: 0)], [Row(amount: 1.234m)],
                     [Row() with { Category = null }], [Row(note: new string('n', 201))], Enumerable.Range(0, 101).Select(_ => Row()).ToArray() })
            Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.PostAsJsonAsync("/api/v1/imports", new ImportInput(Guid.NewGuid(), rows), Token)).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await user.Client.PostAsync("/api/v1/imports", new StringContent("{}"), Token)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await user.Client.PostAsync("/api/v1/imports",
            new StringContent(new string(' ', 128 * 1024 + 1), Encoding.UTF8, "application/json"), Token)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await user.Client.PostAsync("/api/v1/imports",
            new UnknownLengthBody(), Token)).StatusCode);
        Assert.Equal(0, await f.InDb(db => db.Expenses.CountAsync(Token)));
    }

    [Fact]
    public async Task Page_cursor_is_unique_and_invalid_limits_are_rejected()
    {
        using var f = new WebFactory(); var user = await f.User();
        await Import(user.Client, new(Guid.NewGuid(), [Row(), Row(), Row()]));
        var first = (await user.Client.GetFromJsonAsync<PageReply>("/api/v1/expenses?size=2", Token))!;
        Assert.Equal(2, first.Rows.Length); Assert.NotNull(first.Next);
        var second = (await user.Client.GetFromJsonAsync<PageReply>($"/api/v1/expenses?size=2&after={first.Next}", Token))!;
        Assert.Single(second.Rows); Assert.Null(second.Next);
        Assert.Equal(3, first.Rows.Concat(second.Rows).Select(r => r.Id).Distinct().Count());
        foreach (string query in new[] { "size=0", "size=101", "after=-1", "size=bad" })
            Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/v1/expenses?" + query, Token)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_updates_have_one_winner_and_cache_observes_committed_revision()
    {
        using var f = new WebFactory(); var user = await f.User(); var row = Row();
        await Import(user.Client, new(Guid.NewGuid(), [row]));
        Assert.Equal(1250, Assert.Single((await user.Client.GetFromJsonAsync<ReportItem[]>("/api/v1/report", Token))!).AmountMinor);
        var results = await Task.WhenAll(new[] { 20m, 30m }.Select(amount =>
            user.Client.PutAsJsonAsync($"/api/v1/expenses/{row.Id}", new UpdateInput(0, amount), Token)));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var current = (await user.Client.GetFromJsonAsync<ExpenseView>($"/api/v1/expenses/{row.Id}", Token))!;
        Assert.Equal(1, current.Version);
        Assert.Contains(current.AmountMinor, new long[] { 2000, 3000 });
        Assert.Equal(current.AmountMinor, Assert.Single((await user.Client.GetFromJsonAsync<ReportItem[]>("/api/v1/report", Token))!).AmountMinor);
    }

    [Fact]
    public async Task Same_batch_sent_concurrently_has_one_receipt_and_one_event()
    {
        using var f = new WebFactory(); var user = await f.User();
        var batch = new ImportInput(Guid.NewGuid(), [Row()]);
        var outcomes = await Task.WhenAll(Import(user.Client, batch), Import(user.Client, batch));
        Assert.Single(outcomes, x => x.Replayed);
        Assert.Equal(1, await f.InDb(db => db.Receipts.CountAsync(Token)));
        Assert.Equal(1, await f.InDb(db => db.Outbox.CountAsync(Token)));
    }

    [Fact]
    public async Task Rate_limits_are_per_authenticated_user()
    {
        using var f = new WebFactory(permits: 2); var alice = await f.User(); var bob = await f.User();
        for (int i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.OK, (await alice.Client.GetAsync("/api/v1/report", Token)).StatusCode);
        var rejected = await alice.Client.GetAsync("/api/v1/report", Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), rejected.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, (await bob.Client.GetAsync("/api/v1/report", Token)).StatusCode);
    }

    [Fact]
    public async Task Openapi_and_health_have_executable_contracts()
    {
        using var f = new WebFactory(); using var client = f.Client();
        Assert.Equal("Healthy", await client.GetStringAsync("/health/live", Token));
        Assert.Equal("Healthy", await client.GetStringAsync("/health/ready", Token));
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Token);
        Assert.StartsWith("3.1", document.GetProperty("openapi").GetString());
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/imports", out _));
        Assert.True(document.GetProperty("components").GetProperty("schemas").TryGetProperty("ImportReply", out _));
        Assert.Equal("bearer", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(document.GetProperty("paths").GetProperty("/api/v1/report").GetProperty("get").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        using var broken = new WebFactory(migrate: false); using var noSchema = broken.Client();
        Assert.Equal(HttpStatusCode.OK, (await noSchema.GetAsync("/health/live", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await noSchema.GetAsync("/health/ready", Token)).StatusCode);
    }

    [Theory]
    [InlineData("https://localhost:5173", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cors_preflight_only_allows_named_origin(string origin, bool allowed)
    {
        using var f = new WebFactory(); using var client = f.Client();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/imports");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var response = await client.SendAsync(request, Token);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
    }
    private sealed class UnknownLengthBody : HttpContent
    {
        public UnknownLengthBody() => Headers.ContentType = new MediaTypeHeaderValue("application/json");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            byte[] body = Encoding.UTF8.GetBytes(new string(' ', 128 * 1024 + 1));
            return stream.WriteAsync(body, 0, body.Length);
        }
    }
}
