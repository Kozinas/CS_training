using System.Linq.Expressions;
using System.Reflection;
using Course.RuntimeLab;

if (args.Length != 1) return 2;
switch (args[0])
{
    case "19":
        Console.WriteLine(typeof(RuntimeExamples).Assembly.GetName().Name);
        Console.WriteLine(RuntimeExamples.Twice(21));
        Console.WriteLine(typeof(int) == 42.GetType());
        Console.WriteLine(Initialized.Value);
        Console.WriteLine(Initialized.Value);
        Console.WriteLine(Initialized.Runs);
        break;
    case "19-il":
        MethodInfo method = typeof(RuntimeExamples).GetMethod(nameof(RuntimeExamples.Twice))!;
        Console.WriteLine(Convert.ToHexString(method.GetMethodBody()!.GetILAsByteArray()!));
        Console.WriteLine(RuntimeExamples.Twice(21));
        break;
    case "20":
        var source = new PulseSource();
        var observer = new PulseObserver(source);
        source.Raise();
        observer.Dispose();
        observer.Dispose();
        source.Raise();
        Console.WriteLine(observer.Count);
        Console.WriteLine(source.SubscriberCount);
        using (var buffer = new BufferOwner(4))
        {
            buffer.Span.Fill(7);
            Console.WriteLine(buffer.Span[3]);
        }
        break;
    case "21":
        Console.WriteLine(string.Join(",", MetadataValidator.Validate(new ImportOptions())));
        Console.WriteLine(MetadataValidator.Validate(new ImportOptions { Name = "ledger" }).Count);
        Expression<Func<int, bool>> positive = n => n > 0;
        Console.WriteLine(positive.Body.NodeType);
        Console.WriteLine(positive.Compile(preferInterpretation: true)(3));
        dynamic value = "abcd";
        Console.WriteLine(value.Length);
        break;
    case "23":
        Console.WriteLine(PairParser.TrySum("12;30", out int sum));
        Console.WriteLine(sum);
        Console.WriteLine(PairParser.TrySum("12;bad", out _));
        Console.WriteLine(RuntimeExamples.PointerSum([2, 3, 4]));
        Console.WriteLine(RuntimeExamples.InlineSum());
        break;
    case "23-alloc":
        RuntimeExamples.MeasureAllocations();
        break;
    case "24":
        var label = new Label { Text = "  ledger  " };
        Console.WriteLine(label.Text);
        Console.WriteLine(nameof(List<>));
        Label? absent = null;
        int evaluations = 0;
        absent?.Text = (++evaluations).ToString();
        Console.WriteLine(evaluations);
        var counter = new Counter();
        counter += 3;
        Console.WriteLine(counter.Value);
        RefChange increment = (ref value) => value++;
        int number = 4;
        increment(ref number);
        Console.WriteLine(number);
        var part = new PartialSample(9);
        part.Changed += (_, _) => Console.WriteLine(part.Number);
        part.Raise();
        Console.WriteLine(RuntimeExamples.Read(new NumberReader(7)));
        Console.WriteLine(RuntimeExamples.First<int>(new[] { 6, 8 }));
        break;
    default: return 2;
}
return 0;
