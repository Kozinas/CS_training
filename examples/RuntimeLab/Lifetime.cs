using System.Buffers;

namespace Course.RuntimeLab;

public sealed class PulseSource
{
    public event EventHandler? Pulse;
    public int SubscriberCount => Pulse?.GetInvocationList().Length ?? 0;
    public void Raise() => Pulse?.Invoke(this, EventArgs.Empty);
}

// Single-threaded ownership; chapter 32 discusses synchronization.
public sealed class PulseObserver : IDisposable
{
    private PulseSource? _source;
    public int Count { get; private set; }

    public PulseObserver(PulseSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        source.Pulse += OnPulse;
    }

    private void OnPulse(object? sender, EventArgs args) => Count++;

    public void Dispose()
    {
        if (_source is null) return;
        _source.Pulse -= OnPulse;
        _source = null;
    }
}

public sealed class BufferOwner : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private byte[]? _buffer;
    public int Length { get; }

    public BufferOwner(int length, ArrayPool<byte>? pool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        _pool = pool ?? ArrayPool<byte>.Shared;
        _buffer = _pool.Rent(length);
        Length = length;
    }

    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            return _buffer.AsSpan(0, Length);
        }
    }

    public void Dispose()
    {
        if (_buffer is null) return;
        _pool.Return(_buffer, clearArray: true);
        _buffer = null;
    }
}
