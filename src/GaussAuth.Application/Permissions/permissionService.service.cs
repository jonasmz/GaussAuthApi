using System.Text.RegularExpressions;
using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Permissions.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Permissions;

public sealed class PermissionService(IPermissionRepository permissions, IApplicationRepository applications, ISecurityEventRecorder securityEvents, ILogger<PermissionService> logger)
{
    private static readonly Regex CodePattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*(?:\\.[a-z0-9]+(?:-[a-z0-9]+)*)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<PermissionOperationResult> CreateAsync(Guid applicationId, string code, string? description, CancellationToken ct)
    {
        if (applicationId == Guid.Empty) return PermissionOperationResult.Invalid();
        var normalizedCode = code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedCode.Length is < 3 or > 128 || !CodePattern.IsMatch(normalizedCode) || description?.Length > 500) return PermissionOperationResult.Invalid();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return PermissionOperationResult.ApplicationNotFound();
        if (!application.IsActive) return PermissionOperationResult.InactiveApplication();
        if (await permissions.GetByCodeAsync(applicationId, normalizedCode, ct) is not null) return PermissionOperationResult.Duplicate();
        var permission = DomainPermission.Create(Guid.NewGuid(), applicationId, normalizedCode, description, DateTimeOffset.UtcNow);
        await permissions.AddAsync(permission, ct);
        if (!await permissions.TrySaveChangesAsync(ct)) return PermissionOperationResult.Duplicate();
        await securityEvents.RecordAsync(SecurityEventType.PermissionCreated, null, applicationId, null, "permission", permission.Id, ct);
        logger.LogInformation("Permission {PermissionId} created.", permission.Id);
        return PermissionOperationResult.Success(permission);
    }

    public async Task<DomainPermission?> GetAsync(Guid applicationId, Guid permissionId, CancellationToken ct) => await permissions.GetByIdAndApplicationAsync(permissionId, applicationId, ct);

    public async Task<PermissionPage> ListAsync(Guid applicationId, string? cursor, int? limit, CancellationToken ct)
    {
        if (await applications.GetByIdAsync(applicationId, ct) is null) return PermissionPage.ApplicationNotFound();
        if (limit is < 1 or > 100 || (cursor is not null && !Guid.TryParse(cursor, out _))) return PermissionPage.Invalid();
        var take = limit ?? 50;
        var items = await permissions.ListByApplicationAsync(applicationId, cursor is null ? null : Guid.Parse(cursor), take, ct);
        return PermissionPage.Success(items, items.Count == take ? items[^1].Id.ToString() : null);
    }

    public async Task<PermissionOperationResult> UpdateDescriptionAsync(Guid applicationId, Guid permissionId, string? description, CancellationToken ct)
    {
        if (description?.Length > 500) return PermissionOperationResult.Invalid();
        var permission = await permissions.GetByIdAndApplicationAsync(permissionId, applicationId, ct); if (permission is null) return PermissionOperationResult.PermissionNotFound();
        var previousDescription = permission.Description;
        permission.UpdateDescription(description, DateTimeOffset.UtcNow);
        await permissions.SaveChangesAsync(ct);
        if (!string.Equals(previousDescription, permission.Description, StringComparison.Ordinal)) await securityEvents.RecordAsync(SecurityEventType.PermissionUpdated, null, applicationId, null, "permission", permission.Id, ct);
        logger.LogInformation("Permission {PermissionId} description updated.", permission.Id);
        return PermissionOperationResult.Success(permission);
    }

    public async Task<PermissionOperationResult> ActivateAsync(Guid applicationId, Guid permissionId, CancellationToken ct)
    {
        var permission = await permissions.GetByIdAndApplicationAsync(permissionId, applicationId, ct); if (permission is null) return PermissionOperationResult.PermissionNotFound();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return PermissionOperationResult.ApplicationNotFound();
        if (!application.IsActive) return PermissionOperationResult.InactiveApplication();
        var wasActive = permission.IsActive;
        permission.Activate(DateTimeOffset.UtcNow);
        await permissions.SaveChangesAsync(ct);
        if (!wasActive) await securityEvents.RecordAsync(SecurityEventType.PermissionActivated, null, applicationId, null, "permission", permission.Id, ct);
        logger.LogInformation("Permission {PermissionId} lifecycle transition completed with outcome {Outcome}.", permission.Id, "activated");
        return PermissionOperationResult.Success(permission);
    }

    public async Task<PermissionOperationResult> DeactivateAsync(Guid applicationId, Guid permissionId, CancellationToken ct)
    {
        var permission = await permissions.GetByIdAndApplicationAsync(permissionId, applicationId, ct); if (permission is null) return PermissionOperationResult.PermissionNotFound();
        var wasActive = permission.IsActive;
        permission.Deactivate(DateTimeOffset.UtcNow);
        await permissions.SaveChangesAsync(ct);
        if (wasActive) await securityEvents.RecordAsync(SecurityEventType.PermissionDeactivated, null, applicationId, null, "permission", permission.Id, ct);
        logger.LogInformation("Permission {PermissionId} lifecycle transition completed with outcome {Outcome}.", permission.Id, "deactivated");
        return PermissionOperationResult.Success(permission);
    }
}
