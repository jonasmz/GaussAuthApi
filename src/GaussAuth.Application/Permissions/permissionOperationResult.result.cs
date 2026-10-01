using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Application.Permissions;

public sealed class PermissionOperationResult
{
    public DomainPermission? Permission { get; private init; }
    public string? Failure { get; private init; }
    public static PermissionOperationResult Success(DomainPermission value) => new() { Permission = value };
    public static PermissionOperationResult Invalid() => new() { Failure = "invalid" };
    public static PermissionOperationResult ApplicationNotFound() => new() { Failure = "application-not-found" };
    public static PermissionOperationResult InactiveApplication() => new() { Failure = "inactive-application" };
    public static PermissionOperationResult PermissionNotFound() => new() { Failure = "permission-not-found" };
    public static PermissionOperationResult Duplicate() => new() { Failure = "duplicate" };
}
