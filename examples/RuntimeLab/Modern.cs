namespace Course.RuntimeLab;

public delegate void RefChange(ref int value);

public sealed class Label
{
    public string Text
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Text must not be blank.", nameof(value))
            : value.Trim();
    } = "untitled";
}

public sealed class Counter
{
    public int Value { get; private set; }
    public void operator +=(int amount) => Value = checked(Value + amount);
}

public partial class PartialSample
{
    public partial PartialSample(int number);
    public partial int Number { get; }
    public partial event EventHandler? Changed;
}
