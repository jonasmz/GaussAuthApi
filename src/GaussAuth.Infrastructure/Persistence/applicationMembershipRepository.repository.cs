using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class ApplicationMembershipRepository(AuthenticationDbContext context) : IApplicationMembershipRepository
{
    public Task<ApplicationMembership?> GetAsync(Guid userId, Guid applicationId, CancellationToken ct) => context.ApplicationMemberships.SingleOrDefaultAsync(x => x.UserId == userId && x.ApplicationId == applicationId, ct);
    public async Task<IReadOnlyList<ApplicationMembership>> ListByUserAsync(Guid userId, Guid? afterId, int limit, CancellationToken ct) => await context.ApplicationMemberships.Where(x => x.UserId == userId).OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task<IReadOnlyList<ApplicationMembership>> ListByApplicationAsync(Guid applicationId, Guid? afterId, int limit, CancellationToken ct) => await context.ApplicationMemberships.Where(x => x.ApplicationId == applicationId).OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task AddAsync(ApplicationMembership membership, CancellationToken ct) => await context.ApplicationMemberships.AddAsync(membership, ct);
    public async Task<bool> TrySaveChangesAsync(CancellationToken ct) { try { await context.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation && p.ConstraintName is "IX_ApplicationMemberships_UserId_ApplicationId" or "AK_ApplicationMemberships_UserId_ApplicationId") { return false; } }
}
