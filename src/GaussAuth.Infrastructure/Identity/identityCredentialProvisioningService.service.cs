using GaussAuth.Application.Users.Ports;
using Microsoft.AspNetCore.Identity;

namespace GaussAuth.Infrastructure.Identity;

public sealed class IdentityCredentialProvisioningService(
    UserManager<IdentityUser<Guid>> userManager,
    ILookupNormalizer normalizer) : ICredentialProvisioningService
{
    public string NormalizeEmail(string email) => normalizer.NormalizeEmail(email);

    public async Task<CredentialProvisioningResult> CreateCredentialAsync(
        Guid userId,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var identityUser = new IdentityUser<Guid>
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = NormalizeEmail(email),
            Email = email,
            NormalizedEmail = NormalizeEmail(email)
        };

        var result = await userManager.CreateAsync(identityUser, password);

        return result.Succeeded
            ? CredentialProvisioningResult.Success()
            : CredentialProvisioningResult.Failed(
                result.Errors.Select(error => error.Description).ToArray());
    }
}
