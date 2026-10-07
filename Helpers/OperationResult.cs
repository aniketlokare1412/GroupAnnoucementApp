namespace GroupAnnouncementApp.Helpers;

public class OperationResult
{
    public bool IsSuccess { get; protected init; }
    public string? ErrorMessage { get; protected init; }

    public static OperationResult Success() => new() { IsSuccess = true };
    public static OperationResult Fail(string message) => new() { IsSuccess = false, ErrorMessage = message };
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; private init; }

    public static OperationResult<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static new OperationResult<T> Fail(string message) => new() { IsSuccess = false, ErrorMessage = message };
}