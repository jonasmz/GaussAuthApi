using System.ComponentModel.DataAnnotations;
using GaussAuth.Api.Sessions;
using GaussAuth.Application.Passwords;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Passwords;

public static class PasswordsEndpoints
{
    private const string RecoveryMessage = "If an eligible account exists, recovery instructions will be issued.";

    public static IEndpointRouteBuilder MapPasswordsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/password/change", ChangeAsync);
        app.MapPost("/auth/password/recovery", RecoverAsync).RequireRateLimiting("password-recovery");
        app.MapPost("/auth/password/reset", ResetAsync).RequireRateLimiting("password-reset");
        return app;
    }

    private static async Task<IResult> ChangeAsync(ChangePasswordRequest request, HttpContext context, PasswordManagementService service, CancellationToken cancellationToken)
    {
        if (HasValidationErrors(request, out var errors)) return TypedResults.ValidationProblem(errors);
        var credential = context.Request.ReadBearerCredential();
        if (credential is null) return InvalidCredential(context);

        var result = await service.ChangeAsync(credential, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (result.IsSuccess)
        {
            context.Response.Headers.CacheControl = "no-store";
            return TypedResults.NoContent();
        }
        if (result.IsNotAuthenticated) return InvalidCredential(context);
        if (result.ValidationErrors.Count > 0)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(ChangePasswordRequest.NewPassword)] = result.ValidationErrors.ToArray() });
        return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password change failed.");
    }

    private static async Task<IResult> RecoverAsync(PasswordRecoveryRequest request, PasswordManagementService service, CancellationToken cancellationToken)
    {
        if (HasValidationErrors(request, out var errors)) return TypedResults.ValidationProblem(errors);
        await service.RequestRecoveryAsync(request.Email, cancellationToken);
        return TypedResults.Accepted((string?)null, new { message = RecoveryMessage });
    }

    private static async Task<IResult> ResetAsync(ResetPasswordRequest request, PasswordManagementService service, CancellationToken cancellationToken)
    {
        if (HasValidationErrors(request, out var errors)) return TypedResults.ValidationProblem(errors);
        var result = await service.ResetAsync(request.Email, request.RecoveryCredential, request.NewPassword, cancellationToken);
        return result.IsSuccess
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password reset failed.");
    }

    private static IResult InvalidCredential(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Access is not valid.");
    }

    private static bool HasValidationErrors(object request, out Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        errors = results.SelectMany(result => (result.MemberNames.Any() ? result.MemberNames : [string.Empty])
                .Select(member => (member, message: result.ErrorMessage ?? string.Empty)))
            .GroupBy(pair => pair.member)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.message).ToArray());
        return !valid;
    }
}
