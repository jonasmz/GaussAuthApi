using System.Text.RegularExpressions;
using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using DomainApplication = GaussAuth.Domain.Applications.Application;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Applications;

public sealed class ApplicationService(IApplicationRepository repository, ISecurityEventRecorder securityEvents, ILogger<ApplicationService> logger)
{
    private static readonly Regex CodePattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<ApplicationOperationResult> CreateAsync(string code, string name, CancellationToken ct)
    {
        var normalizedCode = code.Trim().ToLowerInvariant(); var normalizedName = name.Trim();
        if (normalizedCode.Length is < 3 or > 64 || !CodePattern.IsMatch(normalizedCode) || normalizedName.Length is < 1 or > 200)
            return ApplicationOperationResult.Invalid();
        if (await repository.GetByCodeAsync(normalizedCode, ct) is not null) return ApplicationOperationResult.Duplicate();
        var application = DomainApplication.Create(Guid.NewGuid(), normalizedCode, normalizedName, DateTimeOffset.UtcNow);
        await repository.AddAsync(application, ct);
        if (!await repository.TrySaveChangesAsync(ct)) return ApplicationOperationResult.Duplicate();
        logger.LogInformation("Application {ApplicationId} created.", application.Id);
        return ApplicationOperationResult.Success(application);
    }
    public Task<DomainApplication?> GetByIdAsync(Guid id, CancellationToken ct) => repository.GetByIdAsync(id, ct);
    public Task<DomainApplication?> GetByCodeAsync(string code, CancellationToken ct) => repository.GetByCodeAsync(code.Trim().ToLowerInvariant(), ct);
    public async Task<ApplicationPage> ListAsync(string? cursor, int? limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100 || (cursor is not null && !Guid.TryParse(cursor, out _))) return ApplicationPage.Invalid();
        var items = await repository.ListAsync(cursor is null ? null : Guid.Parse(cursor), limit ?? 50, ct);
        return ApplicationPage.Success(items, items.Count == (limit ?? 50) ? items[^1].Id.ToString() : null);
    }
    public async Task<DomainApplication?> ActivateAsync(Guid id, CancellationToken ct) { var item = await repository.GetByIdAsync(id, ct); if (item is null) return null; item.Activate(DateTimeOffset.UtcNow); await repository.SaveChangesAsync(ct); await securityEvents.RecordAsync(SecurityEventType.ApplicationActivated, null, item.Id, null, ct); return item; }
    public async Task<DomainApplication?> DeactivateAsync(Guid id, CancellationToken ct) { var item = await repository.GetByIdAsync(id, ct); if (item is null) return null; item.Deactivate(DateTimeOffset.UtcNow); await repository.SaveChangesAsync(ct); await securityEvents.RecordAsync(SecurityEventType.ApplicationDeactivated, null, item.Id, null, ct); return item; }
}
