using System.ComponentModel.DataAnnotations;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Sessions.Ports;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Sessions;

public static class SessionsEndpoints
{
    public static IEndpointRouteBuilder MapSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/session/validate", ValidateAsync).RequireRateLimiting("session-credentials");
        app.MapPost("/auth/session/renew", RenewAsync).RequireRateLimiting("session-credentials");
        app.MapGet("/auth/signing-keys", SigningKeys).RequireRateLimiting("signing-keys");
        return app;
    }

    private static async Task<IResult> ValidateAsync(ValidateSessionRequest request, HttpContext context, SessionService sessions, CancellationToken ct)
    {
        var credential = context.Request.ReadBearerCredential();
        if (credential is null) return InvalidCredential(context);
        if (HasValidationErrors(request, out var errors)) return TypedResults.ValidationProblem(errors);
        var result = await sessions.ValidateAsync(credential, request.ApplicationCode, ct);
        return result.IsSuccess
            ? TypedResults.Ok(new SessionContextResponse(result.UserId!.Value, result.ApplicationId!.Value, result.SessionId!.Value, result.AccessCredentialExpiresAt!.Value))
            : InvalidCredential(context);
    }

    private static async Task<IResult> RenewAsync(HttpContext context, SessionService sessions, CancellationToken ct)
    {
        var credential = context.Request.ReadBearerCredential();
        if (credential is null) return InvalidCredential(context);
        var result = await sessions.RenewAsync(credential, ct);
        if (!result.IsSuccess) return InvalidCredential(context);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new AccessCredentialResponse(result.SessionId!.Value, "Bearer", result.AccessCredential!, result.AccessCredentialExpiresAt!.Value, result.SessionExpiresAt!.Value));
    }

    private static IResult SigningKeys(HttpContext context, IAccessCredentialKeySet keySet)
    {
        context.Response.Headers.CacheControl = "public, max-age=300";
        var keys = keySet.GetPublicKeys().Select(key => new SigningKeyResponse(key.KeyType, key.Curve, key.Use, key.Algorithm, key.KeyId, key.X, key.Y)).ToArray();
        return TypedResults.Ok(new SigningKeySetResponse(keys));
    }

    private static IResult InvalidCredential(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Access is not valid.");
    }

    private static bool HasValidationErrors(object request, out Dictionary<string, string[]> errors)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, results, true);
        errors = results.SelectMany(result => (result.MemberNames.Any() ? result.MemberNames : [string.Empty])
                .Select(member => (member, message: result.ErrorMessage ?? string.Empty)))
            .GroupBy(pair => pair.member)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.message).ToArray());
        return !isValid;
    }
}
