using Course.Review;

namespace Course.ReviewTests;

public sealed class PolicyTests
{
    private static readonly DateTimeOffset Submitted = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-1, ApprovalDecision.FutureSubmission)]
    [InlineData(0, ApprovalDecision.AutoApproved)]
    [InlineData(1799, ApprovalDecision.AutoApproved)]
    [InlineData(1800, ApprovalDecision.Expired)]
    [InlineData(1801, ApprovalDecision.Expired)]
    public void Age_boundary_is_half_open(int seconds, ApprovalDecision expected)
    {
        var policy = ApprovalPolicy.Standard();
        ApprovalDecision actual = policy.Evaluate(100m, "food", Submitted, Submitted.AddSeconds(seconds));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Exact_deadline_is_expired()
    {
        var policy = ApprovalPolicy.Standard();
        Assert.Equal(ApprovalDecision.Expired, policy.Evaluate(100m, "food", Submitted, Submitted.AddMinutes(30)));
        Assert.Equal(ApprovalDecision.AutoApproved,
            policy.Evaluate(100m, "food", Submitted, Submitted.AddMinutes(30).AddTicks(-1)));
    }

    [Theory]
    [InlineData("999.99", ApprovalDecision.AutoApproved)]
    [InlineData("1000.00", ApprovalDecision.AutoApproved)]
    [InlineData("1000.01", ApprovalDecision.ManualReview)]
    public void Amount_boundary_includes_the_limit(string amount, ApprovalDecision expected) =>
        Assert.Equal(expected, ApprovalPolicy.Standard().Evaluate(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            "food", Submitted, Submitted));

    [Fact]
    public void Expired_and_future_take_precedence_over_amount()
    {
        var policy = ApprovalPolicy.Standard();
        Assert.Equal(ApprovalDecision.Expired, policy.Evaluate(2000m, "food", Submitted, Submitted.AddHours(1)));
        Assert.Equal(ApprovalDecision.FutureSubmission, policy.Evaluate(2000m, "food", Submitted, Submitted.AddSeconds(-1)));
    }

    [Fact]
    public void Offsets_represent_instants_not_wall_clock_strings()
    {
        var submitted = Submitted.ToOffset(TimeSpan.FromHours(4));
        Assert.Equal(ApprovalDecision.Expired,
            ApprovalPolicy.Standard().Evaluate(100m, "food", submitted, Submitted.AddMinutes(30)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_amount_is_an_argument_error(int amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ApprovalPolicy.Standard().Evaluate(amount, "food", Submitted, Submitted));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public void Window_must_be_positive_and_at_most_one_day(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApprovalPolicy(new FixedApprovalLimit(10m), TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Category_strategy_is_a_snapshot_with_fallback()
    {
        var source = new Dictionary<string, decimal> { ["travel"] = 500m };
        var limits = new CategoryApprovalLimit(source, new FixedApprovalLimit(1000m));
        source["travel"] = 9000m;
        var policy = new ApprovalPolicy(limits, TimeSpan.FromMinutes(30));
        Assert.Equal(ApprovalDecision.ManualReview, policy.Evaluate(500.01m, " TRAVEL ", Submitted, Submitted));
        Assert.Equal(ApprovalDecision.AutoApproved, policy.Evaluate(1000m, "food", Submitted, Submitted));
        Assert.Equal(ApprovalDecision.AutoApproved, policy.Evaluate(500m, "travel", Submitted, Submitted));
    }

    [Fact]
    public void Invalid_configuration_is_rejected_early()
    {
        Assert.Throws<ArgumentNullException>(() => new ApprovalPolicy(null!, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedApprovalLimit(-1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CategoryApprovalLimit(
            new Dictionary<string, decimal> { ["food"] = -1m }, new FixedApprovalLimit(0m)));
        Assert.Throws<ArgumentException>(() => new CategoryApprovalLimit(
            new Dictionary<string, decimal> { ["food"] = 1m, [" FOOD "] = 2m }, new FixedApprovalLimit(0m)));
        Assert.Throws<ArgumentException>(() => ApprovalPolicy.Standard().Evaluate(1m, " ", Submitted, Submitted));
    }

    [Fact]
    public void Larger_amount_cannot_turn_manual_into_automatic()
    {
        // Deterministic finite property probe, not a replacement for a property-testing library.
        var policy = ApprovalPolicy.Standard();
        foreach (int cents in Enumerable.Range(99_950, 101))
        {
            var lower = policy.Evaluate(cents / 100m, "food", Submitted, Submitted);
            var higher = policy.Evaluate((cents + 1) / 100m, "food", Submitted, Submitted);
            Assert.False(lower == ApprovalDecision.ManualReview && higher == ApprovalDecision.AutoApproved);
        }
    }

    [Fact]
    public void Legacy_boolean_preserves_the_new_decision_contract()
    {
#pragma warning disable CRV001 // Intentional compatibility check for the obsolete entry point.
        Assert.True(ApprovalPolicy.Standard().CanAutoApprove(1m, "food", Submitted, Submitted));
        Assert.False(ApprovalPolicy.Standard().CanAutoApprove(1m, "food", Submitted, Submitted.AddMinutes(30)));
#pragma warning restore CRV001
    }
}
