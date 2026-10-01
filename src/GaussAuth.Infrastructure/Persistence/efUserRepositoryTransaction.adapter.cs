using GaussAuth.Application.Users.Ports;
using Microsoft.EntityFrameworkCore.Storage;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class EfUserRepositoryTransaction(IDbContextTransaction transaction) : IUserRepositoryTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) =>
        transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) =>
        transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
