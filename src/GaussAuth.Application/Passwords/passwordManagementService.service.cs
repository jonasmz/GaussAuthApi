using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Application.Users.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Passwords;

public sealed class PasswordManagementService(
    SessionService sessions,
    IUserRepository users,
    ICredentialProvisioningService credentialProvisioning,
    IPasswordCredentialService credentials,
    IRecoveryDelivery recoveryDelivery,
    ISessionRevoker sessionRevoker,
    ISecurityEventRecorder securityEvents,
    ILogger<PasswordManagementService> logger)
{
    public async Task<PasswordChangeResult> ChangeAsync(string accessCredential, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var context = await sessions.GetAuthenticatedContextAsync(accessCredential, cancellationToken);
        if (!context.IsSuccess || context.UserId is null)
            return context.Reason == SessionRejectionReason.InactiveUser ? PasswordChangeResult.Failure() : PasswordChangeResult.NotAuthenticated();

        var user = await users.GetByIdAsync(context.UserId.Value, cancellationToken);
        if (user is null || !user.IsActive) return PasswordChangeResult.Failure();

        var outcome = await credentials.ChangePasswordAsync(user.Id, currentPassword, newPassword, cancellationToken);
        if (outcome.Outcome != PasswordChangeOutcome.Succeeded)
            return PasswordChangeResult.Failure(outcome.Outcome == PasswordChangeOutcome.PasswordPolicyRejected ? outcome.ValidationErrors : null);

        await sessionRevoker.RevokeAllForUserAsync(user.Id, cancellationToken);
        logger.LogInformation("Password change completed for user {UserId}.", user.Id);
        await securityEvents.RecordAsync(SecurityEventType.PasswordChanged, user.Id, null, null, cancellationToken);
        return PasswordChangeResult.Success();
    }

    public async Task RequestRecoveryAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = credentialProvisioning.NormalizeEmail(email.Trim());
        var user = await users.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive) return;

        var resetCredential = await credentials.GenerateResetCredentialAsync(user.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(resetCredential)) return;
        try
        {
            await recoveryDelivery.DeliverAsync(new RecoveryDeliveryInstruction(user.Id, user.Email, resetCredential), cancellationToken);
            logger.LogInformation("Password recovery instructions issued for user {UserId}.", user.Id);
            await securityEvents.RecordAsync(SecurityEventType.PasswordRecoveryRequested, user.Id, null, null, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning("Password recovery delivery failed for user {UserId}.", user.Id);
        }
    }

    public async Task<PasswordResetResult> ResetAsync(string email, string recoveryCredential, string newPassword, CancellationToken cancellationToken)
    {
        var normalizedEmail = credentialProvisioning.NormalizeEmail(email.Trim());
        var user = await users.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
            return await ResetFailedAsync(user?.Id, cancellationToken);

        var outcome = await credentials.ResetPasswordAsync(user.Id, recoveryCredential, newPassword, cancellationToken);
        if (outcome != PasswordResetOutcome.Succeeded)
            return await ResetFailedAsync(user.Id, cancellationToken);

        await sessionRevoker.RevokeAllForUserAsync(user.Id, cancellationToken);
        logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
        await securityEvents.RecordAsync(SecurityEventType.PasswordReset, user.Id, null, null, cancellationToken);
        return PasswordResetResult.Success();
    }

    private async Task<PasswordResetResult> ResetFailedAsync(Guid? userId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Password reset failed for user {UserId}.", userId);
        await securityEvents.RecordAsync(SecurityEventType.PasswordResetFailed, userId, null, null, cancellationToken);
        return PasswordResetResult.Failure();
    }
}
