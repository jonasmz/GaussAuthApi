using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class SessionRepository(AuthenticationDbContext context) : ISessionRepository
{
    public async Task AddAsync(Session session, CancellationToken ct) => await context.Sessions.AddAsync(session, ct);
    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct) => context.Sessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Session>> ListByUserIdAsync(Guid userId, CancellationToken ct) => await context.Sessions.Where(x => x.UserId == userId).ToListAsync(ct);
    public async Task<IReadOnlyList<Session>> ListAsync(Guid? userId, Guid? applicationId, SessionState? state, DateTimeOffset now,
        (DateTimeOffset CreatedAt, Guid Id)? after, int limit, CancellationToken ct)
    {
        var query = context.Sessions.AsNoTracking().AsQueryable();
        if (userId is { } user) query = query.Where(x => x.UserId == user);
        if (applicationId is { } application) query = query.Where(x => x.ApplicationId == application);
        query = state switch
        {
            SessionState.Revoked => query.Where(x => x.RevokedAt != null),
            SessionState.Expired => query.Where(x => x.RevokedAt == null && x.ExpiresAt <= now),
            SessionState.Active => query.Where(x => x.RevokedAt == null && x.ExpiresAt > now),
            _ => query,
        };
        if (after is { } cursor) query = query.Where(x => x.CreatedAt < cursor.CreatedAt || (x.CreatedAt == cursor.CreatedAt && x.Id.CompareTo(cursor.Id) < 0));
        return await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Session>> ListActiveAsync(Guid? userId, Guid? applicationId, DateTimeOffset now, int take, CancellationToken ct)
    {
        var query = context.Sessions.Where(x => x.RevokedAt == null && x.ExpiresAt > now);
        if (userId is { } user) query = query.Where(x => x.UserId == user);
        if (applicationId is { } application) query = query.Where(x => x.ApplicationId == application);
        return await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
