using GaussAuth.Domain.Roles;

namespace GaussAuth.Application.Roles;

public sealed class RoleOperationResult
{
    public Role? Role { get; private init; }
    public string? Failure { get; private init; }
    public static RoleOperationResult Success(Role value) => new() { Role = value };
    public static RoleOperationResult Invalid() => new() { Failure = "invalid" };
    public static RoleOperationResult ApplicationNotFound() => new() { Failure = "application-not-found" };
    public static RoleOperationResult InactiveApplication() => new() { Failure = "inactive-application" };
    public static RoleOperationResult RoleNotFound() => new() { Failure = "role-not-found" };
    public static RoleOperationResult Duplicate() => new() { Failure = "duplicate" };
}
