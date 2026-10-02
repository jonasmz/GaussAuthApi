using GaussAuth.Application.AuthorizationContext.Ports;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.AuthorizationContext;

/// <summary>
/// Validates consumer credentials against the managed hashes of the Application (current and retiring) and falls back to the
/// configured hashes only while the Application has no managed record. Failure stays uniform and length-bounded.
/// </summary>
public sealed class StoreBackedConsumerCredentialValidator(AuthenticationDbContext context, ConfiguredConsumerCredentialValidator configured) : IConsumerCredentialValidator
{
    private const int MaximumApplicationCodeLength = 64;
    private const int MaximumCredentialLength = 512;

    public async Task<ConsumerCredentialValidationResult> ValidateAsync(string applicationCode, string serviceCredential, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(applicationCode) || applicationCode.Length > MaximumApplicationCodeLength ||
            string.IsNullOrWhiteSpace(serviceCredential) || serviceCredential.Length > MaximumCredentialLength)
            return ConsumerCredentialValidationResult.Failure();

        var code = applicationCode.Trim().ToLowerInvariant();
        var managed = await context.ConsumerCredentials.AsNoTracking()
            .Where(credential => context.Applications.Any(application => application.Id == credential.ApplicationId && application.Code == code))
            .Select(credential => new { credential.CurrentHash, credential.RetiringHash })
            .SingleOrDefaultAsync(cancellationToken);
        if (managed is null) return await configured.ValidateAsync(applicationCode, serviceCredential, cancellationToken);

        var hasher = new PasswordHasher<string>();
        var valid = Matches(hasher, code, managed.CurrentHash, serviceCredential) ||
                    (managed.RetiringHash is not null && Matches(hasher, code, managed.RetiringHash, serviceCredential));
        return valid ? ConsumerCredentialValidationResult.Success() : ConsumerCredentialValidationResult.Failure();
    }

    private static bool Matches(PasswordHasher<string> hasher, string code, string hash, string secret) =>
        hasher.VerifyHashedPassword(code, hash, secret) != PasswordVerificationResult.Failed;
}
