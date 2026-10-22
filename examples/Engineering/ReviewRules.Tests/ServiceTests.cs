using Course.Review;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Course.ReviewTests;

public sealed class ServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static ReviewRequest Request() => new(Guid.NewGuid(), 100m, "food", Start);

    [Fact]
    public async Task Clock_advance_crosses_the_exact_boundary_without_sleep()
    {
        var clock = new FakeTimeProvider(Start);
        var sink = Substitute.For<IReviewSink>();
        var service = new ReviewService(ApprovalPolicy.Standard(), sink, clock);
        var request = Request();
        clock.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromTicks(1));
        var before = await service.ReviewAsync(request, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromTicks(1));
        var at = await service.ReviewAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(ApprovalDecision.AutoApproved, before.Decision);
        Assert.Equal(ApprovalDecision.Expired, at.Decision);
        Assert.Equal(Start.AddMinutes(30), at.EvaluatedAt);
        await sink.Received(1).SaveAsync(Arg.Is<ReviewRecord>(r => r == at), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Service_waits_for_sink_and_uses_one_time_snapshot()
    {
        var clock = new FakeTimeProvider(Start);
        var sink = new DelayedSink(clock);
        var service = new ReviewService(ApprovalPolicy.Standard(), sink, clock);
        Task<ReviewRecord> pending = service.ReviewAsync(Request(), TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(10));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(sink.Saved);
        Assert.Equal(Start, result.EvaluatedAt);
    }

    [Fact]
    public async Task Cancelled_call_never_reaches_the_sink()
    {
        var sink = Substitute.For<IReviewSink>();
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var service = new ReviewService(ApprovalPolicy.Standard(), sink, new FakeTimeProvider(Start));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReviewAsync(Request(), stop.Token));
        await sink.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cancellation_reaches_pending_storage()
    {
        var clock = new FakeTimeProvider(Start);
        var sink = new DelayedSink(clock);
        using var stop = new CancellationTokenSource();
        var pending = new ReviewService(ApprovalPolicy.Standard(), sink, clock).ReviewAsync(Request(), stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(sink.Saved);
    }

    [Fact]
    public async Task Storage_failure_does_not_return_success_or_increment_decorator()
    {
        var storage = Substitute.For<IReviewSink>();
        storage.SaveAsync(Arg.Any<ReviewRecord>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new IOException("injected")));
        var counted = new CountingReviewSink(storage);
        var service = new ReviewService(ApprovalPolicy.Standard(), counted, new FakeTimeProvider(Start));
        await Assert.ThrowsAsync<IOException>(() => service.ReviewAsync(Request(), TestContext.Current.CancellationToken));
        Assert.Equal(0, counted.Saved);
        await storage.Received(1).SaveAsync(Arg.Any<ReviewRecord>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Decorator_counts_completed_writes()
    {
        var counted = new CountingReviewSink(Substitute.For<IReviewSink>());
        var service = new ReviewService(ApprovalPolicy.Standard(), counted, new FakeTimeProvider(Start));
        await service.ReviewAsync(Request(), TestContext.Current.CancellationToken);
        Assert.Equal(1, counted.Saved);
    }

    [Fact]
    public async Task Invalid_request_never_reaches_the_sink()
    {
        var sink = Substitute.For<IReviewSink>();
        var service = new ReviewService(ApprovalPolicy.Standard(), sink, new FakeTimeProvider(Start));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ReviewAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(Request() with { Id = Guid.Empty }, TestContext.Current.CancellationToken));
        await sink.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    private sealed class DelayedSink(TimeProvider clock) : IReviewSink
    {
        public bool Saved { get; private set; }
        public async ValueTask SaveAsync(ReviewRecord record, CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), clock, token);
            Saved = true;
        }
    }
}
