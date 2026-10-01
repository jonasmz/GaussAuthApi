using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Roles.Ports;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Authorization;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Authorization;

public sealed class UserRoleService(
    IUserRoleRepository userRoles,
    IRoleRepository roles,
    IApplicationRepository applications,
    IApplicationMembershipRepository memberships,
    IUserRepository users,
    ISecurityEventRecorder securityEvents, ILogger<UserRoleService> logger)
{
    public async Task<AuthorizationOperationResult> AssignAsync(Guid applicationId, Guid userId, Guid roleId, CancellationToken ct)
    {
        if (applicationId == Guid.Empty || userId == Guid.Empty || roleId == Guid.Empty) return AuthorizationOperationResult.Invalid();
        var user = await users.GetByIdAsync(userId, ct); if (user is null) return AuthorizationOperationResult.UserNotFound();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return AuthorizationOperationResult.ApplicationNotFound();
        var membership = await memberships.GetAsync(userId, applicationId, ct); if (membership is null) return AuthorizationOperationResult.MembershipNotFound();
        var role = await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct); if (role is null) return AuthorizationOperationResult.RoleNotFound();
        if (!user.IsActive) return AuthorizationOperationResult.InactiveUser();
        if (!application.IsActive) return AuthorizationOperationResult.InactiveApplication();
        if (!membership.IsActive) return AuthorizationOperationResult.InactiveMembership();
        if (!role.IsActive) return AuthorizationOperationResult.InactiveRole();

        var existing = await userRoles.GetAsync(userId, roleId, applicationId, ct);
        if (existing is not null)
        {
            if (existing.IsActive) return AuthorizationOperationResult.DuplicateActive();
            existing.Activate(DateTimeOffset.UtcNow);
            await userRoles.SaveChangesAsync(ct);
            await securityEvents.RecordAsync(SecurityEventType.RoleAssigned, userId, applicationId, null, ct);
            logger.LogInformation("UserRole {UserRoleId} lifecycle transition completed with outcome {Outcome}.", existing.Id, "reactivated");
            return AuthorizationOperationResult.SuccessUserRole(existing, created: false);
        }

        var userRole = UserRole.Create(Guid.NewGuid(), applicationId, userId, roleId, DateTimeOffset.UtcNow);
        await userRoles.AddAsync(userRole, ct);
        if (!await userRoles.TrySaveChangesAsync(ct)) return AuthorizationOperationResult.DuplicateActive();
        await securityEvents.RecordAsync(SecurityEventType.RoleAssigned, userId, applicationId, null, ct);
        logger.LogInformation("UserRole {UserRoleId} created.", userRole.Id);
        return AuthorizationOperationResult.SuccessUserRole(userRole, created: true);
    }

    public async Task<AuthorizationOperationResult> RemoveAsync(Guid applicationId, Guid userId, Guid roleId, CancellationToken ct)
    {
        var existing = await userRoles.GetAsync(userId, roleId, applicationId, ct);
        if (existing is null) return AuthorizationOperationResult.RelationshipNotFound();
        existing.Deactivate(DateTimeOffset.UtcNow);
        await userRoles.SaveChangesAsync(ct);
        await securityEvents.RecordAsync(SecurityEventType.RoleRemoved, userId, applicationId, null, ct);
        logger.LogInformation("UserRole {UserRoleId} lifecycle transition completed with outcome {Outcome}.", existing.Id, "deactivated");
        return AuthorizationOperationResult.SuccessUserRole(existing, created: false);
    }

    public async Task<AuthorizationPage<UserRole>> ListAsync(Guid applicationId, Guid userId, string? cursor, int? limit, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct); if (user is null) return AuthorizationPage<UserRole>.UserNotFound();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return AuthorizationPage<UserRole>.ApplicationNotFound();
        if (limit is < 1 or > 100 || (cursor is not null && !Guid.TryParse(cursor, out _))) return AuthorizationPage<UserRole>.Invalid();
        var take = limit ?? 50;
        var items = await userRoles.ListByUserAndApplicationAsync(userId, applicationId, cursor is null ? null : Guid.Parse(cursor), take, ct);
        return AuthorizationPage<UserRole>.Success(items, items.Count == take ? items[^1].Id.ToString() : null);
    }
}
