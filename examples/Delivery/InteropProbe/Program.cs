using InteropProbe;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("platform: this Win32 exercise requires Windows");
    return 2;
}
try
{
    if (args is not ["size", var path])
    {
        Console.Error.WriteLine("usage: InteropProbe size FILE");
        return 2;
    }
    using var file = NativeFile.OpenRead(path);
    long size = NativeFile.Size(file);
    file.Dispose(); // Deliberately prove explicit close; the using cleanup is idempotent.
    bool rejectsClosed = false;
    try { NativeFile.Size(file); }
    catch (ObjectDisposedException) { rejectsClosed = true; }
    var result = new FileReport(size, file.IsClosed, rejectsClosed);
    Console.WriteLine(JsonSerializer.Serialize(result, ReportJson.Default.FileReport));
    return 0;
}
catch (Win32Exception ex)
{
    Console.Error.WriteLine($"native-error: {ex.NativeErrorCode}");
    return 3;
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"input-error: {ex.GetType().Name}");
    return 4;
}

internal sealed record FileReport(long Bytes, bool Closed, bool RejectsClosed);
[JsonSerializable(typeof(FileReport))]
internal partial class ReportJson : JsonSerializerContext;
