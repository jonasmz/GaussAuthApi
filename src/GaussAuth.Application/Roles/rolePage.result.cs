using GaussAuth.Domain.Roles;

namespace GaussAuth.Application.Roles;

public sealed class RolePage
{
    public IReadOnlyList<Role> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public string? Failure { get; private init; }
    public static RolePage Success(IReadOnlyList<Role> items, string? cursor) => new() { Items = items, NextCursor = cursor };
    public static RolePage Invalid() => new() { Failure = "invalid" };
    public static RolePage ApplicationNotFound() => new() { Failure = "application-not-found" };
}
