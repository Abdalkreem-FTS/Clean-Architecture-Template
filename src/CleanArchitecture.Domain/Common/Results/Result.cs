namespace CleanArchitecture.Domain.Common.Results;

public static class Result
{
    public static Success Success => default;
}

public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private readonly List<Error>? _errors;

    public bool IsSuccess { get; }

    private Result(Error error)
    {
        _errors = [error];
    }

    private Result(List<Error> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            throw new ArgumentException("Provide at least one error.", nameof(errors));
        }

        _errors = errors;

        IsSuccess = false;
    }

    private Result(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _value = value;

        IsSuccess = true;
    }

    public bool IsError => !IsSuccess;

    public List<Error> Errors => IsError ? _errors! : [];

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "This result is an error. Check IsSuccess before reading Value.");

    public Error TopError => IsError
        ? _errors![0]
        : throw new InvalidOperationException(
            "This result is a success. Check IsError before reading TopError.");

    public TNextValue Match<TNextValue>(Func<TValue, TNextValue> onValue, Func<List<Error>, TNextValue> onError)
        => IsSuccess ? onValue(Value!) : onError(Errors);

    public static implicit operator Result<TValue>(TValue value)
        => new(value);

    public static implicit operator Result<TValue>(Error error)
        => new(error);

    public static implicit operator Result<TValue>(List<Error> errors)
        => new(errors);
}

public readonly record struct Success;
