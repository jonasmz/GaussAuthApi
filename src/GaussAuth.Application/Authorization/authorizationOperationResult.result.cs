using GaussAuth.Domain.Authorization;

namespace GaussAuth.Application.Authorization;

public sealed class AuthorizationOperationResult
{
    public RolePermission? RolePermission { get; private init; }
    public UserRole? UserRole { get; private init; }
    public bool Created { get; private init; }
    public string? Failure { get; private init; }

    public static AuthorizationOperationResult SuccessRolePermission(RolePermission value, bool created) => new() { RolePermission = value, Created = created };
    public static AuthorizationOperationResult SuccessUserRole(UserRole value, bool created) => new() { UserRole = value, Created = created };
    public static AuthorizationOperationResult Invalid() => new() { Failure = "invalid" };
    public static AuthorizationOperationResult ApplicationNotFound() => new() { Failure = "application-not-found" };
    public static AuthorizationOperationResult RoleNotFound() => new() { Failure = "role-not-found" };
    public static AuthorizationOperationResult PermissionNotFound() => new() { Failure = "permission-not-found" };
    public static AuthorizationOperationResult UserNotFound() => new() { Failure = "user-not-found" };
    public static AuthorizationOperationResult MembershipNotFound() => new() { Failure = "membership-not-found" };
    public static AuthorizationOperationResult RelationshipNotFound() => new() { Failure = "relationship-not-found" };
    public static AuthorizationOperationResult InactiveApplication() => new() { Failure = "inactive-application" };
    public static AuthorizationOperationResult InactiveRole() => new() { Failure = "inactive-role" };
    public static AuthorizationOperationResult InactivePermission() => new() { Failure = "inactive-permission" };
    public static AuthorizationOperationResult InactiveUser() => new() { Failure = "inactive-user" };
    public static AuthorizationOperationResult InactiveMembership() => new() { Failure = "inactive-membership" };
    public static AuthorizationOperationResult CrossApplicationMismatch() => new() { Failure = "cross-application-mismatch" };
    public static AuthorizationOperationResult DuplicateActive() => new() { Failure = "duplicate" };
}
