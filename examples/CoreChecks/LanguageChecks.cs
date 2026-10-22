internal static class LanguageChecks
{
    public static void Run(Action<bool, string> check)
    {
        BaseLabel baseView = new NewLabel();
        check(baseView.Name() == "base" && new NewLabel().Name() == "new", "hiding versus override");

        Tagged original = new(["one"]);
        Tagged copy = original with { };
        copy.Tags.Add("two");
        check(original.Tags.Count == 2, "record with is shallow");
        Tagged independent = original with { Tags = new(original.Tags) };
        independent.Tags.Clear();
        check(original.Tags.Count == 2, "explicit collection copy");

        object boxed = 7;
        check((int)boxed == 7, "boxing copies exact value type");
        bool invalidUnboxing = false;
        try { _ = (long)boxed; }
        catch (InvalidCastException) { invalidUnboxing = true; }
        check(invalidUnboxing, "unboxing is not a numeric conversion");

        List<Func<int>> closures = [];
        for (int i = 0; i < 3; i++)
        {
            closures.Add(() => i);
        }
        check(closures.Select(static f => f()).SequenceEqual(new[] { 3, 3, 3 }), "for captures variable");

        int produced = 0;
        bool disposed = false;
        IEnumerable<int> source = Iterate();
        check(produced == 0, "iterator deferred");
        foreach (int value in source)
        {
            check(value == 1, "first iterator element");
            break;
        }
        check(produced == 1 && disposed, "early disposal");
        int[] snapshot = source.ToArray();
        int before = produced;
        _ = snapshot.Sum();
        _ = snapshot.Sum();
        check(produced == before, "materialized data do not rerun source");

        List<int> values = [1, 2];
        IEnumerable<int> query = values.Where(static value => value > 1);
        values.Add(3);
        check(query.SequenceEqual(new[] { 2, 3 }), "deferred query sees updated source");
        check(Array.Empty<int>().All(static x => x > 0), "All empty");
        check(!Array.Empty<int>().Any(), "Any empty");
        check(new[] { 1, 2, 3 }.Chunk(2).Select(static x => x.Length).SequenceEqual(new[] { 2, 1 }),
            "Chunk includes tail");
        check(new[] { "report", "2026-01" } is ["report", var month] && month == "2026-01", "list pattern");

        IEnumerable<int> Iterate()
        {
            try
            {
                for (int n = 1; n <= 3; n++)
                {
                    produced++;
                    yield return n;
                }
            }
            finally
            {
                disposed = true;
            }
        }
    }

    private class BaseLabel { public string Name() => "base"; }
    private sealed class NewLabel : BaseLabel { public new string Name() => "new"; }
    private sealed record Tagged(List<string> Tags);
}
