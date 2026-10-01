using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Security;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class SecurityEventRepository(AuthenticationDbContext context) : ISecurityEventRepository
{
    public async Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken) =>
        await context.SecurityEvents.AddAsync(securityEvent, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
