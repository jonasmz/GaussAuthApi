using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Login;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
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
    ISecurityEventRecorder securityEvents,
    ILogger<SessionService> logger)
{
    public async Task<SessionOperationResult> CreateAsync(LoginOperationResult authentication, CancellationToken ct)
    {
        if (!authentication.IsSuccess || authentication.UserId is null || authentication.ApplicationId is null)
            return await RejectAsync(SessionRejectionReason.NotAuthenticated, null, null, null, ct, false);

        var eligibility = await EvaluateEligibilityAsync(authentication.UserId.Value, authentication.ApplicationId.Value, ct);
        if (eligibility is not null) return await RejectAsync(eligibility.Value, authentication.UserId, authentication.ApplicationId, null, ct, false);

        var now = timeProvider.GetUtcNow();
        var session = Session.Create(Guid.NewGuid(), authentication.UserId.Value, authentication.ApplicationId.Value, now, policy.SessionLifetime);
        var expiresAt = Min(now + policy.AccessCredentialLifetime, session.ExpiresAt);
        var credential = issuer.Issue(new AccessCredentialClaims(session.Id, session.UserId, session.ApplicationId, now, expiresAt));
        await sessions.AddAsync(session, ct);
        await sessions.SaveChangesAsync(ct);
        logger.LogInformation("Session {SessionId} created for user {UserId} in application {ApplicationId}.", session.Id, session.UserId, session.ApplicationId);
        await securityEvents.RecordAsync(SecurityEventType.SessionCreated, session.UserId, session.ApplicationId, session.Id, ct);
        return SessionOperationResult.Success(session.Id, session.UserId, session.ApplicationId, credential, expiresAt, session.ExpiresAt);
    }

    public async Task<SessionOperationResult> ValidateAsync(string credential, string applicationCode, CancellationToken ct)
    {
        var checkedSession = await CheckCredentialAsync(credential, ct);
        if (checkedSession.Result is not null) return checkedSession.Result;

        var application = await applications.GetByCodeAsync(applicationCode.Trim().ToLowerInvariant(), ct);
        if (application is null || application.Id != checkedSession.Session!.ApplicationId)
            return await RejectAsync(SessionRejectionReason.ApplicationMismatch, checkedSession.Session!.UserId, checkedSession.Session.ApplicationId, checkedSession.Session.Id, ct);

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
        logger.LogInformation("Access credential renewed for session {SessionId} user {UserId} application {ApplicationId}.", session.Id, session.UserId, session.ApplicationId);
        await securityEvents.RecordAsync(SecurityEventType.AccessRenewed, session.UserId, session.ApplicationId, session.Id, ct);
        return SessionOperationResult.Success(session.Id, session.UserId, session.ApplicationId, renewed, expiresAt, session.ExpiresAt);
    }

    public async Task<SessionOperationResult> RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(sessionId, ct);
        if (session is not null && session.RevokedAt is null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await sessions.SaveChangesAsync(ct);
            logger.LogInformation("Session {SessionId} revoked for user {UserId} in application {ApplicationId}.", session.Id, session.UserId, session.ApplicationId);
            await securityEvents.RecordAsync(SecurityEventType.SessionRevoked, session.UserId, session.ApplicationId, session.Id, ct);
        }

        return SessionOperationResult.Success(sessionId, session?.UserId ?? Guid.Empty, session?.ApplicationId ?? Guid.Empty, null, null, session?.ExpiresAt);
    }

    public async Task<SessionOperationResult> LogoutAsync(string credential, CancellationToken ct)
    {
        var claims = await validator.ValidateAsync(credential, ct);
        if (claims is null) return await RejectAsync(SessionRejectionReason.MalformedCredential, null, null, null, ct);
        var result = await RevokeAsync(claims.SessionId, ct);
        logger.LogInformation("Logout completed for session {SessionId} user {UserId} application {ApplicationId}.", claims.SessionId, claims.UserId, claims.ApplicationId);
        await securityEvents.RecordAsync(SecurityEventType.LogoutCompleted, claims.UserId, claims.ApplicationId, claims.SessionId, ct);
        return result;
    }

    private async Task<(Session? Session, AccessCredentialClaims? Claims, SessionOperationResult? Result)> CheckCredentialAsync(string credential, CancellationToken ct)
    {
        var claims = await validator.ValidateAsync(credential, ct);
        if (claims is null) return (null, null, await RejectAsync(SessionRejectionReason.MalformedCredential, null, null, null, ct));

        var now = timeProvider.GetUtcNow();
        if (claims.ExpiresAt <= now) return (null, null, await RejectAsync(SessionRejectionReason.CredentialExpired, claims.UserId, claims.ApplicationId, claims.SessionId, ct));

        var session = await sessions.GetByIdAsync(claims.SessionId, ct);
        if (session is null) return (null, null, await RejectAsync(SessionRejectionReason.SessionNotFound, claims.UserId, claims.ApplicationId, claims.SessionId, ct));
        if (session.Id != claims.SessionId || session.UserId != claims.UserId || session.ApplicationId != claims.ApplicationId)
            return (null, null, await RejectAsync(SessionRejectionReason.ApplicationMismatch, session.UserId, session.ApplicationId, session.Id, ct));

        var state = session.GetState(now);
        if (state == SessionState.Revoked) return (null, null, await RejectAsync(SessionRejectionReason.SessionRevoked, session.UserId, session.ApplicationId, session.Id, ct));
        if (state == SessionState.Expired) return (null, null, await RejectAsync(SessionRejectionReason.SessionExpired, session.UserId, session.ApplicationId, session.Id, ct));

        var eligibility = await EvaluateEligibilityAsync(session.UserId, session.ApplicationId, ct);
        if (eligibility is not null) return (null, null, await RejectAsync(eligibility.Value, session.UserId, session.ApplicationId, session.Id, ct));
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

    private async Task<SessionOperationResult> RejectAsync(SessionRejectionReason reason, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken ct, bool recordSecurityEvent = true)
    {
        if (reason is SessionRejectionReason.MalformedCredential or SessionRejectionReason.NotAuthenticated)
        {
            logger.LogDebug("Session access rejected with reason {Reason}.", reason);
            return SessionOperationResult.Failure(reason);
        }

        logger.LogInformation("Session access rejected for user {UserId} application {ApplicationId} session {SessionId} with reason {Reason}.", userId, applicationId, sessionId, reason);
        if (recordSecurityEvent)
        {
            var eventType = reason switch
            {
                SessionRejectionReason.CredentialExpired or SessionRejectionReason.SessionExpired => SecurityEventType.AccessRejectedExpired,
                SessionRejectionReason.SessionRevoked => SecurityEventType.AccessRejectedRevoked,
                SessionRejectionReason.ApplicationMismatch => SecurityEventType.AccessRejectedApplicationMismatch,
                _ => SecurityEventType.AccessRejectedInvalidState
            };
            await securityEvents.RecordAsync(eventType, userId, applicationId, sessionId, ct);
        }

        return SessionOperationResult.Failure(reason);
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;
}
