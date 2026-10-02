using GaussAuth.Application.Security.Ports;
using Microsoft.EntityFrameworkCore.Storage;

namespace GaussAuth.Infrastructure.Security;

/// <summary>
/// Database transaction over the shared scoped context: the state change and its critical security event become durable
/// together on commit, and disposal without commit rolls both back.
/// </summary>
public sealed class EfSecurityAuditTransaction(IDbContextTransaction transaction) : ISecurityAuditTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
