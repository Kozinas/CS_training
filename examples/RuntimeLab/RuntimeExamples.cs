using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Course.RuntimeLab;

public static class RuntimeExamples
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Twice(int value) => value * 2;

    public static unsafe int PointerSum(int[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int sum = 0;
        fixed (int* pointer = values)
        {
            for (int i = 0; i < values.Length; i++)
                sum = checked(sum + pointer[i]);
        }
        return sum;
    }

    public static int InlineSum()
    {
        FourNumbers numbers = default;
        numbers[0] = 2;
        numbers[3] = 5;
        ReadOnlySpan<int> view = numbers;
        int sum = 0;
        foreach (int value in view) sum += value;
        return sum;
    }

    public static int Read<T>(T reader) where T : INumberReader, allows ref struct
        => reader.Value;

    public static T First<T>(ReadOnlySpan<T> values) => values[0];

    public static ref int FirstRef(Span<int> values) => ref values[0];

    public static int Sum(scoped ReadOnlySpan<int> values)
    {
        int total = 0;
        foreach (int value in values) total = checked(total + value);
        return total;
    }

    public static int SumParams(params ReadOnlySpan<int> values) => Sum(values);

    public static int StackSum()
    {
        Span<int> scratch = stackalloc int[4];
        scratch.Clear();
        scratch[0] = 2;
        scratch[3] = 5;
        return Sum(scratch);
    }

    public static unsafe int FunctionPointerResult()
    {
        delegate* managed<int, int> managed = &Twice;
        delegate* unmanaged[Cdecl]<int, int> native = &Triple;
        return managed(3) + native(3);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Triple(int value) => value * 3;

    public static void MeasureAllocations()
    {
        const string input = "12;30";
        for (int i = 0; i < 1_000; i++)
        {
            PairParser.TrySum(input, out _);
            PairParser.TrySumWithSplit(input, out _);
        }
        long start = GC.GetAllocatedBytesForCurrentThread();
        int splitSum = 0;
        for (int i = 0; i < 10_000; i++)
        {
            PairParser.TrySumWithSplit(input, out int value);
            splitSum += value;
        }
        long splitBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        int spanSum = 0;
        for (int i = 0; i < 10_000; i++)
        {
            PairParser.TrySum(input, out int value);
            spanSum += value;
        }
        long spanBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Console.WriteLine($"checksums={splitSum},{spanSum}");
        Console.WriteLine($"split-bytes={splitBytes}; span-bytes={spanBytes}");
    }
}

public static class Initialized
{
    public static int Runs { get; }
    public static int Value { get; }
    static Initialized()
    {
        Runs++;
        Value = 7;
    }
}

[InlineArray(4)]
public struct FourNumbers
{
    private int _element0;
}

public interface INumberReader { int Value { get; } }
public readonly ref struct NumberReader(int value) : INumberReader
{
    public int Value => value;
}
