using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace InteropProbe;

[SupportedOSPlatform("windows")]
public static partial class NativeFile
{
    private const uint GenericRead = 0x80000000;
    private const uint ShareRead = 1;
    private const uint OpenExisting = 3;
    private const uint NormalAttributes = 0x80;

    // HANDLE is pointer-sized; DWORD is 32-bit even on Windows x64.
    // The SafeFileHandle returned here owns the handle and invokes CloseHandle.
    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string fileName, uint desiredAccess,
        uint shareMode, nint securityAttributes, uint creationDisposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileSizeEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileSize(SafeFileHandle file, out long size);

    public static SafeFileHandle OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Contains('\0')) throw new ArgumentException("An embedded NUL would truncate the native path.");
        var handle = CreateFile(Path.GetFullPath(path), GenericRead, ShareRead, 0, OpenExisting, NormalAttributes, 0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError(); // Read before another native call can replace it.
            handle.Dispose();
            throw new Win32Exception(error);
        }
        return handle;
    }

    public static long Size(SafeFileHandle handle)
    {
        ObjectDisposedException.ThrowIf(handle.IsClosed, handle);
        if (!GetFileSize(handle, out long size)) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return size;
    }
}
