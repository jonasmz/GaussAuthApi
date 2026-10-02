using GaussAuth.Domain.Sessions;

namespace GaussAuth.Application.Sessions.Ports;

public interface ISessionRepository
{
    Task AddAsync(Session session, CancellationToken cancellationToken);
    Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Session>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists sessions newest first by <c>(CreatedAt, Id)</c>, optionally filtered by user, Application, and derived state
    /// (evaluated at <paramref name="now"/>), continuing after the given cursor position.
    /// </summary>
    Task<IReadOnlyList<Session>> ListAsync(Guid? userId, Guid? applicationId, SessionState? state, DateTimeOffset now,
        (DateTimeOffset CreatedAt, Guid Id)? after, int limit, CancellationToken cancellationToken);

    /// <summary>Returns at most <paramref name="take"/> sessions that are neither revoked nor expired at <paramref name="now"/>, oldest first.</summary>
    Task<IReadOnlyList<Session>> ListActiveAsync(Guid? userId, Guid? applicationId, DateTimeOffset now, int take, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
