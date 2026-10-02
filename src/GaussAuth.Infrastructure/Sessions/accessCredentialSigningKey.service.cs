using System.Security.Cryptography;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GaussAuth.Infrastructure.Sessions;

public sealed class AccessCredentialSigningKey : IAccessCredentialKeySet
{
    private const string PemKey = "Sessions:Signing:PrivateKeyPem";
    private const string PemFileKey = "Sessions:Signing:PrivateKeyPemFile";
    private const string NistP256Oid = "1.2.840.10045.3.1.7";
    private readonly PublicSigningKey publicKey;

    public ECDsaSecurityKey SecurityKey { get; }

    /// <summary>True when the key was generated for this process (Development only) and is lost on restart.</summary>
    public bool IsEphemeral { get; }

    private AccessCredentialSigningKey(ECDsa ecdsa, bool isEphemeral)
    {
        IsEphemeral = isEphemeral;
        var parameters = ecdsa.ExportParameters(false);
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(ecdsa));
        var keyId = Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
        SecurityKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
        publicKey = new PublicSigningKey(keyId, "EC", "P-256", "ES256", "sig", Base64UrlEncoder.Encode(parameters.Q.X!), Base64UrlEncoder.Encode(parameters.Q.Y!));
    }

    public IReadOnlyList<PublicSigningKey> GetPublicKeys() => [publicKey];

    public static AccessCredentialSigningKey Load(IConfiguration configuration, IHostEnvironment? environment, ILogger? logger = null)
    {
        var pem = configuration[PemKey];
        var file = configuration[PemFileKey];
        var source = PemKey;
        if (string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(file))
        {
            source = PemFileKey;
            try
            {
                pem = File.ReadAllText(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new StartupConfigurationException(PemFileKey, "could not be read");
            }
        }

        ECDsa ecdsa;
        var ephemeral = false;
        if (string.IsNullOrWhiteSpace(pem))
        {
            // An ephemeral key is a Development convenience only; every other environment must be given a key.
            if (environment is null || !environment.IsDevelopment())
            {
                throw new StartupConfigurationException(
                    PemKey,
                    $"is required outside Development: set it, or {PemFileKey}, to an ECDSA P-256 private key in PEM form");
            }

            logger?.LogWarning("No access credential signing key configured; using an ephemeral development key.");
            ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ephemeral = true;
        }
        else
        {
            ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportFromPem(pem);
                // A public-only PEM imports successfully but cannot sign; exporting private parameters proves a private key.
                var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
                if (ecdsa.KeySize != 256 || !IsNistP256(parameters.Curve)) throw new CryptographicException();
            }
            catch (Exception exception) when (exception is CryptographicException or ArgumentException)
            {
                ecdsa.Dispose();
                throw new StartupConfigurationException(source, "is not a valid PEM-encoded ECDSA P-256 private key");
            }
        }

        return new AccessCredentialSigningKey(ecdsa, ephemeral);
    }

    private static bool IsNistP256(ECCurve curve) =>
        curve.IsNamed && (curve.Oid.Value == NistP256Oid || string.Equals(curve.Oid.FriendlyName, "nistP256", StringComparison.OrdinalIgnoreCase));
}
