using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class DataProtectionPersistenceTests
{
    [TestMethod]
    public void Shared_key_ring_survives_a_provider_restart_but_another_ring_cannot_decrypt()
    {
        var shared = Path.Combine(Path.GetTempPath(), "gaussauth-dp-" + Guid.NewGuid().ToString("N"));
        var other = Path.Combine(Path.GetTempPath(), "gaussauth-dp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var protectedValue = DataProtectionProvider.Create(new DirectoryInfo(shared), b => b.SetApplicationName("GaussAuth"))
                .CreateProtector("reset-credentials").Protect("credential-value");
            Assert.AreEqual("credential-value", DataProtectionProvider.Create(new DirectoryInfo(shared), b => b.SetApplicationName("GaussAuth"))
                .CreateProtector("reset-credentials").Unprotect(protectedValue));
            Assert.Throws<CryptographicException>(() => DataProtectionProvider.Create(new DirectoryInfo(other), b => b.SetApplicationName("GaussAuth"))
                .CreateProtector("reset-credentials").Unprotect(protectedValue));
        }
        finally { if (Directory.Exists(shared)) Directory.Delete(shared, true); if (Directory.Exists(other)) Directory.Delete(other, true); }
    }
}
