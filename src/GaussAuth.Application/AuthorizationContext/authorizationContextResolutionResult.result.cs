namespace GaussAuth.Application.AuthorizationContext;

public sealed record AuthorizationContextResolutionResult(bool IsValid, AuthorizationContext? Context)
{
    public static AuthorizationContextResolutionResult Success(AuthorizationContext context) => new(true, context);
    public static AuthorizationContextResolutionResult Failure() => new(false, null);
}
