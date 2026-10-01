using GaussAuth.Application.AuthorizationContext;
using GaussAuth.Api.Sessions;

namespace GaussAuth.Api.AuthorizationContext;

public static class AuthorizationContextEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizationContextEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/authorization-context", ResolveAsync).RequireRateLimiting("authorization-context");
        return app;
    }

    private static async Task<IResult> ResolveAsync(HttpContext httpContext, AuthorizationContextService service, CancellationToken cancellationToken)
    {
        var accessCredential = httpContext.Request.ReadBearerCredential();
        var applicationCode = httpContext.Request.Headers["X-GaussAuth-Application-Code"].ToString();
        var serviceCredential = httpContext.Request.Headers["X-GaussAuth-Consumer-Secret"].ToString();
        if (accessCredential is null || string.IsNullOrWhiteSpace(applicationCode) || applicationCode.Length > 64 ||
            string.IsNullOrWhiteSpace(serviceCredential) || serviceCredential.Length > 512)
        {
            return InvalidCredential(httpContext);
        }

        var result = await service.ResolveAsync(applicationCode, serviceCredential, accessCredential, cancellationToken);
        if (!result.IsValid || result.Context is null) return InvalidCredential(httpContext);

        httpContext.Response.Headers.CacheControl = "no-store";
        var context = result.Context;
        return TypedResults.Ok(new AuthorizationContextResponse(
            context.UserId,
            context.ApplicationId,
            context.SessionId,
            context.IssuedAt,
            context.CredentialExpiresAt,
            context.SessionExpiresAt,
            context.Roles.Select(role => new AuthorizationContextRoleResponse(role.Id, role.Name)).ToArray(),
            context.Permissions));
    }

    private static IResult InvalidCredential(HttpContext httpContext)
    {
        httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Access is not valid.");
    }
}
