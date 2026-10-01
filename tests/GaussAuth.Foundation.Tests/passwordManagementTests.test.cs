using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Infrastructure.Passwords;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class PasswordManagementTests
{
    [TestMethod]
    public async Task Development_delivery_writes_only_to_configured_ignored_recovery_file()
    {
        var path = Path.Combine(Path.GetTempPath(), ".recovery", $"{Guid.NewGuid():N}.jsonl");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordRecovery:DeliveryFile"] = path }).Build();
            var delivery = new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Development"));
            await delivery.DeliverAsync(new RecoveryDeliveryInstruction(Guid.NewGuid(), "user@example.test", "test-credential"), CancellationToken.None);
            Assert.IsTrue(File.Exists(path));
            Assert.IsTrue(await File.ReadAllTextAsync(path) is { Length: > 0 });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Delivery_fails_without_writing_a_credential_outside_development_or_testing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordRecovery:DeliveryFile"] = path }).Build();
        var delivery = new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Production"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => delivery.DeliverAsync(new RecoveryDeliveryInstruction(Guid.NewGuid(), "user@example.test", "test-credential"), CancellationToken.None));
        Assert.IsFalse(File.Exists(path));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
