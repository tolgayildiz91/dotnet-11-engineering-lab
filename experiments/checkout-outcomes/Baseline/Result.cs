namespace Checkout.Baseline;

// Minimal but competent Result: a class (no default/empty state), guarded construction,
// exactly-one-branch Match, and Bind for reusable short-circuiting composition.
public sealed class Result<T, TError> where T : notnull where TError : notnull
{
    private readonly T? _value;
    private readonly TError? _error;

    private Result(T value, TError? error, bool isSuccess) => (_value, _error, IsSuccess) = (value, error, isSuccess);

    public bool IsSuccess { get; }

    public static Result<T, TError> Ok(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, default, true);
    }

    public static Result<T, TError> Fail(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default!, error, false);
    }

    public static implicit operator Result<T, TError>(T value) => Ok(value);
    public static implicit operator Result<T, TError>(TError error) => Fail(error);

    public TOut Match<TOut>(Func<T, TOut> ok, Func<TError, TOut> fail) =>
        IsSuccess ? ok(_value!) : fail(_error!);

    public Result<TNext, TError> Bind<TNext>(Func<T, Result<TNext, TError>> next) where TNext : notnull =>
        IsSuccess ? next(_value!) : Result<TNext, TError>.Fail(_error!);
}
