using System.ComponentModel.DataAnnotations;
using GaussAuth.Application.Login;
using GaussAuth.Application.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Login;

public static class LoginEndpoints
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", LoginAsync).RequireRateLimiting("login");
        return app;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, LoginService service, SessionService sessions, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (HasValidationErrors(request, out var validationErrors))
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await service.AuthenticateAsync(request.ApplicationCode, request.Email, request.Password, cancellationToken);

        if (!result.IsSuccess)
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication failed.");

        var session = await sessions.CreateAsync(result, cancellationToken);
        if (!session.IsSuccess)
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication failed.");

        httpContext.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new LoginResponse(
            session.UserId!.Value, session.ApplicationId!.Value, session.SessionId!.Value, "Bearer",
            session.AccessCredential!, session.AccessCredentialExpiresAt!.Value, session.SessionExpiresAt!.Value));
    }

    private static bool HasValidationErrors(object request, out Dictionary<string, string[]> errors)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, results, validateAllProperties: true);

        errors = results
            .SelectMany(result => (result.MemberNames.Any() ? result.MemberNames : [string.Empty])
                .Select(member => (member, message: result.ErrorMessage ?? string.Empty)))
            .GroupBy(pair => pair.member)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.message).ToArray());

        return !isValid;
    }
}
