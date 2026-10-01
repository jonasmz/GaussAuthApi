using GaussAuth.Application.AuthorizationContext.Ports;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.AuthorizationContext;

public sealed class ConfiguredConsumerCredentialValidator : IConsumerCredentialValidator
{
    private const int MaximumApplicationCodeLength = 64;
    private const int MaximumCredentialLength = 512;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> hashesByApplication;

    public ConfiguredConsumerCredentialValidator(IConfiguration configuration)
    {
        var hashes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var consumer in configuration.GetSection("AuthorizationConsumers").GetChildren())
        {
            var applicationCode = consumer.Key.Trim();
            if (applicationCode.Length is 0 or > MaximumApplicationCodeLength)
            {
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(consumer["CurrentSecret"]) || !string.IsNullOrWhiteSpace(consumer["RetiringSecret"]))
            {
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");
            }

            var currentHash = consumer["CurrentSecretHash"];
            if (string.IsNullOrWhiteSpace(currentHash) || currentHash.Length > MaximumCredentialLength)
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");

            var consumerHashes = new List<string> { currentHash };
            var retiringHash = consumer["RetiringSecretHash"];
            if (!string.IsNullOrWhiteSpace(retiringHash))
            {
                if (retiringHash.Length > MaximumCredentialLength || consumerHashes.Contains(retiringHash, StringComparer.Ordinal))
                    throw new InvalidOperationException("Authorization consumer configuration is invalid.");
                consumerHashes.Add(retiringHash);
            }

            if (!hashes.TryAdd(applicationCode, consumerHashes))
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");
        }

        hashesByApplication = hashes;
    }

    public Task<ConsumerCredentialValidationResult> ValidateAsync(string applicationCode, string serviceCredential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(applicationCode) || applicationCode.Length > MaximumApplicationCodeLength ||
            string.IsNullOrWhiteSpace(serviceCredential) || serviceCredential.Length > MaximumCredentialLength ||
            !hashesByApplication.TryGetValue(applicationCode.Trim(), out var hashes))
        {
            return Task.FromResult(ConsumerCredentialValidationResult.Failure());
        }

        var hasher = new PasswordHasher<string>();
        var valid = hashes.Any(hash => hasher.VerifyHashedPassword(applicationCode, hash, serviceCredential) != PasswordVerificationResult.Failed);
        return Task.FromResult(valid ? ConsumerCredentialValidationResult.Success() : ConsumerCredentialValidationResult.Failure());
    }
}
