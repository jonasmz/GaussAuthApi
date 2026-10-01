using GaussAuth.Api.Sessions;
using GaussAuth.Application.Administration.Authorization;

namespace GaussAuth.Api.Administration;

/// <summary>
/// Enforces administrative authorization before the endpoint runs. Cross-scope callers always receive the same
/// <c>403</c> whether or not the target exists, and nothing about the target is read before authorization.
/// </summary>
public sealed class AdministrativeAuthorizationFilter(AdministrativeScope scope, string? requiredPermission) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        Guid? targetApplicationId = null;
        if (scope == AdministrativeScope.Application)
        {
            if (Guid.TryParse(http.Request.RouteValues["applicationId"]?.ToString(), out var applicationId)) targetApplicationId = applicationId;
        }

        var authorizer = http.RequestServices.GetRequiredService<AdministrativeAuthorizer>();
        var authorization = await authorizer.AuthorizeAsync(http.Request.ReadBearerCredential(), scope, requiredPermission,
            targetApplicationId, http.RequestAborted);

        switch (authorization.Outcome)
        {
            case AdministrativeAuthorizationOutcome.Authorized:
                http.RequestServices.GetRequiredService<AdministrativeActorContext>().Set(authorization.ActorUserId!.Value);
                return await next(context);
            case AdministrativeAuthorizationOutcome.Forbidden:
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Administrative access is not permitted.");
            default:
                http.Response.Headers.WWWAuthenticate = "Bearer";
                return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Access is not valid.");
        }
    }
}
