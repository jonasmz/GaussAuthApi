using GaussAuth.Application.Applications.Ports;
using DomainApplication = GaussAuth.Domain.Applications.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class ApplicationRepository(AuthenticationDbContext context) : IApplicationRepository
{
    public Task<DomainApplication?> GetByIdAsync(Guid id, CancellationToken ct) => context.Applications.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<DomainApplication?> GetByCodeAsync(string code, CancellationToken ct) => context.Applications.SingleOrDefaultAsync(x => x.Code == code, ct);
    public async Task<IReadOnlyList<DomainApplication>> ListAsync(Guid? afterId, int limit, CancellationToken ct) => await context.Applications.OrderBy(x => x.Id).Where(x => !afterId.HasValue || x.Id.CompareTo(afterId.Value) > 0).Take(limit).ToListAsync(ct);
    public async Task AddAsync(DomainApplication application, CancellationToken ct) => await context.Applications.AddAsync(application, ct);
    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
    public async Task<bool> TrySaveChangesAsync(CancellationToken ct) { try { await context.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation && p.ConstraintName == "IX_Applications_Code") { return false; } }
}
