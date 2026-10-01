namespace GaussAuth.Application.AuthorizationContext.Ports;

public interface IConsumerCredentialValidator
{
    Task<ConsumerCredentialValidationResult> ValidateAsync(string applicationCode, string serviceCredential, CancellationToken cancellationToken);
}
