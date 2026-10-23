using ClientCore;
using System.Net;
using System.Text;
using System.Text.Json;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Specializations.Tests;

public sealed class ControlledSource : IReportSource
{
    public TaskCompletionSource<IReadOnlyList<ReportRow>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? Token { get; private set; }
    public CancellationToken SeenCancellation { get; private set; }
    public async Task<IReadOnlyList<ReportRow>> ReadAsync(string token, CancellationToken cancellationToken)
    {
        Token = token;
        SeenCancellation = cancellationToken;
        return await Completion.Task.WaitAsync(cancellationToken);
    }
}

public sealed class ClientTests
{
    [Fact]
    public async Task LoadingDisablesCommandAndThenPublishesReport()
    {
        var source = new ControlledSource();
        using var model = new ReportViewModel(source) { Token = "alice" };
        var pending = model.LoadCommand.ExecuteAsync(null);
        Assert.Equal("Loading", model.State);
        Assert.False(model.LoadCommand.CanExecute(null));
        Assert.True(model.LoadCancelCommand.CanExecute(null));
        source.Completion.SetResult([new("food", 2, 2000)]);
        await pending;
        Assert.Equal("Ready", model.State);
        Assert.Equal(2000, Assert.Single(model.Rows).AmountMinor);
        Assert.Equal("alice", source.Token);
        Assert.True(model.LoadCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancellationDoesNotBecomeNetworkError()
    {
        var source = new ControlledSource();
        using var model = new ReportViewModel(source);
        var pending = model.LoadCommand.ExecuteAsync(null);
        model.LoadCancelCommand.Execute(null);
        await pending;
        Assert.Equal("Canceled", model.State);
        Assert.Empty(model.Rows);
        Assert.Empty(model.Error);
        Assert.True(source.SeenCancellation.IsCancellationRequested);
    }

    [Fact]
    public async Task EmptyReportIsSuccessfulEmptyState()
    {
        var source = new ControlledSource();
        source.Completion.SetResult([]);
        using var model = new ReportViewModel(source);
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal("Empty", model.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureIsVisibleWithoutLeakingException(bool timeout)
    {
        var source = new ControlledSource();
        source.Completion.SetException(timeout ? new TimeoutException("secret-token") : new HttpRequestException("secret-token"));
        using var model = new ReportViewModel(source);
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal("Error", model.State);
        Assert.DoesNotContain("secret", model.Error);
    }

    [Fact]
    public async Task DisposalCancelsWorkAndClearsCredential()
    {
        var source = new ControlledSource();
        var model = new ReportViewModel(source) { Token = "secret" };
        var pending = model.LoadCommand.ExecuteAsync(null);
        model.Dispose();
        await pending;
        Assert.True(source.SeenCancellation.IsCancellationRequested);
        Assert.Empty(model.Token);
        Assert.Empty(model.Rows);
    }

    [Fact]
    public async Task BearerHeaderIsPerRequestAndServerJsonIsDecoded()
    {
        var tokens = new List<string?>();
        using var http = Client(request =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            Assert.Equal("/api/v1/report", request.RequestUri!.AbsolutePath);
            return Json("[{\"category\":\"food\",\"count\":2,\"amountMinor\":2000}]");
        });
        var client = new LedgerHttpClient(http);
        Assert.Equal(2000, Assert.Single(await client.ReadAsync("alice", TestContext.Current.CancellationToken)).AmountMinor);
        await client.ReadAsync("bob", TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "alice", "bob" }, tokens);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"category\":\"x\",\"count\":-1,\"amountMinor\":1}]")]
    [InlineData("not-json")]
    public async Task InvalidReportIsRejected(string body)
    {
        using var http = Client(_ => Json(body));
        await Assert.ThrowsAsync<JsonException>(() => new LedgerHttpClient(http).ReadAsync("t", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConflictDoesNotAcknowledgeImport()
    {
        using var http = Client(_ => new(HttpStatusCode.Conflict));
        Assert.Equal(ImportOutcome.Conflict, await new LedgerHttpClient(http)
            .SendAsync("t", "{}", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IncompleteReceiptCannotDeleteLocalBatch()
    {
        using var http = Client(_ => Json("{\"added\":0,\"duplicates\":0,\"replayed\":false}"));
        await Assert.ThrowsAsync<JsonException>(() => new LedgerHttpClient(http)
            .SendAsync("t", "{\"rows\":[{}]}", TestContext.Current.CancellationToken));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new Handler(response)) { BaseAddress = new Uri("https://example.invalid/"), MaxResponseContentBufferSize = 64 * 1024 };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(response(request));
    }
}
