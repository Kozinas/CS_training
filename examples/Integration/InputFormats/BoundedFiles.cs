namespace Course.Import;

public static class BoundedFiles
{
    public static async Task<byte[]> ReadAsync(Stream source, int maxBytes, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        using var output = new MemoryStream();
        byte[] buffer = new byte[Math.Min(8192, maxBytes)];
        while (true)
        {
            int count = await source.ReadAsync(buffer, token);
            if (count == 0) break;
            if (output.Length + count > maxBytes) throw new InvalidDataException("byte_limit");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        return output.ToArray();
    }

    public static async Task<byte[]> ReadPathAsync(string path, int maxBytes, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ReadAsync(stream, maxBytes, token);
    }

    // One writer; destination and temp reside on the same filesystem.
    // Normal failures before Move preserve the old file. Not a power-loss transaction.
    public static async Task ReplaceAsync(string path, byte[] bytes, CancellationToken token)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid() + ".tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 8192, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(bytes, token);
                await stream.FlushAsync(token);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
