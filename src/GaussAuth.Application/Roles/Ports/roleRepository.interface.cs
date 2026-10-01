using GaussAuth.Domain.Roles;

namespace GaussAuth.Application.Roles.Ports;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Role?> GetByIdAndApplicationAsync(Guid id, Guid applicationId, CancellationToken cancellationToken);
    Task<Role?> GetByNormalizedNameAsync(Guid applicationId, string normalizedName, CancellationToken cancellationToken);
    Task<IReadOnlyList<Role>> ListByApplicationAsync(Guid applicationId, Guid? afterId, int limit, CancellationToken cancellationToken);
    Task AddAsync(Role role, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
