namespace auth.Domain.Errors;

public record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static readonly Error NullValue = new(
        "Error.NullValue",
        "A null value was provided.",
        ErrorType.Failure);
}
