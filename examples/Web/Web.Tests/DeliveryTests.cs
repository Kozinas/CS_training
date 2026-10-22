using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Course.Web.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Course.Web.Tests.WebFactory;

namespace Course.Web.Tests;

public sealed class DeliveryTests
{
    [Fact]
    public async Task Crash_after_consumer_commit_replays_without_repeating_effect()
    {
        using var f = new WebFactory(); var user = await f.User(); var row = Row();
        await Import(user.Client, new(Guid.NewGuid(), [row]));
        await using var scope = f.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        dispatcher.AfterConsumerCommit = () => throw new IOException("injected_before_ack");
        await Assert.ThrowsAsync<IOException>(() => dispatcher.Drain(Token));
        Assert.Equal(1, await f.InDb(db => db.Inbox.CountAsync(Token)));
        Assert.Equal(1, await f.InDb(db => db.Outbox.CountAsync(o => !o.Delivered, Token)));
        dispatcher.AfterConsumerCommit = null;
        Assert.Equal(1, await dispatcher.Drain(Token));
        Assert.Equal(0, await dispatcher.Drain(Token));
        var projection = await f.InDb(db => db.Projections.SingleAsync(Token));
        Assert.Equal(1, projection.Count); Assert.Equal(1250, projection.AmountMinor);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PutAsJsonAsync($"/api/v1/expenses/{row.Id}", new UpdateInput(0, 20), Token)).StatusCode);
        Assert.Equal(1, await dispatcher.Drain(Token));
        projection = await f.InDb(db => db.Projections.SingleAsync(Token));
        Assert.Equal(1, projection.Count); Assert.Equal(2000, projection.AmountMinor);
    }

    [Fact]
    public async Task Duplicate_consumers_share_an_inbox_transaction()
    {
        using var f = new WebFactory(); var user = await f.User();
        await Import(user.Client, new(Guid.NewGuid(), [Row()]));
        var message = await f.InDb(db => db.Outbox.AsNoTracking().SingleAsync(Token));
        await using var scope = f.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        var results = await Task.WhenAll(Task.Run(() => dispatcher.Consume(message, Token), Token),
            Task.Run(() => dispatcher.Consume(message, Token), Token));
        Assert.Single(results, result => result);
        Assert.Equal(1, await f.InDb(db => db.Inbox.CountAsync(Token)));
        Assert.Equal(1250, await f.InDb(db => db.Projections.Select(p => p.AmountMinor).SingleAsync(Token)));
    }

    [Fact]
    public async Task Grpc_uses_real_serialization_and_authentication()
    {
        using var f = new WebFactory(); var user = await f.User();
        await Import(user.Client, new(Guid.NewGuid(), [Row()]));
        using var channel = GrpcChannel.ForAddress("https://localhost", new GrpcChannelOptions { HttpClient = user.Client });
        var rpc = new Reports.ReportsClient(channel);
        var report = await rpc.GetAsync(new ReportRequest(), cancellationToken: Token);
        Assert.Equal(1250, Assert.Single(report.Lines).AmountMinor);
        using var anonymous = f.Client();
        using var noAuthChannel = GrpcChannel.ForAddress("https://localhost", new GrpcChannelOptions { HttpClient = anonymous });
        var error = await Assert.ThrowsAsync<RpcException>(async () =>
            await new Reports.ReportsClient(noAuthChannel).GetAsync(new ReportRequest(), cancellationToken: Token));
        Assert.Equal(StatusCode.Unauthenticated, error.StatusCode);
        var bob = await f.User();
        using var bobChannel = GrpcChannel.ForAddress("https://localhost", new GrpcChannelOptions { HttpClient = bob.Client });
        Assert.Empty((await new Reports.ReportsClient(bobChannel).GetAsync(new ReportRequest(), cancellationToken: Token)).Lines);
    }

    [Fact]
    public async Task Signalr_notification_only_reaches_the_owners_group()
    {
        using var f = new WebFactory(); var alice = await f.User(); var bob = await f.User();
        HubConnection Connect(string token) => new HubConnectionBuilder().WithUrl("https://localhost/events", o =>
        {
            o.Transports = HttpTransportType.LongPolling;
            o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            o.HttpMessageHandlerFactory = _ => f.Server.CreateHandler();
        }).Build();
        await using var a = Connect(alice.AccessToken); await using var b = Connect(bob.AccessToken);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bobEvents = new ConcurrentQueue<string>();
        a.On<string>("changed", id => received.TrySetResult(id));
        b.On<string>("changed", bobEvents.Enqueue);
        b.On<string>("barrier", _ => barrier.TrySetResult());
        await a.StartAsync(Token); await b.StartAsync(Token);
        Assert.Equal("ready", await a.InvokeAsync<string>("Ping", Token));
        Assert.Equal("ready", await b.InvokeAsync<string>("Ping", Token));
        await Import(alice.Client, new(Guid.NewGuid(), [Row()]));
        await using var scope = f.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().Drain(Token);
        string message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.True(Guid.TryParse(message, out _));
        string bobId = await f.InDb(db => db.Users.Where(u => u.Email == bob.Email).Select(u => u.Id).SingleAsync(Token));
        await scope.ServiceProvider.GetRequiredService<IHubContext<LedgerHub>>().Clients.Group(bobId).SendAsync("barrier", "done", Token);
        await barrier.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Empty(bobEvents);
        await a.StopAsync(Token); await b.StopAsync(Token);
    }

    [Fact]
    public async Task Custom_trace_and_metric_record_commits_not_replays()
    {
        using var f = new WebFactory(); var user = await f.User();
        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WebTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        long committed = 0;
        using var meter = new MeterListener();
        meter.InstrumentPublished = (instrument, l) =>
        { if (instrument.Name == "ledger.imports.committed") l.EnableMeasurementEvents(instrument); };
        meter.SetMeasurementEventCallback<long>((_, value, tags, state) => Interlocked.Add(ref committed, value));
        meter.Start();
        var batch = new ImportInput(Guid.NewGuid(), [Row()]);
        await Import(user.Client, batch); await Import(user.Client, batch);
        Assert.Equal(1, committed);
        Assert.Equal(2, activities.Count);
        Assert.All(activities, a => { Assert.Equal("ledger.import", a.OperationName); Assert.NotEqual(default, a.TraceId); });
    }

    [Theory]
    [InlineData(503, "{}", 503)]
    [InlineData(200, "bad-json", 502)]
    [InlineData(200, "{\"value\":0}", 502)]
    [InlineData(200, "{\"value\":1.25}", 200)]
    public async Task Dependency_failure_has_bounded_and_explicit_http_outcome(int upstream, string body, int expected)
    {
        using var handler = new Upstream(upstream, body);
        using var f = new WebFactory(upstream: handler); var user = await f.User();
        var response = await user.Client.GetAsync("/api/v1/rate", Token);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(0, await f.InDb(db => db.Expenses.CountAsync(Token)));
    }
    [Theory]
    [InlineData(false, 502)]
    [InlineData(true, 504)]
    public async Task Upstream_size_and_deadline_are_bounded(bool neverRespond, int expected)
    {
        using HttpMessageHandler handler = neverRespond ? new WaitingUpstream() : new Upstream(200, new string(' ', 8193));
        using var f = new WebFactory(upstream: handler); var user = await f.User();
        Assert.Equal(expected, (int)(await user.Client.GetAsync("/api/v1/rate", Token)).StatusCode);
    }

    [Fact]
    public async Task Caller_cancellation_reaches_upstream()
    {
        using var handler = new WaitingUpstream();
        using var f = new WebFactory(upstream: handler); var user = await f.User();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var request = user.Client.GetAsync("/api/v1/rate", cancel.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await handler.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
    }
    private sealed class WaitingUpstream : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { Canceled.TrySetResult(); throw; }
            throw new InvalidOperationException("Unreachable");
        }
    }
    private sealed class Upstream(int status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) });
        }
    }
}
