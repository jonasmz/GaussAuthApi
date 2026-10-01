namespace GaussAuth.Application.AuthorizationContext.Ports;

public sealed record ConsumerCredentialValidationResult(bool IsValid)
{
    public static ConsumerCredentialValidationResult Success() => new(true);
    public static ConsumerCredentialValidationResult Failure() => new(false);
}
