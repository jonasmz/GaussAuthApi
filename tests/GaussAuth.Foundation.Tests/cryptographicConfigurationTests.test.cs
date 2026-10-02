using System.Security.Cryptography;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Infrastructure.Configuration;
using GaussAuth.Infrastructure.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class CryptographicConfigurationTests
{
    [TestMethod]
    public async Task Configured_issuer_and_key_validate_only_the_credentials_they_issue()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signingKey = LoadKey(key.ExportPkcs8PrivateKeyPem(), "Production");
        var claims = new AccessCredentialClaims(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));
        var credential = new SignedAccessCredentialIssuer(signingKey, "configured-issuer").Issue(claims);

        var validated = await new SignedAccessCredentialValidator(signingKey, "configured-issuer").ValidateAsync(credential, CancellationToken.None);
        var mismatchedIssuer = await new SignedAccessCredentialValidator(signingKey, "other-issuer").ValidateAsync(credential, CancellationToken.None);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var mismatchedKey = await new SignedAccessCredentialValidator(LoadKey(otherKey.ExportPkcs8PrivateKeyPem(), "Production"), "configured-issuer").ValidateAsync(credential, CancellationToken.None);

        Assert.IsNotNull(validated);
        Assert.AreEqual(claims.SessionId, validated.SessionId);
        Assert.AreEqual(claims.ExpiresAt.ToUnixTimeSeconds(), validated.ExpiresAt.ToUnixTimeSeconds());
        Assert.IsNull(mismatchedIssuer);
        Assert.IsNull(mismatchedKey);
    }

    [TestMethod]
    public void Configured_key_is_nist_p256_and_its_published_set_matches_it()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signingKey = LoadKey(key.ExportPkcs8PrivateKeyPem(), "Production");
        var published = signingKey.GetPublicKeys().Single();

        Assert.AreEqual(256, signingKey.SecurityKey.KeySize);
        Assert.AreEqual("EC", published.KeyType);
        Assert.AreEqual("P-256", published.Curve);
        Assert.AreEqual("ES256", published.Algorithm);
        Assert.AreEqual("sig", published.Use);
        Assert.AreEqual(signingKey.SecurityKey.KeyId, published.KeyId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(published.X));
        Assert.IsFalse(string.IsNullOrWhiteSpace(published.Y));
    }

    [DataRow("Production")]
    [DataRow("Staging")]
    [DataRow("Testing")]
    [TestMethod]
    public void Absent_signing_key_is_refused_outside_development(string environmentName)
    {
        var exception = Assert.Throws<StartupConfigurationException>(() => LoadKey(null, environmentName));
        Assert.Contains("Sessions:Signing:PrivateKeyPem", exception.Message);
    }

    [TestMethod]
    public void Development_only_may_use_an_ephemeral_key()
    {
        var development = LoadKey(null, "Development");
        Assert.IsTrue(development.IsEphemeral);
        Assert.AreEqual(256, development.SecurityKey.KeySize);
    }

    private static AccessCredentialSigningKey LoadKey(string? pem, string environmentName)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sessions:Signing:PrivateKeyPem"] = pem
        }).Build();
        return AccessCredentialSigningKey.Load(configuration, new TestHostEnvironment(environmentName));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GaussAuth.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
