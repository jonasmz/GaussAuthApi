using GaussAuth.Domain.Users;

namespace GaussAuth.Application.Administration.Users.ListUsers;

public sealed class ListUsersResult
{
    public IReadOnlyList<User> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public bool IsInvalid { get; private init; }

    public static ListUsersResult Success(IReadOnlyList<User> items, string? nextCursor) => new() { Items = items, NextCursor = nextCursor };
    public static ListUsersResult Invalid() => new() { IsInvalid = true };
}
