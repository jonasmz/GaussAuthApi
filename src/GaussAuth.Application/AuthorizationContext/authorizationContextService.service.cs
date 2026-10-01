using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.AuthorizationContext.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Sessions;

namespace GaussAuth.Application.AuthorizationContext;

public sealed class AuthorizationContextService(
    IConsumerCredentialValidator consumerCredentials,
    SessionService sessions,
    IUserRoleRepository userRoles,
    ISecurityEventRecorder securityEvents)
{
    public async Task<AuthorizationContextResolutionResult> ResolveAsync(
        string applicationCode,
        string serviceCredential,
        string accessCredential,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(applicationCode) || applicationCode.Length > 64 ||
            string.IsNullOrWhiteSpace(serviceCredential) || serviceCredential.Length > 512 ||
            string.IsNullOrWhiteSpace(accessCredential))
        {
            await securityEvents.RecordAsync(SecurityEventType.AuthorizationContextRejected, null, null, null, cancellationToken);
            return AuthorizationContextResolutionResult.Failure();
        }

        var consumer = await consumerCredentials.ValidateAsync(applicationCode, serviceCredential, cancellationToken);
        if (!consumer.IsValid)
        {
            await securityEvents.RecordAsync(SecurityEventType.ConsumerAuthenticationFailed, null, null, null, cancellationToken);
            await securityEvents.RecordAsync(SecurityEventType.AuthorizationContextRejected, null, null, null, cancellationToken);
            return AuthorizationContextResolutionResult.Failure();
        }

        var session = await sessions.ValidateAsync(accessCredential, applicationCode, cancellationToken);
        if (!session.IsSuccess || session.UserId is null || session.ApplicationId is null || session.SessionId is null ||
            session.AccessCredentialIssuedAt is null || session.AccessCredentialExpiresAt is null || session.SessionExpiresAt is null)
        {
            await securityEvents.RecordAsync(SecurityEventType.AuthorizationContextRejected, session.UserId, session.ApplicationId, session.SessionId, cancellationToken);
            return AuthorizationContextResolutionResult.Failure();
        }

        var roles = await userRoles.GetActiveRolesAsync(session.UserId.Value, session.ApplicationId.Value, cancellationToken);
        var permissions = await userRoles.GetEffectivePermissionsAsync(session.UserId.Value, session.ApplicationId.Value, cancellationToken);
        var context = new AuthorizationContext(
            session.UserId.Value,
            session.ApplicationId.Value,
            session.SessionId.Value,
            session.AccessCredentialIssuedAt.Value,
            session.AccessCredentialExpiresAt.Value,
            session.SessionExpiresAt.Value,
            roles.Select(role => new AuthorizationContextRole(role.Id, role.Name)).ToArray(),
            permissions.Select(permission => permission.Code).Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray());

        await securityEvents.RecordAsync(SecurityEventType.AuthorizationContextResolved, context.UserId, context.ApplicationId, context.SessionId, cancellationToken);
        return AuthorizationContextResolutionResult.Success(context);
    }
}
