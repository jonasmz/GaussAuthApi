namespace GaussAuth.Application.Security.Ports;

public interface ISecurityAuditTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}
