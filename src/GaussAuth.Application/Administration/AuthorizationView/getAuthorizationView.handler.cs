using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Users.Ports;

namespace GaussAuth.Application.Administration.AuthorizationView;

/// <summary>Read-only view of a user's effective authorization in one Application, using the same queries as the authorization context.</summary>
public sealed class GetAuthorizationViewHandler(
    IApplicationRepository applications,
    IUserRepository users,
    IApplicationMembershipRepository memberships,
    IUserRoleRepository userRoles)
{
    public async Task<AuthorizationViewResult> HandleAsync(GetAuthorizationViewQuery query, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(query.ApplicationId, cancellationToken);
        if (application is null) return AuthorizationViewResult.NotFound("application-not-found");
        var user = await users.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null) return AuthorizationViewResult.NotFound("user-not-found");

        var membership = await memberships.GetAsync(query.UserId, query.ApplicationId, cancellationToken);
        var roles = await userRoles.GetActiveRolesAsync(query.UserId, query.ApplicationId, cancellationToken);
        var permissions = await userRoles.GetEffectivePermissionsAsync(query.UserId, query.ApplicationId, cancellationToken);
        return AuthorizationViewResult.Success(new AuthorizationView
        {
            ApplicationId = application.Id,
            ApplicationCode = application.Code,
            ApplicationIsActive = application.IsActive,
            UserId = user.Id,
            UserIsActive = user.IsActive,
            MembershipIsActive = membership?.IsActive,
            Roles = roles.Select(role => (role.Id, role.Name)).ToArray(),
            Permissions = permissions.Select(permission => permission.Code).Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray(),
        });
    }
}
