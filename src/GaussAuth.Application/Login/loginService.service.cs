using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Login.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Users.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Login;

public sealed class LoginService(
    IUserRepository users,
    IApplicationRepository applications,
    IApplicationMembershipRepository memberships,
    ICredentialProvisioningService credentialProvisioning,
    ICredentialVerificationService credentialVerification,
    ILogger<LoginService> logger)
{
    public async Task<LoginOperationResult> AuthenticateAsync(string applicationCode, string email, string password, CancellationToken ct)
    {
        var normalizedEmail = credentialProvisioning.NormalizeEmail(email.Trim());
        var user = await users.GetByNormalizedEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            logger.LogInformation("Login attempt failed with outcome {Outcome}.", "unknown-email");
            return LoginOperationResult.Failure(LoginFailureReason.UnknownEmail);
        }

        var application = await applications.GetByCodeAsync(applicationCode, ct);
        if (application is null)
        {
            logger.LogInformation("Login attempt {UserId} failed with outcome {Outcome}.", user.Id, "unknown-application");
            return LoginOperationResult.Failure(LoginFailureReason.UnknownApplication, user.Id);
        }

        if (!user.IsActive)
        {
            logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", user.Id, application.Id, "inactive-user");
            return LoginOperationResult.Failure(LoginFailureReason.InactiveUser, user.Id, application.Id);
        }

        if (!application.IsActive)
        {
            logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", user.Id, application.Id, "inactive-application");
            return LoginOperationResult.Failure(LoginFailureReason.InactiveApplication, user.Id, application.Id);
        }

        var membership = await memberships.GetAsync(user.Id, application.Id, ct);
        if (membership is null)
        {
            logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", user.Id, application.Id, "missing-membership");
            return LoginOperationResult.Failure(LoginFailureReason.MissingMembership, user.Id, application.Id);
        }

        if (!membership.IsActive)
        {
            logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", user.Id, application.Id, "inactive-membership");
            return LoginOperationResult.Failure(LoginFailureReason.InactiveMembership, user.Id, application.Id);
        }

        var credentialOutcome = await credentialVerification.VerifyPasswordAsync(user.Id, password, ct);
        switch (credentialOutcome)
        {
            case CredentialVerificationOutcome.LockedOut:
                logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", user.Id, application.Id, "locked-out");
                return LoginOperationResult.Failure(LoginFailureReason.LockedOut, user.Id, application.Id);
            case CredentialVerificationOutcome.InvalidPassword:
                logger.LogInformation("Login attempt {UserId} {ApplicationId} failed with outcome {Outcome}.", user.Id, application.Id, "invalid-password");
                return LoginOperationResult.Failure(LoginFailureReason.InvalidPassword, user.Id, application.Id);
        }

        logger.LogInformation("Login attempt {UserId} {ApplicationId} completed with outcome {Outcome}.", user.Id, application.Id, "success");
        return LoginOperationResult.Success(user.Id, application.Id);
    }
}
