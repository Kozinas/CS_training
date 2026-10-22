using System.Buffers;
using System.Globalization;
using System.Reflection;
using Course.RuntimeLab;
using Microsoft.CSharp.RuntimeBinder;

int checks = 0;
void Require(bool value, string message)
{
    checks++;
    if (!value) throw new InvalidOperationException(message);
}
void Throws<T>(Action action) where T : Exception
{
    checks++;
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}

var source = new PulseSource();
var observer = new PulseObserver(source);
source.Raise();
Require(observer.Count == 1 && source.SubscriberCount == 1, "Subscription");
observer.Dispose();
observer.Dispose();
source.Raise();
Require(observer.Count == 1 && source.SubscriberCount == 0, "Unsubscribe exactly once");
try
{
    using var temporary = new PulseObserver(source);
    throw new InvalidOperationException("exercise finally");
}
catch (InvalidOperationException) { }
Require(source.SubscriberCount == 0, "using on exceptional exit");

var pool = new TrackingPool();
var buffer = new BufferOwner(4, pool);
Require(buffer.Span.Length == 4, "Pool may rent a larger array");
buffer.Span.Fill(9);
buffer.Dispose();
buffer.Dispose();
Require(pool.Returns == 1 && pool.Cleared, "Return once with clearing");
Throws<ObjectDisposedException>(() => { _ = buffer.Span.Length; });
Throws<ArgumentOutOfRangeException>(() => new BufferOwner(0));
Throws<ArgumentNullException>(() => new PulseObserver(null!)); // deliberate invalid call

foreach (string? name in new string?[] { null, "", " ", "\t" })
    Require(MetadataValidator.Validate(new ImportOptions { Name = name }).SequenceEqual(["Name"]),
        "Nonblank metadata");
Require(MetadataValidator.Validate(new ImportOptions { Name = "ledger" }).Count == 0, "Valid option");
Throws<InvalidOperationException>(() => MetadataValidator.Validate(new BadSchema()));
Throws<TargetInvocationException>(() => MetadataValidator.Validate(new ThrowingGetter()));
Throws<ArgumentNullException>(() => MetadataValidator.Validate<ImportOptions>(null!));
Throws<ArgumentException>(() => MetadataValidator.Require(2 > 3));
dynamic text = "abc";
Require(text.Length == 3, "Dynamic binding");
Throws<RuntimeBinderException>(() => { _ = text.Missing(); });

var vectors = new (string Text, bool Success, int Sum)[]
{
    ("0;0", true, 0), ("12;30", true, 42), ("0001;2", true, 3),
    ("2147483647;0", true, int.MaxValue), ("2147483646;1", true, int.MaxValue),
    ("2147483647;1", false, 0), ("2147483648;0", false, 0),
    ("", false, 0), (";", false, 0), ("1;", false, 0), (";1", false, 0),
    ("1;2;3", false, 0), ("1; 2", false, 0), ("+1;2", false, 0),
    ("-1;2", false, 0), ("１;2", false, 0), ("1.0;2", false, 0), ("1\n;2", false, 0)
};
CultureInfo previous = CultureInfo.CurrentCulture;
try
{
    foreach (string culture in new[] { "ru-RU", "en-US", "tr-TR" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        foreach (var vector in vectors)
        {
            bool ok = PairParser.TrySum(vector.Text, out int sum);
            bool splitOk = PairParser.TrySumWithSplit(vector.Text, out int splitSum);
            Require(ok == vector.Success && sum == vector.Sum, $"Span: {vector.Text}");
            Require(splitOk == vector.Success && splitSum == vector.Sum, $"Split: {vector.Text}");
        }
        for (int a = 0; a < 20; a++)
        {
            string input = $"{a};{20 - a}";
            Require(PairParser.TrySum(input, out int sum) && sum == 20, "Generated valid pair");
        }
    }
}
finally { CultureInfo.CurrentCulture = previous; }
Require(RuntimeExamples.PointerSum([]) == 0, "Empty pinned array");
Require(RuntimeExamples.PointerSum([2, 3, 4]) == 9, "Pointer boundaries");
Throws<OverflowException>(() => RuntimeExamples.PointerSum([int.MaxValue, 1]));
Require(RuntimeExamples.InlineSum() == 7, "Inline array");
int[] numbers = [2, 3];
ref int first = ref RuntimeExamples.FirstRef(numbers);
first = 8;
Require(numbers[0] == 8, "Ref return aliases storage");
Require(RuntimeExamples.StackSum() == 7, "Initialized stack buffer");
Require(RuntimeExamples.SumParams(2, 3, 4) == 9, "Params span");
Require(RuntimeExamples.FunctionPointerResult() == 15, "Managed/unmanaged function pointers");
Require(RuntimeExamples.Read(new NumberReader(7)) == 7, "Constrained ref struct");
Require(Initialized.Value == 7 && Initialized.Value == 7 && Initialized.Runs == 1, "Static initialization");
Require(typeof(RuntimeExamples).GetMethod(nameof(RuntimeExamples.Twice))!
    .GetMethodBody()!.GetILAsByteArray()!.Length > 0, "JIT build includes IL");
var label = new Label { Text = " ledger " };
Require(label.Text == "ledger", "field validation");
Throws<ArgumentException>(() => label.Text = " ");
Require(label.Text == "ledger", "Setter failure preserves old state");
Label? absent = null;
int calls = 0;
absent?.Text = (++calls).ToString();
Require(calls == 0, "Null conditional assignment is lazy");
var counter = new Counter();
var alias = counter;
counter += 3;
Require(alias.Value == 3 && ReferenceEquals(alias, counter), "Instance compound operator mutates");
var partial = new PartialSample(9);
int events = 0;
EventHandler handler = (_, _) => events++;
partial.Changed += handler;
partial.Raise();
partial.Changed -= handler;
partial.Raise();
Require(partial.Number == 9 && events == 1, "Partial members compose");

Console.WriteLine($"PASS RuntimeChecks: {checks} assertions; ownership; metadata; parsers; modern syntax");
return 0;

file sealed class TrackingPool : ArrayPool<byte>
{
    public int Returns { get; private set; }
    public bool Cleared { get; private set; }
    public override byte[] Rent(int minimumLength) => new byte[minimumLength + 5];
    public override void Return(byte[] array, bool clearArray = false)
    {
        Returns++;
        if (clearArray) Array.Clear(array);
        Cleared = clearArray && array.All(b => b == 0);
    }
}

file sealed class BadSchema
{
    [NonBlank] public int Count => 3;
}
file sealed class ThrowingGetter
{
    [NonBlank] public string Name => throw new InvalidOperationException("Getter ran");
}
