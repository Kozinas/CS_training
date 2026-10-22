using System.Numerics;

namespace Course.BclLab;

public static class NumberAlgorithms
{
    public static T Sum<T>(IEnumerable<T> values) where T : INumber<T>
    {
        T result = T.Zero;
        foreach (T value in values) result = checked(result + value);
        return result;
    }
}
