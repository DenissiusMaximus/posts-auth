using auth.Domain.Errors;

namespace auth.Domain;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("A successful result must not contain an error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("A failure result must contain an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result<T> Success<T>(T value) => new(value);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Failure<T>(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value) : base(true, Error.None)
    {
        _value = value;
    }

    internal Result(Error error) : base(false, error)
    {
        _value = default;
    }

    public T Value =>
        IsFailure
            ? throw new InvalidOperationException("Cannot access the value of a failed result.")
            : _value!;

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
