using GaussAuth.Domain.Authorization;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Application.Authorization.Ports;

public interface IUserRoleRepository
{
    Task<UserRole?> GetAsync(Guid userId, Guid roleId, Guid applicationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserRole>> ListByUserAndApplicationAsync(Guid userId, Guid applicationId, Guid? afterId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<DomainPermission>> GetEffectivePermissionsAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);
    Task AddAsync(UserRole userRole, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
