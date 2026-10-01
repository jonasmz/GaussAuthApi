using DomainApplication = GaussAuth.Domain.Applications.Application;

namespace GaussAuth.Application.Applications.Ports;

public interface IApplicationRepository
{
    Task<DomainApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<DomainApplication?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<DomainApplication>> ListAsync(Guid? afterId, int limit, CancellationToken cancellationToken);
    Task AddAsync(DomainApplication application, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
