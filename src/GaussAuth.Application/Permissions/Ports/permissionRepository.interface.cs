using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Application.Permissions.Ports;

public interface IPermissionRepository
{
    Task<DomainPermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<DomainPermission?> GetByIdAndApplicationAsync(Guid id, Guid applicationId, CancellationToken cancellationToken);
    Task<DomainPermission?> GetByCodeAsync(Guid applicationId, string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<DomainPermission>> ListByApplicationAsync(Guid applicationId, Guid? afterId, int limit, CancellationToken cancellationToken);
    Task AddAsync(DomainPermission permission, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
