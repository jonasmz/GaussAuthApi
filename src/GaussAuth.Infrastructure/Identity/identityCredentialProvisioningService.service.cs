using GaussAuth.Application.Users.Ports;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Identity;

public sealed class IdentityCredentialProvisioningService(
    UserManager<IdentityUser<Guid>> userManager,
    ILookupNormalizer normalizer) : ICredentialProvisioningService
{
    private static readonly string[] DuplicateErrorCodes = ["DuplicateEmail", "DuplicateUserName"];

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

        try
        {
            var result = await userManager.CreateAsync(identityUser, password);

            if (result.Succeeded)
            {
                return CredentialProvisioningResult.Success();
            }

            return result.Errors.Any(error => DuplicateErrorCodes.Contains(error.Code))
                ? CredentialProvisioningResult.DuplicateEmail()
                : CredentialProvisioningResult.Failed(result.Errors.Select(error => error.Description).ToArray());
        }
        catch (DbUpdateException exception) when (IsEmailOrUserNameUniqueViolation(exception))
        {
            return CredentialProvisioningResult.DuplicateEmail();
        }
    }

    private static bool IsEmailOrUserNameUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException &&
        postgresException.ConstraintName is "EmailIndex" or "UserNameIndex";
}
