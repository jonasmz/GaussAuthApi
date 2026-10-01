namespace GaussAuth.Application.Passwords;

public sealed record PasswordChangeResult(bool IsSuccess, bool IsNotAuthenticated, IReadOnlyList<string> ValidationErrors)
{
    public static PasswordChangeResult Success() => new(true, false, []);
    public static PasswordChangeResult NotAuthenticated() => new(false, true, []);
    public static PasswordChangeResult Failure(IReadOnlyList<string>? validationErrors = null) => new(false, false, validationErrors ?? []);
}
