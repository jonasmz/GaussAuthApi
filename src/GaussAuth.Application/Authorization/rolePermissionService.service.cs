using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Permissions.Ports;
using GaussAuth.Application.Roles.Ports;
using GaussAuth.Domain.Authorization;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Authorization;

public sealed class RolePermissionService(
    IRolePermissionRepository rolePermissions,
    IRoleRepository roles,
    IPermissionRepository permissions,
    IApplicationRepository applications,
    ISecurityEventRecorder securityEvents, ILogger<RolePermissionService> logger)
{
    public async Task<AuthorizationOperationResult> AssignAsync(Guid applicationId, Guid roleId, Guid permissionId, CancellationToken ct)
    {
        if (applicationId == Guid.Empty || roleId == Guid.Empty || permissionId == Guid.Empty) return AuthorizationOperationResult.Invalid();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return AuthorizationOperationResult.ApplicationNotFound();
        var role = await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct); if (role is null) return AuthorizationOperationResult.RoleNotFound();
        var permission = await permissions.GetByIdAndApplicationAsync(permissionId, applicationId, ct); if (permission is null) return AuthorizationOperationResult.PermissionNotFound();
        if (!application.IsActive) return AuthorizationOperationResult.InactiveApplication();
        if (!role.IsActive) return AuthorizationOperationResult.InactiveRole();
        if (!permission.IsActive) return AuthorizationOperationResult.InactivePermission();

        var existing = await rolePermissions.GetAsync(roleId, permissionId, ct);
        if (existing is not null)
        {
            if (existing.ApplicationId != applicationId) return AuthorizationOperationResult.CrossApplicationMismatch();
            if (existing.IsActive) return AuthorizationOperationResult.DuplicateActive();
            existing.Activate(DateTimeOffset.UtcNow);
            await rolePermissions.SaveChangesAsync(ct);
            await securityEvents.RecordAsync(SecurityEventType.PermissionAssigned, null, applicationId, null, "role-permission", existing.Id, ct);
            logger.LogInformation("RolePermission {RolePermissionId} lifecycle transition completed with outcome {Outcome}.", existing.Id, "reactivated");
            return AuthorizationOperationResult.SuccessRolePermission(existing, created: false);
        }

        var rolePermission = RolePermission.Create(Guid.NewGuid(), applicationId, roleId, permissionId, DateTimeOffset.UtcNow);
        await rolePermissions.AddAsync(rolePermission, ct);
        if (!await rolePermissions.TrySaveChangesAsync(ct)) return AuthorizationOperationResult.DuplicateActive();
        await securityEvents.RecordAsync(SecurityEventType.PermissionAssigned, null, applicationId, null, "role-permission", rolePermission.Id, ct);
        logger.LogInformation("RolePermission {RolePermissionId} created.", rolePermission.Id);
        return AuthorizationOperationResult.SuccessRolePermission(rolePermission, created: true);
    }

    public async Task<AuthorizationOperationResult> RemoveAsync(Guid applicationId, Guid roleId, Guid permissionId, CancellationToken ct)
    {
        var existing = await rolePermissions.GetAsync(roleId, permissionId, ct);
        if (existing is null || existing.ApplicationId != applicationId) return AuthorizationOperationResult.RelationshipNotFound();
        var wasActive = existing.IsActive;
        existing.Deactivate(DateTimeOffset.UtcNow);
        await rolePermissions.SaveChangesAsync(ct);
        if (wasActive) await securityEvents.RecordAsync(SecurityEventType.PermissionRemoved, null, applicationId, null, "role-permission", existing.Id, ct);
        logger.LogInformation("RolePermission {RolePermissionId} lifecycle transition completed with outcome {Outcome}.", existing.Id, "deactivated");
        return AuthorizationOperationResult.SuccessRolePermission(existing, created: false);
    }

    public async Task<AuthorizationPage<RolePermission>> ListByRoleAsync(Guid applicationId, Guid roleId, string? cursor, int? limit, CancellationToken ct)
    {
        var role = await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct); if (role is null) return AuthorizationPage<RolePermission>.RoleNotFound();
        if (limit is < 1 or > 100 || (cursor is not null && !Guid.TryParse(cursor, out _))) return AuthorizationPage<RolePermission>.Invalid();
        var take = limit ?? 50;
        var items = await rolePermissions.ListByRoleAsync(roleId, cursor is null ? null : Guid.Parse(cursor), take, ct);
        return AuthorizationPage<RolePermission>.Success(items, items.Count == take ? items[^1].Id.ToString() : null);
    }
}
