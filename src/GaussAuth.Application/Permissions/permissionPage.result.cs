using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Application.Permissions;

public sealed class PermissionPage
{
    public IReadOnlyList<DomainPermission> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public string? Failure { get; private init; }
    public static PermissionPage Success(IReadOnlyList<DomainPermission> items, string? cursor) => new() { Items = items, NextCursor = cursor };
    public static PermissionPage Invalid() => new() { Failure = "invalid" };
    public static PermissionPage ApplicationNotFound() => new() { Failure = "application-not-found" };
}
