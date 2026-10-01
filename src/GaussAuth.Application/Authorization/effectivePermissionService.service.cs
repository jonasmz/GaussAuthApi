using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Users.Ports;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Application.Authorization;

public sealed class EffectivePermissionService(IUserRoleRepository userRoles, IUserRepository users, IApplicationRepository applications)
{
    public async Task<AuthorizationPage<DomainPermission>> GetAsync(Guid applicationId, Guid userId, CancellationToken ct)
    {
        if (await users.GetByIdAsync(userId, ct) is null) return AuthorizationPage<DomainPermission>.UserNotFound();
        if (await applications.GetByIdAsync(applicationId, ct) is null) return AuthorizationPage<DomainPermission>.ApplicationNotFound();
        var items = await userRoles.GetEffectivePermissionsAsync(userId, applicationId, ct);
        return AuthorizationPage<DomainPermission>.Success(items, null);
    }
}
