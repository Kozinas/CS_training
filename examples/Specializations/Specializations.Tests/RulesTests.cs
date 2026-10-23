using Analysis;
using GameRules;

namespace Specializations.Tests;

public sealed class RulesTests
{
    [Fact]
    public void InventoryPreservesDeduplicationAcrossSaveAndLimitsCapacity()
    {
        var first = new Inventory();
        Assert.True(first.PickUp("a", 90));
        Assert.False(first.PickUp("b", 11));
        var second = new Inventory();
        second.Restore(first.Snapshot());
        Assert.False(second.PickUp("a", 90));
        Assert.False(second.Spend(91));
        Assert.True(second.Spend(80));
        Assert.Equal(10, second.Coins);
        Assert.True(second.PickUp("b", 11)); // A rejected pickup was not consumed.
    }

    [Fact]
    public void InvalidSaveDoesNotPartiallyReplaceLiveState()
    {
        var inventory = new Inventory();
        inventory.PickUp("a", 10);
        Assert.Throws<ArgumentException>(() => inventory.Restore(new SaveData { Coins = 80, Pickups = ["a", "a"] }));
        Assert.Equal(10, inventory.Coins);
        Assert.False(inventory.PickUp("a", 10));
        Assert.Throws<ArgumentException>(() => inventory.Restore(new SaveData { Version = 2 }));
    }

    [Fact]
    public void EventsDescribeOnlySuccessfulChanges()
    {
        var inventory = new Inventory();
        var observed = new List<int>();
        inventory.Changed += observed.Add;
        inventory.PickUp("a", 10);
        inventory.PickUp("a", 10);
        inventory.Spend(100);
        inventory.Spend(1);
        Assert.Equal(new[] { 10, 9 }, observed);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void DeltaTimeAndNormalizedInputGiveSameDistance(int fps)
    {
        var motion = new Motion();
        for (int i = 0; i < fps; i++) motion.Tick(1, 1, 1.0 / fps);
        Assert.True(ExpenseAnalysis.NearlyEqual(3, Math.Sqrt(motion.X * motion.X + motion.Y * motion.Y)));
    }

    [Fact]
    public void InvalidDeltaCannotPoisonPosition()
    {
        var motion = new Motion();
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Tick(1, 0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Tick(1, 0, 1));
        Assert.Equal(0, motion.X);
    }

    [Fact]
    public void MathNetStatisticsUseSampleDeviationAndExactMoneyTotal()
    {
        var result = ExpenseAnalysis.Analyze([100, 200, 300]);
        Assert.Equal(600, result.TotalMinor);
        Assert.Equal(200, result.MeanMinor);
        Assert.Equal(100, result.SampleDeviationMinor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100001)]
    public void AnalysisRejectsInsufficientOrUnboundedInput(int count) =>
        Assert.Throws<ArgumentException>(() => ExpenseAnalysis.Analyze(new long[count]));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100000000)]
    public void AnalysisRejectsInvalidMoney(long amount) =>
        Assert.Throws<ArgumentException>(() => ExpenseAnalysis.Analyze([100, amount]));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(100003)]
    public void VectorReductionHandlesEmptyInputAndTail(int count)
    {
        var values = Enumerable.Range(0, count).Select(i => i * 0.1).ToArray();
        Assert.True(ExpenseAnalysis.NearlyEqual(ExpenseAnalysis.ScalarSum(values), ExpenseAnalysis.VectorSum(values)));
    }

    [Fact]
    public void ApproximateComparisonRejectsNonFiniteValues()
    {
        Assert.False(ExpenseAnalysis.NearlyEqual(double.NaN, double.NaN));
        Assert.False(ExpenseAnalysis.NearlyEqual(double.PositiveInfinity, double.PositiveInfinity));
        Assert.False(ExpenseAnalysis.NearlyEqual(1, 2));
    }
}
