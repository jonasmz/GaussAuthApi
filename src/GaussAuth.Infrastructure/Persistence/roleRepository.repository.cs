using GaussAuth.Application.Roles.Ports;
using GaussAuth.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class RoleRepository(AuthenticationDbContext context) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct) => context.Roles.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Role?> GetByIdAndApplicationAsync(Guid id, Guid applicationId, CancellationToken ct) => context.Roles.SingleOrDefaultAsync(x => x.Id == id && x.ApplicationId == applicationId, ct);
    public Task<Role?> GetByNormalizedNameAsync(Guid applicationId, string normalizedName, CancellationToken ct) => context.Roles.SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.NormalizedName == normalizedName, ct);
    public async Task<IReadOnlyList<Role>> ListByApplicationAsync(Guid applicationId, Guid? afterId, int limit, CancellationToken ct) => await context.Roles.Where(x => x.ApplicationId == applicationId).OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task AddAsync(Role role, CancellationToken ct) => await context.Roles.AddAsync(role, ct);
    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
    public async Task<bool> TrySaveChangesAsync(CancellationToken ct) { try { await context.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation && p.ConstraintName == "IX_Roles_ApplicationId_NormalizedName") { return false; } }
}
