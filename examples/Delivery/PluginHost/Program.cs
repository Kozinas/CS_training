using PluginContracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;

if (args is not [var pluginPath]) { Console.Error.WriteLine("usage: PluginHost TRUSTED_PLUGIN_DLL"); return 2; }
try
{
    var first = Runner.Invoke(Path.GetFullPath(pluginPath), retain: false);
    bool unloaded = Runner.Collect(first.Reference);
    var second = Runner.Invoke(Path.GetFullPath(pluginPath), retain: true);
    bool retained = !Runner.Collect(second.Reference);
    Runner.Release();
    bool released = Runner.Collect(second.Reference);
    Console.WriteLine(JsonSerializer.Serialize(new { first.Name, first.Value, Unloaded = unloaded,
        Retained = retained, Released = released }));
    return unloaded && retained && released && first.Value == 1200 ? 0 : 1;
}
catch (Exception ex) when (ex is IOException or BadImageFormatException or InvalidOperationException or ReflectionTypeLoadException)
{ Console.Error.WriteLine($"plugin-error: {ex.GetType().Name}"); return 3; }

internal sealed class PluginContext(string path) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver resolver = new(path);
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share exactly one contract identity with the default context.
        if (assemblyName.Name == typeof(IReportRule).Assembly.GetName().Name)
            return typeof(IReportRule).Assembly;
        var dependency = resolver.ResolveAssemblyToPath(assemblyName);
        return dependency is null ? null : LoadFromAssemblyPath(dependency);
    }
    protected override nint LoadUnmanagedDll(string name)
    {
        var path = resolver.ResolveUnmanagedDllToPath(name);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}

internal static class Runner
{
    private static IReportRule? retainedRule;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static (WeakReference Reference, string Name, long Value) Invoke(string path, bool retain)
    {
        var context = new PluginContext(path);
        var reference = new WeakReference(context);
        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            var type = assembly.GetTypes().Single(t => typeof(IReportRule).IsAssignableFrom(t) && !t.IsAbstract);
            var rule = (IReportRule)(Activator.CreateInstance(type) ?? throw new InvalidOperationException("No rule."));
            if (retain) retainedRule = rule;
            return (reference, rule.Name, rule.Apply(1250));
        }
        finally { context.Unload(); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Release() => retainedRule = null;
    public static bool Collect(WeakReference reference)
    {
        // Forced GC is a diagnostic probe, never a production unload loop.
        for (int i = 0; i < 20 && reference.IsAlive; i++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        return !reference.IsAlive;
    }
}
