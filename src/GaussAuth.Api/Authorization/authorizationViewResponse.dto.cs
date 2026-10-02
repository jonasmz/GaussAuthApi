using GaussAuth.Application.Administration.AuthorizationView;

namespace GaussAuth.Api.Authorization;

public sealed record AuthorizationViewResponse(
    AuthorizationViewApplication Application,
    AuthorizationViewUser User,
    AuthorizationViewMembership? Membership,
    IReadOnlyList<AuthorizationViewRole> Roles,
    IReadOnlyList<string> Permissions)
{
    public static AuthorizationViewResponse FromDomain(AuthorizationView view) => new(
        new(view.ApplicationId, view.ApplicationCode, view.ApplicationIsActive),
        new(view.UserId, view.UserIsActive),
        view.MembershipIsActive is { } active ? new(active) : null,
        view.Roles.Select(role => new AuthorizationViewRole(role.Id, role.Name)).ToArray(),
        view.Permissions);
}
