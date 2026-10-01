using GaussAuth.Domain.Security;

namespace GaussAuth.Application.Security.Ports;

public interface ISecurityEventRepository
{
    Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
