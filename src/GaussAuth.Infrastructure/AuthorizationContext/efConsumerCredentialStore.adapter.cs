using System.Security.Cryptography;
using GaussAuth.Application.Administration.ConsumerCredentials;
using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Applications;
using GaussAuth.Infrastructure.Persistence;
using GaussAuth.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.AuthorizationContext;

/// <summary>
/// Managed consumer credentials in <c>ApplicationConsumerCredentials</c>. Secrets are 32 random bytes (base64url) hashed with the
/// same <see cref="PasswordHasher{TUser}"/> scheme as the configured hashes; the plaintext exists only in the returned issuance.
/// </summary>
public sealed class EfConsumerCredentialStore(AuthenticationDbContext context, ConfiguredConsumerCredentialValidator configured, TimeProvider timeProvider) : IConsumerCredentialStore
{
    private const int SecretBytes = 32;

    public async Task<ISecurityAuditTransaction> BeginAuditTransactionAsync(CancellationToken cancellationToken) =>
        new EfSecurityAuditTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    public async Task<ConsumerSecretOperationResult> GenerateAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken)
    {
        if (configured.HasConfigured(applicationCode) || await context.ConsumerCredentials.AnyAsync(item => item.ApplicationId == applicationId, cancellationToken))
            return ConsumerSecretOperationResult.Conflict();

        var (secret, hash) = NewSecret(applicationCode);
        var credential = ConsumerCredential.Create(applicationId, hash, timeProvider.GetUtcNow());
        await context.ConsumerCredentials.AddAsync(credential, cancellationToken);
        return await SaveAsync(credential, secret, cancellationToken);
    }

    public async Task<ConsumerSecretOperationResult> RotateAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken)
    {
        var (secret, hash) = NewSecret(applicationCode);
        var now = timeProvider.GetUtcNow();
        var credential = await context.ConsumerCredentials.SingleOrDefaultAsync(item => item.ApplicationId == applicationId, cancellationToken);
        if (credential is null)
        {
            var configuredHash = configured.GetConfiguredCurrentHash(applicationCode);
            credential = configuredHash is null
                ? ConsumerCredential.Create(applicationId, hash, now)
                : ConsumerCredential.CreateFromConfigured(applicationId, configuredHash, hash, now);
            await context.ConsumerCredentials.AddAsync(credential, cancellationToken);
        }
        else credential.Rotate(hash, now);

        return await SaveAsync(credential, secret, cancellationToken);
    }

    public async Task<ConsumerSecretOperationResult> RetirePreviousAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken)
    {
        var credential = await context.ConsumerCredentials.SingleOrDefaultAsync(item => item.ApplicationId == applicationId, cancellationToken);
        if (credential is null) return ConsumerSecretOperationResult.Conflict();
        credential.RetirePrevious(timeProvider.GetUtcNow());
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConsumerSecretOperationResult.Conflict();
        }

        return ConsumerSecretOperationResult.Described(Describe(credential));
    }

    public async Task<ConsumerSecretMetadata> GetMetadataAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken)
    {
        var credential = await context.ConsumerCredentials.AsNoTracking().SingleOrDefaultAsync(item => item.ApplicationId == applicationId, cancellationToken);
        if (credential is not null) return Describe(credential);
        return configured.HasConfigured(applicationCode)
            ? new ConsumerSecretMetadata(true, ConsumerSecretMetadata.ConfiguredSource, null, null, configured.HasConfiguredRetiring(applicationCode))
            : new ConsumerSecretMetadata(false, null, null, null, false);
    }

    private async Task<ConsumerSecretOperationResult> SaveAsync(ConsumerCredential credential, string secret, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConsumerSecretOperationResult.Conflict();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ConsumerSecretOperationResult.Conflict();
        }

        return ConsumerSecretOperationResult.Issued(new ConsumerSecretIssuance(secret, Describe(credential)));
    }

    private static ConsumerSecretMetadata Describe(ConsumerCredential credential) =>
        new(true, ConsumerSecretMetadata.ManagedSource, credential.CreatedAtUtc, credential.RotatedAtUtc, credential.RetiringHash is not null);

    private static (string Secret, string Hash) NewSecret(string applicationCode)
    {
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        return (secret, new PasswordHasher<string>().HashPassword(applicationCode.Trim().ToLowerInvariant(), secret));
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
