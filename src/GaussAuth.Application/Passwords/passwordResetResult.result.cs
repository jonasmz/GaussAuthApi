namespace GaussAuth.Application.Passwords;

public sealed record PasswordResetResult(bool IsSuccess)
{
    public static PasswordResetResult Success() => new(true);
    public static PasswordResetResult Failure() => new(false);
}
