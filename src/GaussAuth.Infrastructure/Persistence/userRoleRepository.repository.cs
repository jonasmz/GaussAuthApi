using GaussAuth.Application.Authorization;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Domain.Authorization;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class UserRoleRepository(AuthenticationDbContext context) : IUserRoleRepository
{
    public Task<UserRole?> GetAsync(Guid userId, Guid roleId, Guid applicationId, CancellationToken ct) => context.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId && x.ApplicationId == applicationId, ct);
    public async Task<IReadOnlyList<UserRole>> ListByUserAndApplicationAsync(Guid userId, Guid applicationId, Guid? afterId, int limit, CancellationToken ct) => await context.UserRoles.Where(x => x.UserId == userId && x.ApplicationId == applicationId).OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task<IReadOnlyList<ActiveRole>> GetActiveRolesAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        var roles = await (from assignment in context.UserRoles
                           join membership in context.ApplicationMemberships on new { assignment.UserId, assignment.ApplicationId } equals new { membership.UserId, membership.ApplicationId }
                           join role in context.Roles on new { assignment.RoleId, assignment.ApplicationId } equals new { RoleId = role.Id, role.ApplicationId }
                           join application in context.Applications on applicationId equals application.Id
                           join user in context.DomainUsers on userId equals user.Id
                           where assignment.UserId == userId && assignment.ApplicationId == applicationId && assignment.IsActive && membership.IsActive && role.IsActive && application.IsActive && user.IsActive
                           select new { role.Id, role.Name })
            .AsNoTracking()
            .Distinct()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(ct);

        return roles.Select(item => new ActiveRole(item.Id, item.Name)).ToArray();
    }
    public async Task<IReadOnlyList<DomainPermission>> GetEffectivePermissionsAsync(Guid userId, Guid applicationId, CancellationToken ct) => await (from assignment in context.UserRoles
        join membership in context.ApplicationMemberships on new { assignment.UserId, assignment.ApplicationId } equals new { membership.UserId, membership.ApplicationId }
        join role in context.Roles on new { assignment.RoleId, assignment.ApplicationId } equals new { RoleId = role.Id, role.ApplicationId }
        join rolePermission in context.RolePermissions on new { RoleId = role.Id, role.ApplicationId } equals new { rolePermission.RoleId, rolePermission.ApplicationId }
        join permission in context.Permissions on new { PermissionId = rolePermission.PermissionId, rolePermission.ApplicationId } equals new { PermissionId = permission.Id, permission.ApplicationId }
        join application in context.Applications on applicationId equals application.Id
        join user in context.DomainUsers on userId equals user.Id
        where assignment.UserId == userId && assignment.ApplicationId == applicationId && assignment.IsActive && membership.IsActive && role.IsActive && rolePermission.IsActive && permission.IsActive && application.IsActive && user.IsActive
        select permission).AsNoTracking().Distinct().OrderBy(x => x.Code).ToListAsync(ct);
    public async Task AddAsync(UserRole userRole, CancellationToken ct) => await context.UserRoles.AddAsync(userRole, ct);
    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
    public async Task<bool> TrySaveChangesAsync(CancellationToken ct) { try { await context.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation && p.ConstraintName == "IX_UserRoles_UserId_RoleId_ApplicationId") { return false; } }
}
