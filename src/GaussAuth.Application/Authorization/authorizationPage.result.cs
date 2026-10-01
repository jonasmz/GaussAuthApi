namespace GaussAuth.Application.Authorization;

public sealed class AuthorizationPage<T>
{
    public IReadOnlyList<T> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public string? Failure { get; private init; }

    public static AuthorizationPage<T> Success(IReadOnlyList<T> items, string? cursor) => new() { Items = items, NextCursor = cursor };
    public static AuthorizationPage<T> Invalid() => new() { Failure = "invalid" };
    public static AuthorizationPage<T> RoleNotFound() => new() { Failure = "role-not-found" };
    public static AuthorizationPage<T> UserNotFound() => new() { Failure = "user-not-found" };
    public static AuthorizationPage<T> ApplicationNotFound() => new() { Failure = "application-not-found" };
}
