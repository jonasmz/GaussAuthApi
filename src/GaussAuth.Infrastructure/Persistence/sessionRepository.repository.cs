using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class SessionRepository(AuthenticationDbContext context) : ISessionRepository
{
    public async Task AddAsync(Session session, CancellationToken ct) => await context.Sessions.AddAsync(session, ct);
    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct) => context.Sessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
