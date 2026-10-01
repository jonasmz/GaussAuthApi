namespace GaussAuth.Application.Users.Ports;

public interface IUserRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
