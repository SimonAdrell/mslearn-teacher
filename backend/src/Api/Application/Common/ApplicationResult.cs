namespace StudyCoach.BackendApi.Application.Common;

public sealed record ApplicationError(string Code, string Message);

public sealed class ApplicationResult<T>
{
    private ApplicationResult(T? value, ApplicationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ApplicationError? Error { get; }
    public bool IsSuccess => Error is null;

    public static ApplicationResult<T> Success(T value) => new(value, null);
    public static ApplicationResult<T> Failure(string code, string message) => new(default, new ApplicationError(code, message));
}
