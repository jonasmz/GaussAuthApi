namespace GaussAuth.Application.Passwords.Ports;

public sealed record PasswordChangeResult(PasswordChangeOutcome Outcome, IReadOnlyList<string> ValidationErrors)
{
    public static PasswordChangeResult Success() => new(PasswordChangeOutcome.Succeeded, []);
    public static PasswordChangeResult Failure(PasswordChangeOutcome outcome, IReadOnlyList<string>? validationErrors = null) => new(outcome, validationErrors ?? []);
}
