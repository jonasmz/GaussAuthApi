using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Application.Permissions.Ports;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Application.Administration.Bootstrap;

/// <summary>
/// Adds the seeded administrative (<c>auth.*</c>) permissions to an Application. It only stages missing permissions
/// (idempotent); the caller saves, and no roles or assignments are created.
/// </summary>
public sealed class AdministrativePermissionBootstrap(IPermissionRepository permissions)
{
    public async Task EnsureAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (code, description) in AdministrativePermissionCatalog.SeededPermissions)
        {
            if (await permissions.GetByCodeAsync(applicationId, code, cancellationToken) is not null) continue;
            await permissions.AddAsync(DomainPermission.Create(Guid.NewGuid(), applicationId, code, description, now), cancellationToken);
        }
    }
}
