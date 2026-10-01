using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Login.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Users.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Login;

public sealed class LoginService(
    IUserRepository users,
    IApplicationRepository applications,
    IApplicationMembershipRepository memberships,
    ICredentialProvisioningService credentialProvisioning,
    ICredentialVerificationService credentialVerification,
    ISecurityEventRecorder securityEvents,
    ILogger<LoginService> logger)
{
    public async Task<LoginOperationResult> AuthenticateAsync(string applicationCode, string email, string password, CancellationToken ct)
    {
        var normalizedEmail = credentialProvisioning.NormalizeEmail(email.Trim());
        var user = await users.GetByNormalizedEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            return await FailAsync(LoginFailureReason.UnknownEmail, null, null, ct);
        }

        var application = await applications.GetByCodeAsync(applicationCode, ct);
        if (application is null)
        {
            return await FailAsync(LoginFailureReason.UnknownApplication, user.Id, null, ct);
        }

        if (!user.IsActive)
        {
            return await FailAsync(LoginFailureReason.InactiveUser, user.Id, application.Id, ct);
        }

        if (!application.IsActive)
        {
            return await FailAsync(LoginFailureReason.InactiveApplication, user.Id, application.Id, ct);
        }

        var membership = await memberships.GetAsync(user.Id, application.Id, ct);
        if (membership is null)
        {
            return await FailAsync(LoginFailureReason.MissingMembership, user.Id, application.Id, ct);
        }

        if (!membership.IsActive)
        {
            return await FailAsync(LoginFailureReason.InactiveMembership, user.Id, application.Id, ct);
        }

        var credentialOutcome = await credentialVerification.VerifyPasswordAsync(user.Id, password, ct);
        switch (credentialOutcome)
        {
            case CredentialVerificationOutcome.LockedOut:
                return await FailAsync(LoginFailureReason.LockedOut, user.Id, application.Id, ct);
            case CredentialVerificationOutcome.InvalidPassword:
                return await FailAsync(LoginFailureReason.InvalidPassword, user.Id, application.Id, ct);
        }

        logger.LogInformation("Login attempt {UserId} {ApplicationId} completed with outcome {Outcome}.", user.Id, application.Id, "success");
        await securityEvents.RecordAsync(SecurityEventType.LoginSucceeded, user.Id, application.Id, ct);
        return LoginOperationResult.Success(user.Id, application.Id);
    }

    private async Task<LoginOperationResult> FailAsync(LoginFailureReason reason, Guid? userId, Guid? applicationId, CancellationToken ct)
    {
        logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", userId, applicationId, reason);
        var eventType = reason == LoginFailureReason.LockedOut ? SecurityEventType.AccountLockedOut : SecurityEventType.LoginFailed;
        await securityEvents.RecordAsync(eventType, userId, applicationId, ct);
        return LoginOperationResult.Failure(reason, userId, applicationId);
    }
}
