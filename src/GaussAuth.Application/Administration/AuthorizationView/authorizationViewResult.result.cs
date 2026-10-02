namespace GaussAuth.Application.Administration.AuthorizationView;

public sealed class AuthorizationViewResult
{
    public AuthorizationView? View { get; private init; }
    public string? Failure { get; private init; }
    public static AuthorizationViewResult Success(AuthorizationView view) => new() { View = view };
    public static AuthorizationViewResult NotFound(string failure) => new() { Failure = failure };
}
