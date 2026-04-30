namespace UtilityMenuSite.Core.Models.Api;

/// <summary>
/// Result wrapper for API calls. Returns <c>Success</c> with a value, or
/// <c>Failure</c> with an error code + human-readable message. Avoids
/// exceptions for expected-failure cases (auth failures, validation, 4xx).
/// </summary>
public readonly record struct ApiResult<T>(bool IsSuccess, T? Value, string? ErrorCode, string? ErrorMessage)
{
    public static ApiResult<T> Success(T value) => new(true, value, null, null);
    public static ApiResult<T> Failure(string code, string message) => new(false, default, code, message);
}

public readonly record struct ApiResult(bool IsSuccess, string? ErrorCode, string? ErrorMessage)
{
    public static ApiResult Success() => new(true, null, null);
    public static ApiResult Failure(string code, string message) => new(false, code, message);
}
