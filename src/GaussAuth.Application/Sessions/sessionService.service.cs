using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Login;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Sessions;

public sealed class SessionService(
    ISessionRepository sessions,
    IUserRepository users,
    IApplicationRepository applications,
    IApplicationMembershipRepository memberships,
    IAccessCredentialIssuer issuer,
    IAccessCredentialValidator validator,
    SessionPolicy policy,
    TimeProvider timeProvider,
    ILogger<SessionService> logger)
{
    public async Task<SessionOperationResult> CreateAsync(LoginOperationResult authentication, CancellationToken ct)
    {
        _ = logger;
        if (!authentication.IsSuccess || authentication.UserId is null || authentication.ApplicationId is null)
            return SessionOperationResult.Failure(SessionRejectionReason.NotAuthenticated);

        var eligibility = await EvaluateEligibilityAsync(authentication.UserId.Value, authentication.ApplicationId.Value, ct);
        if (eligibility is not null) return SessionOperationResult.Failure(eligibility.Value);

        var now = timeProvider.GetUtcNow();
        var session = Session.Create(Guid.NewGuid(), authentication.UserId.Value, authentication.ApplicationId.Value, now, policy.SessionLifetime);
        var expiresAt = Min(now + policy.AccessCredentialLifetime, session.ExpiresAt);
        var credential = issuer.Issue(new AccessCredentialClaims(session.Id, session.UserId, session.ApplicationId, now, expiresAt));
        await sessions.AddAsync(session, ct);
        await sessions.SaveChangesAsync(ct);
        return SessionOperationResult.Success(session.Id, session.UserId, session.ApplicationId, credential, expiresAt, session.ExpiresAt);
    }

    public async Task<SessionOperationResult> ValidateAsync(string credential, string applicationCode, CancellationToken ct)
    {
        var checkedSession = await CheckCredentialAsync(credential, ct);
        if (checkedSession.Result is not null) return checkedSession.Result;

        var application = await applications.GetByCodeAsync(applicationCode.Trim().ToLowerInvariant(), ct);
        if (application is null || application.Id != checkedSession.Session!.ApplicationId)
            return SessionOperationResult.Failure(SessionRejectionReason.ApplicationMismatch);

        return SuccessFor(checkedSession.Session!, checkedSession.Claims!);
    }

    public async Task<SessionOperationResult> RenewAsync(string credential, CancellationToken ct)
    {
        var checkedSession = await CheckCredentialAsync(credential, ct);
        if (checkedSession.Result is not null) return checkedSession.Result;

        var now = timeProvider.GetUtcNow();
        var session = checkedSession.Session!;
        var expiresAt = Min(now + policy.AccessCredentialLifetime, session.ExpiresAt);
        var renewed = issuer.Issue(new AccessCredentialClaims(session.Id, session.UserId, session.ApplicationId, now, expiresAt));
        return SessionOperationResult.Success(session.Id, session.UserId, session.ApplicationId, renewed, expiresAt, session.ExpiresAt);
    }

    public async Task<SessionOperationResult> RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(sessionId, ct);
        if (session is not null && session.RevokedAt is null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await sessions.SaveChangesAsync(ct);
        }

        return SessionOperationResult.Success(sessionId, session?.UserId ?? Guid.Empty, session?.ApplicationId ?? Guid.Empty, null, null, session?.ExpiresAt);
    }

    public async Task<SessionOperationResult> LogoutAsync(string credential, CancellationToken ct)
    {
        var claims = await validator.ValidateAsync(credential, ct);
        return claims is null
            ? SessionOperationResult.Failure(SessionRejectionReason.MalformedCredential)
            : await RevokeAsync(claims.SessionId, ct);
    }

    private async Task<(Session? Session, AccessCredentialClaims? Claims, SessionOperationResult? Result)> CheckCredentialAsync(string credential, CancellationToken ct)
    {
        var claims = await validator.ValidateAsync(credential, ct);
        if (claims is null) return (null, null, SessionOperationResult.Failure(SessionRejectionReason.MalformedCredential));

        var now = timeProvider.GetUtcNow();
        if (claims.ExpiresAt <= now) return (null, null, SessionOperationResult.Failure(SessionRejectionReason.CredentialExpired));

        var session = await sessions.GetByIdAsync(claims.SessionId, ct);
        if (session is null) return (null, null, SessionOperationResult.Failure(SessionRejectionReason.SessionNotFound));
        if (session.Id != claims.SessionId || session.UserId != claims.UserId || session.ApplicationId != claims.ApplicationId)
            return (null, null, SessionOperationResult.Failure(SessionRejectionReason.ApplicationMismatch));

        var state = session.GetState(now);
        if (state == SessionState.Revoked) return (null, null, SessionOperationResult.Failure(SessionRejectionReason.SessionRevoked));
        if (state == SessionState.Expired) return (null, null, SessionOperationResult.Failure(SessionRejectionReason.SessionExpired));

        var eligibility = await EvaluateEligibilityAsync(session.UserId, session.ApplicationId, ct);
        if (eligibility is not null) return (null, null, SessionOperationResult.Failure(eligibility.Value));
        return (session, claims, null);
    }

    private async Task<SessionRejectionReason?> EvaluateEligibilityAsync(Guid userId, Guid applicationId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return SessionRejectionReason.InactiveUser;
        var application = await applications.GetByIdAsync(applicationId, ct);
        if (application is null || !application.IsActive) return SessionRejectionReason.InactiveApplication;
        var membership = await memberships.GetAsync(userId, applicationId, ct);
        return membership is null || !membership.IsActive ? SessionRejectionReason.InactiveMembership : null;
    }

    private static SessionOperationResult SuccessFor(Session session, AccessCredentialClaims claims) =>
        SessionOperationResult.Success(session.Id, session.UserId, session.ApplicationId, null, claims.ExpiresAt, session.ExpiresAt);

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;
}
