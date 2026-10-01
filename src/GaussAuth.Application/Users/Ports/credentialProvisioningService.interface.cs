namespace GaussAuth.Application.Users.Ports;

public interface ICredentialProvisioningService
{
    string NormalizeEmail(string email);

    Task<CredentialProvisioningResult> CreateCredentialAsync(
        Guid userId,
        string email,
        string password,
        CancellationToken cancellationToken);
}
