namespace GaussAuth.Application.Login.Ports;

public interface ICredentialVerificationService
{
    Task<CredentialVerificationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);
}
