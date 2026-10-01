using GaussAuth.Application.Login.Ports;
using Microsoft.AspNetCore.Identity;

namespace GaussAuth.Infrastructure.Identity;

public sealed class IdentityCredentialVerificationService(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager) : ICredentialVerificationService
{
    public async Task<CredentialVerificationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var identityUser = await userManager.FindByIdAsync(userId.ToString());
        if (identityUser is null)
        {
            return CredentialVerificationOutcome.InvalidPassword;
        }

        var result = await signInManager.CheckPasswordSignInAsync(identityUser, password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return CredentialVerificationOutcome.LockedOut;
        }

        return result.Succeeded ? CredentialVerificationOutcome.Success : CredentialVerificationOutcome.InvalidPassword;
    }
}
