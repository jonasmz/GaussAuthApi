using GaussAuth.Domain.Memberships;

namespace GaussAuth.Application.Memberships.Ports;

public interface IApplicationMembershipRepository
{
    Task<ApplicationMembership?> GetAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationMembership>> ListByUserAsync(Guid userId, Guid? afterId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationMembership>> ListByApplicationAsync(Guid applicationId, Guid? afterId, int limit, CancellationToken cancellationToken);
    Task AddAsync(ApplicationMembership membership, CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
