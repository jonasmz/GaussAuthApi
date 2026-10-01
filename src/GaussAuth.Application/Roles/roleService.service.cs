using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Roles.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Roles;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Roles;

public sealed class RoleService(IRoleRepository roles, IApplicationRepository applications, ISecurityEventRecorder securityEvents, ILogger<RoleService> logger)
{
    public async Task<RoleOperationResult> CreateAsync(Guid applicationId, string name, string? description, CancellationToken ct)
    {
        if (applicationId == Guid.Empty) return RoleOperationResult.Invalid();
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is < 1 or > 200 || description?.Length > 500) return RoleOperationResult.Invalid();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return RoleOperationResult.ApplicationNotFound();
        if (!application.IsActive) return RoleOperationResult.InactiveApplication();
        var normalizedName = trimmedName.ToUpperInvariant();
        if (await roles.GetByNormalizedNameAsync(applicationId, normalizedName, ct) is not null) return RoleOperationResult.Duplicate();
        var role = Role.Create(Guid.NewGuid(), applicationId, trimmedName, normalizedName, description, DateTimeOffset.UtcNow);
        await roles.AddAsync(role, ct);
        if (!await roles.TrySaveChangesAsync(ct)) return RoleOperationResult.Duplicate();
        await securityEvents.RecordAsync(SecurityEventType.RoleCreated, null, applicationId, null, ct);
        logger.LogInformation("Role {RoleId} created.", role.Id);
        return RoleOperationResult.Success(role);
    }

    public async Task<Role?> GetAsync(Guid applicationId, Guid roleId, CancellationToken ct) => await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct);

    public async Task<RolePage> ListAsync(Guid applicationId, string? cursor, int? limit, CancellationToken ct)
    {
        if (await applications.GetByIdAsync(applicationId, ct) is null) return RolePage.ApplicationNotFound();
        if (limit is < 1 or > 100 || (cursor is not null && !Guid.TryParse(cursor, out _))) return RolePage.Invalid();
        var take = limit ?? 50;
        var items = await roles.ListByApplicationAsync(applicationId, cursor is null ? null : Guid.Parse(cursor), take, ct);
        return RolePage.Success(items, items.Count == take ? items[^1].Id.ToString() : null);
    }

    public async Task<RoleOperationResult> UpdateDescriptionAsync(Guid applicationId, Guid roleId, string? description, CancellationToken ct)
    {
        if (description?.Length > 500) return RoleOperationResult.Invalid();
        var role = await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct); if (role is null) return RoleOperationResult.RoleNotFound();
        role.UpdateDescription(description, DateTimeOffset.UtcNow);
        await roles.SaveChangesAsync(ct);
        await securityEvents.RecordAsync(SecurityEventType.RoleActivated, null, applicationId, null, ct);
        logger.LogInformation("Role {RoleId} description updated.", role.Id);
        return RoleOperationResult.Success(role);
    }

    public async Task<RoleOperationResult> ActivateAsync(Guid applicationId, Guid roleId, CancellationToken ct)
    {
        var role = await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct); if (role is null) return RoleOperationResult.RoleNotFound();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return RoleOperationResult.ApplicationNotFound();
        if (!application.IsActive) return RoleOperationResult.InactiveApplication();
        role.Activate(DateTimeOffset.UtcNow);
        await roles.SaveChangesAsync(ct);
        await securityEvents.RecordAsync(SecurityEventType.RoleDeactivated, null, applicationId, null, ct);
        logger.LogInformation("Role {RoleId} lifecycle transition completed with outcome {Outcome}.", role.Id, "activated");
        return RoleOperationResult.Success(role);
    }

    public async Task<RoleOperationResult> DeactivateAsync(Guid applicationId, Guid roleId, CancellationToken ct)
    {
        var role = await roles.GetByIdAndApplicationAsync(roleId, applicationId, ct); if (role is null) return RoleOperationResult.RoleNotFound();
        role.Deactivate(DateTimeOffset.UtcNow);
        await roles.SaveChangesAsync(ct);
        logger.LogInformation("Role {RoleId} lifecycle transition completed with outcome {Outcome}.", role.Id, "deactivated");
        return RoleOperationResult.Success(role);
    }
}
