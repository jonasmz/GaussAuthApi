using System.Security.Cryptography;
using System.Text;
using GaussAuth.Application.AuthorizationContext.Ports;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.AuthorizationContext;

public sealed class ConfiguredConsumerCredentialValidator : IConsumerCredentialValidator
{
    private const int MaximumApplicationCodeLength = 64;
    private const int MaximumCredentialLength = 512;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<byte[]>> credentialsByApplication;

    public ConfiguredConsumerCredentialValidator(IConfiguration configuration)
    {
        var credentials = new Dictionary<string, IReadOnlyList<byte[]>>(StringComparer.OrdinalIgnoreCase);
        var configuredSecrets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var consumer in configuration.GetSection("AuthorizationConsumers").GetChildren())
        {
            var applicationCode = consumer.Key.Trim();
            if (applicationCode.Length is 0 or > MaximumApplicationCodeLength)
            {
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");
            }

            var currentSecret = consumer["CurrentSecret"];
            if (string.IsNullOrWhiteSpace(currentSecret) || currentSecret.Length > MaximumCredentialLength)
            {
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");
            }

            var secrets = new List<string> { currentSecret };
            var retiringSecret = consumer["RetiringSecret"];
            if (!string.IsNullOrWhiteSpace(retiringSecret))
            {
                if (retiringSecret.Length > MaximumCredentialLength || secrets.Contains(retiringSecret, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException("Authorization consumer configuration is invalid.");
                }

                secrets.Add(retiringSecret);
            }

            if (!credentials.TryAdd(applicationCode, secrets.Select(secret => Encoding.UTF8.GetBytes(secret)).ToArray()) ||
                secrets.Any(secret => !configuredSecrets.Add(secret)))
            {
                throw new InvalidOperationException("Authorization consumer configuration is invalid.");
            }
        }

        credentialsByApplication = credentials;
    }

    public Task<ConsumerCredentialValidationResult> ValidateAsync(string applicationCode, string serviceCredential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(applicationCode) || applicationCode.Length > MaximumApplicationCodeLength ||
            string.IsNullOrWhiteSpace(serviceCredential) || serviceCredential.Length > MaximumCredentialLength ||
            !credentialsByApplication.TryGetValue(applicationCode.Trim(), out var credentials))
        {
            return Task.FromResult(ConsumerCredentialValidationResult.Failure());
        }

        var presentedCredential = Encoding.UTF8.GetBytes(serviceCredential);
        var valid = credentials.Any(credential => CryptographicOperations.FixedTimeEquals(credential, presentedCredential));
        return Task.FromResult(valid ? ConsumerCredentialValidationResult.Success() : ConsumerCredentialValidationResult.Failure());
    }
}
