using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using Course.BclLab;
using Course.Import;

if (args.Length != 1) return 2;
switch (args[0])
{
    case "25":
        var queue = new PriorityQueue<string, (int Priority, long Sequence)>();
        queue.Enqueue("later", (2, 0));
        queue.Enqueue("first", (1, 1));
        queue.Enqueue("second", (1, 2));
        while (queue.TryDequeue(out string? job, out _)) Console.WriteLine(job);
        var original = ImmutableList.Create(1, 2);
        var changed = original.Add(3);
        Console.WriteLine($"{original.Count},{changed.Count}");
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["food"] = 2 }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        Console.WriteLine(lookup["FOOD"]);
        Console.WriteLine(BigInteger.Pow(2, 100));
        break;
    case "26":
        Console.WriteLine("e\u0301".Normalize(NormalizationForm.FormC) == "\u00e9");
        var count = TextAndTime.CountText("\U0001F600");
        Console.WriteLine($"{count.Utf16},{count.Scalars},{count.Elements}");
        Console.WriteLine(TextAndTime.CategoryPattern().IsMatch("food_1"));
        var time = TextAndTime.ToInstant(new DateTime(2026, 1, 15, 12, 0, 0), TextAndTime.TrainingZone());
        Console.WriteLine(time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Console.WriteLine(TextAndTime.TrainingZone().IsInvalidTime(new DateTime(2026, 3, 8, 2, 30, 0)));
        Console.WriteLine(TextAndTime.TrainingZone().IsAmbiguousTime(new DateTime(2026, 11, 1, 1, 30, 0)));
        break;
    case "27":
        var csv = OtherFormats.ReadCsv("id,date,category,amount\r\na,2026-01-01,\"food,office\",1.25\r\n");
        Console.WriteLine(csv[0].Category);
        var xml = OtherFormats.ReadXml("<items><item>tea &amp; milk</item></items>");
        Console.WriteLine(xml.Root!.Element("item")!.Value);
        Console.WriteLine(await OtherFormats.ReadZipReportAsync(OtherFormats.ZipSingle("ledger"), 100, default));
        break;
    default: return 2;
}
return 0;
