namespace Course.Ledger.Core;

/// <summary>Either a non-null value or a stable error code. Value is invalid on failure.</summary>
public sealed class Result<T> where T : notnull
{
    private readonly T? _value;
    public bool IsSuccess { get; }
    public string? Error { get; }

    public T Value
    {
        get
        {
            if (!IsSuccess || _value is null)
            {
                throw new InvalidOperationException("A failed result has no value.");
            }
            return _value;
        }
    }

    private Result(T? value, string? error, bool success)
    {
        _value = value;
        Error = error;
        IsSuccess = success;
    }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, null, true);
    }

    public static Result<T> Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(default, error, false);
    }
}
