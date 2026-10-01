using GaussAuth.Domain.Authorization;

namespace GaussAuth.Application.Authorization.Ports;

public interface IRolePermissionRepository
{
    Task<RolePermission?> GetAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermission>> ListByRoleAsync(Guid roleId, Guid? afterId, int limit, CancellationToken cancellationToken);
    Task AddAsync(RolePermission rolePermission, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
