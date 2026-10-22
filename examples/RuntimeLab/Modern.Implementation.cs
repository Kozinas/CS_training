namespace Course.RuntimeLab;

public partial class PartialSample
{
    private readonly int _number;
    private event EventHandler? _changed;
    public partial PartialSample(int number) { _number = number; }
    public partial int Number => _number;
    public partial event EventHandler? Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }
    public void Raise() => _changed?.Invoke(this, EventArgs.Empty);
}
