using GaussAuth.Application.Permissions.Ports;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class PermissionRepository(AuthenticationDbContext context) : IPermissionRepository
{
    public Task<DomainPermission?> GetByIdAsync(Guid id, CancellationToken ct) => context.Permissions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<DomainPermission?> GetByIdAndApplicationAsync(Guid id, Guid applicationId, CancellationToken ct) => context.Permissions.SingleOrDefaultAsync(x => x.Id == id && x.ApplicationId == applicationId, ct);
    public Task<DomainPermission?> GetByCodeAsync(Guid applicationId, string code, CancellationToken ct) => context.Permissions.SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.Code == code, ct);
    public async Task<IReadOnlyList<DomainPermission>> ListByApplicationAsync(Guid applicationId, Guid? afterId, int limit, CancellationToken ct) => await context.Permissions.Where(x => x.ApplicationId == applicationId).OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task AddAsync(DomainPermission permission, CancellationToken ct) => await context.Permissions.AddAsync(permission, ct);
    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
    public async Task<bool> TrySaveChangesAsync(CancellationToken ct) { try { await context.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation && p.ConstraintName == "IX_Permissions_ApplicationId_Code") { return false; } }
}
