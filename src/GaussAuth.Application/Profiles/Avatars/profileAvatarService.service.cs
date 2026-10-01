using GaussAuth.Application.Profiles.Avatars.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Profiles.Avatars;

public sealed class ProfileAvatarService(
    SessionService sessions,
    IUserRepository users,
    IProfileImageProcessor processor,
    IProfileImageStorage storage,
    ISecurityEventRecorder securityEvents,
    SecurityEventCatalog catalog,
    TimeProvider timeProvider,
    ILogger<ProfileAvatarService> logger)
{
    public async Task<ProfileAvatarResult> SetAsync(string accessCredential, Stream content, CancellationToken cancellationToken)
    {
        var userId = await AuthenticateAsync(accessCredential, cancellationToken);
        if (userId is null)
        {
            return ProfileAvatarResult.NotAuthenticated();
        }

        var processed = await processor.ProcessAsync(content, cancellationToken);
        if (!processed.IsSuccess)
        {
            var rejection = processed.Rejection!.Value;
            logger.LogInformation("Profile image upload rejected for user {UserId}: {Reason}.", userId, RejectionReason(rejection));
            await securityEvents.RecordAsync(
                new SecurityEventDraft(catalog.Get(SecurityEventType.AvatarUploadRejected), userId, null, null,
                    Reason: RejectionReason(rejection)),
                cancellationToken);
            return ProfileAvatarResult.Rejected(rejection);
        }

        var staged = await storage.StageAsync(processed.Image!, cancellationToken);
        var promoted = false;
        var committed = false;
        try
        {
            await storage.PromoteAsync(staged, cancellationToken);
            promoted = true;

            string? previous;
            UserProfile profile;
            await using (var transaction = await users.BeginTransactionAsync(cancellationToken))
            {
                var user = await users.GetByIdForUpdateAsync(userId.Value, cancellationToken);
                if (user is null || !user.IsActive)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    await DeleteQuietlyAsync(staged.Reference, userId.Value, "set-inactive", cancellationToken);
                    return ProfileAvatarResult.NotAuthenticated();
                }

                previous = user.Profile.AvatarReference;
                user.SetAvatarReference(staged.Reference.Value, timeProvider.GetUtcNow());
                await users.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                profile = user.Profile;
            }

            logger.LogInformation("Profile avatar set for user {UserId} with reference {Reference}.", userId, staged.Reference.Value);
            await RetirePreviousAsync(previous, userId.Value, cancellationToken);
            await securityEvents.RecordAsync(SecurityEventType.AvatarUpdated, userId, null, null, cancellationToken);
            return ProfileAvatarResult.Success(profile);
        }
        catch
        {
            // Compensate only before the reference commits; afterwards the new file is the current avatar.
            if (committed)
            {
                throw;
            }

            if (promoted)
            {
                await DeleteQuietlyAsync(staged.Reference, userId.Value, "set-compensation", CancellationToken.None);
            }
            else
            {
                await storage.DiscardStagedAsync(staged, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<ProfileAvatarResult> RemoveAsync(string accessCredential, CancellationToken cancellationToken)
    {
        var userId = await AuthenticateAsync(accessCredential, cancellationToken);
        if (userId is null)
        {
            return ProfileAvatarResult.NotAuthenticated();
        }

        string? previous;
        UserProfile profile;
        await using (var transaction = await users.BeginTransactionAsync(cancellationToken))
        {
            var user = await users.GetByIdForUpdateAsync(userId.Value, cancellationToken);
            if (user is null || !user.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProfileAvatarResult.NotAuthenticated();
            }

            previous = user.Profile.AvatarReference;
            if (previous is not null)
            {
                user.ClearAvatarReference(timeProvider.GetUtcNow());
                await users.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            profile = user.Profile;
        }

        if (previous is not null)
        {
            logger.LogInformation("Profile avatar removed for user {UserId}.", userId);
            await RetirePreviousAsync(previous, userId.Value, cancellationToken);
            await securityEvents.RecordAsync(SecurityEventType.AvatarRemoved, userId, null, null, cancellationToken);
        }

        return ProfileAvatarResult.Success(profile);
    }

    private async Task<Guid?> AuthenticateAsync(string accessCredential, CancellationToken cancellationToken)
    {
        var context = await sessions.GetAuthenticatedContextAsync(accessCredential, cancellationToken);
        return context.IsSuccess ? context.UserId : null;
    }

    private async Task RetirePreviousAsync(string? previous, Guid userId, CancellationToken cancellationToken)
    {
        if (previous is null || !ProfileImageReference.TryParse(previous, out var reference))
        {
            return;
        }

        await DeleteQuietlyAsync(reference, userId, "retire-previous", cancellationToken);
    }

    private async Task DeleteQuietlyAsync(ProfileImageReference reference, Guid userId, string operation, CancellationToken cancellationToken)
    {
        bool deleted;
        try
        {
            deleted = await storage.DeleteAsync(reference, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            deleted = false;
        }

        if (!deleted)
        {
            logger.LogWarning(
                "Profile image cleanup left an orphan: user {UserId} reference {Reference} operation {Operation} outcome {Outcome}.",
                userId, reference.Value, operation, "orphaned");
        }
    }

    private static string RejectionReason(ProfileImageRejection rejection) => rejection switch
    {
        ProfileImageRejection.Empty => "empty",
        ProfileImageRejection.TooLarge => "too-large",
        ProfileImageRejection.UnsupportedType => "unsupported-type",
        ProfileImageRejection.Undecodable => "undecodable",
        ProfileImageRejection.DimensionsExceeded => "dimensions-exceeded",
        _ => "rejected"
    };
}
