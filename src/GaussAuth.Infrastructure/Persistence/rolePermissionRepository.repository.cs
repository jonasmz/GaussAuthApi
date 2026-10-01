using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class RolePermissionRepository(AuthenticationDbContext context) : IRolePermissionRepository
{
    public Task<RolePermission?> GetAsync(Guid roleId, Guid permissionId, CancellationToken ct) => context.RolePermissions.SingleOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
    public async Task<IReadOnlyList<RolePermission>> ListByRoleAsync(Guid roleId, Guid? afterId, int limit, CancellationToken ct) => await context.RolePermissions.Where(x => x.RoleId == roleId).OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task AddAsync(RolePermission rolePermission, CancellationToken ct) => await context.RolePermissions.AddAsync(rolePermission, ct);
    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
    public async Task<bool> TrySaveChangesAsync(CancellationToken ct) { try { await context.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation && p.ConstraintName == "IX_RolePermissions_RoleId_PermissionId") { return false; } }
}
