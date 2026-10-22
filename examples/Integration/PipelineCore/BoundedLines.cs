using System.Runtime.CompilerServices;

namespace Course.Pipeline;

public static class BoundedLines
{
    // Caller owns the stream; each yielded array belongs to its consumer.
    public static async IAsyncEnumerable<byte[]> ReadAsync(Stream input, PipelineOptions options,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        options.Validate();
        byte[] buffer = new byte[4096];
        byte[] line = new byte[options.MaxLineBytes];
        long total = 0;
        int used = 0, count = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > options.MaxInputBytes) throw new InvalidDataException("input_byte_limit");
            for (int i = 0; i < read; i++)
            {
                token.ThrowIfCancellationRequested();
                if (buffer[i] == '\n')
                {
                    if (++count > options.MaxLines) throw new InvalidDataException("line_count_limit");
                    yield return Finish(line, used, count);
                    used = 0;
                }
                else
                {
                    if (used == line.Length) throw new InvalidDataException("line_byte_limit");
                    line[used++] = buffer[i];
                }
            }
        }
        if (used > 0)
        {
            if (++count > options.MaxLines) throw new InvalidDataException("line_count_limit");
            yield return Finish(line, used, count);
        }
    }

    private static byte[] Finish(byte[] buffer, int count, int lineNumber)
    {
        ReadOnlySpan<byte> text = buffer.AsSpan(0, count);
        if (text.EndsWith(new byte[] { 13 })) text = text[..^1];
        if (lineNumber == 1 && text.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) text = text[3..];
        return text.ToArray();
    }
}
