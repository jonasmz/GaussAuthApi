using System.Security.Cryptography;
using GaussAuth.Application.Sessions.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GaussAuth.Infrastructure.Sessions;

public sealed class AccessCredentialSigningKey : IAccessCredentialKeySet
{
    private const string InvalidConfiguration = "Access credential signing configuration is missing or invalid.";
    private readonly PublicSigningKey publicKey;

    public ECDsaSecurityKey SecurityKey { get; }

    private AccessCredentialSigningKey(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(false);
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(ecdsa));
        var keyId = Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
        SecurityKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
        publicKey = new PublicSigningKey(keyId, "EC", "P-256", "ES256", "sig", Base64UrlEncoder.Encode(parameters.Q.X!), Base64UrlEncoder.Encode(parameters.Q.Y!));
    }

    public IReadOnlyList<PublicSigningKey> GetPublicKeys() => [publicKey];

    public static AccessCredentialSigningKey Load(IConfiguration configuration, IHostEnvironment? environment, ILogger? logger = null)
    {
        try
        {
            var pem = configuration["Sessions:Signing:PrivateKeyPem"];
            var file = configuration["Sessions:Signing:PrivateKeyPemFile"];
            if (string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(file)) pem = File.ReadAllText(file);

            ECDsa ecdsa;
            if (string.IsNullOrWhiteSpace(pem))
            {
                if (environment is null || !environment.IsDevelopment()) throw new InvalidOperationException(InvalidConfiguration);
                logger?.LogWarning("No access credential signing key configured; using an ephemeral development key.");
                ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            }
            else
            {
                ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pem);
                if (ecdsa.KeySize != 256) throw new InvalidOperationException(InvalidConfiguration);
            }
            return new AccessCredentialSigningKey(ecdsa);
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception) { throw new InvalidOperationException(InvalidConfiguration); }
    }
}
