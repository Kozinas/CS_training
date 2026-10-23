using MathNet.Numerics.Statistics;
using System.Numerics;

namespace Analysis;

public sealed record AnalysisResult(int Count, long TotalMinor, double MeanMinor, double SampleDeviationMinor);

public static class ExpenseAnalysis
{
    public static AnalysisResult Analyze(ReadOnlySpan<long> amounts)
    {
        if (amounts.Length is < 2 or > 100_000) throw new ArgumentException("Expected 2..100000 amounts.");
        long total = 0;
        var values = new double[amounts.Length];
        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] is <= 0 or > 99_999_999) throw new ArgumentException("Invalid amount in minor units.");
            total = checked(total + amounts[i]);
            values[i] = amounts[i];
        }
        // Money stays integral. Floating point is used only for statistical estimates.
        return new(amounts.Length, total, values.Mean(), values.StandardDeviation());
    }

    public static double ScalarSum(ReadOnlySpan<double> values)
    {
        double sum = 0;
        foreach (double value in values) sum += value;
        return sum;
    }

    public static double VectorSum(ReadOnlySpan<double> values)
    {
        var sum = Vector<double>.Zero;
        int i = 0;
        for (; i <= values.Length - Vector<double>.Count; i += Vector<double>.Count)
            sum += new Vector<double>(values.Slice(i, Vector<double>.Count));
        double result = Vector.Sum(sum);
        for (; i < values.Length; i++) result += values[i];
        return result;
    }

    public static bool NearlyEqual(double a, double b, double absolute = 1e-8, double relative = 1e-12) =>
        double.IsFinite(a) && double.IsFinite(b)
        && Math.Abs(a - b) <= absolute + relative * Math.Max(Math.Abs(a), Math.Abs(b));
}
