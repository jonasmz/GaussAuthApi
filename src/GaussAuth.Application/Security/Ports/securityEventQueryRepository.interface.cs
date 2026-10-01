using GaussAuth.Domain.Security;
using GaussAuth.Application.Security;

namespace GaussAuth.Application.Security.Ports;

public interface ISecurityEventQueryRepository
{
    Task<IReadOnlyList<SecurityEvent>> ListAsync(SecurityEventQuery query, Guid? forcedApplicationId,
        bool includeGlobalEvents, CancellationToken cancellationToken);
}
