using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Sessions;

namespace GaussAuth.Application.Administration.Authorization;

/// <summary>
/// Decides whether the caller of an administrative operation may perform it, before any data is read or changed.
/// Global administrators may perform any administrative operation (administrative authority only, never added to effective
/// permissions). Everyone else needs the required <c>auth.*</c> permission in the Application of their own session, and
/// that Application must be the target Application.
/// </summary>
public sealed class AdministrativeAuthorizer(
    SessionService sessions,
    IUserRoleRepository userRoles,
    IGlobalAdministratorPolicy globalAdministrators,
    SecurityEventCatalog catalog,
    ISecurityEventRecorder securityEvents)
{
    public const string GlobalRequirement = "global-administrator";

    public async Task<AdministrativeAuthorization> AuthorizeAsync(string? accessCredential, AdministrativeScope scope,
        string? requiredPermission, Guid? targetApplicationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessCredential)) return AdministrativeAuthorization.Unauthenticated();

        var session = await sessions.GetAuthenticatedContextAsync(accessCredential, cancellationToken);
        if (!session.IsSuccess || session.UserId is not { } actorUserId || session.ApplicationId is not { } sessionApplicationId)
            return AdministrativeAuthorization.Unauthenticated();

        if (globalAdministrators.IsGlobalAdministrator(actorUserId)) return AdministrativeAuthorization.Authorized(actorUserId, isGlobal: true);

        if (scope == AdministrativeScope.Application && !string.IsNullOrEmpty(requiredPermission) &&
            targetApplicationId == sessionApplicationId)
        {
            var permissions = await userRoles.GetEffectivePermissionsAsync(actorUserId, sessionApplicationId, cancellationToken);
            if (permissions.Any(permission => string.Equals(permission.Code, requiredPermission, StringComparison.Ordinal)))
                return AdministrativeAuthorization.Authorized(actorUserId, isGlobal: false);
        }

        await RecordDeniedAsync(actorUserId, scope, requiredPermission, targetApplicationId, cancellationToken);
        return AdministrativeAuthorization.Forbidden(actorUserId);
    }

    private Task RecordDeniedAsync(Guid actorUserId, AdministrativeScope scope, string? requiredPermission, Guid? targetApplicationId, CancellationToken cancellationToken)
    {
        var reason = scope == AdministrativeScope.Global ? GlobalRequirement : requiredPermission;
        return securityEvents.RecordAsync(new SecurityEventDraft(catalog.Get(SecurityEventType.AdministrativeAccessDenied),
            UserId: null, ApplicationId: targetApplicationId, SessionId: null, Reason: reason, ActorUserId: actorUserId), cancellationToken);
    }
}
