using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Course.CompilerLab;

int checks = 0;
void Require(bool value, string message)
{
    checks++;
    if (!value) throw new InvalidOperationException(message);
}
string platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
    ?? throw new InvalidOperationException("Run using the .NET SDK/runtime.");
var references = platform.Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
CSharpCompilation Compile(string source, string name = "Course.Generated.Sample") =>
    CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parse)],
        references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable));

var cases = new (string Source, int Count)[]
{
    ("namespace Course.Domain; class C { void M() { System.Console.WriteLine(1); } }", 1),
    ("using Out = System.Console; namespace Course.Domain.Sub; class C { void M() { Out.WriteLine(1); } }", 1),
    ("using static System.Console; namespace Course.Domain; class C { void M() { WriteLine(1); } }", 1),
    ("namespace Course.Ui; class C { void M() { System.Console.WriteLine(1); } }", 0),
    ("namespace Course.DomainExtra; class C { void M() { System.Console.WriteLine(1); } }", 0),
    ("namespace Course.Domain; class Console { public static void WriteLine(int x) {} } class C { void M() { Console.WriteLine(1); } }", 0),
    ("namespace Course.Domain; class C { string M() => \"Console.WriteLine(1)\"; }", 0)
};
foreach (var test in cases)
{
    var compilation = Compile(test.Source);
    Require(!compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
        "Invalid analyzer test input");
    // Synchronous host; production async programming is covered in chapter 31.
    var diagnostics = compilation.WithAnalyzers([new DomainConsoleAnalyzer()])
        .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    Require(diagnostics.Length == test.Count &&
        diagnostics.All(d => d.Id == "COURSE001"), "Analyzer false positive/negative");
    if (test.Count > 0)
        Require(diagnostics[0].Location.IsInSource, "Diagnostic must point at a call");
}

const string consumer = """
    namespace Course.Generated;
    public static partial class BuildInfo
    {
        public static int Length => Title.Length;
    }
    """;
GeneratorDriver CreateDriver(params AdditionalText[] files) =>
    CSharpGeneratorDriver.Create([new BannerGenerator().AsSourceGenerator()],
        additionalTexts: files, parseOptions: parse);

string title = "C# \"course\"\nline 2";
GeneratorDriver driver = CreateDriver(new TextFile("course.txt", title));
driver = driver.RunGeneratorsAndUpdateCompilation(Compile(consumer), out var updated, out var errors);
Require(errors.Length == 0, "Unexpected generator diagnostics");
Require(!updated.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
    "Generated source doesn't compile");
Require(driver.GetRunResult().GeneratedTrees.Length == 1, "Expected one generated file");
using (var stream = new MemoryStream())
{
    var result = updated.Emit(stream);
    Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    stream.Position = 0;
    var context = new AssemblyLoadContext("course-generator-test", isCollectible: true);
    try
    {
        var assembly = context.LoadFromStream(stream);
        var type = assembly.GetType("Course.Generated.BuildInfo", throwOnError: true)!;
        Require((string?)type.GetProperty("Title")!.GetValue(null) == title,
            "Generated string literal changed the input");
        Require((int?)type.GetProperty("Length")!.GetValue(null) == title.Length,
            "Generated and user partial declarations did not compose");
    }
    finally { context.Unload(); }
}
foreach (AdditionalText[] files in new AdditionalText[][]
{
    [], [new TextFile("course.txt", " ")],
    [new TextFile("a/course.txt", "one"), new TextFile("b/course.txt", "two")]
})
{
    var invalid = CreateDriver(files).RunGeneratorsAndUpdateCompilation(
        Compile("class Empty {}"), out _, out var diagnostics);
    Require(diagnostics.Length == 1 && diagnostics[0].Id == "COURSE002", "Missing input diagnostic");
    Require(invalid.GetRunResult().GeneratedTrees.Length == 0, "Invalid input generated source");
}
var repeated = driver.RunGenerators(Compile(consumer));
Require(repeated.GetRunResult().GeneratedTrees.Single().ToString() ==
    driver.GetRunResult().GeneratedTrees.Single().ToString(), "Unstable generation");

// These snippets must fail for the intended semantic reason.
var rejected = new (string Source, string Id)[]
{
    ("class C { System.Span<int> field; }", "CS8345"),
    ("class C { static System.Span<int> M() { return stackalloc int[4]; } }", "CS8353"),
    ("class C { static int M() { dynamic x = 1; return x.Missing(); } }", "")
};
foreach (var test in rejected)
{
    var diagnostics = Compile(test.Source).GetDiagnostics();
    Require(test.Id.Length == 0
        ? !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
        : diagnostics.Any(d => d.Id == test.Id), $"Unexpected compiler behavior: {test.Id}");
}
Console.WriteLine($"PASS CompilerLab: {checks} assertions; semantic analyzer; generator; emit; ref safety");
Console.WriteLine("COURSE001: aliases detected; user Console ignored");
Console.WriteLine("BuildInfo.g.cs: compiled and executed");
return 0;

file sealed class TextFile(string path, string text) : AdditionalText
{
    public override string Path => path;
    public override SourceText GetText(CancellationToken cancellationToken = default) =>
        SourceText.From(text);
}
