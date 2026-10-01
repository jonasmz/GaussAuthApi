using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Memberships;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Memberships;

public sealed class MembershipService(IApplicationMembershipRepository memberships, IApplicationRepository applications, IUserRepository users, ISecurityEventRecorder securityEvents, ILogger<MembershipService> logger)
{
    public async Task<MembershipOperationResult> CreateAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        if (userId == Guid.Empty || applicationId == Guid.Empty) return MembershipOperationResult.Invalid();
        var user = await users.GetByIdAsync(userId, ct); if (user is null) return MembershipOperationResult.UserNotFound();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return MembershipOperationResult.ApplicationNotFound();
        if (!application.IsActive) return MembershipOperationResult.InactiveApplication();
        if (await memberships.GetAsync(userId, applicationId, ct) is not null) return MembershipOperationResult.Duplicate();
        var membership = ApplicationMembership.Create(Guid.NewGuid(), userId, applicationId, user.IsActive, DateTimeOffset.UtcNow);
        await memberships.AddAsync(membership, ct);
        if (!await memberships.TrySaveChangesAsync(ct)) return MembershipOperationResult.Duplicate();
        await securityEvents.RecordAsync(SecurityEventType.MembershipCreated, userId, applicationId, null, ct);
        logger.LogInformation("Membership {MembershipId} created.", membership.Id);
        return MembershipOperationResult.Success(membership);
    }
    public Task<ApplicationMembership?> GetAsync(Guid userId, Guid applicationId, CancellationToken ct) => memberships.GetAsync(userId, applicationId, ct);
    public async Task<MembershipOperationResult> ActivateAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        if (userId == Guid.Empty || applicationId == Guid.Empty) return MembershipOperationResult.Invalid();
        var membership = await memberships.GetAsync(userId, applicationId, ct); if (membership is null) return MembershipOperationResult.MembershipNotFound();
        var user = await users.GetByIdAsync(userId, ct); if (user is null) return MembershipOperationResult.UserNotFound();
        var application = await applications.GetByIdAsync(applicationId, ct); if (application is null) return MembershipOperationResult.ApplicationNotFound();
        if (!user.IsActive) return MembershipOperationResult.InactiveUser();
        if (!application.IsActive) return MembershipOperationResult.InactiveApplication();
        membership.Activate(DateTimeOffset.UtcNow);
        await memberships.TrySaveChangesAsync(ct);
        await securityEvents.RecordAsync(SecurityEventType.MembershipActivated, userId, applicationId, null, ct);
        logger.LogInformation("Membership {MembershipId} lifecycle transition completed with outcome {Outcome}.", membership.Id, "activated");
        return MembershipOperationResult.Success(membership);
    }

    public async Task<MembershipOperationResult> DeactivateAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        if (userId == Guid.Empty || applicationId == Guid.Empty) return MembershipOperationResult.Invalid();
        var membership = await memberships.GetAsync(userId, applicationId, ct); if (membership is null) return MembershipOperationResult.MembershipNotFound();
        membership.Deactivate(DateTimeOffset.UtcNow);
        await memberships.TrySaveChangesAsync(ct);
        await securityEvents.RecordAsync(SecurityEventType.MembershipDeactivated, userId, applicationId, null, ct);
        logger.LogInformation("Membership {MembershipId} lifecycle transition completed with outcome {Outcome}.", membership.Id, "deactivated");
        return MembershipOperationResult.Success(membership);
    }
    public async Task<MembershipPage> ListByUserAsync(Guid userId, string? cursor, int? limit, CancellationToken ct) { if (await users.GetByIdAsync(userId, ct) is null) return MembershipPage.UserNotFound(); return await ListAsync((after, take) => memberships.ListByUserAsync(userId, after, take, ct), cursor, limit); }
    public async Task<MembershipPage> ListByApplicationAsync(Guid applicationId, string? cursor, int? limit, CancellationToken ct) { if (await applications.GetByIdAsync(applicationId, ct) is null) return MembershipPage.ApplicationNotFound(); return await ListAsync((after, take) => memberships.ListByApplicationAsync(applicationId, after, take, ct), cursor, limit); }
    private static async Task<MembershipPage> ListAsync(Func<Guid?, int, Task<IReadOnlyList<ApplicationMembership>>> query, string? cursor, int? limit)
    { if (limit is < 1 or > 100 || (cursor is not null && !Guid.TryParse(cursor, out _))) return MembershipPage.Invalid(); var take = limit ?? 50; var items = await query(cursor is null ? null : Guid.Parse(cursor), take); return MembershipPage.Success(items, items.Count == take ? items[^1].Id.ToString() : null); }
}
