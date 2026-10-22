using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Course.AsyncLab;

BenchmarkSwitcher.FromAssembly(typeof(AmountBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class AmountBenchmarks
{
    private string[] rows = [];
    [Params(100, 1000)] public int Count { get; set; }
    [GlobalSetup] public void Setup() => rows = Enumerable.Repeat("2026-01-10;food;12.50", Count).ToArray();
    [Benchmark(Baseline = true)] public decimal Split() => AmountAlgorithms.SplitSum(rows);
    [Benchmark] public decimal Span() => AmountAlgorithms.SpanSum(rows);
}
