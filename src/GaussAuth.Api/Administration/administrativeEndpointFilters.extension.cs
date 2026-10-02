using GaussAuth.Application.Administration.Authorization;

namespace GaussAuth.Api.Administration;

public static class AdministrativeEndpointFilters
{
    /// <summary>Requires the global administrator capability.</summary>
    public static TBuilder RequireGlobalAdministrator<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new AdministrativeAuthorizationFilter(AdministrativeScope.Global, null));

    /// <summary>
    /// Requires the given <c>auth.*</c> permission in the target Application (route value <c>applicationId</c>),
    /// or the global administrator capability.
    /// </summary>
    public static TBuilder RequireApplicationAdministrator<TBuilder>(this TBuilder builder, string permission) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new AdministrativeAuthorizationFilter(AdministrativeScope.Application, permission));
}
